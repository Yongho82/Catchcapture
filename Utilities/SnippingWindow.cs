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
using System.IO;
using CatchCapture.Models;

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
        // 넘버링 도구 관련
        private int numberingNext = 1; // 배지 번호 자동 증가
        private Dictionary<int, Canvas> numberingGroups = new Dictionary<int, Canvas>();  // ← 추가
        // 십자선 관련 필드 추가
        private Line? crosshairHorizontal;
        private Line? crosshairVertical;

        // 언어 변경 시 런타임 갱신을 위해 툴바 참조 저장
        private Border? toolbarContainer;
        private StackPanel? toolbarPanel;

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
                    Text = LocalizationManager.Get("SelectAreaGuide"),
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
                // Debug logging disabled
            };
            memoryCleanupTimer.Start();
            this.KeyDown += SnippingWindow_KeyDown;

            // 언어 변경 이벤트 구독: 즉시편집 UI 텍스트 런타임 갱신
            try { LocalizationManager.LanguageChanged += OnLanguageChanged; } catch { }
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
                MessageBox.Show(LocalizationManager.Get("SelectionTooSmall"), LocalizationManager.Get("Info"));
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
            try { LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
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
            toolbarContainer = new Border
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
            toolbarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            toolbarContainer.Child = toolbarPanel;
            // [이동] 툴바 위치를 먼저 계산
            double toolbarWidth = 450; // 대략적인 툴바 너비
            double toolbarLeft = selectionLeft;
            double toolbarTop = selectionTop + selectionHeight + 10;
            
            // 화면 경계 체크
            if (toolbarLeft + toolbarWidth > vWidth)
                toolbarLeft = vWidth - toolbarWidth - 10;
            if (toolbarLeft < 10)
                toolbarLeft = 10;
            if (toolbarTop + 44 > vHeight)
                toolbarTop = selectionTop - 44 - 10;
            
            // 펜 버튼
            var penButton = CreateToolButton("pen.png", LocalizationManager.Get("Pen"), LocalizationManager.Get("Pen"));
            penButton.Click += (s, e) => 
            {
                currentTool = "펜";
                SetActiveToolButton(penButton);
                ShowColorPalette("펜", toolbarLeft, toolbarTop + 60); // [수정]
                EnableDrawingMode();
            };
            
            // 형광펜 버튼
            var highlighterButton = CreateToolButton("highlight.png", LocalizationManager.Get("Highlighter"), LocalizationManager.Get("Highlighter"));
            highlighterButton.Click += (s, e) => 
            {
                currentTool = "형광펜";
                selectedColor = Colors.Yellow;
                SetActiveToolButton(highlighterButton);
                ShowColorPalette("형광펜", toolbarLeft, toolbarTop + 60); // [수정]
                EnableDrawingMode();
            };
            
            // 텍스트 버튼
            var textButton = CreateToolButton("text.png", LocalizationManager.Get("Text"), LocalizationManager.Get("TextAdd"));
            textButton.Click += (s, e) => 
            {
                currentTool = "텍스트";
                SetActiveToolButton(textButton);
                ShowColorPalette("텍스트", toolbarLeft, toolbarTop + 60); // [수정]
                EnableTextMode();
            };
            
            // 도형 버튼
            var shapeButton = CreateToolButton("shape.png", LocalizationManager.Get("ShapeLbl"), LocalizationManager.Get("ShapeOptions"));
            shapeButton.Click += (s, e) => 
            {
                currentTool = "도형";
                SetActiveToolButton(shapeButton);
                ShowColorPalette("도형", toolbarLeft, toolbarTop + 60); // [수정]
                EnableShapeMode();
            };
            
            // 넘버링 버튼 (도형 다음)
            var numberingButton = CreateToolButton("numbering.png", LocalizationManager.Get("Numbering"), LocalizationManager.Get("Numbering"));
            numberingButton.Click += (s, e) =>
            {
                currentTool = "넘버링";
                SetActiveToolButton(numberingButton);
                
                double selectionLeft = Canvas.GetLeft(selectionRectangle);
                double selectionTop = Canvas.GetTop(selectionRectangle);
                double selectionHeight = selectionRectangle.Height;
                double toolbarLeft = selectionLeft;
                double toolbarTop = selectionTop + selectionHeight + 10;
                if (toolbarTop + 44 > vHeight) toolbarTop = selectionTop - 44 - 10;
                
                ShowColorPalette("넘버링", toolbarLeft, toolbarTop + 60);  // ← 팔레트 표시
                EnableNumberingMode();
            };
            
            // 모자이크 버튼
            var mosaicButton = CreateToolButton("mosaic.png", LocalizationManager.Get("Mosaic"), LocalizationManager.Get("Mosaic"));
            mosaicButton.Click += (s, e) => 
            { 
                currentTool = "모자이크"; 
                SetActiveToolButton(mosaicButton);
                ShowColorPalette("모자이크", toolbarLeft, toolbarTop + 60); // [수정]
                EnableMosaicMode();
            };
            
            // 지우개 버튼
            var eraserButton = CreateToolButton("eraser.png", LocalizationManager.Get("Eraser"), LocalizationManager.Get("Eraser"));
            eraserButton.Click += (s, e) => 
            { 
                currentTool = "지우개"; 
                SetActiveToolButton(eraserButton);
                HideColorPalette(); 
                EnableEraserMode();
            };

            // 이미지 검색 버튼
            var imageSearchButton = CreateToolButton("img_find.png", LocalizationManager.Get("ImageSearch"), LocalizationManager.Get("ImageSearchTooltip"));
            imageSearchButton.Click += async (s, e) => 
            { 
                await PerformImageSearch();
            };

            // OCR 버튼
            var ocrButton = CreateToolButton("extract_text.png", LocalizationManager.Get("OCR"), LocalizationManager.Get("Extract"));
            ocrButton.Click += async (s, e) => 
            { 
                await PerformOcr();
            };

            // 구분선
            var separator = new Border
            {
                Width = 1,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),  // 밝은 회색
                Margin = new Thickness(3, 0, 3, 0)  // 마진도 줄임
            };

             
            // 취소 버튼
            var cancelButton = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(0.5),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = $"{LocalizationManager.Get("Cancel")} (ESC)",
                Style = null,
                Template = new ControlTemplate(typeof(Button))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
                }
            };
            
            var cancelStackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var cancelText = new TextBlock
            {
                Text = "✕",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, -2, 0, 0)
            };
            
            var cancelLabel = new TextBlock
            {
                Text = LocalizationManager.Get("Cancel"),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            
            cancelStackPanel.Children.Add(cancelText);
            cancelStackPanel.Children.Add(cancelLabel);
            
            var cancelBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                Child = cancelStackPanel
            };
            
            cancelButton.Content = cancelBorder;
            
            cancelButton.MouseEnter += (s, e) =>
            {
                cancelBorder.Background = new SolidColorBrush(Color.FromRgb(255, 230, 230));
                cancelText.Foreground = new SolidColorBrush(Color.FromRgb(200, 50, 50));
                cancelLabel.Foreground = new SolidColorBrush(Color.FromRgb(200, 50, 50));
            };
            
            cancelButton.MouseLeave += (s, e) =>
            {
                cancelBorder.Background = Brushes.White;
                cancelText.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                cancelLabel.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            };
            
            cancelButton.Click += (s, e) =>
            {
                // 모든 그린 요소 제거
                foreach (var element in drawnElements.ToList())
                {
                    canvas.Children.Remove(element);
                    
                    // 텍스트박스인 경우 관련 버튼도 제거
                    if (element is TextBox textBox && textBox.Tag != null)
                    {
                        if (textBox.Tag is ValueTuple<Button, Button> tagButtons1)
                        {
                            var confirmButton = tagButtons1.Item1;
                            var cancelButton = tagButtons1.Item2;
                            if (confirmButton != null && canvas.Children.Contains(confirmButton))
                                canvas.Children.Remove(confirmButton);
                            if (cancelButton != null && canvas.Children.Contains(cancelButton))
                                canvas.Children.Remove(cancelButton);
                        }
                    }
                }
                
                drawnElements.Clear();
                ClearTextSelection();
            };
                       
            // 완료 버튼
            var doneButton = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = $"{LocalizationManager.Get("OK")} (Enter)",
                Style = null,
                Template = new ControlTemplate(typeof(Button))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
                }
            };
            
            var doneStackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var doneText = new TextBlock
            {
                Text = "✓",
                FontSize = 16,  // 크기를 약간 줄임
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, -2, 0, 0)  // 위로 2px 이동
            };
            
            var doneLabel = new TextBlock
            {
                Text = LocalizationManager.Get("OK"),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            
            doneStackPanel.Children.Add(doneText);
            doneStackPanel.Children.Add(doneLabel);
            
            // 완료 버튼용 Border
            var doneBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                Child = doneStackPanel
            };
            
            doneButton.Content = doneBorder;
            
            doneButton.MouseEnter += (s, e) =>
            {
                if (drawnElements.Count > 0)
                {
                    // 편집한 내용이 있으면 진한 하늘색
                    doneBorder.Background = new SolidColorBrush(Color.FromRgb(100, 180, 255));
                }
                else
                {
                    // 편집 안 했으면 기본 호버색
                    doneBorder.Background = new SolidColorBrush(Color.FromRgb(232, 243, 255));
                }
            };
            
            doneButton.MouseLeave += (s, e) =>
            {
                doneBorder.Background = Brushes.White;
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
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Margin = new Thickness(3, 0, 3, 0)
            };
            
            // 실행 취소 버튼
            var undoButton = CreateActionButton("undo.png", LocalizationManager.Get("UndoLbl"), $"{LocalizationManager.Get("UndoLbl")} (Ctrl+Z)");
            undoButton.Click += (s, e) => UndoLastAction();
            
            // 초기화 버튼
            var resetButton = CreateActionButton("reset.png", LocalizationManager.Get("ResetLbl"), $"{LocalizationManager.Get("ResetLbl")} (Ctrl+R)");
            resetButton.Click += (s, e) => ResetAllDrawings();

            // 구분선 3
            var separator3 = new Border
            {
                Width = 1,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Margin = new Thickness(3, 0, 3, 0)
            };
            
            // 복사 버튼
            var copyButton = CreateActionButton("copy_selected.png", LocalizationManager.Get("CopySelected"), $"{LocalizationManager.Get("CopyToClipboard")} (Ctrl+C)");
            copyButton.Click += (s, e) => CopyToClipboard();
            
            // 저장 버튼
            var saveButton = CreateActionButton("save_selected.png", LocalizationManager.Get("Save"), $"{LocalizationManager.Get("Save")} (Ctrl+S)");
            saveButton.Click += (s, e) => SaveToFile();

            // [수정] 버튼들을 toolbarStackPanel에 추가
            toolbarPanel.Children.Add(penButton);
            toolbarPanel.Children.Add(highlighterButton);
            toolbarPanel.Children.Add(textButton);
            toolbarPanel.Children.Add(shapeButton);
            toolbarPanel.Children.Add(numberingButton);
            toolbarPanel.Children.Add(mosaicButton);
            toolbarPanel.Children.Add(eraserButton);
            toolbarPanel.Children.Add(imageSearchButton);
            toolbarPanel.Children.Add(ocrButton);
            toolbarPanel.Children.Add(separator);
            toolbarPanel.Children.Add(cancelButton);
            toolbarPanel.Children.Add(doneButton);
            toolbarPanel.Children.Add(separator2);
            toolbarPanel.Children.Add(undoButton);
            toolbarPanel.Children.Add(resetButton);
            toolbarPanel.Children.Add(separator3);
            toolbarPanel.Children.Add(copyButton);
            toolbarPanel.Children.Add(saveButton);
            
            // 툴바를 선택 영역 바로 아래에 배치
            canvas.Children.Add(toolbarContainer);    
            Canvas.SetLeft(toolbarContainer, toolbarLeft);
            Canvas.SetTop(toolbarContainer, toolbarTop);

            // 펜을 기본 도구로 선택하고 빨간색으로 설정
            currentTool = "펜";
            selectedColor = Colors.Red;
            SetActiveToolButton(penButton);
            ShowColorPalette("펜", selectionLeft, selectionTop + selectionHeight + 68);
            EnableDrawingMode();
        }

        private Button CreateToolButton(string iconPath, string label, string tooltip)
        {
            var button = new Button
            {
                Width = 40,  // 요청하신 45 크기
                Height = 40,
                Margin = new Thickness(0.5),
                Padding = new Thickness(0),
                Background = Brushes.Transparent, // 버튼 자체는 투명
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Style = null,
                Template = new ControlTemplate(typeof(Button))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
                }
            };
            
            // Border로 둥근 모서리 효과 구현
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0.5)
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
                Width = 40,
                Height = 40,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Style = null,
                Template = new ControlTemplate(typeof(Button))
                {
                    VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
                }
            };
            
            // 이미 보더가 있는데, 안 보이신다면 색상을 더 진하게 변경:
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0.5)
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
                    Text = LocalizationManager.Get("Color"),
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
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),  // 밝은 회색
                    Margin = new Thickness(3, 0, 3, 0)  // 마진도 줄임
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
                var optionLabel = new TextBlock { Text = LocalizationManager.Get("TextOptions"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(optionLabel);

                // 폰트 크기
                var sizePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                sizePanel.Children.Add(new TextBlock { Text = LocalizationManager.Get("SizeLabel") + ":", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
                var sizeCombo = new ComboBox { Width = 60, Height = 25 };
                int[] sizes = { 10, 12, 14, 16, 18, 24, 36, 48, 72 };
                foreach (var s in sizes) sizeCombo.Items.Add(s);
                sizeCombo.SelectedItem = textFontSize;
                sizeCombo.SelectionChanged += (s, e) => { if (sizeCombo.SelectedItem is int newSize) { textFontSize = newSize; if (selectedTextBox != null) selectedTextBox.FontSize = newSize; } };
                sizePanel.Children.Add(sizeCombo);
                optionSection.Children.Add(sizePanel);

                // 폰트 종류
                var fontPanel = new StackPanel { Orientation = Orientation.Horizontal };
                fontPanel.Children.Add(new TextBlock { Text = LocalizationManager.Get("Font") + ":", VerticalAlignment = VerticalAlignment.Center, Width = 40 });
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
                var optionLabel = new TextBlock { Text = LocalizationManager.Get("ShapeOptions"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
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
                thicknessPanel.Children.Add(new TextBlock { Text = LocalizationManager.Get("Thickness") + ":", VerticalAlignment = VerticalAlignment.Center, Width = 35, FontSize = 11 });
                var thicknessSlider = new Slider { Minimum = 1, Maximum = 10, Value = shapeBorderThickness, Width = 80, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center };
                thicknessSlider.ValueChanged += (s, e) => { shapeBorderThickness = thicknessSlider.Value; };
                thicknessPanel.Children.Add(thicknessSlider);
                optionSection.Children.Add(thicknessPanel);

                // 채우기 및 투명도
                var fillPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                var fillCheckBox = new CheckBox { Content = LocalizationManager.Get("Fill"), IsChecked = shapeIsFilled, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                fillCheckBox.Checked += (s, e) => { shapeIsFilled = true; };
                fillCheckBox.Unchecked += (s, e) => { shapeIsFilled = false; };
                fillPanel.Children.Add(fillCheckBox);
                var opacitySlider = new Slider { Minimum = 0, Maximum = 1, Value = shapeFillOpacity, Width = 60, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = LocalizationManager.Get("FillOpacity") };
                opacitySlider.ValueChanged += (s, e) => { shapeFillOpacity = opacitySlider.Value; };
                fillPanel.Children.Add(opacitySlider);
                optionSection.Children.Add(fillPanel);
            }
            else if (tool == "모자이크")
            {
                // [모자이크 옵션]
                var optionLabel = new TextBlock { Text = LocalizationManager.Get("Mosaic"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                optionSection.Children.Add(optionLabel);

                var intensityPanel = new StackPanel { Orientation = Orientation.Horizontal };
                intensityPanel.Children.Add(new TextBlock { Text = LocalizationManager.Get("Intensity") + ":", VerticalAlignment = VerticalAlignment.Center, Width = 35 });

                var slider = new Slider
                {
                    Minimum = 5,
                    Maximum = 50,
                    Value = mosaicIntensity,
                    Width = 120,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 5,
                    ToolTip = LocalizationManager.Get("Intensity")
                };
                slider.ValueChanged += (s, e) => { mosaicIntensity = slider.Value; };
                intensityPanel.Children.Add(slider);
                
                optionSection.Children.Add(intensityPanel);
            }
            else
            {
                // [기본 두께 옵션 (펜, 형광펜)]
                var thicknessLabel = new TextBlock { Text = LocalizationManager.Get("Thickness"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
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
                    
                    var text = new TextBlock
                    {
                        Text = $"{p}px",
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
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
                BitmapSource? imageToOcr = null;
                
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
                    MessageBox.Show(LocalizationManager.Get("NoImageForOcr"), LocalizationManager.Get("Info"));
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
                    MessageBox.Show(LocalizationManager.Get("NoExtractedText"), LocalizationManager.Get("Info"));
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
                MessageBox.Show($"{LocalizationManager.Get("OcrError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PerformImageSearch()
        {
            try
            {
                // 경고(CS1998) 방지용 최소 대기
                await Task.Yield();

                // 즉시편집 모드에서는 편집 요소가 남아 있으면 먼저 확정(✓)하도록 안내
                if (instantEditMode && drawnElements != null && drawnElements.Count > 0)
                {
                    MessageBox.Show("편집 내용을 먼저 확정(✓)한 뒤 이용하세요.", LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 대상 이미지: 선택된 영역의 동결 이미지 우선 사용
                BitmapSource? imageToSearch = SelectedFrozenImage;
                if (imageToSearch == null && screenCapture != null && SelectedArea.Width > 0 && SelectedArea.Height > 0)
                {
                    // 폴백: 전체 스크린샷에서 선택 영역 크롭 (가상 스크린 좌표 보정)
                    var relX = SelectedArea.X - (int)Math.Round(vLeft);
                    var relY = SelectedArea.Y - (int)Math.Round(vTop);

                    // 경계 체크
                    relX = Math.Max(0, Math.Min(relX, screenCapture.PixelWidth - 1));
                    relY = Math.Max(0, Math.Min(relY, screenCapture.PixelHeight - 1));
                    int cw = Math.Max(0, Math.Min(SelectedArea.Width, screenCapture.PixelWidth - relX));
                    int ch = Math.Max(0, Math.Min(SelectedArea.Height, screenCapture.PixelHeight - relY));

                    if (cw > 0 && ch > 0)
                    {
                        imageToSearch = new CroppedBitmap(screenCapture, new Int32Rect(relX, relY, cw, ch));
                    }
                }

                if (imageToSearch == null)
                {
                    MessageBox.Show(LocalizationManager.Get("NoImageToSave"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 메인창의 기존 이미지 검색 플로우 재사용 (Image Edit와 동일 경로)
                var ownerWin = this.Owner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
                if (ownerWin != null)
                {
                    ownerWin.SearchImageOnGoogle(imageToSearch);
                }

                // 검색 트리거 후 오버레이 닫기
                try { DialogResult = true; Close(); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("Error")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (textBox.Tag is ValueTuple<Button, Button> tagButtons1)
                    {
                        var confirmButton = tagButtons1.Item1;
                        var cancelButton = tagButtons1.Item2;
                        if (confirmButton != null && canvas.Children.Contains(confirmButton))
                            canvas.Children.Remove(confirmButton);
                        if (cancelButton != null && canvas.Children.Contains(cancelButton))
                            canvas.Children.Remove(cancelButton);
                    }
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
                LocalizationManager.Get("ConfirmReset"), 
                LocalizationManager.Get("Confirm"), 
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
                        if (textBox.Tag is ValueTuple<Button, Button> tagButtons2)
                        {
                            var confirmButton = tagButtons2.Item1;
                            var cancelButton = tagButtons2.Item2;
                            if (confirmButton != null && canvas.Children.Contains(confirmButton))
                                canvas.Children.Remove(confirmButton);
                            if (cancelButton != null && canvas.Children.Contains(cancelButton))
                                canvas.Children.Remove(cancelButton);
                        }
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
                    
                    // "복사되었습니다" 스티커(토스트) 표시
                    ShowCopyCompleteSticker();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("CopyError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 복사 완료 스티커 표시 메서드
        private void ShowCopyCompleteSticker()
        {
            var sticker = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = 140,  // 너비 줄임 (200 -> 140)
                Height = 36,  // 높이 줄임 (50 -> 36)
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // 투명도 약간 더 줌
                CornerRadius = new CornerRadius(18), // 둥근 알약 모양
                Child = new TextBlock
                {
                    Text = LocalizationManager.Get("Copied"), // 느낌표 제거하고 심플하게
                    Foreground = Brushes.White,
                    FontSize = 12, // 폰트 크기 줄임 (16 -> 12)
                    FontWeight = FontWeights.Normal, // 볼드 제거
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 1) // 텍스트 시각적 중앙 보정
                }
            };

            sticker.Content = border;
            sticker.Show();

            // 1초 후 자동으로 사라짐
            Task.Delay(1000).ContinueWith(_ => 
            {
                Dispatcher.Invoke(() => sticker.Close());
            });
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
                    MessageBox.Show(LocalizationManager.Get("NoImageToSave"), LocalizationManager.Get("Info"));
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
                    
                    MessageBox.Show($"{LocalizationManager.Get("ImageSaved")}:\n{saveDialog.FileName}", LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("SaveError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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

            UIElement? elementToRemove = null;
            
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
                Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)), // 초록색
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationManager.Get("Confirm")
            };

            // 취소 버튼 생성
            var cancelButton = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)), // 빨간색
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationManager.Get("Cancel")
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
            double selectionLeft = Canvas.GetLeft(selectionRectangle);
            double selectionTop = Canvas.GetTop(selectionRectangle);
            double selectionRight = selectionLeft + selectionRectangle.Width;
            double selectionBottom = selectionTop + selectionRectangle.Height;

            // 버튼이 선택 영역을 벗어나지 않도록 위치 조정
            double confirmLeft = Math.Min(left + 105, selectionRight - 24);
            double confirmTop = Math.Max(top - 28, selectionTop);
            double cancelLeft = Math.Min(left + 77, selectionRight - 24);
            double cancelTop = Math.Max(top - 28, selectionTop);

            Canvas.SetLeft(confirmButton, confirmLeft);
            Canvas.SetTop(confirmButton, confirmTop);
            Canvas.SetLeft(cancelButton, cancelLeft);
            Canvas.SetTop(cancelButton, cancelTop);

            canvas.Children.Add(confirmButton);
            canvas.Children.Add(cancelButton);

            // 태그 업데이트 (버튼 참조 저장)
            textBox.Tag = (confirmButton, cancelButton);
            
            // 키 이벤트 핸들러 재등록 (중복 방지)
            textBox.KeyDown -= TextBox_KeyDown;
            textBox.KeyDown += TextBox_KeyDown;
            
            textBox.Focus();
        }

        // [추가] 텍스트 박스 키 이벤트 핸들러
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 편집 중 (IsReadOnly == false)일 때: Ctrl+Enter 확정, Esc 취소
                if (!textBox.IsReadOnly)
                {
                    if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (textBox.Tag is ValueTuple<Button, Button> tags1)
                        {
                            var confirmButton = tags1.Item1;
                            var cancelButton = tags1.Item2;
                            ConfirmTextBox(textBox, confirmButton, cancelButton);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.Escape)
                    {
                        if (textBox.Tag is ValueTuple<Button, Button> tags2)
                        {
                            var confirmButton = tags2.Item1;
                            var cancelButton = tags2.Item2;
                            canvas.Children.Remove(textBox);
                            canvas.Children.Remove(confirmButton);
                            canvas.Children.Remove(cancelButton);
                            drawnElements.Remove(textBox);
                            selectedTextBox = null;
                            e.Handled = true;
                        }
                    }
                    return;
                }

                // 확정 상태에서는 Ctrl+Enter로 편집 전환, Esc로 선택 해제만 처리
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ClearTextSelection();
                    EnableTextBoxEditing(textBox);
                    textBox.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    ClearTextSelection();
                    e.Handled = true;
                }
            }
        }

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
            
            // 선택 영역 경계 계산
            double selectionLeft = Canvas.GetLeft(selectionRectangle);
            double selectionTop = Canvas.GetTop(selectionRectangle);
            double selectionRight = selectionLeft + selectionRectangle.Width;
            double selectionBottom = selectionTop + selectionRectangle.Height;

            // 텍스트박스가 선택 영역을 벗어나지 않도록 위치 제한
            double textBoxLeft = Math.Max(selectionLeft, Math.Min(clickPoint.X, selectionRight - textBox.MinWidth));
            double textBoxTop = Math.Max(selectionTop, Math.Min(clickPoint.Y, selectionBottom - textBox.MinHeight));

            Canvas.SetLeft(textBox, textBoxLeft);
            Canvas.SetTop(textBox, textBoxTop);
            
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
                ToolTip = LocalizationManager.Get("Delete")
            };
            
            textDeleteButton.Click += (s, e) =>
            {
                // 텍스트박스 삭제
                canvas.Children.Remove(textBox);
                canvas.Children.Remove(textDeleteButton);
                drawnElements.Remove(textBox);
                selectedTextBox = null;
            };
            
            Canvas.SetLeft(textDeleteButton, left + width - 20);
            Canvas.SetTop(textDeleteButton, top - 28);
            canvas.Children.Add(textDeleteButton);
        }

        private void Canvas_DrawMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(canvas);
            
            // 선택 영역 내부인지 확인
            if (!IsPointInSelection(clickPoint))
                return;
            
            // 넘버링 모드 처리
            if (currentTool == "넘버링")
            {    
                // 기존 넘버링 그룹 클릭은 무시 (드래그용)
                if (e.OriginalSource is FrameworkElement clickedElement)
                {
                    // 클릭한 요소의 부모를 따라가서 Canvas(그룹)인지 확인
                    DependencyObject parent = clickedElement;
                    while (parent != null && parent != canvas)
                    {
                        if (parent is Canvas groupCanvas && 
                            numberingGroups.ContainsValue(groupCanvas))
                        {
                            return;  // 기존 넘버링 그룹이므로 새로 생성 안 함
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
                
                // 툴바의 버튼만 무시
                if (e.OriginalSource is Button || 
                    (e.OriginalSource is FrameworkElement fe && fe.Parent is Button))
                {
                    return;
                }               
                CreateNumberingAt(clickPoint);
                return;
            }
            
            // 다른 도구들은 버튼 클릭 무시
            if (e.OriginalSource is FrameworkElement source && 
            (source is Button || source.Parent is Button || source.TemplatedParent is Button))
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
                isDrawingShape = true;
                shapeStartPoint = clickPoint;
                
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
            // ← 라인 2836-2848 삭제 (중복된 넘버링 처리 제거)

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
                    else if (element is Canvas groupCanvas)
                    {
                        // 넘버링 그룹 렌더링
                        double groupLeft = Canvas.GetLeft(groupCanvas) - selectionLeft;
                        double groupTop = Canvas.GetTop(groupCanvas) - selectionTop;
                        
                        foreach (var child in groupCanvas.Children)
                        {
                            if (child is Border border)
                            {
                                // 배지 렌더링
                                double badgeLeft = groupLeft + Canvas.GetLeft(border);
                                double badgeTop = groupTop + Canvas.GetTop(border);
                                
                                var ellipse = new EllipseGeometry(
                                    new Point(badgeLeft + border.Width / 2, badgeTop + border.Height / 2),
                                    border.Width / 2,
                                    border.Height / 2);
                                
                                drawingContext.DrawGeometry(border.Background, 
                                    new Pen(border.BorderBrush, border.BorderThickness.Left), ellipse);
                                
                                // 배지 내 텍스트
                                if (border.Child is TextBlock tb)
                                {
                                    var formattedText = new FormattedText(
                                        tb.Text,
                                        System.Globalization.CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight,
                                        new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                                        tb.FontSize,
                                        tb.Foreground,
                                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                    
                                    drawingContext.DrawText(formattedText, 
                                        new Point(badgeLeft + (border.Width - formattedText.Width) / 2, 
                                                badgeTop + (border.Height - formattedText.Height) / 2));
                                }
                            }
                            else if (child is TextBox noteTextBox && !string.IsNullOrWhiteSpace(noteTextBox.Text))
                            {
                                // 텍스트박스 렌더링
                                double textBoxLeft = groupLeft + Canvas.GetLeft(noteTextBox);
                                double textBoxTop = groupTop + Canvas.GetTop(noteTextBox);
                                
                                var formattedText = new FormattedText(
                                    noteTextBox.Text,
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface(noteTextBox.FontFamily, noteTextBox.FontStyle, noteTextBox.FontWeight, noteTextBox.FontStretch),
                                    noteTextBox.FontSize,
                                    noteTextBox.Foreground,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                
                                drawingContext.DrawText(formattedText, new Point(textBoxLeft, textBoxTop));
                            }
                        }     
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
        // 도형 옵션 버튼 생성 도우미 (사각형/원/선/화살표)
        private Button CreateShapeOptionButton(string content, ShapeType type)
        {
            var button = new Button
            {
                Content = content,
                Width = 30,
                Height = 24,
                Margin = new Thickness(2, 0, 2, 0),
                FontSize = 14,
                Padding = new Thickness(0, -4, 0, 0),
                Background = shapeType == type ? new SolidColorBrush(Color.FromRgb(200, 230, 255)) : Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            button.Click += (s, e) =>
            {
                shapeType = type;
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

        // 언어 변경 시 즉시편집 툴바/팔레트 런타임 갱신
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (!instantEditMode) return;
            try
            {
                Dispatcher.Invoke(() =>
                {
                    RebuildInstantEditUIPreservingState();
                });
            }
            catch { }
        }

        // 툴바/팔레트를 재생성하면서 현재 상태(도구/색/두께/도형옵션)를 보존
        private void RebuildInstantEditUIPreservingState()
        {
            // 현재 상태 보존
            string prevTool = currentTool;
            var prevColor = selectedColor;
            int prevPen = penThickness;
            int prevHigh = highlightThickness;
            var prevShapeType = shapeType;
            double prevShapeBorder = shapeBorderThickness;
            bool prevShapeFill = shapeIsFilled;
            double prevShapeOpacity = shapeFillOpacity;

            // 기존 팔레트/툴바 제거
            HideColorPalette();
            if (toolbarContainer != null)
            {
                try { canvas.Children.Remove(toolbarContainer); } catch { }
                toolbarContainer = null;
                toolbarPanel = null;
            }

            // 툴바 재생성 (LocalizationManager.Get으로 라벨/툴팁 재적용)
            ShowEditToolbar();

            // 상태 복원
            selectedColor = prevColor;
            penThickness = prevPen;
            highlightThickness = prevHigh;
            shapeType = prevShapeType;
            shapeBorderThickness = prevShapeBorder;
            shapeIsFilled = prevShapeFill;
            shapeFillOpacity = prevShapeOpacity;

            // 현재 도구 다시 반영 (팔레트 표시 포함)
            double selectionLeft = Canvas.GetLeft(selectionRectangle);
            double selectionTop = Canvas.GetTop(selectionRectangle);
            double selectionHeight = selectionRectangle.Height;
            double toolbarLeft = selectionLeft;
            double toolbarTop = selectionTop + selectionHeight + 10;
            if (toolbarTop + 44 > vHeight) toolbarTop = selectionTop - 44 - 10;

            switch (prevTool)
            {
                case "펜":
                    currentTool = "펜";
                    ShowColorPalette("펜", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableDrawingMode();
                    break;
                case "형광펜":
                    currentTool = "형광펜";
                    selectedColor = Colors.Yellow;
                    ShowColorPalette("형광펜", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableDrawingMode();
                    break;
                case "텍스트":
                    currentTool = "텍스트";
                    ShowColorPalette("텍스트", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableTextMode();
                    break;
                case "도형":
                    currentTool = "도형";
                    ShowColorPalette("도형", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableShapeMode();
                    break;
                case "모자이크":
                    currentTool = "모자이크";
                    ShowColorPalette("모자이크", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableMosaicMode();
                    break;
                case "지우개":
                    currentTool = "지우개";
                    HideColorPalette();
                    EnableEraserMode();
                    break;
                case "넘버링":
                    currentTool = "넘버링";
                    ShowColorPalette("넘버링", toolbarLeft, toolbarTop + 60);  // ← 팔레트 표시
                    EnableNumberingMode();
                    break;
                default:
                    // 기본 펜 선택
                    currentTool = "펜";
                    ShowColorPalette("펜", toolbarLeft, toolbarTop + 60); // [수정]
                    EnableDrawingMode();
                    break;
            }
        }

        private void EnableNumberingMode()
        {
            // 기존 이벤트 제거
            canvas.MouseLeftButtonDown -= Canvas_TextMouseDown;
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown;
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;

            // 넘버링용 이벤트 다시 등록
            canvas.MouseLeftButtonDown += Canvas_DrawMouseDown;
            canvas.MouseMove += Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp += Canvas_DrawMouseUp;
            
            // 그리기 모드 비활성화 (넘버링은 클릭만 필요)
            isDrawingEnabled = false;
            
            Cursor = Cursors.Arrow;
        }

        private void CreateNumberingAt(Point canvasPoint)
        {
            int myNumber = numberingNext;
            
            var group = new Canvas();
            group.Background = Brushes.Transparent;
            numberingGroups[myNumber] = group;

            double badgeSize = 24;
            var badgeBorder = new Border
            {
                Width = badgeSize,
                Height = badgeSize,
                CornerRadius = new CornerRadius(badgeSize / 2),
                Background = new SolidColorBrush(selectedColor),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2)
            };
            var numText = new TextBlock
            {
                Text = myNumber.ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = numText;
            group.Children.Add(badgeBorder);
            Canvas.SetLeft(badgeBorder, 0);
            Canvas.SetTop(badgeBorder, 0);

            var noteBox = new TextBox
            {
                Width = 160,
                Height = 28,
                Background = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(6, 3, 6, 3),
                FontSize = 12,
                Foreground = Brushes.White,
                Text = string.Empty
            };
            group.Children.Add(noteBox);
            Canvas.SetLeft(noteBox, badgeSize + 8);
            Canvas.SetTop(noteBox, -(noteBox.Height - badgeSize) / 2);

            Border selectionBorder = null;
            RoutedEventHandler gotFocusHandler = null;
            RoutedEventHandler lostFocusHandler = null;

            gotFocusHandler = (s, e) =>
            {
                // ReadOnly 상태면 테두리 표시 안 함 (확정 후)
                if (noteBox.IsReadOnly) return;
                
                if (selectionBorder == null)
                {
                    selectionBorder = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(100, 200, 255)),
                        BorderThickness = new Thickness(2),
                        Background = Brushes.Transparent,
                        Width = noteBox.Width + 4,
                        Height = noteBox.Height + 4,
                        IsHitTestVisible = false
                    };
                    group.Children.Add(selectionBorder);
                    Canvas.SetLeft(selectionBorder, Canvas.GetLeft(noteBox) - 2);
                    Canvas.SetTop(selectionBorder, Canvas.GetTop(noteBox) - 2);
                    Panel.SetZIndex(selectionBorder, -1);
                }
                else
                {
                    selectionBorder.Visibility = Visibility.Visible;
                }
            };

            lostFocusHandler = (s, e) =>
            {
                if (selectionBorder != null)
                {
                    selectionBorder.Visibility = Visibility.Collapsed;
                }
            };

            noteBox.GotFocus += gotFocusHandler;
            noteBox.LostFocus += lostFocusHandler;
            noteBox.LostFocus += (s, e) =>
            {
                if (selectionBorder != null)
                {
                    selectionBorder.Visibility = Visibility.Collapsed;
                }
            };

            var confirmBtn = new Button
            {
                Width = 22,
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationManager.Get("OK")
            };
            confirmBtn.Content = new TextBlock
            {
                Text = "✓",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var deleteBtn = new Button
            {
                Width = 22,
                Height = 22,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = LocalizationManager.Get("Delete")
            };
            try
            {
                deleteBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/icons/delete_selected.png")),
                    Width = 18,
                    Height = 18
                };
            }
            catch { deleteBtn.Content = new TextBlock { Text = "🗑", FontSize = 14 }; }

            void UpdateButtonsPosition()
            {
                double nbLeft = Canvas.GetLeft(noteBox);
                double nbTop = Canvas.GetTop(noteBox);
                double midY = nbTop + (noteBox.Height - confirmBtn.Height) / 2.0;
                Canvas.SetLeft(confirmBtn, nbLeft + noteBox.Width + 6);
                Canvas.SetTop(confirmBtn, midY);
                Canvas.SetLeft(deleteBtn, nbLeft + noteBox.Width + 6 + confirmBtn.Width + 6);
                Canvas.SetTop(deleteBtn, midY);
            }

            group.Children.Add(confirmBtn);
            group.Children.Add(deleteBtn);
            Panel.SetZIndex(confirmBtn, 1000);
            Panel.SetZIndex(deleteBtn, 1000);

            UpdateButtonsPosition();

            canvas.Children.Add(group);
            Canvas.SetLeft(group, canvasPoint.X - badgeSize / 2);
            Canvas.SetTop(group, canvasPoint.Y - badgeSize / 2);
            Panel.SetZIndex(group, 500);

            drawnElements.Add(group);
            undoStack.Push(group);

            // 드래그 이벤트 (간단 버전)
            bool isDrag = false;
            Point dragStart = new Point();
            Point origin = new Point();

            MouseButtonEventHandler noteBoxMouseDown = (s, e) =>
            {
                var pos = e.GetPosition(noteBox);
                bool isNearBorder = pos.X < 5 || pos.X > noteBox.Width - 5 || 
                                    pos.Y < 5 || pos.Y > noteBox.Height - 5;
                
                if (isNearBorder)
                {
                    // 다른 마우스 캡처가 있으면 해제
                    if (noteBox.IsMouseCaptured) noteBox.ReleaseMouseCapture();
                    if (badgeBorder.IsMouseCaptured) badgeBorder.ReleaseMouseCapture();
                    
                    isDrag = true;
                    dragStart = e.GetPosition(group);
                    origin = new Point(Canvas.GetLeft(noteBox), Canvas.GetTop(noteBox));
                    noteBox.CaptureMouse();
                    e.Handled = true;
                }
            };

            MouseEventHandler noteBoxMouseMove = (s, e) =>
            {
                if (!isDrag) return;
                var p = e.GetPosition(group);
                double dx = p.X - dragStart.X;
                double dy = p.Y - dragStart.Y;
                Canvas.SetLeft(noteBox, origin.X + dx);
                Canvas.SetTop(noteBox, origin.Y + dy);
                UpdateButtonsPosition();
                e.Handled = true;
            };

            MouseButtonEventHandler noteBoxMouseUp = (s, e) =>
            {
                isDrag = false;
                if (noteBox.IsMouseCaptured)
                {
                    noteBox.ReleaseMouseCapture();
                }
                e.Handled = true;
            };

            noteBox.PreviewMouseLeftButtonDown += noteBoxMouseDown;
            noteBox.PreviewMouseMove += noteBoxMouseMove;
            noteBox.PreviewMouseLeftButtonUp += noteBoxMouseUp;

            bool isDragBadge = false;
            Point dragStartBadge = new Point();
            Point originBadge = new Point();

            MouseButtonEventHandler badgeMouseDown = (s, e) =>
            {
                isDragBadge = true;
                dragStartBadge = e.GetPosition(group);
                originBadge = new Point(Canvas.GetLeft(badgeBorder), Canvas.GetTop(badgeBorder));
                badgeBorder.CaptureMouse();
                e.Handled = true;
            };

            MouseEventHandler badgeMouseMove = (s, e) =>
            {
                if (!isDragBadge) return;
                var p = e.GetPosition(group);
                double dx = p.X - dragStartBadge.X;
                double dy = p.Y - dragStartBadge.Y;
                Canvas.SetLeft(badgeBorder, originBadge.X + dx);
                Canvas.SetTop(badgeBorder, originBadge.Y + dy);
                e.Handled = true;
            };

            MouseButtonEventHandler badgeMouseUp = (s, e) =>
            {
                isDragBadge = false;
                try { badgeBorder.ReleaseMouseCapture(); } catch { }
            };

            badgeBorder.PreviewMouseLeftButtonDown += badgeMouseDown;
            badgeBorder.PreviewMouseMove += badgeMouseMove;
            badgeBorder.PreviewMouseLeftButtonUp += badgeMouseUp;

            deleteBtn.Click += (s, e) =>
            {
                if (canvas.Children.Contains(group)) canvas.Children.Remove(group);
                drawnElements.Remove(group);
                
                numberingGroups.Remove(myNumber);
                
                if (numberingGroups.Count > 0)
                {
                    numberingNext = numberingGroups.Keys.Max() + 1;
                }
                else
                {
                    numberingNext = 1;
                }
            };

            confirmBtn.Click += (s, e) =>
            {
                noteBox.IsReadOnly = true;
                noteBox.BorderBrush = Brushes.Transparent;
                noteBox.Background = Brushes.Transparent;
                noteBox.BorderThickness = new Thickness(0);
                confirmBtn.Visibility = Visibility.Collapsed;
                deleteBtn.Visibility = Visibility.Collapsed;
                // 포커스 해제 및 테두리 숨김
                noteBox.Focusable = false;  // ← 추가: 포커스 불가로 설정
                if (selectionBorder != null)
                {
                    selectionBorder.Visibility = Visibility.Collapsed;  // ← 추가
                }                
                noteBox.PreviewMouseLeftButtonDown -= noteBoxMouseDown;
                noteBox.PreviewMouseMove -= noteBoxMouseMove;
                noteBox.PreviewMouseLeftButtonUp -= noteBoxMouseUp;
                
                badgeBorder.PreviewMouseLeftButtonDown -= badgeMouseDown;
                badgeBorder.PreviewMouseMove -= badgeMouseMove;
                badgeBorder.PreviewMouseLeftButtonUp -= badgeMouseUp;
                
                noteBox.Cursor = Cursors.Arrow;
                badgeBorder.Cursor = Cursors.Arrow;

                bool isConfirmed = true;
                bool isDragGroup = false;
                Point dragStartGroup = new Point();
                Point originGroupPos = new Point();
                
                MouseButtonEventHandler groupMouseDown = null;
                MouseEventHandler groupMouseMove = null;
                MouseButtonEventHandler groupMouseUp = null;
                
                groupMouseDown = (gs, ge) =>
                {
                    isDragGroup = true;
                    dragStartGroup = ge.GetPosition(canvas);
                    originGroupPos = new Point(Canvas.GetLeft(group), Canvas.GetTop(group));
                    
                    // 클릭한 요소가 캡처해야 함
                    if (gs is UIElement element)
                    {
                        element.CaptureMouse();
                    }
                    ge.Handled = true;
                };

                groupMouseMove = (gs, ge) =>
                {
                    // 드래그 중이 아니거나 마우스 캡처가 없으면 중단
                    if (!isDragGroup) return;
                    if (gs is UIElement element && !element.IsMouseCaptured) 
                    {
                        isDragGroup = false;
                        return;
                    }
                    
                    var p = ge.GetPosition(canvas);
                    double dx = p.X - dragStartGroup.X;
                    double dy = p.Y - dragStartGroup.Y;
                    Canvas.SetLeft(group, originGroupPos.X + dx);
                    Canvas.SetTop(group, originGroupPos.Y + dy);
                    ge.Handled = true;
                };

                groupMouseUp = (gs, ge) =>
                {
                    isDragGroup = false;
                    if (gs is UIElement element && element.IsMouseCaptured)
                    {
                        element.ReleaseMouseCapture();
                    }
                };
                                
                badgeBorder.MouseLeftButtonDown += groupMouseDown;
                badgeBorder.MouseMove += groupMouseMove;
                badgeBorder.MouseLeftButtonUp += groupMouseUp;

                noteBox.MouseLeftButtonDown += groupMouseDown;
                noteBox.MouseMove += groupMouseMove;
                noteBox.MouseLeftButtonUp += groupMouseUp;
                // LostMouseCapture 이벤트 추가 (안전장치)
                badgeBorder.LostMouseCapture += (s, e) =>
                {
                    isDragGroup = false;
                };

                noteBox.LostMouseCapture += (s, e) =>
                {
                    isDragGroup = false;
                };

                badgeBorder.Cursor = Cursors.SizeAll;
                noteBox.Cursor = Cursors.SizeAll;
                badgeBorder.Cursor = Cursors.SizeAll;
                noteBox.Cursor = Cursors.SizeAll;

                MouseButtonEventHandler noteBoxDoubleClick = null;
                noteBoxDoubleClick = (ns, ne) =>
                {
                    if (isConfirmed)
                    {
                        noteBox.IsReadOnly = false;
                        noteBox.BorderBrush = Brushes.White;
                        noteBox.Background = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
                        noteBox.BorderThickness = new Thickness(2);
                        confirmBtn.Visibility = Visibility.Visible;
                        deleteBtn.Visibility = Visibility.Visible;
                        
                        badgeBorder.MouseLeftButtonDown -= groupMouseDown;
                        badgeBorder.MouseMove -= groupMouseMove;
                        badgeBorder.MouseLeftButtonUp -= groupMouseUp;

                        noteBox.MouseLeftButtonDown -= groupMouseDown;
                        noteBox.MouseMove -= groupMouseMove;
                        noteBox.MouseLeftButtonUp -= groupMouseUp;
                        
                        isConfirmed = false;
                        noteBox.Focus();
                        ne.Handled = true;
                    }
                };
                noteBox.MouseDoubleClick += noteBoxDoubleClick;
            };
            
            numberingNext++;
        }
    }
}