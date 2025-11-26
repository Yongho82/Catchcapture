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

        // 돋보기 관련 필드 추가
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
            
            // 하단에 편집 툴바 추가
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // 펜 버튼
            var penButton = CreateToolButton("✏️", "펜");
            // 형광펜 버튼
            var highlighterButton = CreateToolButton("🖍️", "형광펜");
            // 지우개 버튼
            var eraserButton = CreateToolButton("🧹", "지우개");
            // 완료 버튼
            var doneButton = CreateToolButton("✓", "완료");
            
            // 완료 버튼 클릭 이벤트
            doneButton.Click += (s, e) =>
            {
                DialogResult = true;
                Close();
            };
            
            toolbar.Children.Add(penButton);
            toolbar.Children.Add(highlighterButton);
            toolbar.Children.Add(eraserButton);
            toolbar.Children.Add(doneButton);
            
            // 캔버스 하단에 툴바 배치
            canvas.Children.Add(toolbar);
            Canvas.SetLeft(toolbar, (vWidth - 300) / 2);
            Canvas.SetBottom(toolbar, 20);
        }

        private Button CreateToolButton(string icon, string tooltip)
        {
            return new Button
            {
                Content = icon,
                Width = 50,
                Height = 40,
                Margin = new Thickness(5),
                ToolTip = tooltip
            };
        }
    }
}