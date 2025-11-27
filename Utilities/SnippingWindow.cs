using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.IO;

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
        private Stack<UIElement> undoStack = new Stack<UIElement>();
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
        // [추가] 모자이크 관련 필드
        private double mosaicIntensity = 15; // 모자이크 강도 (기본값)
        private Rectangle? tempMosaicSelection; // 모자이크 영역 선택용 사각형
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
            this.KeyDown += SnippingWindow_KeyDown;
        }

        private void SnippingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 즉시편집 모드일 때만 단축키 활성화
            if (!instantEditMode) return;
            
            // Ctrl + Z: 실행 취소
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UndoLastAction();
                e.Handled = true;
            }
            // Ctrl + R: 초기화
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ResetAllDrawings();
                e.Handled = true;
            }
            // Ctrl + C: 클립보드 복사
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CopyToClipboard();
                e.Handled = true;
            }
            // Ctrl + S: 파일로 저장
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveToFile();
                e.Handled = true;
            }
            // Enter: 완료 (확정)
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                ConfirmAndClose();
                e.Handled = true;
            }
            // Esc: 취소
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
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
            
            // [수정] 편집 툴바 컨테이너 (둥근 모서리 Border)
            var toolbar = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5),
                Height = 55,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.15
                }
            };

            // [수정] 내부 스택패널
            var toolbarStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            toolbar.Child = toolbarStackPanel;

            // 펜 버튼
            var penButton = CreateToolButton("highlight.png", "펜", "펜 도구");
            penButton.Click += (s, e) => 
            {
                currentTool = "펜";
                SetActiveToolButton(penButton);
                ShowColorPalette("펜", selectionLeft, selectionTop + selectionHeight + 80);
                EnableDrawingMode();
            };
            
            // 형광펜 버튼
            var highlighterButton = CreateToolButton("highlight.png", "형광펜", "형광펜 도구");
            highlighterButton.Click += (s, e) => 
            {
                currentTool = "형광펜";
                selectedColor = Colors.Yellow;
                SetActiveToolButton(highlighterButton);
                ShowColorPalette("형광펜", selectionLeft, selectionTop + selectionHeight + 80);
                EnableDrawingMode();
            };
            
            // 텍스트 버튼
            var textButton = CreateToolButton("text.png", "텍스트", "텍스트 입력");
            textButton.Click += (s, e) => 
            {
                currentTool = "텍스트";
                SetActiveToolButton(textButton);
                ShowColorPalette("텍스트", selectionLeft, selectionTop + selectionHeight + 80);
                EnableTextMode();
            };
            
            // 도형 버튼
            var shapeButton = CreateToolButton("shape.png", "도형", "도형 그리기");
            shapeButton.Click += (s, e) => 
            {
                currentTool = "도형";
                SetActiveToolButton(shapeButton);
                ShowColorPalette("도형", selectionLeft, selectionTop + selectionHeight + 80);
                EnableShapeMode();
            };
            
            // 모자이크 버튼
            var mosaicButton = CreateToolButton("mosaic.png", "모자이크", "모자이크 효과");
            mosaicButton.Click += (s, e) => 
            { 
                currentTool = "모자이크"; 
                SetActiveToolButton(mosaicButton);
                ShowColorPalette("모자이크", selectionLeft, selectionTop + selectionHeight + 80);
                EnableMosaicMode();
            };
            
            // 지우개 버튼
            var eraserButton = CreateToolButton("eraser.png", "지우개", "요소 삭제");
            eraserButton.Click += (s, e) => 
            { 
                currentTool = "지우개"; 
                SetActiveToolButton(eraserButton);
                HideColorPalette(); 
                EnableEraserMode();
            };

            // OCR 버튼
            var ocrButton = CreateToolButton("extract_text.png", "OCR", "텍스트 추출");
            ocrButton.Click += async (s, e) => 
            { 
                await PerformOcr();
            };

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
                Width = 45,
                Height = 45,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "완료 (Enter)",
                Style = null
            };
            
            var doneStackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var doneText = new TextBlock
            {
                Text = "✓",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 1)
            };
            
            var doneLabel = new TextBlock
            {
                Text = "완료",
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.White
            };
            
            doneStackPanel.Children.Add(doneText);
            doneStackPanel.Children.Add(doneLabel);
            
            // 완료 버튼용 Border
            var doneBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 90, 158)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                Child = doneStackPanel
            };
            
            doneButton.Content = doneBorder;
            
            doneButton.MouseEnter += (s, e) =>
            {
                doneBorder.Background = new SolidColorBrush(Color.FromRgb(0, 140, 255));
            };
            
            doneButton.MouseLeave += (s, e) =>
            {
                doneBorder.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            };
            
            // 완료 버튼 클릭 이벤트
            doneButton.Click += (s, e) =>
            {
                ConfirmAndClose();
            };
            
            // 구분선 2
            var separator2 = new Border
            {
                Width = 1,
                Height = 30,
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                Margin = new Thickness(5, 0, 5, 0)
            };
            
            // 실행 취소 버튼
            var undoButton = CreateActionButton("undo.png", "실행취소", "실행 취소 (Ctrl+Z)");
            undoButton.Click += (s, e) => UndoLastAction();
            
            // 초기화 버튼
            var resetButton = CreateActionButton("reset.png", "초기화", "전체 초기화 (Ctrl+R)");
            resetButton.Click += (s, e) => ResetAllDrawings();

            // 구분선 3
            var separator3 = new Border
            {
                Width = 1,
                Height = 30,
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                Margin = new Thickness(5, 0, 5, 0)
            };
            
            // 복사 버튼
            var copyButton = CreateActionButton("copy_selected.png", "복사", "클립보드 복사 (Ctrl+C)");
            copyButton.Click += (s, e) => CopyToClipboard();
            
            // 저장 버튼
            var saveButton = CreateActionButton("save_selected.png", "저장", "파일로 저장 (Ctrl+S)");
            saveButton.Click += (s, e) => SaveToFile();

            // [수정] 버튼들을 toolbarStackPanel에 추가
            toolbarStackPanel.Children.Add(penButton);
            toolbarStackPanel.Children.Add(highlighterButton);
            toolbarStackPanel.Children.Add(textButton);
            toolbarStackPanel.Children.Add(shapeButton);
            toolbarStackPanel.Children.Add(mosaicButton);
            toolbarStackPanel.Children.Add(eraserButton);
            toolbarStackPanel.Children.Add(ocrButton);
            toolbarStackPanel.Children.Add(separator);
            toolbarStackPanel.Children.Add(doneButton);
            toolbarStackPanel.Children.Add(separator2);
            toolbarStackPanel.Children.Add(undoButton);
            toolbarStackPanel.Children.Add(resetButton);
            toolbarStackPanel.Children.Add(separator3);
            toolbarStackPanel.Children.Add(copyButton);
            toolbarStackPanel.Children.Add(saveButton);
            
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
            ShowColorPalette("펜", selectionLeft, selectionTop + selectionHeight + 80);
            EnableDrawingMode();
        }

        private Button CreateToolButton(string iconPath, string label, string tooltip)
        {
            var button = new Button
            {
                Width = 45,  // 요청하신 45 크기
                Height = 45,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent, // 버튼 자체는 투명
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Style = null
            };
            
            // Border로 둥근 모서리 효과 구현
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2)
            };
            
            // 아이콘 + 텍스트 세로 배치
            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // 아이콘 이미지
            try
            {
                var icon = new Image
                {
                    Width = 18,  // 요청하신 18 크기
                    Height = 18,
                    Margin = new Thickness(0, 1, 0, 1),
                    Source = new BitmapImage(new Uri($"pack://application:,,,/icons/{iconPath}", UriKind.Absolute))
                };
                stackPanel.Children.Add(icon);
            }
            catch
            {
                var iconText = new TextBlock
                {
                    Text = GetEmojiForTool(label),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 1)
                };
                stackPanel.Children.Add(iconText);
            }
            
            // 텍스트
            var textBlock = new TextBlock
            {
                Text = label,
                FontSize = 9, // 요청하신 9 크기
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;
            button.Content = border;
            
            // 호버 효과 (Border의 색상을 변경)
            button.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromRgb(232, 243, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            };
            
            button.MouseLeave += (s, e) =>
            {
                if (button != activeToolButton)
                {
                    border.Background = Brushes.White;
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240));
                }
            };
            
            return button;
        }

        private string GetEmojiForTool(string label)
        {
            return label switch
            {
                "펜" => "🖊️",
                "형광펜" => "🖍️",
                "텍스트" => "📝",
                "도형" => "🔲",
                "모자이크" => "🎨",
                "지우개" => "🧹",
                "OCR" => "🔍",
                "실행취소" => "↶",
                "초기화" => "⟲",
                "복사" => "📋",
                "저장" => "💾",
                _ => "●"
            };
        }

        private Button CreateActionButton(string iconPath, string label, string tooltip)
        {
            var button = new Button
            {
                Width = 45,
                Height = 45,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Style = null
            };
            
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2)
            };
            
            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            try
            {
                var icon = new Image
                {
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(0, 1, 0, 1),
                    Source = new BitmapImage(new Uri($"pack://application:,,,/icons/{iconPath}", UriKind.Absolute))
                };
                stackPanel.Children.Add(icon);
            }
            catch
            {
                var iconText = new TextBlock
                {
                    Text = GetEmojiForTool(label),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 1)
                };
                stackPanel.Children.Add(iconText);
            }
            
            var textBlock = new TextBlock
            {
                Text = label,
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            stackPanel.Children.Add(textBlock);
            border.Child = stackPanel;
            button.Content = border;
            
            button.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromRgb(232, 243, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            };
            
            button.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.White;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240));
            };
            
            return button;
        }

        private void SetActiveToolButton(Button button)
        {
            // 이전 활성 버튼 초기화 (Border 스타일 복구)
            if (activeToolButton != null && activeToolButton.Content is Border oldBorder)
            {
                oldBorder.Background = Brushes.White;
                oldBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240));
            }
            
            // 새 활성 버튼 설정 (Border 스타일 강조)
            activeToolButton = button;
            if (activeToolButton != null && activeToolButton.Content is Border newBorder)
            {
                newBorder.Background = new SolidColorBrush(Color.FromRgb(232, 243, 255));
                newBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
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
                Width = 320
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
            
            // 1. 색상 섹션 (모자이크가 아닐 때만 표시)
            if (tool != "모자이크")
            {
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
            }
            
            // 3. 옵션 섹션 (도구별 분기)
            var optionSection = new StackPanel();
            Grid.SetColumn(optionSection, 2);
            mainGrid.Children.Add(optionSection);

            if (tool == "텍스트")
            {
                // [텍스트 옵션]
                var optionLabel = new TextBlock { Text = "텍스트 옵션", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(optionLabel);

                // 폰트 크기
                var sizePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                sizePanel.Children.Add(new TextBlock { Text = "크기:", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
                var sizeCombo = new ComboBox { Width = 60, Height = 25 };
                int[] sizes = { 10, 12, 14, 16, 18, 24, 36, 48, 72 };
                foreach (var s in sizes) sizeCombo.Items.Add(s);
                sizeCombo.SelectedItem = textFontSize;
                sizeCombo.SelectionChanged += (s, e) => { if (sizeCombo.SelectedItem is int newSize) { textFontSize = newSize; if (selectedTextBox != null) selectedTextBox.FontSize = newSize; } };
                sizePanel.Children.Add(sizeCombo);
                optionSection.Children.Add(sizePanel);

                // 폰트 종류
                var fontPanel = new StackPanel { Orientation = Orientation.Horizontal };
                fontPanel.Children.Add(new TextBlock { Text = "폰트:", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
                var fontCombo = new ComboBox { Width = 100, Height = 25 };
                string[] fonts = { "Malgun Gothic", "Arial", "Consolas", "Gulim", "Dotum" };
                foreach (var f in fonts) fontCombo.Items.Add(f);
                fontCombo.SelectedItem = textFontFamily;
                fontCombo.SelectionChanged += (s, e) => { if (fontCombo.SelectedItem is string newFont) { textFontFamily = newFont; if (selectedTextBox != null) selectedTextBox.FontFamily = new FontFamily(newFont); } };
                fontPanel.Children.Add(fontCombo);
                optionSection.Children.Add(fontPanel);
            }
            else if (tool == "도형")
            {
                // [도형 옵션]
                var optionLabel = new TextBlock { Text = "도형 옵션", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(optionLabel);

                // 도형 종류
                var shapeTypePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                shapeTypePanel.Children.Add(CreateShapeOptionButton("□", ShapeType.Rectangle));
                shapeTypePanel.Children.Add(CreateShapeOptionButton("○", ShapeType.Ellipse));
                shapeTypePanel.Children.Add(CreateShapeOptionButton("╱", ShapeType.Line));
                shapeTypePanel.Children.Add(CreateShapeOptionButton("↗", ShapeType.Arrow));
                optionSection.Children.Add(shapeTypePanel);

                // 두께
                var thicknessPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                thicknessPanel.Children.Add(new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center, Width = 35, FontSize = 11 });
                var thicknessSlider = new Slider { Minimum = 1, Maximum = 10, Value = shapeBorderThickness, Width = 80, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center };
                thicknessSlider.ValueChanged += (s, e) => { shapeBorderThickness = thicknessSlider.Value; };
                thicknessPanel.Children.Add(thicknessSlider);
                optionSection.Children.Add(thicknessPanel);

                // 채우기 및 투명도
                var fillPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                var fillCheckBox = new CheckBox { Content = "채우기", IsChecked = shapeIsFilled, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                fillCheckBox.Checked += (s, e) => { shapeIsFilled = true; };
                fillCheckBox.Unchecked += (s, e) => { shapeIsFilled = false; };
                fillPanel.Children.Add(fillCheckBox);
                var opacitySlider = new Slider { Minimum = 0, Maximum = 1, Value = shapeFillOpacity, Width = 60, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "채우기 투명도" };
                opacitySlider.ValueChanged += (s, e) => { shapeFillOpacity = opacitySlider.Value; };
                fillPanel.Children.Add(opacitySlider);
                optionSection.Children.Add(fillPanel);
            }
            else if (tool == "모자이크")
            {
                // [모자이크 옵션]
                var optionLabel = new TextBlock { Text = "모자이크 옵션", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(optionLabel);

                var intensityPanel = new StackPanel { Orientation = Orientation.Horizontal };
                intensityPanel.Children.Add(new TextBlock { Text = "강도:", VerticalAlignment = VerticalAlignment.Center, Width = 35 });

                var slider = new Slider
                {
                    Minimum = 5,
                    Maximum = 50,
                    Value = mosaicIntensity,
                    Width = 120,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 5,
                    ToolTip = "모자이크 강도 조절"
                };
                slider.ValueChanged += (s, e) => { mosaicIntensity = slider.Value; };
                intensityPanel.Children.Add(slider);
                
                optionSection.Children.Add(intensityPanel);
            }
            else
            {
                // [기본 두께 옵션 (펜, 형광펜)]
                var thicknessLabel = new TextBlock { Text = "두께", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(thicknessLabel);
                
                var thicknessList = new StackPanel();
                int[] presets = new int[] { 1, 3, 5, 8, 12 };
                foreach (var p in presets)
                {
                    var item = new Grid { Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand, Background = Brushes.Transparent };
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    var line = new Border { Height = p, Width = 30, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(line, 0); item.Children.Add(line);
                    
                    var text = new TextBlock { Text = $"{p}px", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(text, 1); item.Children.Add(text);

                    int thickness = p;
                    item.MouseLeftButtonDown += (s, e) =>
                    {
                        if (currentTool == "형광펜") highlightThickness = thickness;
                        else penThickness = thickness;
                        foreach (var child in thicknessList.Children) { if (child is Grid g) g.Background = Brushes.Transparent; }
                        item.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
                    };
                    thicknessList.Children.Add(item);
                }
                optionSection.Children.Add(thicknessList);
            }
            
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
            
            // [수정] 텍스트 모드 이벤트 제거 (중복 실행 방지)
            canvas.MouseLeftButtonDown -= Canvas_TextMouseDown;
            
            // 기존 그리기 이벤트 제거 후 다시 등록
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

        private void EnableEraserMode()
        {
            isDrawingEnabled = false;
            canvas.Cursor = Cursors.Hand; // 지우개 커서
            
            // 기존 이벤트 제거
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown;
            canvas.MouseLeftButtonDown -= Canvas_TextMouseDown;
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;
            
            // 지우개 이벤트 등록
            canvas.MouseLeftButtonDown += Canvas_EraserMouseDown;
            
            // 텍스트 선택 해제
            if (selectedTextBox != null)
            {
                ClearTextSelection();
            }
        }
        private async Task PerformOcr()
        {
            try
            {
                // 선택 영역만 크롭하여 OCR 수행
                BitmapSource imageToOcr = null;
                
                if (screenCapture != null && selectionRectangle != null)
                {
                    double selectionLeft = Canvas.GetLeft(selectionRectangle);
                    double selectionTop = Canvas.GetTop(selectionRectangle);
                    double selectionWidth = selectionRectangle.Width;
                    double selectionHeight = selectionRectangle.Height;
                    
                    // 선택 영역만 크롭
                    var croppedBitmap = new CroppedBitmap(
                        screenCapture,
                        new Int32Rect(
                            (int)selectionLeft,
                            (int)selectionTop,
                            (int)selectionWidth,
                            (int)selectionHeight
                        )
                    );
                    
                    imageToOcr = croppedBitmap;
                }
                
                if (imageToOcr == null)
                {
                    MessageBox.Show("OCR을 수행할 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 로딩 표시
                this.Cursor = Cursors.Wait;
                
                // OCR 실행
                string extractedText = await CatchCapture.Utilities.OcrUtility.ExtractTextFromImageAsync(imageToOcr);
                
                // 커서 복원
                this.Cursor = Cursors.Arrow;
                
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    MessageBox.Show("추출된 텍스트가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // OCR 결과창 표시
                var resultWindow = new OcrResultWindow(extractedText);
                resultWindow.Owner = this;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show($"텍스트 추출 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmAndClose()
        {
            // 그린 내용을 이미지에 합성
            if (drawnElements.Count > 0)
            {
                SaveDrawingsToImage();
            }
            
            DialogResult = true;
            Close();
        }
        
        private void UndoLastAction()
        {
            if (drawnElements.Count == 0)
            {
                return; // 조용히 무시
            }
            
            // 마지막 요소 제거
            var lastElement = drawnElements[drawnElements.Count - 1];
            drawnElements.RemoveAt(drawnElements.Count - 1);
            canvas.Children.Remove(lastElement);
            
            // 텍스트박스인 경우 관련 버튼도 제거
            if (lastElement is TextBox textBox)
            {
                if (textBox.Tag != null)
                {
                    dynamic tags = textBox.Tag;
                    if (tags.confirmButton != null && canvas.Children.Contains(tags.confirmButton))
                        canvas.Children.Remove(tags.confirmButton);
                    if (tags.cancelButton != null && canvas.Children.Contains(tags.cancelButton))
                        canvas.Children.Remove(tags.cancelButton);
                }
            }
            
            // 선택 상태 초기화
            if (selectedTextBox == lastElement)
            {
                ClearTextSelection();
            }
        }
        
        private void ResetAllDrawings()
        {
            if (drawnElements.Count == 0)
            {
                return; // 조용히 무시
            }
            
            var result = MessageBox.Show(
                "모든 그리기 내용을 삭제하시겠습니까?", 
                "확인", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                // 모든 그린 요소 제거
                foreach (var element in drawnElements.ToList())
                {
                    canvas.Children.Remove(element);
                    
                    // 텍스트박스인 경우 관련 버튼도 제거
                    if (element is TextBox textBox && textBox.Tag != null)
                    {
                        dynamic tags = textBox.Tag;
                        if (tags.confirmButton != null && canvas.Children.Contains(tags.confirmButton))
                            canvas.Children.Remove(tags.confirmButton);
                        if (tags.cancelButton != null && canvas.Children.Contains(tags.cancelButton))
                            canvas.Children.Remove(tags.cancelButton);
                    }
                }
                
                drawnElements.Clear();
                ClearTextSelection();
            }
        }
        
        private void CopyToClipboard()
        {
            try
            {
                // 그린 내용을 이미지에 합성
                if (drawnElements.Count > 0)
                {
                    SaveDrawingsToImage();
                }
                
                if (SelectedFrozenImage != null)
                {
                    Clipboard.SetImage(SelectedFrozenImage);
                    MessageBox.Show("이미지가 클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"클립보드 복사 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveToFile()
        {
            try
            {
                // 그린 내용을 이미지에 합성
                if (drawnElements.Count > 0)
                {
                    SaveDrawingsToImage();
                }
                
                if (SelectedFrozenImage == null)
                {
                    MessageBox.Show("저장할 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp",
                    DefaultExt = ".png",
                    FileName = $"캡처_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                
                if (saveDialog.ShowDialog() == true)
                {
                    BitmapEncoder encoder;
                    string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                    
                    switch (extension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            encoder = new JpegBitmapEncoder();
                            break;
                        case ".bmp":
                            encoder = new BmpBitmapEncoder();
                            break;
                        default:
                            encoder = new PngBitmapEncoder();
                            break;
                    }
                    
                    encoder.Frames.Add(BitmapFrame.Create(SelectedFrozenImage));
                    
                    using (var stream = new System.IO.FileStream(saveDialog.FileName, System.IO.FileMode.Create))
                    {
                        encoder.Save(stream);
                    }
                    
                    MessageBox.Show($"이미지가 저장되었습니다.\n{saveDialog.FileName}", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Canvas_EraserMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 버튼 클릭 시 무시
            if (e.OriginalSource is FrameworkElement source && 
               (source is Button || source.Parent is Button || source.TemplatedParent is Button))
                return;

            Point clickPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(clickPoint))
                return;

            // 클릭한 위치에 있는 요소 찾기 (역순으로 검색 - 최상위 요소부터)
            UIElement elementToRemove = null;
            
            for (int i = drawnElements.Count - 1; i >= 0; i--)
            {
                var element = drawnElements[i];
                
                if (element is Polyline polyline)
                {
                    // Polyline의 점들 중 하나라도 클릭 범위 내에 있으면 선택
                    foreach (var point in polyline.Points)
                    {
                        if (Math.Abs(point.X - clickPoint.X) < 10 && Math.Abs(point.Y - clickPoint.Y) < 10)
                        {
                            elementToRemove = polyline;
                            break;
                        }
                    }
                }
                else if (element is TextBox textBox)
                {
                    double left = Canvas.GetLeft(textBox);
                    double top = Canvas.GetTop(textBox);
                    double right = left + textBox.ActualWidth;
                    double bottom = top + textBox.ActualHeight;
                    
                    if (clickPoint.X >= left && clickPoint.X <= right &&
                        clickPoint.Y >= top && clickPoint.Y <= bottom)
                    {
                        elementToRemove = textBox;
                    }
                }
                else if (element is Shape shape)
                {
                    if (shape is Line line)
                    {
                        // 선의 경우 클릭 위치가 선 근처인지 확인
                        double distance = DistanceFromPointToLine(clickPoint, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                        if (distance < 10)
                        {
                            elementToRemove = shape;
                        }
                    }
                    else
                    {
                        double left = Canvas.GetLeft(shape);
                        double top = Canvas.GetTop(shape);
                        double right = left + shape.Width;
                        double bottom = top + shape.Height;
                        
                        if (clickPoint.X >= left && clickPoint.X <= right &&
                            clickPoint.Y >= top && clickPoint.Y <= bottom)
                        {
                            elementToRemove = shape;
                        }
                    }
                }
                else if (element is Canvas arrowCanvas)
                {
                    // 화살표의 경우 자식 요소 확인
                    foreach (var child in arrowCanvas.Children)
                    {
                        if (child is Line l)
                        {
                            double distance = DistanceFromPointToLine(clickPoint, new Point(l.X1, l.Y1), new Point(l.X2, l.Y2));
                            if (distance < 10)
                            {
                                elementToRemove = arrowCanvas;
                                break;
                            }
                        }
                    }
                }
                else if (element is Image image)
                {
                    // 모자이크 이미지
                    double left = Canvas.GetLeft(image);
                    double top = Canvas.GetTop(image);
                    double right = left + image.Width;
                    double bottom = top + image.Height;
                    
                    if (clickPoint.X >= left && clickPoint.X <= right &&
                        clickPoint.Y >= top && clickPoint.Y <= bottom)
                    {
                        elementToRemove = image;
                    }
                }
                
                if (elementToRemove != null)
                    break;
            }
            
            // 요소 삭제
            if (elementToRemove != null)
            {
                canvas.Children.Remove(elementToRemove);
                drawnElements.Remove(elementToRemove);
            }
        }
        
        // 점에서 선까지의 거리 계산 (지우개용 헬퍼 메서드)
        private double DistanceFromPointToLine(Point point, Point lineStart, Point lineEnd)
        {
            double A = point.X - lineStart.X;
            double B = point.Y - lineStart.Y;
            double C = lineEnd.X - lineStart.X;
            double D = lineEnd.Y - lineStart.Y;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = -1;
            
            if (lenSq != 0)
                param = dot / lenSq;

            double xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            double dx = point.X - xx;
            double dy = point.Y - yy;
            
            return Math.Sqrt(dx * dx + dy * dy);
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
        
        private void EnableMosaicMode()
        {
            isDrawingEnabled = true;
            canvas.Cursor = Cursors.Cross; // [수정] 십자 커서로 변경
            
            // 이벤트 재설정 (텍스트 모드 등에서 전환될 때를 대비)
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
            // [수정] 버튼이나 UI 컨트롤을 클릭했을 때는 그리기 로직을 실행하지 않음
            if (e.OriginalSource is FrameworkElement source && 
               (source is Button || source.Parent is Button || source.TemplatedParent is Button))
                return;

            Point clickPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
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
            else if (currentTool == "모자이크")
            {
                isDrawingShape = true; // 드래그 로직 재사용
                shapeStartPoint = clickPoint;
                
                // 영역 표시용 점선 사각형
                tempMosaicSelection = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0))
                };
                Canvas.SetLeft(tempMosaicSelection, clickPoint.X);
                Canvas.SetTop(tempMosaicSelection, clickPoint.Y);
                tempMosaicSelection.Width = 0;
                tempMosaicSelection.Height = 0;

                canvas.Children.Add(tempMosaicSelection);
                canvas.CaptureMouse();
                return;
            }

            if (!isDrawingEnabled) return;
            
            lastDrawPoint = clickPoint;
            
            // 새 선 시작
            Color strokeColor = selectedColor;
            if (currentTool == "형광펜")
            {
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

            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(currentPoint))
                return;

            if (currentTool == "도형")
            {
                if (isDrawingShape && tempShape != null)
                {
                    UpdateShapeProperties(tempShape, shapeStartPoint, currentPoint);
                }
                return;
            }
            else if (currentTool == "모자이크")
            {
                if (isDrawingShape && tempMosaicSelection != null)
                {
                    double left = Math.Min(shapeStartPoint.X, currentPoint.X);
                    double top = Math.Min(shapeStartPoint.Y, currentPoint.Y);
                    double width = Math.Abs(currentPoint.X - shapeStartPoint.X);
                    double height = Math.Abs(currentPoint.Y - shapeStartPoint.Y);

                    tempMosaicSelection.Width = width;
                    tempMosaicSelection.Height = height;
                    Canvas.SetLeft(tempMosaicSelection, left);
                    Canvas.SetTop(tempMosaicSelection, top);
                }
                return;
            }

            if (!isDrawingEnabled || currentPolyline == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            // 부드러운 선을 위해 중간 점들을 보간
            double distance = Math.Sqrt(
                Math.Pow(currentPoint.X - lastDrawPoint.X, 2) + 
                Math.Pow(currentPoint.Y - lastDrawPoint.Y, 2));
            
            if (distance > 2)
            {
                int steps = (int)(distance / 2);
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
            else if (currentTool == "모자이크")
            {
                if (isDrawingShape && tempMosaicSelection != null)
                {
                    // 모자이크 적용
                    Rect rect = new Rect(Canvas.GetLeft(tempMosaicSelection), Canvas.GetTop(tempMosaicSelection), tempMosaicSelection.Width, tempMosaicSelection.Height);
                    ApplyMosaic(rect);

                    // 임시 사각형 제거
                    canvas.Children.Remove(tempMosaicSelection);
                    tempMosaicSelection = null;
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
                        if (string.IsNullOrWhiteSpace(textBox.Text))
                            continue;
                        
                        var formattedText = new FormattedText(
                            textBox.Text,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                            textBox.FontSize,
                            textBox.Foreground,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        
                        double textLeft = Canvas.GetLeft(textBox) - selectionLeft;
                        double textTop = Canvas.GetTop(textBox) - selectionTop;
                        
                        drawingContext.DrawText(formattedText, new Point(textLeft, textTop));
                    }     
                    else if (element is Shape shape)
                    {
                        if (shape is Line line)
                        {
                            // [수정] 선 좌표 보정 (선택 영역 기준)
                            drawingContext.DrawLine(new Pen(line.Stroke, line.StrokeThickness), 
                                new Point(line.X1 - selectionLeft, line.Y1 - selectionTop), 
                                new Point(line.X2 - selectionLeft, line.Y2 - selectionTop));
                        }
                        else
                        {
                            // [수정] 도형 좌표 보정 (선택 영역 기준)
                            double left = Canvas.GetLeft(shape) - selectionLeft;
                            double top = Canvas.GetTop(shape) - selectionTop;
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
                                // [수정] 화살표 선 좌표 보정
                                drawingContext.DrawLine(new Pen(l.Stroke, l.StrokeThickness), 
                                    new Point(l.X1 - selectionLeft, l.Y1 - selectionTop), 
                                    new Point(l.X2 - selectionLeft, l.Y2 - selectionTop));
                            }
                            else if (child is Polygon p)
                            {
                                StreamGeometry streamGeometry = new StreamGeometry();
                                using (StreamGeometryContext geometryContext = streamGeometry.Open())
                                {
                                    // [수정] 화살표 머리 좌표 보정
                                    var startPoint = new Point(p.Points[0].X - selectionLeft, p.Points[0].Y - selectionTop);
                                    geometryContext.BeginFigure(startPoint, true, true);
                                    for (int i = 1; i < p.Points.Count; i++)
                                    {
                                        var nextPoint = new Point(p.Points[i].X - selectionLeft, p.Points[i].Y - selectionTop);
                                        geometryContext.LineTo(nextPoint, true, false);
                                    }
                                }
                                drawingContext.DrawGeometry(p.Fill, new Pen(p.Stroke, p.StrokeThickness), streamGeometry);
                            }
                        }
                    }
                    else if (element is Image image)
                    {
                        double left = Canvas.GetLeft(image) - selectionLeft;
                        double top = Canvas.GetTop(image) - selectionTop;
                        var rect = new Rect(left, top, image.Width, image.Height);
                        
                        RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.NearestNeighbor);
                        drawingContext.DrawImage(image.Source, rect);
                    }
                }
            }
            
            renderBitmap.Render(drawingVisual);
            renderBitmap.Freeze();
            
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
        // 파일 맨 끝에 추가
        public enum ShapeType
        {
            Rectangle,
            Ellipse,
            Line,
            Arrow
        }
        private void ApplyMosaic(Rect rect)
        {
            if (screenCapture == null) return;

            // 좌표 보정
            int x = (int)rect.X;
            int y = (int)rect.Y;
            int w = (int)rect.Width;
            int h = (int)rect.Height;

            if (w <= 0 || h <= 0) return;

            // 원본 이미지 범위 체크
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + w > screenCapture.PixelWidth) w = screenCapture.PixelWidth - x;
            if (y + h > screenCapture.PixelHeight) h = screenCapture.PixelHeight - y;

            try
            {
                // 1. 해당 영역 크롭
                var cropped = new CroppedBitmap(screenCapture, new Int32Rect(x, y, w, h));

                // 2. 축소 (모자이크 강도만큼)
                // 강도가 클수록 이미지가 작아졌다가 늘어나면서 픽셀이 커짐
                double scale = 1.0 / Math.Max(1, mosaicIntensity);
                
                var scaleTransform = new ScaleTransform(scale, scale);
                var transformed = new TransformedBitmap(cropped, scaleTransform);

                // 3. Image 컨트롤 생성
                // [중요] 소스는 '축소된 이미지'를 사용하고, 크기는 '원본 영역 크기'로 설정
                var mosaicImage = new Image
                {
                    Source = transformed, 
                    Width = w,
                    Height = h,
                    Stretch = Stretch.Fill
                };
                
                // 4. 픽셀화 효과 적용 (NearestNeighbor: 인접 픽셀 반복)
                // 이 옵션이 있어야 흐려지지 않고 깍두기처럼 나옵니다.
                RenderOptions.SetBitmapScalingMode(mosaicImage, BitmapScalingMode.NearestNeighbor);

                Canvas.SetLeft(mosaicImage, x);
                Canvas.SetTop(mosaicImage, y);

                canvas.Children.Add(mosaicImage);
                drawnElements.Add(mosaicImage);
            }
            catch { /* 오류 무시 */ }
        }
    }
}