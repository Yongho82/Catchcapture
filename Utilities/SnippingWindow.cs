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
         // 도형 관련 필드
        private ShapeType shapeType = ShapeType.Rectangle;
        private double shapeBorderThickness = 2;
        private double shapeFillOpacity = 0.5; // 기본 투명도 50%
        private bool shapeIsFilled = false;
        private UIElement? tempShape;
        private Point shapeStartPoint;
        private bool isDrawingShape = false;
        // 텍스트 편집 관련 필드
        private TextBox? selectedTextBox;
        private int textFontSize = 16;
        private string textFontFamily = "Malgun Gothic";
        private Rectangle? textSelectionBorder; // 선택된 텍스트 테두리
        private Button? textDeleteButton; // 삭제 버튼
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
            textButton.Click += (s, e) => 
            {
                currentTool = "텍스트";
                SetActiveToolButton(textButton);
                ShowColorPalette("텍스트", selectionLeft, selectionTop + selectionHeight + 60);
                EnableTextMode();
            };            
            // 도형 버튼
            var shapeButton = CreateToolButton("🔲", "도형");
            shapeButton.Click += (s, e) => 
            {
                currentTool = "도형";
                SetActiveToolButton(shapeButton);
                ShowColorPalette("도형", selectionLeft, selectionTop + selectionHeight + 60);
                EnableShapeMode();
            };
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
            // 기존 팔레트 제거
            HideColorPalette();
            
            // 팔레트 컨테이너 생성
            var background = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Width = 320 // 너비 증가
            };
            
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 옵션
            background.Child = mainGrid;
            
            background.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 5,
                ShadowDepth = 1,
                Opacity = 0.2
            };
            
            // 1. 색상 섹션 (공통)
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
                Content = "+",
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            addButton.Click += (s, e) =>
            {
                var colorDialog = new System.Windows.Forms.ColorDialog();
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                    customColors.Add(newColor);
                    colorGrid.Children.Insert(colorGrid.Children.Count - 1, CreateColorSwatch(newColor, colorGrid));
                    selectedColor = newColor;
                    UpdateColorSelection(colorGrid);
                }
            };
            colorGrid.Children.Add(addButton);
            
            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);
            
            // 2. 구분선
            var separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 5, 15, 5)
            };
            Grid.SetColumn(separator, 1);
            mainGrid.Children.Add(separator);
            
            // 3. 옵션 섹션 (도구별 분기)
            var optionSection = new StackPanel();
            Grid.SetColumn(optionSection, 2);
            mainGrid.Children.Add(optionSection);

            if (tool == "텍스트")
            {
                // [텍스트 옵션: 폰트 크기 및 종류]
                var optionLabel = new TextBlock
                {
                    Text = "텍스트 옵션",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                optionSection.Children.Add(optionLabel);

                // 폰트 크기 콤보박스
                var sizePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                sizePanel.Children.Add(new TextBlock { Text = "크기:", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
                
                var sizeCombo = new ComboBox { Width = 60, Height = 25 };
                int[] sizes = { 10, 12, 14, 16, 18, 24, 36, 48, 72 };
                foreach (var s in sizes) sizeCombo.Items.Add(s);
                sizeCombo.SelectedItem = textFontSize;
                sizeCombo.SelectionChanged += (s, e) => 
                {
                    if (sizeCombo.SelectedItem is int newSize)
                    {
                        textFontSize = newSize;
                        if (selectedTextBox != null) selectedTextBox.FontSize = newSize;
                    }
                };
                sizePanel.Children.Add(sizeCombo);
                optionSection.Children.Add(sizePanel);

                // 폰트 종류 콤보박스
                var fontPanel = new StackPanel { Orientation = Orientation.Horizontal };
                fontPanel.Children.Add(new TextBlock { Text = "폰트:", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
                
                var fontCombo = new ComboBox { Width = 100, Height = 25 };
                string[] fonts = { "Malgun Gothic", "Arial", "Consolas", "Gulim", "Dotum" };
                foreach (var f in fonts) fontCombo.Items.Add(f);
                fontCombo.SelectedItem = textFontFamily;
                fontCombo.SelectionChanged += (s, e) => 
                {
                    if (fontCombo.SelectedItem is string newFont)
                    {
                        textFontFamily = newFont;
                        if (selectedTextBox != null) selectedTextBox.FontFamily = new FontFamily(newFont);
                    }
                };
                fontPanel.Children.Add(fontCombo);
                optionSection.Children.Add(fontPanel);
            }
            else if (tool == "도형")
            {
                // [도형 옵션]
                var optionLabel = new TextBlock
                {
                    Text = "도형 옵션",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                optionSection.Children.Add(optionLabel);

                // 1. 도형 종류 선택
                var shapeTypePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                
                var rectBtn = CreateShapeOptionButton("□", ShapeType.Rectangle);
                var ellipseBtn = CreateShapeOptionButton("○", ShapeType.Ellipse);
                var lineBtn = CreateShapeOptionButton("╱", ShapeType.Line);
                var arrowBtn = CreateShapeOptionButton("↗", ShapeType.Arrow);
                
                shapeTypePanel.Children.Add(rectBtn);
                shapeTypePanel.Children.Add(ellipseBtn);
                shapeTypePanel.Children.Add(lineBtn);
                shapeTypePanel.Children.Add(arrowBtn);
                
                optionSection.Children.Add(shapeTypePanel);

                // 2. 두께 슬라이더
                var thicknessPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                thicknessPanel.Children.Add(new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center, Width = 35, FontSize = 11 });
                
                var thicknessSlider = new Slider
                {
                    Minimum = 1,
                    Maximum = 10,
                    Value = shapeBorderThickness,
                    Width = 80,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 1,
                    VerticalAlignment = VerticalAlignment.Center
                };
                thicknessSlider.ValueChanged += (s, e) => { shapeBorderThickness = thicknessSlider.Value; };
                thicknessPanel.Children.Add(thicknessSlider);
                optionSection.Children.Add(thicknessPanel);

                // 3. 채우기 체크박스 및 투명도 슬라이더
                var fillPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                
                var fillCheckBox = new CheckBox
                {
                    Content = "채우기",
                    IsChecked = shapeIsFilled,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                fillCheckBox.Checked += (s, e) => { shapeIsFilled = true; };
                fillCheckBox.Unchecked += (s, e) => { shapeIsFilled = false; };
                fillPanel.Children.Add(fillCheckBox);

                // 투명도 슬라이더
                var opacitySlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = shapeFillOpacity,
                    Width = 60,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "채우기 투명도"
                };
                opacitySlider.ValueChanged += (s, e) => { shapeFillOpacity = opacitySlider.Value; };
                fillPanel.Children.Add(opacitySlider);

                optionSection.Children.Add(fillPanel);
            }
            
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
                
                // [수정] 도구에 따라 적절한 모드 활성화
                if (currentTool == "텍스트")
                {
                    EnableTextMode();
                }
                else
                {
                    EnableDrawingMode();
                }
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

        private void EnableTextMode()
        {
            isDrawingEnabled = false;
            Cursor = Cursors.IBeam;
            
            // 텍스트 입력용 마우스 이벤트 등록
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown; // 그리기 이벤트 제거
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;
            
            canvas.MouseLeftButtonDown += Canvas_TextMouseDown;
        }

        private void EnableShapeMode()
        {
            isDrawingEnabled = true;
            canvas.Cursor = Cursors.Cross;
            
            // 마우스 이벤트 재설정
            canvas.MouseLeftButtonDown -= Canvas_TextMouseDown;
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown;
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;

            canvas.MouseLeftButtonDown += Canvas_DrawMouseDown;
            canvas.MouseMove += Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp += Canvas_DrawMouseUp;
            
            // 텍스트 선택 해제
            if (selectedTextBox != null)
            {
                ClearTextSelection();
            }
        }

        private Button CreateShapeOptionButton(string content, ShapeType type)
        {
            var button = new Button
            {
                Content = content,
                Width = 30,
                Height = 24,
                Margin = new Thickness(2, 0, 2, 0),
                FontSize = 14,
                Padding = new Thickness(0, -4, 0, 0), // [수정] 아이콘 위치 상향 조정
                Background = shapeType == type ? new SolidColorBrush(Color.FromRgb(200, 230, 255)) : Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            button.Click += (s, e) =>
            {
                shapeType = type;
                // UI 업데이트 (형제 버튼들 배경색 초기화)
                if (button.Parent is Panel parent)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Button btn) btn.Background = Brushes.White;
                    }
                    button.Background = new SolidColorBrush(Color.FromRgb(200, 230, 255));
                }
            };
            return button;
        }

        private UIElement? CreateShape(Point start, Point current)
        {
            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            Shape? shape = null;

            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    shape = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(selectedColor),
                        StrokeThickness = shapeBorderThickness,
                        // [수정] 투명도 적용
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb((byte)(shapeFillOpacity * 255), selectedColor.R, selectedColor.G, selectedColor.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    break;

                case ShapeType.Ellipse:
                    shape = new Ellipse
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(selectedColor),
                        StrokeThickness = shapeBorderThickness,
                        // [수정] 투명도 적용
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb((byte)(shapeFillOpacity * 255), selectedColor.R, selectedColor.G, selectedColor.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    break;

                case ShapeType.Line:
                    shape = new Line
                    {
                        X1 = start.X, Y1 = start.Y,
                        X2 = current.X, Y2 = current.Y,
                        Stroke = new SolidColorBrush(selectedColor),
                        StrokeThickness = shapeBorderThickness
                    };
                    break;

                case ShapeType.Arrow:
                    return CreateArrow(start, current);
            }
            return shape;
        }

        private UIElement CreateArrow(Point start, Point end)
        {
            Canvas arrowCanvas = new Canvas();
            Line line = new Line
            {
                X1 = start.X, Y1 = start.Y,
                X2 = end.X, Y2 = end.Y,
                Stroke = new SolidColorBrush(selectedColor),
                StrokeThickness = shapeBorderThickness
            };
            arrowCanvas.Children.Add(line);

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 15 + shapeBorderThickness * 2;
            double arrowAngle = Math.PI / 6;

            Point arrowPoint1 = new Point(end.X - arrowLength * Math.Cos(angle - arrowAngle), end.Y - arrowLength * Math.Sin(angle - arrowAngle));
            Point arrowPoint2 = new Point(end.X - arrowLength * Math.Cos(angle + arrowAngle), end.Y - arrowLength * Math.Sin(angle + arrowAngle));

            Polygon arrowHead = new Polygon
            {
                Points = new PointCollection { end, arrowPoint1, arrowPoint2 },
                Fill = new SolidColorBrush(selectedColor),
                Stroke = new SolidColorBrush(selectedColor),
                StrokeThickness = 1
            };
            arrowCanvas.Children.Add(arrowHead);
            return arrowCanvas;
        }

        private void UpdateShapeProperties(UIElement shape, Point start, Point current)
        {
            if (shape == null) return;
            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            if (shape is Rectangle rect)
            {
                rect.Width = width; rect.Height = height;
                Canvas.SetLeft(rect, left); Canvas.SetTop(rect, top);
            }
            else if (shape is Ellipse ellipse)
            {
                ellipse.Width = width; ellipse.Height = height;
                Canvas.SetLeft(ellipse, left); Canvas.SetTop(ellipse, top);
            }
            else if (shape is Line shapeLine)
            {
                shapeLine.X1 = start.X; shapeLine.Y1 = start.Y;
                shapeLine.X2 = current.X; shapeLine.Y2 = current.Y;
            }
            else if (shape is Canvas arrowCanvas)
            {
                arrowCanvas.Children.Clear();
                Line line = new Line
                {
                    X1 = start.X, Y1 = start.Y,
                    X2 = current.X, Y2 = current.Y,
                    Stroke = new SolidColorBrush(selectedColor),
                    StrokeThickness = shapeBorderThickness
                };
                arrowCanvas.Children.Add(line);

                double angle = Math.Atan2(current.Y - start.Y, current.X - start.X);
                double arrowLength = 15 + shapeBorderThickness * 2;
                double arrowAngle = Math.PI / 6;

                Point arrowPoint1 = new Point(current.X - arrowLength * Math.Cos(angle - arrowAngle), current.Y - arrowLength * Math.Sin(angle - arrowAngle));
                Point arrowPoint2 = new Point(current.X - arrowLength * Math.Cos(angle + arrowAngle), current.Y - arrowLength * Math.Sin(angle + arrowAngle));

                Polygon arrowHead = new Polygon
                {
                    Points = new PointCollection { current, arrowPoint1, arrowPoint2 },
                    Fill = new SolidColorBrush(selectedColor),
                    Stroke = new SolidColorBrush(selectedColor),
                    StrokeThickness = 1
                };
                arrowCanvas.Children.Add(arrowHead);
            }
        }

        // [추가] 부모 컨트롤 찾기 헬퍼
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
            if (child is T parent) return parent; // 자기 자신이 찾는 타입이면 반환

            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            
            return FindParent<T>(parentObject);
        }

        // [추가] 텍스트 박스 편집 모드 활성화 (버튼 생성 및 이벤트 연결)
        private void EnableTextBoxEditing(TextBox textBox)
        {
            textBox.IsReadOnly = false;
            textBox.Background = Brushes.Transparent;
            textBox.BorderThickness = new Thickness(2);
            textBox.BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue);
            
            double left = Canvas.GetLeft(textBox);
            double top = Canvas.GetTop(textBox);
            
            // 확정 버튼 생성
            var confirmButton = new Button
            {
                Content = "✓",
                Width = 24,
                Height = 24,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "텍스트 확정 (Ctrl+Enter)"
            };

            // 취소 버튼 생성
            var cancelButton = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "취소"
            };

            // 이벤트 연결
            confirmButton.Click += (s, e) => ConfirmTextBox(textBox, confirmButton, cancelButton);
            cancelButton.Click += (s, e) => 
            {
                canvas.Children.Remove(textBox);
                canvas.Children.Remove(confirmButton);
                canvas.Children.Remove(cancelButton);
                drawnElements.Remove(textBox);
                selectedTextBox = null;
            };

            // 위치 설정
            Canvas.SetLeft(confirmButton, left + 105);
            Canvas.SetTop(confirmButton, top - 28);
            Canvas.SetLeft(cancelButton, left + 77);
            Canvas.SetTop(cancelButton, top - 28);

            canvas.Children.Add(confirmButton);
            canvas.Children.Add(cancelButton);

            // 태그 업데이트 (버튼 참조 저장)
            textBox.Tag = new { confirmButton, cancelButton };
            
            // 키 이벤트 핸들러 재등록 (중복 방지)
            textBox.KeyDown -= TextBox_KeyDown;
            textBox.KeyDown += TextBox_KeyDown;
            
            textBox.Focus();
        }

        // [추가] 텍스트 박스 키 이벤트 핸들러
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
            dynamic tags = textBox.Tag;
            if (tags == null) return;
            
            // Tag에서 버튼 가져오기
            Button confirmButton = tags.confirmButton;
            Button cancelButton = tags.cancelButton;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ConfirmTextBox(textBox, confirmButton, cancelButton);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // 취소 버튼 클릭과 동일
                canvas.Children.Remove(textBox);
                canvas.Children.Remove(confirmButton);
                canvas.Children.Remove(cancelButton);
                drawnElements.Remove(textBox);
                selectedTextBox = null;
                e.Handled = true;
            }
        }

        // [수정] 캔버스 클릭 핸들러
        private void Canvas_TextMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 버튼이나 텍스트박스 클릭 시 무시 (이벤트 버블링 방지 -> X버튼 클릭 문제 해결)
            if (e.OriginalSource is DependencyObject obj && 
                (FindParent<Button>(obj) != null || FindParent<TextBox>(obj) != null))
            {
                return;
            }

            Point clickPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(clickPoint))
                return;
            
            // 기존 선택 해제
            ClearTextSelection();    

            // 새 텍스트박스 생성
            var textBox = new TextBox
            {
                MinWidth = 100,
                MinHeight = 30,
                FontSize = textFontSize,
                FontFamily = new FontFamily(textFontFamily),
                Foreground = new SolidColorBrush(selectedColor),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            Canvas.SetLeft(textBox, clickPoint.X);
            Canvas.SetTop(textBox, clickPoint.Y);
            
            canvas.Children.Add(textBox);
            drawnElements.Add(textBox);
            selectedTextBox = textBox;
            
            // 드래그 이벤트 등록
            textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            textBox.PreviewMouseMove += TextBox_PreviewMouseMove;
            textBox.PreviewMouseLeftButtonUp += TextBox_PreviewMouseLeftButtonUp;

            // 편집 모드 활성화 (버튼 생성 등)
            EnableTextBoxEditing(textBox);
        }

        // 텍스트박스 드래그 관련 변수
        private bool isTextDragging = false;
        private Point textDragStartPoint;

        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 이미 편집 중이면(IsReadOnly == false) 간섭하지 않음 (텍스트 선택, 커서 이동 등 허용)
                if (!textBox.IsReadOnly) return;

                // 더블클릭으로 확정된 텍스트 수정 가능
                if (e.ClickCount == 2)
                {
                    // 선택 UI(점선, 휴지통) 제거
                    ClearTextSelection();
                    
                    // 편집 모드 활성화 (확정/취소 버튼 다시 생성)
                    EnableTextBoxEditing(textBox);
                    
                    textBox.SelectAll();
                    e.Handled = true;
                    return;
                }

                // 선택 표시 (점선, 휴지통)
                ShowTextSelection(textBox);
                selectedTextBox = textBox;
                
                // 확정된 텍스트박스는 바로 드래그 가능
                isTextDragging = true;
                textDragStartPoint = e.GetPosition(canvas);
                textBox.CaptureMouse();
                e.Handled = true;
            }
        }

        private void TextBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isTextDragging && sender is TextBox textBox)
            {
                Point currentPoint = e.GetPosition(canvas);
                double offsetX = currentPoint.X - textDragStartPoint.X;
                double offsetY = currentPoint.Y - textDragStartPoint.Y;
                
                double newLeft = Canvas.GetLeft(textBox) + offsetX;
                double newTop = Canvas.GetTop(textBox) + offsetY;
                
                Canvas.SetLeft(textBox, newLeft);
                Canvas.SetTop(textBox, newTop);
                
                // 점선 테두리도 함께 이동
                if (textSelectionBorder != null)
                {
                    Canvas.SetLeft(textSelectionBorder, newLeft - 2);
                    Canvas.SetTop(textSelectionBorder, newTop - 2);
                }
                
                // 삭제 버튼도 함께 이동
                if (textDeleteButton != null)
                {
                    double width = textBox.ActualWidth > 0 ? textBox.ActualWidth : textBox.MinWidth;
                    Canvas.SetLeft(textDeleteButton, newLeft + width - 20);
                    Canvas.SetTop(textDeleteButton, newTop - 28);
                }
                
                textDragStartPoint = currentPoint;
            }
        }

        private void TextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isTextDragging && sender is TextBox textBox)
            {
                isTextDragging = false;
                textBox.ReleaseMouseCapture();
            }
        }

        private void ConfirmTextBox(TextBox textBox, Button confirmButton, Button cancelButton)
        {
            if (textBox == null) return;
            
            // 텍스트박스 확정 처리
            textBox.IsReadOnly = true;
            textBox.BorderThickness = new Thickness(0);
            textBox.Background = Brushes.Transparent;
            textBox.Cursor = Cursors.Arrow;
            
            // 확정/취소 버튼 제거
            canvas.Children.Remove(confirmButton);
            canvas.Children.Remove(cancelButton);
            
            selectedTextBox = null;
        }

        private void ClearTextSelection()
        {
            // 기존 텍스트박스가 있으면 포커스 해제
            if (selectedTextBox != null)
            {
                selectedTextBox.IsReadOnly = true;
                selectedTextBox.BorderThickness = new Thickness(0);
                selectedTextBox.Background = Brushes.Transparent;
            }
            
            // 선택 테두리 제거
            if (textSelectionBorder != null && canvas.Children.Contains(textSelectionBorder))
            {
                canvas.Children.Remove(textSelectionBorder);
                textSelectionBorder = null;
            }
            
            // 삭제 버튼 제거
            if (textDeleteButton != null && canvas.Children.Contains(textDeleteButton))
            {
                canvas.Children.Remove(textDeleteButton);
                textDeleteButton = null;
            }
        }

        private void ShowTextSelection(TextBox textBox)
        {
            // 기존 선택 해제
            ClearTextSelection();
            
            double left = Canvas.GetLeft(textBox);
            double top = Canvas.GetTop(textBox);
            double width = textBox.ActualWidth > 0 ? textBox.ActualWidth : textBox.MinWidth;
            double height = textBox.ActualHeight > 0 ? textBox.ActualHeight : textBox.MinHeight;
            
            // 점선 테두리 생성
            textSelectionBorder = new Rectangle
            {
                Width = width + 4,
                Height = height + 4,
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            
            Canvas.SetLeft(textSelectionBorder, left - 2);
            Canvas.SetTop(textSelectionBorder, top - 2);
            canvas.Children.Add(textSelectionBorder);
            
            // 삭제 버튼 생성
            textDeleteButton = new Button
            {
                Content = "🗑️",
                Width = 24,
                Height = 24,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)), // 빨간색
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "삭제"
            };
            
            textDeleteButton.Click += (s, e) =>
            {
                // 텍스트박스 삭제
                canvas.Children.Remove(textBox);
                drawnElements.Remove(textBox);
                
                // 확정 버튼도 삭제 (있다면)
                if (textBox.Tag is Button confirmBtn && canvas.Children.Contains(confirmBtn))
                {
                    canvas.Children.Remove(confirmBtn);
                }
                
                ClearTextSelection();
                selectedTextBox = null;
            };
            
            Canvas.SetLeft(textDeleteButton, left + width - 20);
            Canvas.SetTop(textDeleteButton, top - 28);
            canvas.Children.Add(textDeleteButton);
        }


        private void Canvas_DrawMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(canvas);
            
            // [추가] 선택 영역 내부인지 확인
            if (!IsPointInSelection(clickPoint))
                return;

            if (currentTool == "도형")
            {
                isDrawingShape = true;
                shapeStartPoint = clickPoint;
                tempShape = CreateShape(shapeStartPoint, shapeStartPoint);
                if (tempShape != null)
                {
                    canvas.Children.Add(tempShape);
                }
                canvas.CaptureMouse();
                return;
            }

            if (!isDrawingEnabled) return;
            
            lastDrawPoint = clickPoint;
            
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
            Point currentPoint = e.GetPosition(canvas);

            if (currentTool == "도형")
            {
                if (isDrawingShape && tempShape != null)
                {
                    UpdateShapeProperties(tempShape, shapeStartPoint, currentPoint);
                }
                return;
            }

            if (!isDrawingEnabled || currentPolyline == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            
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
            if (currentTool == "도형")
            {
                if (isDrawingShape && tempShape != null)
                {
                    drawnElements.Add(tempShape);
                    tempShape = null;
                    isDrawingShape = false;
                }
                canvas.ReleaseMouseCapture();
                return;
            }

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
                    else if (element is TextBox textBox)
                    {
                        // 텍스트가 비어있으면 건너뛰기
                        if (string.IsNullOrWhiteSpace(textBox.Text))
                            continue;
                        
                        // 텍스트 렌더링
                        var formattedText = new FormattedText(
                            textBox.Text,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                            textBox.FontSize,
                            textBox.Foreground,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        
                        // 텍스트 위치 계산 (선택 영역 기준으로 오프셋 조정)
                        double textLeft = Canvas.GetLeft(textBox) - selectionLeft;
                        double textTop = Canvas.GetTop(textBox) - selectionTop;
                        
                        drawingContext.DrawText(formattedText, new Point(textLeft, textTop));
                    }     
                else if (element is Shape shape)
                {
                    if (shape is Line line)
                    {
                        drawingContext.DrawLine(new Pen(line.Stroke, line.StrokeThickness), new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                    }
                    else
                    {
                        double left = Canvas.GetLeft(shape);
                        double top = Canvas.GetTop(shape);
                        drawingContext.PushTransform(new TranslateTransform(left, top));

                        if (shape is Rectangle rect)
                        {
                            drawingContext.DrawRectangle(rect.Fill, new Pen(rect.Stroke, rect.StrokeThickness), new Rect(0, 0, rect.Width, rect.Height));
                        }
                        else if (shape is Ellipse ellipse)
                        {
                            drawingContext.DrawEllipse(ellipse.Fill, new Pen(ellipse.Stroke, ellipse.StrokeThickness), new Point(ellipse.Width / 2, ellipse.Height / 2), ellipse.Width / 2, ellipse.Height / 2);
                        }
                        drawingContext.Pop();
                    }
                }
                else if (element is Canvas arrowCanvas)
                {
                    foreach (var child in arrowCanvas.Children)
                    {
                        if (child is Line l)
                        {
                            drawingContext.DrawLine(new Pen(l.Stroke, l.StrokeThickness), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2));
                        }
                        else if (child is Polygon p)
                        {
                            StreamGeometry streamGeometry = new StreamGeometry();
                            using (StreamGeometryContext geometryContext = streamGeometry.Open())
                            {
                                geometryContext.BeginFigure(p.Points[0], true, true);
                                for (int i = 1; i < p.Points.Count; i++)
                                {
                                    geometryContext.LineTo(p.Points[i], true, false);
                                }
                            }
                            drawingContext.DrawGeometry(p.Fill, new Pen(p.Stroke, p.StrokeThickness), streamGeometry);
                        }
                    }
                }
            }
            
            renderBitmap.Render(drawingVisual);
            renderBitmap.Freeze();
            
            // SelectedFrozenImage 업데이트
            SelectedFrozenImage = renderBitmap;
            }
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
        // 파일 맨 끝에 추가
        public enum ShapeType
        {
            Rectangle,
            Ellipse,
            Line,
            Arrow
        }
    }
}