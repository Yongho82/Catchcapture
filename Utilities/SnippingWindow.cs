using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture.Utilities
{
    public class SnippingWindow : Window, IDisposable
    {
        private Point startPoint;
        private readonly Rectangle selectionRectangle;
        private readonly Canvas canvas;
        private bool isSelecting = false;
        private readonly TextBlock? infoTextBlock;
        private readonly TextBlock sizeTextBlock;
        private readonly System.Windows.Shapes.Path overlayPath;
        private Image screenImage;
        private BitmapSource? screenCapture;
        private readonly RectangleGeometry fullScreenGeometry;
        private readonly RectangleGeometry selectionGeometry;
        private readonly System.Diagnostics.Stopwatch moveStopwatch = new();
        private const int MinMoveIntervalMs = 4; // ~240Hz 업데이트 제한
        private Point lastUpdatePoint;
        private const double MinMoveDelta = 1.0; // 최소 픽셀 이동 임계값
        // Rendering 프레임 병합용
        private bool hasPendingUpdate = false;
        private Rect pendingRect;
        // Virtual screen bounds
        private readonly double vLeft;
        private readonly double vTop;
        private readonly double vWidth;
        private readonly double vHeight;
        private bool disposed = false;
        private System.Windows.Threading.DispatcherTimer? memoryCleanupTimer;
        private bool instantEditMode = false; 
        private Border? colorPalette;
        private Color selectedColor = Colors.Yellow; // 형광펜 기본 색상
        private List<Color> customColors = new List<Color>();
        // PreviewWindow와 동일한 공용 색상 팔레트
        private static readonly Color[] SharedColorPalette = new[]
        {
            Colors.Black, Colors.White, Colors.Red, Colors.Orange,
            Colors.Yellow, Colors.Green, Colors.Blue, Colors.Purple,
            Color.FromRgb(139, 69, 19), Color.FromRgb(255, 192, 203)
        };
        private string currentTool = ""; 
        private bool isDrawingEnabled = false; 
        private List<UIElement> drawnElements = new List<UIElement>();
        private Point lastDrawPoint; 
        private Polyline? currentPolyline; 
        private int penThickness = 3; 
        private int highlightThickness = 8; 
        private Button? activeToolButton; 
        // 텍스트 편집 관련 필드
        private TextBox? selectedTextBox;
        private int textFontSize = 16;
        private string textFontFamily = "Malgun Gothic";
        private Border? magnifierBorder;
        private Image? magnifierImage;
        private const double MagnifierSize = 150; // 돋보기 크기
        private const double MagnificationFactor = 3.0; // 확대 배율

        // 십자선 관련 필드 추가
        private Line? crosshairHorizontal;
        private Line? crosshairVertical;

        public Int32Rect SelectedArea { get; private set; }
        public bool IsCancelled { get; private set; } = false;
        public BitmapSource? SelectedFrozenImage { get; private set; }

        public SnippingWindow(bool showGuideText = false, BitmapSource? cachedScreenshot = null)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = false; // 투명창 비활성화: GPU 가속 유지로 드래그 끊김 감소
            Background = Brushes.Black;
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            UseLayoutRounding = true;

            // Place and size the window to cover the entire virtual screen (all monitors)
            vLeft = SystemParameters.VirtualScreenLeft;
            vTop = SystemParameters.VirtualScreenTop;
            vWidth = SystemParameters.VirtualScreenWidth;
            vHeight = SystemParameters.VirtualScreenHeight;

            Left = vLeft;
            Top = vTop;
            Width = vWidth;
            Height = vHeight;
            WindowState = WindowState.Normal; // Important: Normal to respect manual Left/Top/Size across monitors

            // 캔버스 설정 (virtual desktop size)
            canvas = new Canvas();
            canvas.Width = vWidth;
            canvas.Height = vHeight;
            canvas.SnapsToDevicePixels = true;
            // Always hit-testable even with transparent content
            canvas.Background = Brushes.Transparent;
            Content = canvas;

            // 캐시된 스크린샷이 있으면 즉시 사용, 없으면 새로 캡처
            if (cachedScreenshot != null)
            {
                screenCapture = cachedScreenshot;
                screenImage = new Image { Source = screenCapture };
                Panel.SetZIndex(screenImage, -1);
                canvas.Children.Add(screenImage);
            }
            else
            {
                // 기존 방식: 동기 캡처
                screenCapture = ScreenCaptureUtility.CaptureScreen();
                screenImage = new Image { Source = screenCapture };
                Panel.SetZIndex(screenImage, -1);
                canvas.Children.Add(screenImage);
            }

            // 반투명 오버레이 생성: 전체를 어둡게, 선택 영역은 투명한 '구멍'
            fullScreenGeometry = new RectangleGeometry(new Rect(0, 0, vWidth, vHeight));
            selectionGeometry = new RectangleGeometry(new Rect(0, 0, 0, 0));
            if (fullScreenGeometry.CanFreeze) fullScreenGeometry.Freeze();
            // selectionGeometry는 런타임 갱신 필요하므로 Freeze 불가

            var geometryGroup = new GeometryGroup { FillRule = FillRule.EvenOdd };
            geometryGroup.Children.Add(fullScreenGeometry);
            geometryGroup.Children.Add(selectionGeometry);

            overlayPath = new System.Windows.Shapes.Path
            {
                Data = geometryGroup,
                Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)) // 약간 어둡게
            };
            overlayPath.IsHitTestVisible = false;
            if (overlayPath.Fill is SolidColorBrush sb && sb.CanFreeze) sb.Freeze();
            // 렌더링 캐시 힌트로 성능 개선
            RenderOptions.SetCachingHint(overlayPath, CachingHint.Cache);
            RenderOptions.SetCacheInvalidationThresholdMinimum(overlayPath, 0.5);
            RenderOptions.SetCacheInvalidationThresholdMaximum(overlayPath, 2.0);
            overlayPath.CacheMode = new BitmapCache();
            canvas.Children.Add(overlayPath);

            // 가이드 텍스트 (옵션)
            if (showGuideText)
            {
                infoTextBlock = new TextBlock
                {
                    Text = "영역을 선택하세요 (ESC 키를 눌러 취소)",
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    Padding = new Thickness(10),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(20)
                };
                if (infoTextBlock.Background is SolidColorBrush sb2 && sb2.CanFreeze) sb2.Freeze();
                canvas.Children.Add(infoTextBlock);
                Canvas.SetLeft(infoTextBlock, (vWidth - 300) / 2 + 0);
                Canvas.SetTop(infoTextBlock, vTop + 20 - vTop);
            }

            // 크기 표시용 텍스트 블록 추가
            sizeTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Visibility = Visibility.Collapsed
            };
            TextOptions.SetTextFormattingMode(sizeTextBlock, TextFormattingMode.Display);
            if (sizeTextBlock.Background is SolidColorBrush sb3 && sb3.CanFreeze) sb3.Freeze();
            canvas.Children.Add(sizeTextBlock);

            // 선택 영역 직사각형(테두리만 표시)
            var strokeBrush = new SolidColorBrush(Colors.DeepSkyBlue);
            strokeBrush.Freeze(); // GC로부터 보호
            
            var dashArray = new DoubleCollection { 4, 2 };
            dashArray.Freeze(); // GC로부터 보호
            
            selectionRectangle = new Rectangle
            {
                Stroke = strokeBrush,
                StrokeThickness = 2,
                StrokeDashArray = dashArray,
                Fill = Brushes.Transparent
            };
            selectionRectangle.IsHitTestVisible = false;
            selectionRectangle.SnapsToDevicePixels = true;
            canvas.Children.Add(selectionRectangle);
            // 돋보기 생성
            CreateMagnifier();

            // 이벤트 핸들러 등록
            MouseLeftButtonDown += SnippingWindow_MouseLeftButtonDown;
            MouseMove += SnippingWindow_MouseMove;
            MouseLeftButtonUp += SnippingWindow_MouseLeftButtonUp;
            KeyDown += SnippingWindow_KeyDown;
            // Keep drag active even if window momentarily deactivates or capture is lost
            Deactivated += SnippingWindow_Deactivated;
            LostMouseCapture += SnippingWindow_LostMouseCapture;
            Loaded += (s, e) => { try { Activate(); Focus(); } catch { } };

            // 비동기 캡처 제거: 어둡게 되는 시점과 즉시 상호작용 가능 상태를 일치시킴

            moveStopwatch.Start();

            // 메모리 모니터링 타이머 설정 (5분마다 실행)
            memoryCleanupTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5) // 30초 → 5분으로 변경
            };
            memoryCleanupTimer.Tick += (s, e) => 
            {
                // 강제 GC 대신 메모리 사용량만 로깅
                var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024;
                System.Diagnostics.Debug.WriteLine($"SnippingWindow 메모리 사용량: {memoryUsed}MB");
            };
            memoryCleanupTimer.Start();
        }

        private void CreateMagnifier()
        {
            // 돋보기 이미지
            magnifierImage = new Image
            {
                Width = MagnifierSize,
                Height = MagnifierSize,
                Stretch = Stretch.None
            };
            RenderOptions.SetBitmapScalingMode(magnifierImage, BitmapScalingMode.NearestNeighbor);

            // 돋보기 테두리
            magnifierBorder = new Border
            {
                Width = MagnifierSize,
                Height = MagnifierSize,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Background = Brushes.Black,
                Child = magnifierImage,
                CornerRadius = new CornerRadius(MagnifierSize / 2), // 원형
                Visibility = Visibility.Collapsed
            };
            
            // 그림자 효과
            magnifierBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.7
            };

            canvas.Children.Add(magnifierBorder);
            Panel.SetZIndex(magnifierBorder, 1000); // 최상위 표시
            // --- 여기부터 추가된 코드 ---
            // 십자선 초기화
            crosshairHorizontal = new Line
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Visibility = Visibility.Collapsed
            };
            crosshairVertical = new Line
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Visibility = Visibility.Collapsed
            };
            
            // 캔버스에 추가 (돋보기보다 위에)
            canvas.Children.Add(crosshairHorizontal);
            canvas.Children.Add(crosshairVertical);
            Panel.SetZIndex(crosshairHorizontal, 1001);
            Panel.SetZIndex(crosshairVertical, 1001);
            // --- 여기까지 ---
        }
        
        private void SnippingWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);
            isSelecting = true;
            hasPendingUpdate = false;
            Mouse.Capture(this);
            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // 선택 영역 초기화
            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);
            selectionRectangle.Width = 0;
            selectionRectangle.Height = 0;
            
            // 크기 표시 숨기기
            sizeTextBlock.Visibility = Visibility.Collapsed;
        }
        private void SnippingWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting) return;
            isSelecting = false;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            if (Mouse.Captured == this) Mouse.Capture(null);
            
            // 돋보기 숨기기
            if (magnifierBorder != null)
                magnifierBorder.Visibility = Visibility.Collapsed;
            
            Point endPoint = e.GetPosition(canvas);

            // 선택된 영역 계산
            double left = Math.Min(startPoint.X, endPoint.X);
            double top = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                MessageBox.Show("선택된 영역이 너무 작습니다. 다시 선택해주세요.", "알림");
                return;
            }

            // Convert from WPF DIPs to device pixels and offset by virtual screen origin
            var dpi = VisualTreeHelper.GetDpi(this);
            int pxLeft = (int)Math.Round(left * dpi.DpiScaleX);
            int pxTop = (int)Math.Round(top * dpi.DpiScaleY);
            int pxWidth = (int)Math.Round(width * dpi.DpiScaleX);
            int pxHeight = (int)Math.Round(height * dpi.DpiScaleY);

            int globalX = (int)Math.Round(vLeft) + pxLeft;
            int globalY = (int)Math.Round(vTop) + pxTop;

            SelectedArea = new Int32Rect(globalX, globalY, pxWidth, pxHeight);

            // 동결된 배경(screenCapture)에서 선택 영역만 잘라 저장 (가능할 때)
            try
            {
                if (screenCapture != null && pxWidth > 0 && pxHeight > 0)
                {
                    int relX = globalX - (int)Math.Round(vLeft);
                    int relY = globalY - (int)Math.Round(vTop);

                    // 경계 체크
                    relX = Math.Max(0, Math.Min(relX, screenCapture.PixelWidth - 1));
                    relY = Math.Max(0, Math.Min(relY, screenCapture.PixelHeight - 1));
                    int cw = Math.Max(0, Math.Min(pxWidth, screenCapture.PixelWidth - relX));
                    int ch = Math.Max(0, Math.Min(pxHeight, screenCapture.PixelHeight - relY));

                    if (cw > 0 && ch > 0)
                    {
                        var cropRect = new Int32Rect(relX, relY, cw, ch);
                        SelectedFrozenImage = new CroppedBitmap(screenCapture, cropRect);
                    }
                }
            }
            catch { /* ignore crop errors */ }

            if (instantEditMode)
            {
                ShowEditToolbar();
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }
        private void SnippingWindow_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(canvas);
            
            // 돋보기 업데이트 (선택 중이 아닐 때도 표시)
            UpdateMagnifier(currentPoint);
            
            if (!isSelecting) return;
            if (moveStopwatch.ElapsedMilliseconds < MinMoveIntervalMs) return;
            moveStopwatch.Restart();

            // 너무 작은 이동은 무시
            if (Math.Abs(currentPoint.X - lastUpdatePoint.X) < MinMoveDelta &&
                Math.Abs(currentPoint.Y - lastUpdatePoint.Y) < MinMoveDelta)
            {
                return;
            }
            lastUpdatePoint = currentPoint;

            // 마우스 위치로부터 목표 사각형만 계산하고, 실제 UI 업데이트는 Rendering 시에 수행
            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);
            pendingRect = new Rect(left, top, width, height);
            hasPendingUpdate = true;
        }

         private string? lastSizeText;
        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!hasPendingUpdate) return;
            var rect = pendingRect;
            hasPendingUpdate = false;

            // 선택 영역 업데이트
            Canvas.SetLeft(selectionRectangle, rect.Left);
            Canvas.SetTop(selectionRectangle, rect.Top);
            selectionRectangle.Width = rect.Width;
            selectionRectangle.Height = rect.Height;

            // 오버레이의 투명 영역(선택 영역)을 갱신
            selectionGeometry.Rect = rect;

            // 정보 텍스트는 고정 문구 유지
            // 크기 텍스트는 간헐적 갱신
            if (rect.Width > 5 && rect.Height > 5)
            {
                sizeTextBlock.Visibility = Visibility.Visible;
                string text = $"{(int)rect.Width} x {(int)rect.Height}";
                if (text != lastSizeText)
                {
                    sizeTextBlock.Text = text;
                    // 최소 측정으로 DesiredSize 확보
                    sizeTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    lastSizeText = text;
                }
                double tw = sizeTextBlock.DesiredSize.Width;
                double th = sizeTextBlock.DesiredSize.Height;
                const double margin = 8;
                // 선택 영역 내부의 오른쪽-아래 모서리에 배치
                Canvas.SetLeft(sizeTextBlock, rect.Left + rect.Width - tw - margin);
                Canvas.SetTop(sizeTextBlock, rect.Top + rect.Height - th - margin);
            }
            else
            {
                sizeTextBlock.Visibility = Visibility.Collapsed;
            }
        }
        private void UpdateMagnifier(Point mousePos)
        {
            if (magnifierBorder == null || magnifierImage == null || screenCapture == null)
                return;

            try
            {
                // 돋보기 표시
                magnifierBorder.Visibility = Visibility.Visible;

                // 마우스 위치를 픽셀 좌표로 변환
                var dpi = VisualTreeHelper.GetDpi(this);
                int centerX = (int)(mousePos.X * dpi.DpiScaleX);
                int centerY = (int)(mousePos.Y * dpi.DpiScaleY);

                // 확대할 영역 크기 계산
                int cropSize = (int)(MagnifierSize / MagnificationFactor);
                int halfCrop = cropSize / 2;

                // 크롭 영역 계산 (경계 체크)
                int cropX = Math.Max(0, Math.Min(centerX - halfCrop, screenCapture.PixelWidth - cropSize));
                int cropY = Math.Max(0, Math.Min(centerY - halfCrop, screenCapture.PixelHeight - cropSize));
                int cropW = Math.Min(cropSize, screenCapture.PixelWidth - cropX);
                int cropH = Math.Min(cropSize, screenCapture.PixelHeight - cropY);

                if (cropW > 0 && cropH > 0)
                {
                    // 영역 크롭
                    var croppedBitmap = new CroppedBitmap(screenCapture, new Int32Rect(cropX, cropY, cropW, cropH));
                    
                    // 확대
                    var transform = new ScaleTransform(MagnificationFactor, MagnificationFactor);
                    var transformedBitmap = new TransformedBitmap(croppedBitmap, transform);
                    
                    // DrawingVisual을 사용하여 십자선 추가
                    var drawingVisual = new DrawingVisual();
                    using (var drawingContext = drawingVisual.RenderOpen())
                    {
                        // 확대된 이미지 그리기
                        drawingContext.DrawImage(transformedBitmap, new Rect(0, 0, transformedBitmap.PixelWidth, transformedBitmap.PixelHeight));
                        
                        // 십자선 그리기 (중앙에 작은 십자가)
                        double centerXPos = transformedBitmap.PixelWidth / 2.0;
                        double centerYPos = transformedBitmap.PixelHeight / 2.0;

                        var redPen = new Pen(Brushes.Red, 2);
                        redPen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0); // 2픽셀 선, 2픽셀 공백
                        redPen.Freeze();

                        // 십자선 길이 (작게)
                        double crosshairLength = 30;

                        // 가로선 (중앙에서 좌우로 작게)
                        drawingContext.DrawLine(redPen, 
                            new Point(centerXPos - crosshairLength, centerYPos), 
                            new Point(centerXPos + crosshairLength, centerYPos));
                        // 세로선 (중앙에서 위아래로 작게)
                        drawingContext.DrawLine(redPen, 
                            new Point(centerXPos, centerYPos - crosshairLength), 
                            new Point(centerXPos, centerYPos + crosshairLength));
                    }
                    
                    // RenderTargetBitmap으로 변환
                    var renderBitmap = new RenderTargetBitmap(
                        transformedBitmap.PixelWidth,
                        transformedBitmap.PixelHeight,
                        96, 96,
                        PixelFormats.Pbgra32);
                    renderBitmap.Render(drawingVisual);
                    renderBitmap.Freeze();
                    
                    magnifierImage.Source = renderBitmap;
                }

                // 돋보기 위치 설정 (마우스 오른쪽 위)
                double offsetX = 20;
                double offsetY = -MagnifierSize - 20;
                
                double magnifierX = mousePos.X + offsetX;
                double magnifierY = mousePos.Y + offsetY;
                
                // 화면 경계 체크
                if (magnifierX + MagnifierSize > vWidth)
                    magnifierX = mousePos.X - MagnifierSize - offsetX;
                if (magnifierY < 0)
                    magnifierY = mousePos.Y + offsetX;

                Canvas.SetLeft(magnifierBorder, magnifierX);
                Canvas.SetTop(magnifierBorder, magnifierY);
                
                // 십자선 업데이트 (화면 전체에 걸쳐 표시)
                if (crosshairHorizontal != null && crosshairVertical != null)
                {
                    crosshairHorizontal.Visibility = Visibility.Visible;
                    crosshairVertical.Visibility = Visibility.Visible;
                    
                    // 가로선 (화면 왼쪽 끝에서 오른쪽 끝까지)
                    crosshairHorizontal.X1 = 0;
                    crosshairHorizontal.X2 = vWidth;
                    crosshairHorizontal.Y1 = mousePos.Y;
                    crosshairHorizontal.Y2 = mousePos.Y;
                    
                    // 세로선 (화면 위쪽 끝에서 아래쪽 끝까지)
                    crosshairVertical.X1 = mousePos.X;
                    crosshairVertical.X2 = mousePos.X;
                    crosshairVertical.Y1 = 0;
                    crosshairVertical.Y2 = vHeight;
                }
            }
            catch
            {
                // 돋보기 업데이트 실패 시 숨김
                magnifierBorder.Visibility = Visibility.Collapsed;
                if (crosshairHorizontal != null) crosshairHorizontal.Visibility = Visibility.Collapsed;
                if (crosshairVertical != null) crosshairVertical.Visibility = Visibility.Collapsed;
            }
        }
        private void SnippingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsCancelled = true;
                DialogResult = false;
                Close();
            }
        }

        private void SnippingWindow_Deactivated(object? sender, EventArgs e)
        {
            // If user is dragging and the window deactivates (e.g., OS focus glitch), re-activate and recapture
            if (isSelecting)
            {
                try
                {
                    Topmost = true;
                    Activate();
                    if (Mouse.Captured != this && Mouse.LeftButton == MouseButtonState.Pressed)
                    {
                        Mouse.Capture(this);
                    }
                }
                catch { }
            }
        }

        private void SnippingWindow_LostMouseCapture(object? sender, MouseEventArgs e)
        {
            // During drag, if capture is lost while the left button is still down, recapture to keep visuals updating
            if (isSelecting && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    Mouse.Capture(this);
                }
                catch { }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 창이 닫힐 때 리소스 정리
            CleanupResources();
            base.OnClosed(e);
        }

        private void CleanupResources()
        {
            if (disposed) return;

            try
            {
                // 메모리 정리 타이머 정지
                if (memoryCleanupTimer != null)
                {
                    memoryCleanupTimer.Stop();
                    memoryCleanupTimer = null;
                }

                // 이벤트 핸들러 해제
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                
                // 마우스 캡처 해제
                if (Mouse.Captured == this)
                {
                    Mouse.Capture(null);
                }

                // 스톱워치 정지
                moveStopwatch?.Stop();

                // 캐시 모드 해제로 GPU 메모리 정리
                if (overlayPath?.CacheMode is BitmapCache cache)
                {
                    overlayPath.CacheMode = null;
                }
                
                if (selectionRectangle?.CacheMode is BitmapCache cache2)
                {
                    selectionRectangle.CacheMode = null;
                }

                // 강제 가비지 컬렉션 (메모리 누수 방지)
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { /* 정리 중 오류 무시 */ }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    CleanupResources();
                }
                disposed = true;
            }
        }



        ~SnippingWindow()
        {
            Dispose(false);
        }
        public void EnableInstantEditMode()
        {
            instantEditMode = true;
        }

        private void ShowEditToolbar()
        {
            // 선택 모드 종료
            isSelecting = false;
            
            // 마우스 이벤트 핸들러 해제
            MouseMove -= SnippingWindow_MouseMove;
            MouseLeftButtonDown -= SnippingWindow_MouseLeftButtonDown;
            MouseLeftButtonUp -= SnippingWindow_MouseLeftButtonUp;
            
            // 돋보기 숨기기
            if (magnifierBorder != null)
                magnifierBorder.Visibility = Visibility.Collapsed;
            if (crosshairHorizontal != null)
                crosshairHorizontal.Visibility = Visibility.Collapsed;
            if (crosshairVertical != null)
                crosshairVertical.Visibility = Visibility.Collapsed;
            
            // 크기 표시 숨기기
            sizeTextBlock.Visibility = Visibility.Collapsed;
            
            // 마우스 커서 변경
            Cursor = Cursors.Arrow;
            
            // 선택 영역 위치 계산
            double selectionLeft = Canvas.GetLeft(selectionRectangle);
            double selectionTop = Canvas.GetTop(selectionRectangle);
            double selectionWidth = selectionRectangle.Width;
            double selectionHeight = selectionRectangle.Height;
            
            // 편집 툴바 생성
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(250, 255, 255, 255)),
                Height = 48
            };
            
            // 툴바에 그림자 효과 추가
            toolbar.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 8,
                ShadowDepth = 2,
                Opacity = 0.3
            };


            // 펜 버튼
            var penButton = CreateToolButton("🖊️", "펜");
            penButton.Click += (s, e) => 
            {
                currentTool = "펜";
                SetActiveToolButton(penButton); // 활성화 표시
                ShowColorPalette("펜", selectionLeft, selectionTop + selectionHeight + 60);
                EnableDrawingMode();
            };
            
            // 형광펜 버튼
            var highlighterButton = CreateToolButton("🖍️", "형광펜");
            highlighterButton.Click += (s, e) => 
            {
                currentTool = "형광펜";
                selectedColor = Colors.Yellow; // 
                SetActiveToolButton(highlighterButton);
                ShowColorPalette("형광펜", selectionLeft, selectionTop + selectionHeight + 60);
                EnableDrawingMode();
            };
            // 텍스트 버튼
            var textButton = CreateToolButton("📝", "텍스트");
            textButton.Click += (s, e) => ShowColorPalette("텍스트", selectionLeft, selectionTop + selectionHeight + 60);
            
            // 도형 버튼
            var shapeButton = CreateToolButton("🔲", "도형");
            shapeButton.Click += (s, e) => ShowColorPalette("도형", selectionLeft, selectionTop + selectionHeight + 60);
            
            // 모자이크 버튼
            var mosaicButton = CreateToolButton("🎨", "모자이크");
            mosaicButton.Click += (s, e) => { currentTool = "모자이크"; HideColorPalette(); };
            
            // 지우개 버튼
            var eraserButton = CreateToolButton("🧹", "지우개");
            eraserButton.Click += (s, e) => { currentTool = "지우개"; HideColorPalette(); };
            
            // 구분선
            var separator = new Border
            {
                Width = 1,
                Height = 30,
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                Margin = new Thickness(5, 0, 5, 0)
            };
            
            // 완료 버튼
            var doneButton = new Button
            {
                Content = "✓",
                Width = 36,
                Height = 36,
                Margin = new Thickness(4),
                ToolTip = "완료",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), // 투명 배경
                Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100)), // 회색 텍스트
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Style = null
            };
            
            // 호버 효과
            doneButton.MouseEnter += (s, e) =>
            {
                doneButton.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                doneButton.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)); // 호버 시 파란색
            };
            
            doneButton.MouseLeave += (s, e) =>
            {
                doneButton.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                doneButton.Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100)); // 기본 회색
            };
            
            // 완료 버튼 클릭 이벤트
            doneButton.Click += (s, e) =>
            {
                // 그린 내용을 이미지에 합성
                if (drawnElements.Count > 0)
                {
                    SaveDrawingsToImage();
                }
                
                DialogResult = true;
                Close();
            };
            toolbar.Children.Add(penButton);
            toolbar.Children.Add(highlighterButton);
            toolbar.Children.Add(textButton);
            toolbar.Children.Add(shapeButton);
            toolbar.Children.Add(mosaicButton);
            toolbar.Children.Add(eraserButton);
            toolbar.Children.Add(separator);
            toolbar.Children.Add(doneButton);
            
            // 툴바를 선택 영역 바로 아래에 배치
            canvas.Children.Add(toolbar);
            
            // 툴바 크기 측정
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double toolbarWidth = toolbar.DesiredSize.Width;
            
            // 선택 영역 중앙 하단에 배치
            double toolbarLeft = selectionLeft + (selectionWidth - toolbarWidth) / 2;
            double toolbarTop = selectionTop + selectionHeight + 10;
            
            // 화면 경계 체크
            if (toolbarLeft + toolbarWidth > vWidth)
                toolbarLeft = vWidth - toolbarWidth - 10;
            if (toolbarLeft < 10)
                toolbarLeft = 10;
            if (toolbarTop + 44 > vHeight)
                toolbarTop = selectionTop - 44 - 10; // 위쪽에 배치
            
            Canvas.SetLeft(toolbar, toolbarLeft);
            Canvas.SetTop(toolbar, toolbarTop);

            // 펜을 기본 도구로 선택하고 빨간색으로 설정
            currentTool = "펜";
            selectedColor = Colors.Red;
            SetActiveToolButton(penButton);
            ShowColorPalette("펜", selectionLeft, selectionTop + selectionHeight + 60);
            EnableDrawingMode();
        }

        private Button CreateToolButton(string icon, string tooltip)
        {
            var button = new Button
            {
                Content = icon,
                Width = 36,
                Height = 36,
                Margin = new Thickness(4),
                ToolTip = tooltip,
                FontSize = 16,
                Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Style = null
            };
            
            // 호버 효과
            button.MouseEnter += (s, e) =>
            {
                if (button != activeToolButton)
                    button.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            };
            
            button.MouseLeave += (s, e) =>
            {
                if (button != activeToolButton)
                    button.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            };
            
            return button;
        }
        
        private void SetActiveToolButton(Button button)
        {
            // 이전 활성 버튼 초기화
            if (activeToolButton != null)
            {
                activeToolButton.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            }
            
            // 새 활성 버튼 설정
            activeToolButton = button;
            if (activeToolButton != null)
            {
                activeToolButton.Background = new SolidColorBrush(Color.FromArgb(80, 0, 120, 212)); // 파란색 호버
            }
        }
        private void ShowColorPalette(string tool, double left, double top)
        {
            currentTool = tool;
            
            // 기존 팔레트 제거
            HideColorPalette();
            
            // PreviewWindow 스타일의 색상 팔레트 생성
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // 배경
            var background = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(250, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(208, 215, 229)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = mainGrid
            };
            
            background.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 5,
                ShadowDepth = 1,
                Opacity = 0.2
            };
            
            // 색상 섹션
            var colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            var colorLabel = new TextBlock
            {
                Text = "색상",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            colorSection.Children.Add(colorLabel);
            
            var colorGrid = new WrapPanel { Width = 130 };
            
            foreach (var c in SharedColorPalette)
            {
                colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
            }
            
            foreach (var c in customColors)
            {
                colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
            }
            
            // [+] 버튼
            var addButton = new Button
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                Content = "+",
                Cursor = Cursors.Hand,
                ToolTip = "색상 추가"
            };
            
            addButton.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    if (!customColors.Contains(newColor))
                    {
                        customColors.Add(newColor);
                    }
                    colorGrid.Children.Remove(addButton);
                    var newSwatch = CreateColorSwatch(newColor, colorGrid);
                    colorGrid.Children.Add(newSwatch);
                    colorGrid.Children.Add(addButton);
                    selectedColor = newColor;
                    UpdateColorSelection(colorGrid);
                }
            };
            colorGrid.Children.Add(addButton);
            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);
            
            // 구분선
            var separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 5, 15, 5)
            };
            Grid.SetColumn(separator, 1);
            mainGrid.Children.Add(separator);
            
            // 두께 섹션
            var thicknessSection = new StackPanel();
            var thicknessLabel = new TextBlock
            {
                Text = "두께",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            thicknessSection.Children.Add(thicknessLabel);
            
            var thicknessList = new StackPanel();
            int[] presets = new int[] { 1, 3, 5, 8, 12 };
            
            foreach (var p in presets)
            {
                var item = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var line = new Border
                {
                    Height = p,
                    Width = 30,
                    Background = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(line, 0);
                item.Children.Add(line);
                
                var text = new TextBlock
                {
                    Text = $"{p}px",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(text, 1);
                item.Children.Add(text);

                // 두께 선택 이벤트 추가
                int thickness = p; // 클로저 문제 방지
                item.MouseLeftButtonDown += (s, e) =>
                {
                    if (currentTool == "형광펜")
                    {
                        highlightThickness = thickness;
                    }
                    else
                    {
                        penThickness = thickness;
                    }
                    
                    // 시각적 피드백 (선택된 항목 강조)
                    foreach (var child in thicknessList.Children)
                    {
                        if (child is Grid g)
                        {
                            g.Background = Brushes.Transparent;
                        }
                    }
                    item.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
                };

                thicknessList.Children.Add(item);
            }
            thicknessSection.Children.Add(thicknessList);
            Grid.SetColumn(thicknessSection, 2);
            mainGrid.Children.Add(thicknessSection);
            
            // 캔버스에 추가
            canvas.Children.Add(background);
            Canvas.SetLeft(background, left);
            Canvas.SetTop(background, top);
            
            colorPalette = background;
        }
        
        private Border CreateColorSwatch(Color c, WrapPanel parentPanel)
        {
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(c),
                BorderBrush = (c == selectedColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(c == selectedColor ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };
            
            if (c == selectedColor)
            {
                swatch.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }
            
            swatch.MouseLeftButtonDown += (s, e) =>
            {
                selectedColor = c;
                UpdateColorSelection(parentPanel);
                
                // 색상 선택 후 그리기 모드 활성화
                EnableDrawingMode();
            };
            
            return swatch;
        }
        
        private void UpdateColorSelection(WrapPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                {
                    bool isSelected = (sc.Color == selectedColor);
                    b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    b.BorderThickness = new Thickness(isSelected ? 2 : 1);
                    b.Effect = isSelected ? new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 2,
                        ShadowDepth = 0,
                        Opacity = 0.5
                    } : null;
                }
            }
        }
        
        private void HideColorPalette()
        {
            if (colorPalette != null && canvas.Children.Contains(colorPalette))
            {
                canvas.Children.Remove(colorPalette);
                colorPalette = null;
            }
        }
                private void EnableDrawingMode()
        {
            isDrawingEnabled = true;
            Cursor = Cursors.Pen;
            
            // 마우스 이벤트 다시 등록
            canvas.MouseLeftButtonDown += Canvas_DrawMouseDown;
            canvas.MouseMove += Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp += Canvas_DrawMouseUp;
        }
        
        private void Canvas_DrawMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawingEnabled) return;
            
            Point clickPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(clickPoint))
                return;
            
            lastDrawPoint = clickPoint;
            
            // 새 선 시작
            Color strokeColor = selectedColor;
            if (currentTool == "형광펜")
            {
                // 형광펜은 색상 자체에 투명도 적용 (알파값 128 = 50%)
                strokeColor = Color.FromArgb(128, selectedColor.R, selectedColor.G, selectedColor.B);
            }

            currentPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = currentTool == "형광펜" ? highlightThickness : penThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };

            // 형광펜일 경우 화면 표시용 투명도도 적용
            if (currentTool == "형광펜")
            {
                currentPolyline.Opacity = 0.5;
            }
            
            currentPolyline.Points.Add(lastDrawPoint);
            canvas.Children.Add(currentPolyline);
            drawnElements.Add(currentPolyline);
            
            canvas.CaptureMouse();
        }
        
        private void Canvas_DrawMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawingEnabled || currentPolyline == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            Point currentPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(currentPoint))
                return;
            
            // 부드러운 선을 위해 중간 점들을 보간
            double distance = Math.Sqrt(
                Math.Pow(currentPoint.X - lastDrawPoint.X, 2) + 
                Math.Pow(currentPoint.Y - lastDrawPoint.Y, 2));
            
            // 거리가 2픽셀 이상일 때만 중간 점 추가
            if (distance > 2)
            {
                int steps = (int)(distance / 2); // 2픽셀마다 점 추가
                for (int i = 1; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    Point interpolated = new Point(
                        lastDrawPoint.X + (currentPoint.X - lastDrawPoint.X) * t,
                        lastDrawPoint.Y + (currentPoint.Y - lastDrawPoint.Y) * t);
                    currentPolyline.Points.Add(interpolated);
                }
            }
            
            currentPolyline.Points.Add(currentPoint);
            lastDrawPoint = currentPoint;
        }
        
        private void Canvas_DrawMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawingEnabled) return;
            
            currentPolyline = null;
            canvas.ReleaseMouseCapture();
        }
        private void SaveDrawingsToImage()
        {
            // 선택 영역의 위치와 크기
            double selectionLeft = Canvas.GetLeft(selectionRectangle);
            double selectionTop = Canvas.GetTop(selectionRectangle);
            double selectionWidth = selectionRectangle.Width;
            double selectionHeight = selectionRectangle.Height;
            
            // DPI 스케일 계산
            var dpi = VisualTreeHelper.GetDpi(this);
            int pixelWidth = (int)Math.Round(selectionWidth * dpi.DpiScaleX);
            int pixelHeight = (int)Math.Round(selectionHeight * dpi.DpiScaleY);
            
            // 선택 영역만 렌더링
            var renderBitmap = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                96, 96,
                PixelFormats.Pbgra32);
            
            // 선택 영역으로 이동한 DrawingVisual 생성
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // 배경 이미지 그리기 (선택 영역만)
                if (screenCapture != null)
                {
                    var sourceRect = new Rect(selectionLeft, selectionTop, selectionWidth, selectionHeight);
                    var destRect = new Rect(0, 0, selectionWidth, selectionHeight);
                    drawingContext.DrawImage(new CroppedBitmap(screenCapture, 
                        new Int32Rect((int)selectionLeft, (int)selectionTop, 
                        (int)selectionWidth, (int)selectionHeight)), destRect);
                }
                
                // 그린 요소들 그리기 (선택 영역 기준으로 오프셋 조정)
                foreach (var element in drawnElements)
                {
                    if (element is Polyline polyline)
                    {
                        var adjustedPoints = new PointCollection();
                        foreach (var point in polyline.Points)
                        {
                            adjustedPoints.Add(new Point(point.X - selectionLeft, point.Y - selectionTop));
                        }

                        var pen = new Pen(polyline.Stroke, polyline.StrokeThickness)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round,
                            LineJoin = PenLineJoin.Round
                        };

                        // 투명도가 있는 경우 PushOpacity 사용
                        if (polyline.Opacity < 1.0)
                        {
                            drawingContext.PushOpacity(polyline.Opacity);
                        }

                        for (int i = 0; i < adjustedPoints.Count - 1; i++)
                        {
                            drawingContext.DrawLine(pen, adjustedPoints[i], adjustedPoints[i + 1]);
                        }

                        // PushOpacity를 사용했으면 Pop 필요
                        if (polyline.Opacity < 1.0)
                        {
                            drawingContext.Pop();
                        }
                    }
                }
            }
            
            renderBitmap.Render(drawingVisual);
            renderBitmap.Freeze();
            
            // SelectedFrozenImage 업데이트
            SelectedFrozenImage = renderBitmap;
        }
        private bool IsPointInSelection(Point point)
        {
            if (selectionRectangle == null) return false;
            
            double left = Canvas.GetLeft(selectionRectangle);
            double top = Canvas.GetTop(selectionRectangle);
            double right = left + selectionRectangle.Width;
            double bottom = top + selectionRectangle.Height;
            
            return point.X >= left && point.X <= right && 
                   point.Y >= top && point.Y <= bottom;
        }
    }
}