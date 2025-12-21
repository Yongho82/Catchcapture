using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CatchCapture.Models;

using ResLoc = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture.Utilities
{
    public class SnippingWindow : Window, IDisposable
    {
        private Point startPoint;
        private readonly Canvas canvas;
        private bool isSelecting = false;
        private readonly TextBlock? infoTextBlock;
        private Image screenImage;
        private BitmapSource? screenCapture;

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
        private string currentTool = ""; 
        private List<UIElement> drawnElements = new List<UIElement>();
        private Stack<UIElement> undoStack = new Stack<UIElement>();
        private int penThickness = 3; 
 
        private Button? activeToolButton; 
         // 도형 관련 필드
        private ShapeType shapeType = ShapeType.Rectangle;
        private double shapeBorderThickness = 2;
        private double shapeFillOpacity = 0.5; // 기본 투명도 50%
        private bool shapeIsFilled = false;
        // [추가] 모자이크 관련 필드
        private double mosaicIntensity = 15; // 모자이크 강도 (기본값)
        // 텍스트 편집 관련 필드
        private double textFontSize = 16;
        private string textFontFamily = "Malgun Gothic";
        private FontWeight textFontWeight = FontWeights.Normal; // 추가
        private FontStyle textFontStyle = FontStyles.Normal; // 추가
        private bool textUnderlineEnabled = false; // 추가
        private bool textShadowEnabled = false; // 추가
        
        private Border? magnifierBorder;
        private Image? magnifierImage;
        private const double MagnifierSize = 150; // 돋보기 크기
        private const double MagnificationFactor = 3.0; // 확대 배율
        // 넘버링 도구 관련
        private int numberingNext = 1; // 배지 번호 자동 증가
        // 십자선 관련 필드 추가
        private Line? crosshairHorizontal;
        private Line? crosshairVertical;
        private double highlightOpacity = 0.5; // 형광펜 투명도 (0.0 ~ 1.0)
        private double highlightThickness = 8.0; // 형광펜 두께(double)로 변경
        private double numberingBadgeSize = 24; // 넘버링 배지 크기
        private double numberingTextSize = 12;  // 넘버링 텍스트 크기
        private bool showMagnifier = true;
        // Magnifier UI extras
        private TextBlock? coordsTextBlock;
        private Rectangle? colorPreviewRect;
        private TextBlock? colorValueTextBlock;
        private bool isColorHexFormat = true;
        private Color lastHoverColor = Colors.Transparent;

        // 언어 변경 시 런타임 갱신을 위해 툴바 참조 저장
        private Border? toolbarContainer;
        private StackPanel? toolbarPanel;
        private bool isVerticalToolbarLayout = false; // 툴바가 세로 레이아웃인지 추적

        // [추가] 리사이즈 핸들 관련 필드 (캡처 영역용)
        private List<Rectangle> resizeHandles = new List<Rectangle>();
        private bool isResizing = false;
        private string resizeDirection = "";
        private Point resizeStartPoint;
        private Rect resizeStartRect;

        // [추가] 요소 선택 및 조정 관련 필드 (그려진 객체용)
        private UIElement? selectedObject;
        private Rectangle? objectSelectionBorder;
        private List<Rectangle> objectResizeHandles = new List<Rectangle>();
        private bool isDraggingObject = false;
        private bool isResizingObject = false;
        private string objectResizeDirection = "";
        private Point objectDragLastPoint;
        private Button? objectDeleteButton;
        private Button? objectConfirmButton;

        private SharedCanvasEditor _editorManager;

        public Int32Rect SelectedArea { get; private set; }
        public bool IsCancelled { get; private set; } = false;
        public BitmapSource? SelectedFrozenImage { get; private set; }
        
        // 선택 영역 정보 (즉시편집 모드에서 사용)
        private Rect currentSelectionRect = Rect.Empty;

        public SnippingWindow(bool showGuideText = false, BitmapSource? cachedScreenshot = null)
        {
            var settings = Settings.Load();
            showMagnifier = settings.ShowMagnifier;

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

            // [수정] 오버레이 먼저 표시: 스크린샷 이미지 컨테이너만 미리 생성
            screenImage = new Image();
            Panel.SetZIndex(screenImage, -1);
            canvas.Children.Add(screenImage);

            // 캐시된 스크린샷이 있으면 즉시 사용
            if (cachedScreenshot != null)
            {
                screenCapture = cachedScreenshot;
                screenImage.Source = screenCapture;
            }
            // 없으면 Loaded 이벤트에서 비동기 캡처 (아래 Loaded 핸들러에서 처리)
            else
            {
                // 기존 방식: 동기 캡처
                screenCapture = ScreenCaptureUtility.CaptureScreen();
                screenImage = new Image { Source = screenCapture };
                Panel.SetZIndex(screenImage, -1);
                canvas.Children.Add(screenImage);
            }

            // 통합 오버레이 클래스 초기화 (기존 직접 생성하던 Geometry/Path/Rectangle 대체)
            selectionOverlay = new CaptureSelectionOverlay(canvas);
            
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

            // (sizeTextBlock 및 selectionRectangle 생성 코드 제거됨)

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
            Loaded += async (s, e) => 
            { 
                try 
                { 
                    Activate(); 
                    Focus(); 
                    
                    // [수정] 캐시된 스크린샷이 없으면 비동기로 캡처
                    if (screenCapture == null)
                    {
                        // 백그라운드에서 캡처 (UI 스레드 블로킹 방지)
                        var captured = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());
                        if (captured != null)
                        {
                            screenCapture = captured;
                            screenImage.Source = screenCapture;
                        }
                    }
                } 
                catch { } 
            };

            // 비동기 캡처 제거: 어둡게 되는 시점과 즉시 상호작용 가능 상태를 일치시킴

            // moveStopwatch.Start(); // Removed
            magnifierStopwatch.Start();

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
            this.PreviewKeyDown += SnippingWindow_PreviewKeyDown;

            _editorManager = new SharedCanvasEditor(canvas, drawnElements, undoStack);
            _editorManager.MosaicRequired += (rect) => ApplyMosaic(rect);

            // 언어 변경 이벤트 구독: 즉시편집 UI 텍스트 런타임 갱신
            try { LocalizationManager.LanguageChanged += OnLanguageChanged; } catch { }
        }

        private void SnippingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 즉시편집 모드에서 엔터키 입력 시 확정 처리 (버튼 포커스 문제 해결)
            if (instantEditMode && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                // 텍스트 박스 편집 중일 때는 엔터키가 줄바꿈 역할을 해야 하므로 닫지 않음
                if (Keyboard.FocusedElement is TextBox tb && !tb.IsReadOnly)
                {
                    return;
                }

                ConfirmAndClose();
                e.Handled = true;
            }
        }

        private void SnippingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // [수정] ESC 키는 항상 동작 (영역 선택 전에도 취소 가능)
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
                return;
            }

            // Shift Key Toggle for Color Format
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                if (!e.IsRepeat)
                {
                   isColorHexFormat = !isColorHexFormat;
                   UpdateColorInfoText();
                }
                e.Handled = true;
            }

            // C for Copy Color
            if (e.Key == Key.C)
            {
                CopyColorToClipboard(closeWindow: true);
                e.Handled = true;
            }
            
            // 즉시편집 모드일 때만 나머지 단축키 활성화
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
        }

        // 돋보기 내부 십자선 (UI 요소로 분리하여 성능 개선)
        private Line? magnifierCrosshairH;
        private Line? magnifierCrosshairV;
        private Canvas? magnifierCanvas; // 이미지 + 십자선을 담는 컨테이너

        private void CreateMagnifier()
        {
            if (!showMagnifier) return;
            double borderThick = 1.0;
            double cornerRad = 15.0; // [Requested] Rounded corners
            
            // Image part
            // Calculate inner size so it fits inside the border without being clipped
            // Border Width is fixed to MagnifierSize (150), so content must be smaller by 2*Thickness
            double innerSize = MagnifierSize - (borderThick * 2); 

            // 1. Magnifier Image
            magnifierImage = new Image
            {
                Width = innerSize,
                Height = innerSize,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(magnifierImage, BitmapScalingMode.NearestNeighbor);

            // 2. Crosshairs inside Magnifier [Requested] Red Dotted
            double centerPos = innerSize / 2.0;
            
            magnifierCrosshairH = new Line
            {
                X1 = 0, X2 = innerSize,
                Y1 = centerPos, Y2 = centerPos,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 }, // Dotted
                IsHitTestVisible = false
            };

            magnifierCrosshairV = new Line
            {
                X1 = centerPos, X2 = centerPos,
                Y1 = 0, Y2 = innerSize,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 }, // Dotted
                IsHitTestVisible = false
            };

            // Center small frame (pixel selector)
            var centerFrame = new Rectangle
            {
                Width = 7, Height = 7,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerFrame, centerPos - 3.5);
            Canvas.SetTop(centerFrame, centerPos - 3.5);

            // Container for Image + Crosshairs
            magnifierCanvas = new Canvas
            {
                Width = innerSize,
                Height = innerSize,
                Background = Brushes.White
            };
            magnifierCanvas.Children.Add(magnifierImage);
            magnifierCanvas.Children.Add(magnifierCrosshairH);
            magnifierCanvas.Children.Add(magnifierCrosshairV);
            magnifierCanvas.Children.Add(centerFrame);

            // Clip content to rounded corners
            magnifierCanvas.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, innerSize, innerSize),
                RadiusX = Math.Max(0, cornerRad - borderThick),
                RadiusY = Math.Max(0, cornerRad - borderThick)
            };

            // 3. Info Panel (Bottom)
            var infoStack = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), // Sleek Dark
                Padding = new Thickness(8),
                Child = infoStack,
                CornerRadius = new CornerRadius(0, 0, cornerRad, cornerRad) // Rounded bottom
            };

            // 3.1 Coordinates
            coordsTextBlock = new TextBlock
            {
                Text = "(0, 0)",
                Foreground = Brushes.White,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // 3.2 Color Row (Preview + Value)
            var colorRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            colorPreviewRect = new Rectangle
            {
                Width = 14, Height = 14,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.White,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            colorValueTextBlock = new TextBlock
            {
                Text = "#FFFFFF",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            colorRow.Children.Add(colorPreviewRect);
            colorRow.Children.Add(colorValueTextBlock);

            // 3.3 Helper Text
            var helpText1 = new TextBlock
            {
                Text = "[C] 색상복사",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            var helpText2 = new TextBlock
            {
                Text = "[Shift] RGB/HEX 값 바꾸기",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 5)
            };

            infoStack.Children.Add(coordsTextBlock);
            infoStack.Children.Add(colorRow);
            infoStack.Children.Add(helpText1);
            infoStack.Children.Add(helpText2);

            // Main Container (Border -> StackPanel)
            var containerStack = new StackPanel();
            containerStack.Children.Add(magnifierCanvas);
            containerStack.Children.Add(infoBorder);

            magnifierBorder = new Border
            {
                Width = MagnifierSize,
                // Height is Auto
                BorderBrush = Brushes.White, // White Border
                BorderThickness = new Thickness(borderThick),
                Background = Brushes.Black,
                Child = containerStack,
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(cornerRad) // Rounded total
            };

            // Shadow
            magnifierBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.7
            };

            canvas.Children.Add(magnifierBorder);
            Panel.SetZIndex(magnifierBorder, 1000);

            // Fullscreen Crosshair (Keep existing logic but user didn't ask to change it, 
            // but previous code had it. I'll preserve it.)
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
            
            // Add full screen crosshairs behind magnifier? Or atop?
            // Existing code added them after magnifier but typically crosshairs are below UI.
            // But previous code was: canvas.Children.Add(crosshairHorizontal);
            // I'll keep it.
            canvas.Children.Add(crosshairHorizontal);
            canvas.Children.Add(crosshairVertical);
            Panel.SetZIndex(crosshairHorizontal, 1001);
            Panel.SetZIndex(crosshairVertical, 1001);
        }
        
        private void SnippingWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);
            isSelecting = true;
            Mouse.Capture(this);
            // CompositionTarget.Rendering += CompositionTarget_Rendering; // 제거됨 (오버레이 클래스 내에서 처리)

            // 오버레이 시작
            selectionOverlay?.StartSelection(startPoint);
            
            // 크기 표시 숨기기 (오버레이가 처리하지만 혹시 모르니 제거)
            // sizeTextBlock.Visibility = Visibility.Collapsed;
        }
        private void SnippingWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting) return;
            isSelecting = false;
            // CompositionTarget.Rendering -= CompositionTarget_Rendering; // 제거됨
            if (Mouse.Captured == this) Mouse.Capture(null);
            
            // 돋보기 숨기기
            if (magnifierBorder != null)
                magnifierBorder.Visibility = Visibility.Collapsed;
            
            // 오버레이 종료 및 최종 사각형 획득
            Rect finalRect = selectionOverlay?.EndSelection(hideVisuals: !instantEditMode) ?? new Rect(0,0,0,0);
            
            // 선택 영역 저장 (다른 메서드에서 사용)
            currentSelectionRect = finalRect;

            // 선택된 영역 계산 (Start/End Point 대신 최종 Rect 사용)
            double width = finalRect.Width;
            double height = finalRect.Height;

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                // Just Clicked (very small movement) -> Copy Color with Sticker
                CopyColorToClipboard(closeWindow: false); 
                
                // Reset selection (오버레이 리셋)
                selectionOverlay?.Reset();
                return;
            }

            // Convert from WPF DIPs to device pixels and offset by virtual screen origin
            var dpi = VisualTreeHelper.GetDpi(this);
            int pxLeft = (int)Math.Round(finalRect.Left * dpi.DpiScaleX);
            int pxTop = (int)Math.Round(finalRect.Top * dpi.DpiScaleY);
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
        // 리팩토링된 오버레이 클래스 사용
        private CaptureSelectionOverlay? selectionOverlay;
        
        // 돋보기 업데이트용 throttling
        private Point lastMagnifierPoint;
        private const double MinMagnifierMoveDelta = 3.0; 
        private readonly System.Diagnostics.Stopwatch magnifierStopwatch = new();
        private const int MinMagnifierIntervalMs = 16; 
        private const int MinMagnifierIntervalMsDragging = 33; 
        private const double MinMagnifierMoveDeltaDragging = 5.0; 

        // MouseMove Handler
        private void SnippingWindow_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(canvas);
            
            // 드래그 중 돋보기 처리
            int magnifierInterval = isSelecting ? MinMagnifierIntervalMsDragging : MinMagnifierIntervalMs;
            double magnifierDelta = isSelecting ? MinMagnifierMoveDeltaDragging : MinMagnifierMoveDelta;
            
            if (magnifierStopwatch.ElapsedMilliseconds >= magnifierInterval ||
                Math.Abs(currentPoint.X - lastMagnifierPoint.X) >= magnifierDelta ||
                Math.Abs(currentPoint.Y - lastMagnifierPoint.Y) >= magnifierDelta)
            {
                UpdateMagnifier(currentPoint);
                lastMagnifierPoint = currentPoint;
                magnifierStopwatch.Restart();
            }
            
            if (!isSelecting) return;

            // 오버레이 업데이트 위임 (내부적으로 스로틀링 처리됨)
            selectionOverlay?.UpdateSelection(currentPoint);
        }

        private void HideMagnifier()
        {
            if (magnifierBorder != null)
                magnifierBorder.Visibility = Visibility.Collapsed;
            if (crosshairHorizontal != null)
                crosshairHorizontal.Visibility = Visibility.Collapsed;
            if (crosshairVertical != null)
                crosshairVertical.Visibility = Visibility.Collapsed;
        }

        // CompositionTarget_Rendering 메서드 제거 (CaptureSelectionOverlay가 내부적으로 처리)
        private void UpdateMagnifier(Point mousePos)
        {
            if (!showMagnifier) return;
            if (magnifierBorder == null || magnifierImage == null || screenCapture == null)
                return;

            try
            {
                // 돋보기 표시
                magnifierBorder.Visibility = Visibility.Visible;

                var dpi = VisualTreeHelper.GetDpi(this);
                int centerX = (int)(mousePos.X * dpi.DpiScaleX);
                int centerY = (int)(mousePos.Y * dpi.DpiScaleY);

                // 1. Update Zoomed Image
                int cropSize = (int)(MagnifierSize / MagnificationFactor);
                int halfCrop = cropSize / 2;

                int cropX = Math.Max(0, Math.Min(centerX - halfCrop, screenCapture.PixelWidth - cropSize));
                int cropY = Math.Max(0, Math.Min(centerY - halfCrop, screenCapture.PixelHeight - cropSize));
                int cropW = Math.Min(cropSize, screenCapture.PixelWidth - cropX);
                int cropH = Math.Min(cropSize, screenCapture.PixelHeight - cropY);

                if (cropW > 0 && cropH > 0)
                {
                    var croppedBitmap = new CroppedBitmap(screenCapture, new Int32Rect(cropX, cropY, cropW, cropH));
                    magnifierImage.Source = croppedBitmap;
                }

                // 2. Extract Color at Cursor
                if (centerX >= 0 && centerY >= 0 && centerX < screenCapture.PixelWidth && centerY < screenCapture.PixelHeight)
                {
                    // For efficiency, maybe we could read from 'croppedBitmap' center, but it might be slightly offset due to clamping.
                    // Reading from main bitmap:
                    byte[] pixels = new byte[4]; 
                    // Create a 1x1 crop to read pixel? OR CopyPixels from large image.
                    // CopyPixels from large image is efficient enough for single pixel.
                    var rect = new Int32Rect(centerX, centerY, 1, 1);
                    try 
                    {
                        // Note: Stride calculation. 4 bytes per pixel.
                        screenCapture.CopyPixels(rect, pixels, 4, 0);
                        // BGRA assumption
                        lastHoverColor = Color.FromRgb(pixels[2], pixels[1], pixels[0]);
                        
                        if (colorPreviewRect != null) 
                            colorPreviewRect.Fill = new SolidColorBrush(lastHoverColor);
                        
                        UpdateColorInfoText();
                    }
                    catch { }
                }

                // 3. Update Coords Text
                if (coordsTextBlock != null)
                {
                    coordsTextBlock.Text = $"({centerX}, {centerY})";
                }

                // 4. Update Position
                double offsetX = 20; 
                double offsetY = 20; 
                
                double magnifierX = mousePos.X + offsetX;
                double magnifierY = mousePos.Y + offsetY;
                
                // Get actual height including the new info panel
                double totalHeight = magnifierBorder.ActualHeight;
                if (double.IsNaN(totalHeight) || totalHeight == 0) totalHeight = MagnifierSize + 100; // Estimate

                if (magnifierX + MagnifierSize > vWidth)
                    magnifierX = mousePos.X - MagnifierSize - offsetX;
                if (magnifierY + totalHeight > vHeight)
                    magnifierY = mousePos.Y - totalHeight - offsetY;

                Canvas.SetLeft(magnifierBorder, magnifierX);
                Canvas.SetTop(magnifierBorder, magnifierY);

                // 5. Update Fullscreen Crosshairs
                if (crosshairHorizontal != null && crosshairVertical != null)
                {
                    crosshairHorizontal.Visibility = Visibility.Visible;
                    crosshairVertical.Visibility = Visibility.Visible;
                    
                    crosshairHorizontal.X1 = vLeft; crosshairHorizontal.X2 = vLeft + vWidth;
                    crosshairHorizontal.Y1 = mousePos.Y; crosshairHorizontal.Y2 = mousePos.Y;
                    
                    crosshairVertical.X1 = mousePos.X; crosshairVertical.X2 = mousePos.X;
                    crosshairVertical.Y1 = vTop; crosshairVertical.Y2 = vTop + vHeight;
                }
            }
            catch
            {
                magnifierBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateColorInfoText()
        {
            if (colorValueTextBlock == null) return;
            
            // Force Layout Update or Text Refresh
            if (isColorHexFormat)
            {
                colorValueTextBlock.Text = $"#{lastHoverColor.R:X2}{lastHoverColor.G:X2}{lastHoverColor.B:X2}";
            }
            else
            {
                colorValueTextBlock.Text = $"({lastHoverColor.R}, {lastHoverColor.G}, {lastHoverColor.B})";
            }
        }

        private void CopyColorToClipboard(bool closeWindow = false)
        {
            try
            {
                string textToCopy = "";
                if (isColorHexFormat)
                    textToCopy = $"#{lastHoverColor.R:X2}{lastHoverColor.G:X2}{lastHoverColor.B:X2}";
                else
                    textToCopy = $"{lastHoverColor.R}, {lastHoverColor.G}, {lastHoverColor.B}";
                
                Clipboard.SetText(textToCopy);
                
                // Show Sticker (Toast)
                string msg = "클립보드에 복사되었습니다.";
                
                StickerWindow.Show(msg);

                if (closeWindow)
                {
                    DialogResult = false; // Cancel capture essentially, but job done.
                    Close();
                }
            }
            catch { }
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
                // CompositionTarget.Rendering -= CompositionTarget_Rendering; // 제거됨
                selectionOverlay?.Dispose();
                
                // 마우스 캡처 해제
                if (Mouse.Captured == this)
                {
                    Mouse.Capture(null);
                }

                // 스톱워치 정지
                // moveStopwatch?.Stop();

                // 캐시 모드 해제로 GPU 메모리 정리
                /* Removed overlayPath and selectionRectangle cleanup as they are handled by selectionOverlay.Dispose() */

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
            selectionOverlay?.SetSizeTextVisibility(Visibility.Collapsed);
            
            // 마우스 커서 변경
            Cursor = Cursors.Arrow;
            
            // 선택 영역 위치 계산
            double selectionLeft = currentSelectionRect.Left;
            double selectionTop = currentSelectionRect.Top;
            double selectionWidth = currentSelectionRect.Width;
            double selectionHeight = currentSelectionRect.Height;
            
            // [수정] 하단 감지: 선택 영역이 화면 하단에 가까운지 확인
            bool isNearBottom = (selectionTop + selectionHeight + 160) > vHeight; // 툴바+팔레트 공간(~160px) 확보 불가
            isVerticalToolbarLayout = isNearBottom; // 멤버 변수에 저장
            
            // [수정] 편집 툴바 컨테이너 (둥근 모서리 Border)
            toolbarContainer = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.15
                }
            };
            toolbarContainer.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
            toolbarContainer.SetResourceReference(TextElement.ForegroundProperty, "ThemeForeground");

            if (isVerticalToolbarLayout)
            {
                toolbarContainer.Width = 60; // 세로 레이아웃 고정 너비
            }
            else
            {
                toolbarContainer.Height = 55; // 가로 레이아웃 고정 높이
            }

            // [수정] 내부 스택패널 - 레이아웃에 따라 방향 조정
            toolbarPanel = new StackPanel
            {
                Orientation = isVerticalToolbarLayout ? Orientation.Vertical : Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            toolbarContainer.Child = toolbarPanel;
            
            // [이동] 툴바 위치를 먼저 계산
            double toolbarWidth = isVerticalToolbarLayout ? 60 : 450;
            double toolbarHeight = isVerticalToolbarLayout ? 600 : 55;
            double toolbarLeft, toolbarTop;
            
            if (isVerticalToolbarLayout)
            {
                // 세로 레이아웃: 선택 영역 우측에 배치
                toolbarLeft = selectionLeft + selectionWidth + 10;
                toolbarTop = selectionTop;
                
                // 우측 경계 체크
                if (toolbarLeft + toolbarWidth > vWidth)
                    toolbarLeft = selectionLeft - toolbarWidth - 10; // 왼쪽에 배치
                if (toolbarLeft < 10)
                    toolbarLeft = 10;
                    
                // 상하 경계 체크
                if (toolbarTop + toolbarHeight > vHeight)
                    toolbarTop = vHeight - toolbarHeight - 10;
                if (toolbarTop < 10)
                    toolbarTop = 10;
            }
            else
            {
                // 가로 레이아웃: 선택 영역 하단에 배치 (기존 로직)
                toolbarLeft = selectionLeft;
                toolbarTop = selectionTop + selectionHeight + 10;
                
                // 화면 경계 체크
                if (toolbarLeft + toolbarWidth > vWidth)
                    toolbarLeft = vWidth - toolbarWidth - 10;
                if (toolbarLeft < 10)
                    toolbarLeft = 10;
                if (toolbarTop + 44 > vHeight)
                    toolbarTop = selectionTop - 44 - 10;
            }
            
            // 팔레트는 동적 계산으로 변경되었으므로 내부 함수 제거

            
            // 선택 버튼
            var selectButton = CreateToolButton("sc_cursor.png", LocalizationManager.Get("Select"), LocalizationManager.Get("SelectTooltip"));
            selectButton.Click += (s, e) => ToggleToolPalette("선택", selectButton);

            // 펜 버튼
            var penButton = CreateToolButton("pen.png", LocalizationManager.Get("Pen"), LocalizationManager.Get("Pen"));
            penButton.Click += (s, e) => ToggleToolPalette("펜", penButton);
            
            // 형광펜 버튼
            var highlighterButton = CreateToolButton("highlight.png", LocalizationManager.Get("Highlighter"), LocalizationManager.Get("Highlighter"));
            highlighterButton.Click += (s, e) => ToggleToolPalette("형광펜", highlighterButton);
            
            // 텍스트 버튼
            var textButton = CreateToolButton("text.png", LocalizationManager.Get("Text"), LocalizationManager.Get("TextAdd"));
            textButton.Click += (s, e) => ToggleToolPalette("텍스트", textButton);
            
            // 도형 버튼
            var shapeButton = CreateToolButton("shape.png", LocalizationManager.Get("ShapeLbl"), LocalizationManager.Get("ShapeOptions"));
            shapeButton.Click += (s, e) => ToggleToolPalette("도형", shapeButton);
            
            // 넘버링 버튼 (도형 다음)
            var numberingButton = CreateToolButton("numbering.png", LocalizationManager.Get("Numbering"), LocalizationManager.Get("Numbering"));
            numberingButton.Click += (s, e) => ToggleToolPalette("넘버링", numberingButton);
            
            // 모자이크 버튼
            var mosaicButton = CreateToolButton("mosaic.png", LocalizationManager.Get("Mosaic"), LocalizationManager.Get("Mosaic"));
            mosaicButton.Click += (s, e) => ToggleToolPalette("모자이크", mosaicButton);
            
            // 지우개 버튼
            var eraserButton = CreateToolButton("eraser.png", LocalizationManager.Get("Eraser"), LocalizationManager.Get("Eraser"));
            eraserButton.Click += (s, e) => ToggleToolPalette("지우개", eraserButton);

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

            // [추가] 고정핀 버튼
            var pinButton = CreateToolButton("pin.png", LocalizationManager.Get("Pin") ?? "Pin", LocalizationManager.Get("PinToScreen") ?? "Pin to Screen");
            pinButton.Click += (s, e) => 
            {
                PinImageToScreen();
            };

            // 구분선
            var separator = new Border
            {
                Tag = "Separator", // [분별용 태그]
                Width = isVerticalToolbarLayout ? 30 : 1,
                Height = isVerticalToolbarLayout ? 1 : 30,
                Background = (Brush)Application.Current.FindResource("ThemeBorder"),
                Margin = isVerticalToolbarLayout ? new Thickness(0, 3, 0, 3) : new Thickness(3, 0, 3, 0)
            };

             
            // 재캡처 버튼 (기존 취소 버튼 위치)
            var cancelButton = new Button
            {
                Width = 45,
                Height = 40,
                Margin = new Thickness(0.5),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = $"{LocalizationManager.Get("Recapture")} (ESC)",
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
            
            // X 텍스트 대신 영역 캡처 아이콘 사용
            var cancelIcon = new Image
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 1, 0, 1),
                Source = new BitmapImage(new Uri("pack://application:,,,/icons/area_capture.png", UriKind.Absolute))
            };
            
            var cancelLabel = new TextBlock
            {
                Text = LocalizationManager.Get("Recapture"),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)Application.Current.FindResource("ThemeForeground")
            };
            cancelLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            
            var cancelViewbox = new Viewbox
            {
                Width = 43,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = cancelLabel
            };

            cancelStackPanel.Children.Add(cancelIcon);
            cancelStackPanel.Children.Add(cancelViewbox);
            
            var cancelBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                Child = cancelStackPanel
            };
            cancelBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
            
            cancelButton.Content = cancelBorder;
            
            cancelButton.MouseEnter += (s, e) =>
            {
                cancelBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBorder");
                cancelBorder.Opacity = 0.6;
            };
            
            cancelButton.MouseLeave += (s, e) =>
            {
                cancelBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
                cancelBorder.Opacity = 1.0;
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
                            var cancelButton1 = tagButtons1.Item2; // Renamed to avoid partial conflict
                            if (confirmButton != null && canvas.Children.Contains(confirmButton))
                                canvas.Children.Remove(confirmButton);
                            if (cancelButton1 != null && canvas.Children.Contains(cancelButton1))
                                canvas.Children.Remove(cancelButton1);
                        }
                    }
                }
                drawnElements.Clear();
                undoStack.Clear();
                
                // 툴바와 팔레트 제거
                if (toolbarContainer != null)
                {
                    canvas.Children.Remove(toolbarContainer);
                    toolbarContainer = null;
                }
                HideColorPalette();
                RemoveResizeHandles(); // 리사이즈 핸들 제거
                
                // 즉시편집 모드 해제하고 다시 영역 선택 시작
                isSelecting = false;
                
                // 선택 영역 초기화
                selectionOverlay?.Reset();
                currentSelectionRect = Rect.Empty;
                
                // 마우스 이벤트 복원
                this.MouseLeftButtonDown += SnippingWindow_MouseLeftButtonDown;
                this.MouseMove += SnippingWindow_MouseMove;
                this.MouseLeftButtonUp += SnippingWindow_MouseLeftButtonUp;
            };

            // [추가] 리사이즈 핸들 생성
            CreateResizeHandles();
                       
            // 완료 버튼
            var doneButton = new Button
            {
                Width = 45,
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
                Foreground = (Brush)Application.Current.FindResource("ThemeForeground"),
                Margin = new Thickness(0, -2, 0, 0)  // 위로 2px 이동
            };
            doneText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            
            var doneLabel = new TextBlock
            {
                Text = LocalizationManager.Get("OK"),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)Application.Current.FindResource("ThemeForeground")
            };
            doneLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            
            doneStackPanel.Children.Add(doneText);
            doneStackPanel.Children.Add(doneLabel);
            
            // 완료 버튼용 Border
            var doneBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                Child = doneStackPanel
            };
            doneBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
            
            doneButton.Content = doneBorder;
            
            doneButton.MouseEnter += (s, e) =>
            {
                doneBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBorder");
                doneBorder.Opacity = (drawnElements.Count > 0) ? 0.9 : 0.6;
            };
            
            doneButton.MouseLeave += (s, e) =>
            {
                doneBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
                doneBorder.Opacity = 1.0;
            };
            
            // 완료 버튼 클릭 이벤트
            doneButton.Click += (s, e) =>
            {
                ConfirmAndClose();
            };
            
            // 구분선 2
            var separator2 = new Border
            {
                Tag = "Separator",
                Width = isVerticalToolbarLayout ? 30 : 1,
                Height = isVerticalToolbarLayout ? 1 : 30,
                Background = (Brush)Application.Current.FindResource("ThemeBorder"),
                Margin = isVerticalToolbarLayout ? new Thickness(0, 3, 0, 3) : new Thickness(3, 0, 3, 0)
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
                Tag = "Separator",
                Width = isVerticalToolbarLayout ? 30 : 1,
                Height = isVerticalToolbarLayout ? 1 : 30,
                Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                Margin = isVerticalToolbarLayout ? new Thickness(0, 3, 0, 3) : new Thickness(3, 0, 3, 0)
            };
            
            // 복사 버튼
            var copyButton = CreateActionButton("copy_selected.png", LocalizationManager.Get("CopySelected"), $"{LocalizationManager.Get("CopyToClipboard")} (Ctrl+C)");
            copyButton.Click += (s, e) => CopyToClipboard();
            
            // 저장 버튼
            var saveButton = CreateActionButton("save_selected.png", LocalizationManager.Get("Save"), $"{LocalizationManager.Get("Save")} (Ctrl+S)");
            saveButton.Click += (s, e) => SaveToFile();

            // [수정] 버튼들을 toolbarStackPanel에 추가
            toolbarPanel.Children.Add(selectButton);
            toolbarPanel.Children.Add(penButton);
            toolbarPanel.Children.Add(highlighterButton);
            toolbarPanel.Children.Add(textButton);
            toolbarPanel.Children.Add(shapeButton);
            toolbarPanel.Children.Add(numberingButton);
            toolbarPanel.Children.Add(mosaicButton);
            toolbarPanel.Children.Add(eraserButton);
            toolbarPanel.Children.Add(imageSearchButton);
            toolbarPanel.Children.Add(ocrButton);
            toolbarPanel.Children.Add(pinButton); // [Fix] Add pinButton to toolbar
            toolbarPanel.Children.Add(separator);
            toolbarPanel.Children.Add(cancelButton);
            toolbarPanel.Children.Add(doneButton);
            toolbarPanel.Children.Add(separator2);
            toolbarPanel.Children.Add(undoButton);
            toolbarPanel.Children.Add(resetButton);
            toolbarPanel.Children.Add(separator3);
            toolbarPanel.Children.Add(copyButton);
            toolbarPanel.Children.Add(saveButton);
            
            // 툴바 위치 설정 (초기화)
            UpdateToolbarPosition();

            // 펜을 기본 도구로 선택하고 빨간색으로 설정 (팔레트는 띄우지 않음)
            currentTool = "펜";
            selectedColor = Colors.Red;
            SetActiveToolButton(penButton);
            EnableDrawingMode();
        }

        private Button CreateToolButton(string iconPath, string label, string tooltip)
        {
            var button = new Button
            {
                Width = 45,
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
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0.5)
            };
            border.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
            
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
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            
            var viewbox = new Viewbox
            {
                Width = 43,
                Stretch = Stretch.Uniform, // 비율 유지하며 축소
                StretchDirection = StretchDirection.DownOnly, // 커지지는 않고 작아지기만 함
                Child = textBlock
            };
            
            stackPanel.Children.Add(viewbox);
            border.Child = stackPanel;
            button.Content = border;
            
            // 호버 효과 (Border의 색상을 변경)
            button.MouseEnter += (s, e) =>
            {
                border.SetResourceReference(Border.BackgroundProperty, "ThemeBorder");
                border.Opacity = 0.6;
            };
            
            button.MouseLeave += (s, e) =>
            {
                if (button != activeToolButton)
                {
                    border.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
                    border.Opacity = 1.0;
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
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0.5)
            };
            border.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");

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
                Margin = new Thickness(0, 0, 0, 0)
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            
            var viewbox = new Viewbox
            {
                Width = 43,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = textBlock
            };
            
            stackPanel.Children.Add(viewbox);
            border.Child = stackPanel;
            button.Content = border;
            
            button.MouseEnter += (s, e) =>
            {
                border.SetResourceReference(Border.BackgroundProperty, "ThemeBorder");
                border.Opacity = 0.6;
            };
            
            button.MouseLeave += (s, e) =>
            {
                border.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
                border.Opacity = 1.0;
            };
            
            return button;
        }

        private void SetActiveToolButton(Button button)
        {
            // 이전 활성 버튼 초기화 (Border 스타일 복구)
            if (activeToolButton != null && activeToolButton.Content is Border oldBorder)
            {
                oldBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
                oldBorder.Opacity = 1.0;
            }
            
            // 새 활성 버튼 설정 (Border 스타일 강조)
            activeToolButton = button;
            if (activeToolButton != null && activeToolButton.Content is Border newBorder)
            {
                newBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBorder");
                newBorder.Opacity = 0.8;
            }
        }
        
        private void ShowColorPalette(string tool, double left, double top)
        {
            // 기존 팔레트 제거
            HideColorPalette();
            
            // [수정] 도구에 따라 레이아웃 분기 (도형은 세로형, 나머지는 가로형)
            Border background;
            
            if (tool == "도형")
            {
                // [도형] PreviewWindow와 동일한 세로형 레이아웃으로 변경
                background = new Border
                {
                    Width = 240, // 세로형 레이아웃에 맞는 너비 (220 -> 240 넉넉하게)
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10)
                };
                
                var mainStack = new StackPanel { Orientation = Orientation.Vertical };
                
                // (1) 도형 종류 섹션
                var shapeLabel = new TextBlock 
                { 
                    Text = LocalizationManager.Get("ShapeLbl"), 
                    FontWeight = FontWeights.SemiBold, 
                    FontSize = 12, 
                    Margin = new Thickness(0, 0, 0, 4) 
                };
                mainStack.Children.Add(shapeLabel);

                var shapeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                // CreateShapeOptionButton은 스니핑윈도우 내부에 존재하는 메서드 사용
                shapeRow.Children.Add(CreateShapeOptionButton("□", ShapeType.Rectangle));
                shapeRow.Children.Add(CreateShapeOptionButton("○", ShapeType.Ellipse));
                shapeRow.Children.Add(CreateShapeOptionButton("╱", ShapeType.Line));
                shapeRow.Children.Add(CreateShapeOptionButton("↗", ShapeType.Arrow));
                mainStack.Children.Add(shapeRow);
                
                // (2) 스타일 섹션 (윤곽선/채우기 + 투명도)
                var styleLabel = new TextBlock 
                { 
                    Text = LocalizationManager.Get("LineStyle"), 
                    FontWeight = FontWeights.SemiBold, 
                    FontSize = 12, 
                    Margin = new Thickness(0, 0, 0, 4) 
                };
                mainStack.Children.Add(styleLabel);
                
                var fillRow = new StackPanel { Orientation = Orientation.Horizontal };
                var outlineBtn = CreateFillOptionButton(LocalizationManager.Get("Outline"), false);
                var fillBtn = CreateFillOptionButton(LocalizationManager.Get("Fill"), true);
                fillRow.Children.Add(outlineBtn);
                fillRow.Children.Add(fillBtn);
                mainStack.Children.Add(fillRow);
                
                // 투명도 슬라이더 (채우기일 때만 활성화)
                var opacityPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Margin = new Thickness(0, 8, 0, 0),
                    IsEnabled = shapeIsFilled,
                    Opacity = shapeIsFilled ? 1.0 : 0.5,
                    Tag = "OpacityPanel"
                };
                
                opacityPanel.Children.Add(new TextBlock 
                { 
                    Text = LocalizationManager.Get("FillOpacity"), 
                    FontSize = 10, 
                    Foreground = Brushes.Gray, 
                    VerticalAlignment = VerticalAlignment.Center, 
                    Margin = new Thickness(0, 0, 8, 0) 
                });
                
                var opacitySlider = new Slider 
                { 
                    Minimum = 0, Maximum = 100, 
                    Value = shapeFillOpacity * 100, 
                    Width = 80, 
                    VerticalAlignment = VerticalAlignment.Center, 
                    IsSnapToTickEnabled = true, 
                    TickFrequency = 10 
                };
                
                var opacityVal = new TextBlock 
                { 
                    Text = $"{(int)(shapeFillOpacity * 100)}%", 
                    FontSize = 10, 
                    Foreground = Brushes.Gray, 
                    VerticalAlignment = VerticalAlignment.Center, 
                    Margin = new Thickness(8, 0, 0, 0),
                    Width = 30
                };
                
                opacitySlider.ValueChanged += (s, e) => 
                { 
                    shapeFillOpacity = opacitySlider.Value / 100.0; 
                    opacityVal.Text = $"{(int)opacitySlider.Value}%";
                };
                
                opacityPanel.Children.Add(opacitySlider);
                opacityPanel.Children.Add(opacityVal);
                mainStack.Children.Add(opacityPanel);
                
                // 버튼 클릭 시 불투명도 패널 제어
                fillBtn.Click += (s, e) => { opacityPanel.IsEnabled = true; opacityPanel.Opacity = 1.0; };
                outlineBtn.Click += (s, e) => { opacityPanel.IsEnabled = false; opacityPanel.Opacity = 0.5; };
                
                // 구분선
                mainStack.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.FindResource("ThemeBorder"), Margin = new Thickness(0, 10, 0, 10) });

                // (3) 색상 섹션
                var colorGrid = new WrapPanel { Width = 220 };
                AddColorSwatches(colorGrid);
                mainStack.Children.Add(colorGrid);
                
                background.Child = mainStack;
            }
            else
            {
                // [기본] 가로형 그리드 (좌측 색상 | 옵션) - 기존 형광펜, 텍스트, 펜, 넘버링 등
                background = new Border
                {
                    Width = 320,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10)
                };
                
                var mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 옵션
                background.Child = mainGrid;

                // 1. 색상 섹션 (모자이크 제외)
                if (tool != "모자이크")
                {
                    var colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
                    colorSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Color"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                    
                    var colorGrid = new WrapPanel { Width = 150 };
                    AddColorSwatches(colorGrid);
                    
                    colorSection.Children.Add(colorGrid);
                    Grid.SetColumn(colorSection, 0);
                    mainGrid.Children.Add(colorSection);
                    
                    // 구분선
                    var separator = new Border
                    {
                        Width = 1,
                        Height = 30,
                        Background = (Brush)Application.Current.FindResource("ThemeBorder"),
                        Margin = new Thickness(3, 0, 3, 0)
                    };
                    Grid.SetColumn(separator, 1);
                    mainGrid.Children.Add(separator);
                }

                // 2. 옵션 섹션
                var optionSection = new StackPanel();
                Grid.SetColumn(optionSection, 2);
                mainGrid.Children.Add(optionSection);
                
                if (tool == "넘버링")
                {
                    // [넘버링 수정] PreviewWindow와 동일하게 슬라이더 1개로 통일
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Size"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                    
                    var sizePanel = new StackPanel { Orientation = Orientation.Horizontal };
                    var sizeSlider = new Slider 
                    { 
                        Minimum = 10, Maximum = 60, 
                        Value = numberingBadgeSize, 
                        Width = 100, 
                        VerticalAlignment = VerticalAlignment.Center 
                    }; 
                    var sizeVal = new TextBlock 
                    { 
                        Text = $"{(int)numberingBadgeSize}px", 
                        VerticalAlignment = VerticalAlignment.Center, 
                        Margin = new Thickness(8, 0, 0, 0) 
                    };
                    
                    sizeSlider.ValueChanged += (s, e) => 
                    {
                        numberingBadgeSize = e.NewValue;
                        numberingTextSize = e.NewValue * 0.5; // 텍스트 크기 자동 조정 (비율 유지)
                        sizeVal.Text = $"{(int)e.NewValue}px";
                    };
                    sizePanel.Children.Add(sizeSlider);
                    sizePanel.Children.Add(sizeVal);
                    optionSection.Children.Add(sizePanel);
                }
                else if (tool == "텍스트")
                {
                    // 폰트 선택
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Font"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                    var fontCombo = new ComboBox { Width = 120, Height = 25, Margin = new Thickness(0, 0, 0, 10) };
                    string[] fonts = { 
                        "Malgun Gothic", "Gulim", "Dotum", "Batang", "Gungsuh", 
                        "Arial", "Segoe UI", "Verdana", "Tahoma", "Times New Roman", 
                        "Consolas", "Impact", "Comic Sans MS" 
                    };
                    foreach (var f in fonts) fontCombo.Items.Add(f);
                    fontCombo.SelectedItem = textFontFamily;
                    fontCombo.SelectionChanged += (s, e) => 
                    { 
                        if (fontCombo.SelectedItem is string newFont) 
                        { 
                            textFontFamily = newFont; 
                            if (selectedObject is TextBox tb) tb.FontFamily = new FontFamily(newFont); 
                        } 
                    };
                    optionSection.Children.Add(fontCombo);

                    // 크기 (Slider)
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("SizeLabel"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
                    var sizeSlider = new Slider { Minimum = 8, Maximum = 72, Value = textFontSize, Width = 120, Margin = new Thickness(0,0,0,2) };
                    var sizeVal = new TextBlock { Text = $"{(int)textFontSize}px", FontSize = 11, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
                    
                    sizeSlider.ValueChanged += (s, e) => 
                    { 
                        textFontSize = e.NewValue; 
                        sizeVal.Text = $"{(int)textFontSize}px"; 
                        if (selectedObject is TextBox tb) tb.FontSize = textFontSize; 
                    };
                    optionSection.Children.Add(sizeSlider);
                    optionSection.Children.Add(sizeVal);

                    // 스타일 구분선
                    optionSection.Children.Add(new Border { Height = 10 });
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Style"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });

                    var stylePanel = new StackPanel();

                    // Bold
                    var boldCheck = new CheckBox { Content = LocalizationManager.Get("Bold"), IsChecked = textFontWeight == FontWeights.Bold, Margin = new Thickness(0, 2, 0, 2) };
                    boldCheck.Checked += (s, e) => { textFontWeight = FontWeights.Bold; if (selectedObject is TextBox tb) tb.FontWeight = FontWeights.Bold; };
                    boldCheck.Unchecked += (s, e) => { textFontWeight = FontWeights.Normal; if (selectedObject is TextBox tb) tb.FontWeight = FontWeights.Normal; };
                    stylePanel.Children.Add(boldCheck);

                    // Italic
                    var italicCheck = new CheckBox { Content = LocalizationManager.Get("Italic"), IsChecked = textFontStyle == FontStyles.Italic, Margin = new Thickness(0, 2, 0, 2) };
                    italicCheck.Checked += (s, e) => { textFontStyle = FontStyles.Italic; if (selectedObject is TextBox tb) tb.FontStyle = FontStyles.Italic; };
                    italicCheck.Unchecked += (s, e) => { textFontStyle = FontStyles.Normal; if (selectedObject is TextBox tb) tb.FontStyle = FontStyles.Normal; };
                    stylePanel.Children.Add(italicCheck);

                    // Underline
                    var underlineCheck = new CheckBox { Content = LocalizationManager.Get("Underline"), IsChecked = textUnderlineEnabled, Margin = new Thickness(0, 2, 0, 2) };
                    underlineCheck.Checked += (s, e) => 
                    { 
                        textUnderlineEnabled = true; 
                        if (selectedObject is TextBox tb) tb.TextDecorations = TextDecorations.Underline; 
                    };
                    underlineCheck.Unchecked += (s, e) => 
                    { 
                        textUnderlineEnabled = false; 
                        if (selectedObject is TextBox tb) tb.TextDecorations = null; 
                    };
                    stylePanel.Children.Add(underlineCheck);

                    // Shadow
                    var shadowCheck = new CheckBox { Content = LocalizationManager.Get("Shadow"), IsChecked = textShadowEnabled, Margin = new Thickness(0, 2, 0, 2) };
                    shadowCheck.Checked += (s, e) => 
                    { 
                        textShadowEnabled = true; 
                        if (selectedObject is TextBox tb) 
                            tb.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Opacity = 0.5 }; 
                    };
                    shadowCheck.Unchecked += (s, e) => 
                    { 
                        textShadowEnabled = false; 
                        if (selectedObject is TextBox tb) tb.Effect = null; 
                    };
                    stylePanel.Children.Add(shadowCheck);

                    optionSection.Children.Add(stylePanel);
                }
                else if (tool == "모자이크")
                {
                    // [기존 모자이크 옵션 유지]
                    var optionLabel = new TextBlock { Text = LocalizationManager.Get("Mosaic"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                    optionSection.Children.Add(optionLabel);

                    var intensityPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    intensityPanel.Children.Add(new TextBlock { Text = LocalizationManager.Get("Intensity") + ":", VerticalAlignment = VerticalAlignment.Center, Width = 35 });

                    var slider = new Slider
                    {
                        Minimum = 5, Maximum = 50, Value = mosaicIntensity, Width = 120,
                        VerticalAlignment = VerticalAlignment.Center, IsSnapToTickEnabled = true, TickFrequency = 5,
                        ToolTip = LocalizationManager.Get("Intensity")
                    };
                    slider.ValueChanged += (s, e) => { mosaicIntensity = slider.Value; };
                    intensityPanel.Children.Add(slider);
                    optionSection.Children.Add(intensityPanel);
                }
                else if (tool == "형광펜")
                {
                    // 두께
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Thickness"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                    var thicknessSlider = new Slider 
                    { 
                        Minimum = 1, Maximum = 50, Value = highlightThickness, 
                        Width = 100, Margin = new Thickness(0, 0, 0, 5) 
                    };
                    thicknessSlider.ValueChanged += (s, e) => { highlightThickness = e.NewValue; };
                    optionSection.Children.Add(thicknessSlider);

                    // 투명도
                    optionSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Opacity"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8) });
                    var opacityPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    var opacitySlider = new Slider 
                    { 
                        Minimum = 0, Maximum = 1, Value = highlightOpacity, 
                        Width = 100, Margin = new Thickness(0, 0, 0, 5) 
                    };
                    var opacityVal = new TextBlock { Text = $"{(int)(highlightOpacity * 100)}%", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0)};
                    
                    opacitySlider.ValueChanged += (s, e) => 
                    { 
                        highlightOpacity = e.NewValue; 
                        opacityVal.Text = $"{(int)(e.NewValue * 100)}%"; 
                    };
                    opacityPanel.Children.Add(opacitySlider);
                    opacityPanel.Children.Add(opacityVal);
                    optionSection.Children.Add(opacityPanel);
                }



                else
                {
                    // [기본 펜]
                    var thicknessLabel = new TextBlock { Text = LocalizationManager.Get("Thickness"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                    optionSection.Children.Add(thicknessLabel);
                    
                    var thicknessList = new StackPanel();
                    int[] presets = new int[] { 1, 3, 5, 8, 12 };
                    foreach (var p in presets)
                    {
                        var item = new Grid { Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand, Background = Brushes.Transparent };
                        item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                        item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        
                        var line = new Border { Height = p, Width = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                        line.SetResourceReference(Border.BackgroundProperty, "ThemeForeground");
                        Grid.SetColumn(line, 0); item.Children.Add(line);
                        
                        var text = new TextBlock { Text = $"{p}px", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)Application.Current.FindResource("ThemeForeground"), Opacity = 0.6, Margin = new Thickness(8, 0, 0, 0) };
                        Grid.SetColumn(text, 1); item.Children.Add(text);

                        int thickness = p;
                        item.MouseLeftButtonDown += (s, e) =>
                        {
                            penThickness = thickness;
                            foreach (var child in thicknessList.Children) { if (child is Grid g) g.Background = Brushes.Transparent; }
                            item.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
                        };
                        if (penThickness == thickness) item.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
                        thicknessList.Children.Add(item);
                    }
                    optionSection.Children.Add(thicknessList);
                }
            }
            
            background.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
            background.SetResourceReference(Border.BorderBrushProperty, "ThemeBorder");
            background.SetResourceReference(TextElement.ForegroundProperty, "ThemeForeground");
            
            background.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 5, ShadowDepth = 1, Opacity = 0.2
            };
            
            canvas.Children.Add(background);
            Canvas.SetLeft(background, left);
            Canvas.SetTop(background, top);
            colorPalette = background;
        }

        // [추가] 색상 패널 생성 도우미 메서드
        private void AddColorSwatches(WrapPanel colorGrid)
        {
             foreach (var c in UIConstants.SharedColorPalette) 
                 if (c != Colors.Transparent) colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
             
             foreach (var c in customColors) 
                 colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
             
             // [+] 버튼
             var addButton = new Button
             {
                 Content = "+", Width = 20, Height = 20, Margin = new Thickness(2),
                 BorderThickness = new Thickness(1), Cursor = Cursors.Hand
             };
             addButton.SetResourceReference(Button.BorderBrushProperty, "ThemeBorder");
             addButton.SetResourceReference(Button.BackgroundProperty, "ThemeBackground");
             addButton.SetResourceReference(Button.ForegroundProperty, "ThemeForeground");
             
             addButton.Click += (s, e) =>
             {
                 var dlg = new System.Windows.Forms.ColorDialog();
                 if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                 {
                      var newColor = Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                      customColors.Add(newColor);
                      colorGrid.Children.Insert(colorGrid.Children.Count - 1, CreateColorSwatch(newColor, colorGrid));
                      selectedColor = newColor;
                      UpdateColorSelection(colorGrid);
                 }
             };
             colorGrid.Children.Add(addButton);
        }

        // [추가] 채우기/윤곽선 스타일 버튼 생성
        private Button CreateFillOptionButton(string text, bool isFilled)
        {
             var btn = new Button
             {
                 Content = text, Width = 65, Height = 28, Margin = new Thickness(0, 0, 4, 0),
                 FontSize = 10,
                 Background = (shapeIsFilled == isFilled) ? new SolidColorBrush(Color.FromRgb(72, 152, 255)) : Brushes.White,
                 Foreground = (shapeIsFilled == isFilled) ? Brushes.White : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                 BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)), BorderThickness = new Thickness(1)
             };
             btn.Style = null; // 스타일 초기화
             
             btn.Click += (s, e) =>
             {
                 shapeIsFilled = isFilled;
                 // 버튼 상태 갱신 (부모 패널의 형제 요소들 순회)
                 if (s is Button b && b.Parent is StackPanel sp)
                 {
                     foreach(var child in sp.Children)
                     {
                         if (child is Button otherBtn)
                         {
                             // 간단히 텍스트로 채우기 버튼 식별 (LocalizationManager 값과 비교)
                             bool otherIsFilled = otherBtn.Content.ToString() == LocalizationManager.Get("Fill");
                             bool active = (shapeIsFilled == otherIsFilled);
                             otherBtn.Background = active ? new SolidColorBrush(Color.FromRgb(72, 152, 255)) : Brushes.White;
                             otherBtn.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(60, 60, 60));
                         }
                     }
                 }
             };
             return btn;
        }
        
        private Border CreateColorSwatch(Color c, WrapPanel parentPanel)
        {
            var swatch = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(c),
                BorderThickness = new Thickness(c == selectedColor ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };
            swatch.SetResourceReference(Border.BorderBrushProperty, (c == selectedColor) ? "ThemeForeground" : "ThemeBorder");
            
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

        private void SetupEditorEvents()
        {
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown;
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;
            canvas.MouseLeftButtonDown -= Canvas_SelectMouseDown;
            
            canvas.MouseLeftButtonDown += Canvas_DrawMouseDown;
            canvas.MouseMove += Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp += Canvas_DrawMouseUp;
        }

        private void EnableDrawingMode()
        {
            currentTool = "펜";
            canvas.Cursor = Cursors.Pen;
            SetupEditorEvents();
        }

        private void EnableTextMode()
        {
            currentTool = "텍스트";
            canvas.Cursor = Cursors.IBeam;
            SetupEditorEvents();
        }

        private void EnableSelectMode()
        {
            currentTool = "";
            canvas.Cursor = Cursors.Arrow;

            // 기존 이벤트 제거
            canvas.MouseLeftButtonDown -= Canvas_DrawMouseDown;
            canvas.MouseLeftButtonDown -= Canvas_SelectMouseDown;
            canvas.MouseMove -= Canvas_DrawMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_DrawMouseUp;

            // 선택용 이벤트 등록
            canvas.MouseLeftButtonDown += Canvas_SelectMouseDown;
        }

        private void Canvas_SelectMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 리사이즈 핸들이나 이미 선택된 요소의 부속 버튼 클릭 시 무시
            if (e.OriginalSource is FrameworkElement fe && (fe.Name.StartsWith("ResizeHandle") || fe.Parent is Button || fe is Button))
                return;

            Point clickPoint = e.GetPosition(canvas);

            // 요소 찾기 (역순으로 검색하여 가장 위에 있는 요소 선택)
            UIElement? clickedElement = null;
            for (int i = drawnElements.Count - 1; i >= 0; i--)
            {
                var element = drawnElements[i];
                if (InteractiveEditor.IsPointInElement(clickPoint, element))
                {
                    clickedElement = element;
                    break;
                }
            }

            if (clickedElement != null)
            {
                SelectObject(clickedElement);
                if (clickedElement is not TextBox) // 텍스트박스는 자체 드래그 로직 사용
                {
                    isDraggingObject = true;
                    objectDragLastPoint = clickPoint;
                    canvas.CaptureMouse();
                    
                    canvas.MouseMove -= Canvas_SelectMouseMove;
                    canvas.MouseMove += Canvas_SelectMouseMove;
                    canvas.MouseLeftButtonUp -= Canvas_SelectMouseUp;
                    canvas.MouseLeftButtonUp += Canvas_SelectMouseUp;
                }
            }
            else
            {
                DeselectObject();
            }
        }

        private void Canvas_SelectMouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingObject && selectedObject != null)
            {
                Point currentPoint = e.GetPosition(canvas);
                double dx = currentPoint.X - objectDragLastPoint.X;
                double dy = currentPoint.Y - objectDragLastPoint.Y;

                InteractiveEditor.MoveElement(selectedObject, dx, dy);
                objectDragLastPoint = currentPoint;
                UpdateObjectSelectionUI();
            }
        }

        private void Canvas_SelectMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingObject)
            {
                isDraggingObject = false;
                canvas.ReleaseMouseCapture();
            }
        }

        private void SelectObject(UIElement element)
        {
            if (selectedObject == element) return;
            DeselectObject();

            selectedObject = element;


            // 선택 강조 효과 (점선 테두리)
            UpdateObjectSelectionUI();
            CreateObjectResizeHandles();
        }

        private void DeselectObject()
        {
            if (selectedObject != null)
            {
                // [수정] 텍스트박스인 경우 SharedCanvasEditor 내부 텍스트박스로의 접근이 제한적이므로
                // 일단은 전용 선택 해제 로직 없이 일반 선택 해제만 진행
                // 추후 SharedCanvasEditor에서 선택 해제 로직을 통합 관리할 수도 있음
                
                selectedObject = null;
                if (objectSelectionBorder != null)
                {
                    canvas.Children.Remove(objectSelectionBorder);
                    objectSelectionBorder = null;
                }
                if (objectDeleteButton != null)
                {
                    canvas.Children.Remove(objectDeleteButton);
                    objectDeleteButton = null;
                }
                if (objectConfirmButton != null)
                {
                    canvas.Children.Remove(objectConfirmButton);
                    objectConfirmButton = null;
                }
                RemoveObjectResizeHandles();
            }
            isDraggingObject = false;
            isResizingObject = false;
            Mouse.Capture(null);
            canvas.MouseMove -= Canvas_SelectMouseMove;
            canvas.MouseLeftButtonUp -= Canvas_SelectMouseUp;
            canvas.MouseMove -= ObjectResizeHandle_MouseMove;
            canvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
        }

        private void EnableEraserMode()
        {
            currentTool = "지우개";
            canvas.Cursor = Cursors.Hand;
            SetupEditorEvents();
            DeselectObject();
        }
        private async Task PerformOcr()
        {
            try
            {
                // 선택 영역만 크롭하여 OCR 수행
                BitmapSource? imageToOcr = null;
                
                if (screenCapture != null && !currentSelectionRect.IsEmpty)
                {
                    double selectionLeft = currentSelectionRect.Left;
                    double selectionTop = currentSelectionRect.Top;
                    double selectionWidth = currentSelectionRect.Width;
                    double selectionHeight = currentSelectionRect.Height;
                    
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
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.Get("NoImageForOcr"), LocalizationManager.Get("Info"));
                    return;
                }
                
                // 로딩 표시
                this.Cursor = Cursors.Wait;
                
                // OCR 실행
                var ocrResult = await CatchCapture.Utilities.OcrUtility.ExtractTextFromImageAsync(imageToOcr);
                
                // 커서 복원
                this.Cursor = Cursors.Arrow;
                
                if (string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.Get("NoExtractedText"), LocalizationManager.Get("Info"));
                    return;
                }
                
                // OCR 결과창 표시
                var resultWindow = new OcrResultWindow(ocrResult.Text, ocrResult.ShowWarning);
                resultWindow.Owner = this;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.Get("OcrError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    CatchCapture.CustomMessageBox.Show(ResLoc.GetString("EditConfirmGuide"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.Get("NoImageToSave"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.Get("Error")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            // [수정] InteractiveEditor를 통해 관련 부속 요소들도 함께 제거
            InteractiveEditor.RemoveInteractiveElement(canvas, lastElement);
        }
        
        private void ResetAllDrawings()
        {
            if (drawnElements.Count == 0)
            {
                return; // 조용히 무시
            }
            
            var result = CatchCapture.CustomMessageBox.Show(
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
                    
                    // InteractiveEditor를 통해 관련 부속 요소 제거
                    InteractiveEditor.RemoveInteractiveElement(canvas, element);
                }
                
                drawnElements.Clear();
                _editorManager.ResetNumbering(); // 넘버링 번호도 초기화
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
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.Get("CopyError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                Width = 170,  // 너비 줄임 (200 -> 140)
                Height = 36,  // 높이 줄임 (50 -> 36)
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // 투명도 약간 더 줌
                CornerRadius = new CornerRadius(18), // 둥근 알약 모양
                Child = new TextBlock
                {
                    Text = LocalizationManager.Get("CopiedToClipboard"), // 느낌표 제거하고 심플하게
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
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.Get("NoImageToSave"), LocalizationManager.Get("Info"));
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
                    
                    CatchCapture.CustomMessageBox.Show($"{LocalizationManager.Get("ImageSaved")}:\n{saveDialog.FileName}", LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.Get("SaveError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateObjectResizeHandles()
        {
            RemoveObjectResizeHandles();
            if (selectedObject == null) return;
            if (selectedObject is Polyline) return; // Polyline은 리사이즈 복잡해서 일단 보류

            string[] directions = { "NW", "N", "NE", "W", "E", "SW", "S", "SE" };
            Cursor[] cursors = { Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE, Cursors.SizeWE, Cursors.SizeNESW, Cursors.SizeNS, Cursors.SizeNWSE };

            for (int i = 0; i < directions.Length; i++)
            {
                var handle = new Rectangle
                {
                    Name = "ResizeHandle_" + directions[i],
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = Brushes.DeepSkyBlue,
                    StrokeThickness = 1,
                    Cursor = cursors[i]
                };

                string dir = directions[i];
                handle.MouseLeftButtonDown += (s, e) => ObjectResizeHandle_MouseDown(s, e, dir);
                
                canvas.Children.Add(handle);
                Panel.SetZIndex(handle, 2010);
                objectResizeHandles.Add(handle);
            }
            UpdateObjectResizeHandles(InteractiveEditor.GetElementBounds(selectedObject));
        }

        private void RemoveObjectResizeHandles()
        {
            foreach (var h in objectResizeHandles) canvas.Children.Remove(h);
            objectResizeHandles.Clear();
        }

        private void UpdateObjectResizeHandles(Rect bounds)
        {
            foreach (var handle in objectResizeHandles)
            {
                string dir = handle.Name.Replace("ResizeHandle_", "");
                double left = 0, top = 0;

                switch (dir)
                {
                    case "NW": left = bounds.Left - 4; top = bounds.Top - 4; break;
                    case "N": left = bounds.Left + bounds.Width / 2 - 4; top = bounds.Top - 4; break;
                    case "NE": left = bounds.Right - 4; top = bounds.Top - 4; break;
                    case "W": left = bounds.Left - 4; top = bounds.Top + bounds.Height / 2 - 4; break;
                    case "E": left = bounds.Right - 4; top = bounds.Top + bounds.Height / 2 - 4; break;
                    case "SW": left = bounds.Left - 4; top = bounds.Bottom - 4; break;
                    case "S": left = bounds.Left + bounds.Width / 2 - 4; top = bounds.Bottom - 4; break;
                    case "SE": left = bounds.Right - 4; top = bounds.Bottom - 4; break;
                }

                Canvas.SetLeft(handle, left);
                Canvas.SetTop(handle, top);
            }
        }

        private void ObjectResizeHandle_MouseDown(object sender, MouseButtonEventArgs e, string direction)
        {
            if (sender is UIElement handle)
            {
                objectResizeDirection = direction;
                objectDragLastPoint = e.GetPosition(canvas);
                
                canvas.CaptureMouse();
                canvas.MouseMove -= ObjectResizeHandle_MouseMove;
                canvas.MouseMove += ObjectResizeHandle_MouseMove;
                canvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
                canvas.MouseLeftButtonUp += ObjectResizeHandle_MouseUp;
                
                isResizingObject = true;
                e.Handled = true;
            }
        }

        private void ObjectResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizingObject && selectedObject != null)
            {
                Point currentPoint = e.GetPosition(canvas);
                double dx = currentPoint.X - objectDragLastPoint.X;
                double dy = currentPoint.Y - objectDragLastPoint.Y;

                InteractiveEditor.ResizeElement(selectedObject, dx, dy, objectResizeDirection);
                objectDragLastPoint = currentPoint;
                UpdateObjectSelectionUI();
            }
        }

        private void ObjectResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isResizingObject)
            {
                isResizingObject = false;
                canvas.ReleaseMouseCapture();
                canvas.MouseMove -= ObjectResizeHandle_MouseMove;
                canvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
                e.Handled = true;
            }
        }
        
        private void SyncEditorProperties()
        {
            if (_editorManager == null) return;
            _editorManager.CurrentTool = currentTool;
            _editorManager.SelectedColor = selectedColor;
            _editorManager.PenThickness = penThickness;
            _editorManager.HighlightThickness = highlightThickness;
            _editorManager.HighlightOpacity = highlightOpacity;
            _editorManager.CurrentShapeType = shapeType;
            _editorManager.ShapeBorderThickness = shapeBorderThickness;
            _editorManager.ShapeIsFilled = shapeIsFilled;
            _editorManager.ShapeFillOpacity = shapeFillOpacity;
            _editorManager.NumberingTextSize = numberingTextSize;
            _editorManager.TextFontSize = textFontSize;
            _editorManager.TextFontFamily = textFontFamily;
            _editorManager.TextFontWeight = textFontWeight;
            _editorManager.TextFontStyle = textFontStyle;
            _editorManager.TextUnderlineEnabled = textUnderlineEnabled;
            _editorManager.TextShadowEnabled = textShadowEnabled;
        }

        private void UpdateObjectSelectionUI()
        {
            if (selectedObject == null) return;

            Rect bounds = InteractiveEditor.GetElementBounds(selectedObject);
            
            if (objectSelectionBorder == null)
            {
                objectSelectionBorder = new Rectangle
                {
                    Stroke = Brushes.DeepSkyBlue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false
                };
                canvas.Children.Add(objectSelectionBorder);
            }

            objectSelectionBorder.Width = bounds.Width + 6;
            objectSelectionBorder.Height = bounds.Height + 6;
            Canvas.SetLeft(objectSelectionBorder, bounds.Left - 3);
            Canvas.SetTop(objectSelectionBorder, bounds.Top - 3);

            // 확정 버튼 (V)
            if (objectConfirmButton == null)
            {
                objectConfirmButton = new Button
                {
                    Content = "✓",
                    Width = 20,
                    Height = 20,
                    Background = Brushes.Green,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = LocalizationManager.Get("Confirm") ?? "확정"
                };
                objectConfirmButton.Click += (s, e) => {
                    DeselectObject();
                };
                canvas.Children.Add(objectConfirmButton);
            }
            Canvas.SetLeft(objectConfirmButton, bounds.Right - 18);
            Canvas.SetTop(objectConfirmButton, bounds.Top - 15);

            // 삭제 버튼
            if (objectDeleteButton == null)
            {
                objectDeleteButton = new Button
                {
                    Content = "✕",
                    Width = 20,
                    Height = 20,
                    Background = Brushes.Red,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = LocalizationManager.Get("Delete") ?? "삭제"
                };
                objectDeleteButton.Click += (s, e) => {
                    UIElement? toRemove = selectedObject;
                    DeselectObject();
                    if (toRemove != null)
                    {
                        canvas.Children.Remove(toRemove);
                        drawnElements.Remove(toRemove);
                    }
                };
                canvas.Children.Add(objectDeleteButton);
            }
            Canvas.SetLeft(objectDeleteButton, bounds.Right + 5);
            Canvas.SetTop(objectDeleteButton, bounds.Top - 15);
            
            UpdateObjectResizeHandles(bounds);
        }

        private void EnableShapeMode()
        {
            currentTool = "도형";
            canvas.Cursor = Cursors.Cross;
            SetupEditorEvents();
        }

        private void EnableMosaicMode()
        {
            currentTool = "모자이크";
            canvas.Cursor = Cursors.Cross;
            SetupEditorEvents();
        }

        private void EnableNumberingMode()
        {
            currentTool = "넘버링";
            canvas.Cursor = Cursors.Hand;
            SetupEditorEvents();
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


        private void Canvas_DrawMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(canvas);
            if (!IsPointInSelection(clickPoint)) return;

            SyncEditorProperties();
            _editorManager.StartDrawing(clickPoint, e.OriginalSource);
            numberingNext = _editorManager.NextNumber; // 넘버링 번호 동기화
        }
        
        private void Canvas_DrawMouseMove(object sender, MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(canvas);
            if (!IsPointInSelection(currentPoint)) return;

            _editorManager.UpdateDrawing(currentPoint);
        }
        
        private void Canvas_DrawMouseUp(object sender, MouseButtonEventArgs e)
        {
            _editorManager.FinishDrawing();
        }

        private void SaveDrawingsToImage()
        {
            // 선택 영역의 위치와 크기
            double selectionLeft = currentSelectionRect.Left;
            double selectionTop = currentSelectionRect.Top;
            double selectionWidth = currentSelectionRect.Width;
            double selectionHeight = currentSelectionRect.Height;
            
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

                        if (polyline.Opacity < 1.0)
                        {
                            drawingContext.PushOpacity(polyline.Opacity);
                        }

                        for (int i = 0; i < adjustedPoints.Count - 1; i++)
                        {
                            drawingContext.DrawLine(pen, adjustedPoints[i], adjustedPoints[i + 1]);
                        }

                        if (polyline.Opacity < 1.0)
                        {
                            drawingContext.Pop();
                        }
                    }
                    else if (element is TextBox textBox)
                    {
                        if (string.IsNullOrWhiteSpace(textBox.Text))
                            continue;
                        
                        double tLeft = Canvas.GetLeft(textBox);
                        double tTop = Canvas.GetTop(textBox);
                        if (double.IsNaN(tLeft)) tLeft = 0;
                        if (double.IsNaN(tTop)) tTop = 0;
                        double textLeft = tLeft - selectionLeft;
                        double textTop = tTop - selectionTop;

                        double tWidth = textBox.Width;
                        double tHeight = textBox.Height;
                        if (double.IsNaN(tWidth)) tWidth = textBox.ActualWidth;
                        if (double.IsNaN(tHeight)) tHeight = textBox.ActualHeight;

                        // 배경 및 테두리 (활성화된 상태일 때만 그릴지 검토, 일단 항상 그림)
                        if (textBox.Background != null && textBox.Background != Brushes.Transparent)
                        {
                            drawingContext.DrawRectangle(textBox.Background, 
                                (textBox.BorderThickness.Left > 0) ? new Pen(textBox.BorderBrush, textBox.BorderThickness.Left) : null,
                                new Rect(textLeft, textTop, tWidth, tHeight));
                        }

                        var formattedText = new FormattedText(
                            textBox.Text,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                            textBox.FontSize,
                            textBox.Foreground,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        
                        drawingContext.DrawText(formattedText, new Point(textLeft + textBox.Padding.Left, textTop + textBox.Padding.Top));
                    }     
                    else if (element is Shape shape)
                    {
                        if (shape is Line line)
                        {
                            drawingContext.DrawLine(new Pen(line.Stroke, line.StrokeThickness), 
                                new Point(line.X1 - selectionLeft, line.Y1 - selectionTop), 
                                new Point(line.X2 - selectionLeft, line.Y2 - selectionTop));
                        }
                        else
                        {
                            double sLeft = Canvas.GetLeft(shape);
                            double sTop = Canvas.GetTop(shape);
                            if (double.IsNaN(sLeft)) sLeft = 0;
                            if (double.IsNaN(sTop)) sTop = 0;
                            double left = sLeft - selectionLeft;
                            double top = sTop - selectionTop;
                            
                            double sWidth = shape.Width;
                            double sHeight = shape.Height;
                            if (double.IsNaN(sWidth)) sWidth = shape.ActualWidth;
                            if (double.IsNaN(sHeight)) sHeight = shape.ActualHeight;

                            drawingContext.PushTransform(new TranslateTransform(left, top));

                            if (shape is Rectangle rect)
                            {
                                drawingContext.DrawRectangle(rect.Fill, new Pen(rect.Stroke, rect.StrokeThickness), new Rect(0, 0, sWidth, sHeight));
                            }
                            else if (shape is Ellipse ellipse)
                            {
                                drawingContext.DrawEllipse(ellipse.Fill, new Pen(ellipse.Stroke, ellipse.StrokeThickness), new Point(sWidth / 2, sHeight / 2), sWidth / 2, sHeight / 2);
                            }
                            drawingContext.Pop();
                        } 
                    }
                    else if (element is Image image)
                    {
                        double iLeft = Canvas.GetLeft(image);
                        double iTop = Canvas.GetTop(image);
                        if (double.IsNaN(iLeft)) iLeft = 0;
                        if (double.IsNaN(iTop)) iTop = 0;
                        double left = iLeft - selectionLeft;
                        double top = iTop - selectionTop;
                        
                        double iWidth = image.Width;
                        double iHeight = image.Height;
                        if (double.IsNaN(iWidth)) iWidth = image.ActualWidth;
                        if (double.IsNaN(iHeight)) iHeight = image.ActualHeight;

                        drawingContext.DrawImage(image.Source, new Rect(left, top, iWidth, iHeight));
                    }                    
                    else if (element is Canvas canvas)
                    {
                        // 넘버링 그룹인지 확인 (Border 자식이 있으면 넘버링)
                        bool isNumbering = false;
                        foreach (var child in canvas.Children)
                        {
                            if (child is Border)
                            {
                                isNumbering = true;
                                break;
                            }
                        }

                        if (isNumbering)
                        {
                            double gLeft = Canvas.GetLeft(canvas);
                            double gTop = Canvas.GetTop(canvas);
                            if (double.IsNaN(gLeft)) gLeft = 0;
                            if (double.IsNaN(gTop)) gTop = 0;
                            double groupLeft = gLeft - selectionLeft;
                            double groupTop = gTop - selectionTop;
                            
                            foreach (var child in canvas.Children)
                            {
                                if (child is Border border)
                                {
                                    // 배지 렌더링
                                    double bLeft = Canvas.GetLeft(border);
                                    double bTop = Canvas.GetTop(border);
                                    if (double.IsNaN(bLeft)) bLeft = 0;
                                    if (double.IsNaN(bTop)) bTop = 0;

                                    double badgeLeft = groupLeft + bLeft;
                                    double badgeTop = groupTop + bTop;
                                    
                                    double bWidth = border.Width;
                                    double bHeight = border.Height;
                                    if (double.IsNaN(bWidth)) bWidth = border.ActualWidth;
                                    if (double.IsNaN(bHeight)) bHeight = border.ActualHeight;

                                    var ellipse = new EllipseGeometry(
                                        new Point(badgeLeft + bWidth / 2, badgeTop + bHeight / 2),
                                        bWidth / 2,
                                        bHeight / 2);
                                    
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
                                            new Point(badgeLeft + (bWidth - formattedText.Width) / 2, 
                                                    badgeTop + (bHeight - formattedText.Height) / 2));
                                    }
                                }
                                else if (child is TextBox noteTextBox && !string.IsNullOrWhiteSpace(noteTextBox.Text))
                                {
                                    // 텍스트박스 렌더링
                                    double tbLeft = Canvas.GetLeft(noteTextBox);
                                    double tbTop = Canvas.GetTop(noteTextBox);
                                    if (double.IsNaN(tbLeft)) tbLeft = 0;
                                    if (double.IsNaN(tbTop)) tbTop = 0;

                                    double textBoxLeft = groupLeft + tbLeft;
                                    double textBoxTop = groupTop + tbTop;
                                    
                                    double ntWidth = noteTextBox.Width;
                                    double ntHeight = noteTextBox.Height;
                                    if (double.IsNaN(ntWidth)) ntWidth = noteTextBox.ActualWidth;
                                    if (double.IsNaN(ntHeight)) ntHeight = noteTextBox.ActualHeight;

                                    // 텍스트박스 배경/테두리 그리기
                                    if (noteTextBox.Background != null && noteTextBox.Background != Brushes.Transparent)
                                    {
                                        drawingContext.DrawRectangle(noteTextBox.Background, 
                                            (noteTextBox.BorderThickness.Left > 0) ? new Pen(noteTextBox.BorderBrush, noteTextBox.BorderThickness.Left) : null,
                                            new Rect(textBoxLeft, textBoxTop, ntWidth, ntHeight));
                                    }

                                    var formattedText = new FormattedText(
                                        noteTextBox.Text,
                                        System.Globalization.CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight,
                                        new Typeface(noteTextBox.FontFamily, noteTextBox.FontStyle, noteTextBox.FontWeight, noteTextBox.FontStretch),
                                        noteTextBox.FontSize,
                                        noteTextBox.Foreground,
                                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                    
                                    drawingContext.DrawText(formattedText, new Point(textBoxLeft + noteTextBox.Padding.Left, textBoxTop + noteTextBox.Padding.Top));
                                }
                            }
                        }
                        else
                        {
                            // [화살표 렌더링]
                            foreach (var child in canvas.Children)
                            {
                                if (child is Line l)
                                {
                                    drawingContext.DrawLine(new Pen(l.Stroke, l.StrokeThickness), 
                                        new Point(l.X1 - selectionLeft, l.Y1 - selectionTop), 
                                        new Point(l.X2 - selectionLeft, l.Y2 - selectionTop));
                                }
                                else if (child is Polygon p)
                                {
                                    StreamGeometry streamGeometry = new StreamGeometry();
                                    using (StreamGeometryContext geometryContext = streamGeometry.Open())
                                    {
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
                    }
                }
            }
            
            renderBitmap.Render(drawingVisual);
            renderBitmap.Freeze();
            
            SelectedFrozenImage = renderBitmap;
        } // 메서드 종료

        private bool IsPointInSelection(Point point)
        {
            if (currentSelectionRect.IsEmpty) return false;
            
            double left = currentSelectionRect.Left;
            double top = currentSelectionRect.Top;
            double right = left + currentSelectionRect.Width;
            double bottom = top + currentSelectionRect.Height;
            
            return point.X >= left && point.X <= right && 
                   point.Y >= top && point.Y <= bottom;
        }
        // 파일 맨 끝에 추가

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
            double prevHigh = highlightThickness;
            double prevHighOpacity = highlightOpacity; // [추가]
            var prevShapeType = shapeType;
            double prevShapeBorder = shapeBorderThickness;
            bool prevShapeFill = shapeIsFilled;
            double prevShapeOpacity = shapeFillOpacity;
            double prevBadgeSize = numberingBadgeSize; // [추가]
            double prevTextSize = numberingTextSize;   // [추가]

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
            highlightOpacity = prevHighOpacity; 
            shapeType = prevShapeType;
            shapeBorderThickness = prevShapeBorder;
            shapeIsFilled = prevShapeFill;
            shapeFillOpacity = prevShapeOpacity;
            numberingBadgeSize = prevBadgeSize; 
            numberingTextSize = prevTextSize;

            // 현재 도구 다시 반영 (팔레트 표시 포함)
            double selectionLeft = currentSelectionRect.Left;
            double selectionTop = currentSelectionRect.Top;
            double selectionHeight = currentSelectionRect.Height;
            double toolbarLeft = selectionLeft;
            double toolbarTop = selectionTop + selectionHeight + 10;
            if (toolbarTop + 44 > vHeight) toolbarTop = selectionTop - 44 - 10;

            switch (prevTool)
            {
                case "펜":
                    currentTool = "펜";
                // 팔레트 위치 재계산 (동적)
                ShowPaletteAtCurrentPosition(); 
                    break;
                case "형광펜":
                    currentTool = "형광펜";
                    selectedColor = Colors.Yellow;
                    ShowPaletteAtCurrentPosition();
                    EnableDrawingMode();
                    break;
                case "텍스트":
                    currentTool = "텍스트";
                    ShowPaletteAtCurrentPosition();
                    EnableTextMode();
                    break;
                case "도형":
                    currentTool = "도형";
                    ShowPaletteAtCurrentPosition();
                    EnableShapeMode();
                    break;
                case "모자이크":
                    currentTool = "모자이크";
                    ShowPaletteAtCurrentPosition();
                    EnableMosaicMode();
                    break;
                case "지우개":
                    currentTool = "지우개";
                    HideColorPalette();
                    EnableEraserMode();
                    break;
                case "넘버링":
                    currentTool = "넘버링";
                    ShowPaletteAtCurrentPosition();
                    EnableNumberingMode();
                    break;
                default:
                    // 기본 펜 선택
                    currentTool = "펜";
                    ShowPaletteAtCurrentPosition();
                    EnableDrawingMode();
                    break;
            }
        }
        
        // [추가] 툴바 버튼 클릭 처리 (토글 및 모드 전환)
        private void ToggleToolPalette(string toolName, Button button)
        {
            // 같은 툴이면 토글
            if (currentTool == toolName)
            {
                if (colorPalette != null && canvas.Children.Contains(colorPalette))
                {
                    HideColorPalette();
                }
                else
                {
                    if (toolName != "지우개")
                        ShowPaletteAtCurrentPosition();
                }
            }
            // 다른 툴이면 전환 및 팔레트 표시
            else
            {
                currentTool = toolName;
                SetActiveToolButton(button);
                
                switch (toolName)
                {
                    case "선택": EnableSelectMode(); HideColorPalette(); return; 
                    case "펜": EnableDrawingMode(); break;
                    case "형광펜": selectedColor = Colors.Yellow; EnableDrawingMode(); break; 
                    case "텍스트": EnableTextMode(); break;
                    case "도형": EnableShapeMode(); break;
                    case "넘버링": EnableNumberingMode(); break;
                    case "모자이크": EnableMosaicMode(); break;
                    case "지우개": EnableEraserMode(); HideColorPalette(); return;
                }
                
                // 툴 변경 시에는 팔레트 표시 (사용자의 "클릭했을때 나오게" 요청 반영)
                ShowPaletteAtCurrentPosition();
            }
        }

        private void ShowPaletteAtCurrentPosition()
        {
             Point pos = CalculatePalettePosition();
             ShowColorPalette(currentTool, pos.X, pos.Y);
        }

        private Point CalculatePalettePosition()
        {
            if (toolbarContainer == null) return new Point(0,0);
            
            double tLeft = Canvas.GetLeft(toolbarContainer);
            double tTop = Canvas.GetTop(toolbarContainer);
            double tWidth = toolbarContainer.ActualWidth;
            if (double.IsNaN(tWidth) || tWidth < 10) tWidth = isVerticalToolbarLayout ? 60 : 450;
            
            if (isVerticalToolbarLayout)
            {
                 // 세로 레이아웃
                 double selectionLeft = currentSelectionRect.Left;
                 // 툴바가 선택 영역 오른쪽에 있으면 팔레트는 더 오른쪽
                 if (tLeft > selectionLeft)
                     return new Point(tLeft + tWidth + 10, tTop);
                 else
                     return new Point(tLeft - 330, tTop);
            }
            else
            {
                // 가로 레이아웃: 툴바 아래
                return new Point(tLeft, tTop + 60);
            }
        }
        
        private void UpdatePalettePosition()
        {
            if (colorPalette != null && canvas.Children.Contains(colorPalette))
            {
                Point pos = CalculatePalettePosition();
                Canvas.SetLeft(colorPalette, pos.X);
                Canvas.SetTop(colorPalette, pos.Y);
            }
        }



        // [추가] 리사이즈 핸들 생성 메서드
        private void CreateResizeHandles()
        {
            RemoveResizeHandles();

            string[] directions = { "NW", "N", "NE", "W", "E", "SW", "S", "SE" };
            Cursor[] cursors = { Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE, Cursors.SizeWE, Cursors.SizeNESW, Cursors.SizeNS, Cursors.SizeNWSE };

            for (int i = 0; i < 8; i++)
            {
                var handle = new Rectangle
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = directions[i],
                    Cursor = cursors[i]
                };

                handle.PreviewMouseLeftButtonDown += ResizeHandle_PreviewMouseLeftButtonDown;
                // MouseMove는 Canvas 전체에서 처리하거나 핸들에서 처리. 
                // 핸들이 작아서 빠르게 움직이면 놓칠 수 있으니 캡처 사용.
                
                canvas.Children.Add(handle);
                Panel.SetZIndex(handle, 2000); // 최상위
                resizeHandles.Add(handle);
            }

            // 캔버스 전체에 드래그 이벤트 연결 (핸들 놓침 방지용)
            canvas.PreviewMouseMove += ResizeHandle_PreviewMouseMove;
            canvas.PreviewMouseLeftButtonUp += ResizeHandle_PreviewMouseLeftButtonUp;

            UpdateResizeHandles();
        }

        private void RemoveResizeHandles()
        {
            foreach (var handle in resizeHandles)
            {
                canvas.Children.Remove(handle);
            }
            resizeHandles.Clear();
            
            canvas.PreviewMouseMove -= ResizeHandle_PreviewMouseMove;
            canvas.PreviewMouseLeftButtonUp -= ResizeHandle_PreviewMouseLeftButtonUp;
        }

        private void UpdateResizeHandles()
        {
            if (resizeHandles.Count != 8) return;

            double left = currentSelectionRect.Left;
            double top = currentSelectionRect.Top;
            double w = currentSelectionRect.Width;
            double h = currentSelectionRect.Height;
            double offset = 5; // 핸들 크기의 절반

            // NW (0)
            SetHandlePosition(resizeHandles[0], left - offset, top - offset);
            // N (1)
            SetHandlePosition(resizeHandles[1], left + w / 2 - offset, top - offset);
            // NE (2)
            SetHandlePosition(resizeHandles[2], left + w - offset, top - offset);
            // W (3)
            SetHandlePosition(resizeHandles[3], left - offset, top + h / 2 - offset);
            // E (4)
            SetHandlePosition(resizeHandles[4], left + w - offset, top + h / 2 - offset);
            // SW (5)
            SetHandlePosition(resizeHandles[5], left - offset, top + h - offset);
            // S (6)
            SetHandlePosition(resizeHandles[6], left + w / 2 - offset, top + h - offset);
            // SE (7)
            SetHandlePosition(resizeHandles[7], left + w - offset, top + h - offset);
        }

        private void SetHandlePosition(UIElement element, double x, double y)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
        }

        private void ResizeHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle handle && handle.Tag is string dir)
            {
                isResizing = true;
                resizeDirection = dir;
                resizeStartPoint = e.GetPosition(canvas);
                resizeStartRect = new Rect(
                    currentSelectionRect.Left,
                    currentSelectionRect.Top,
                    currentSelectionRect.Width,
                    currentSelectionRect.Height
                );

                handle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ResizeHandle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isResizing) return;

            Point currentPoint = e.GetPosition(canvas);
            double dx = currentPoint.X - resizeStartPoint.X;
            double dy = currentPoint.Y - resizeStartPoint.Y;

            double newLeft = resizeStartRect.Left;
            double newTop = resizeStartRect.Top;
            double newWidth = resizeStartRect.Width;
            double newHeight = resizeStartRect.Height;

            // 최소 크기 제한
            double minSize = 20;

            if (resizeDirection.Contains("N"))
            {
                newTop += dy;
                newHeight -= dy;
                if (newHeight < minSize) { newTop -= (minSize - newHeight); newHeight = minSize; }
            }
            if (resizeDirection.Contains("S"))
            {
                newHeight += dy;
                if (newHeight < minSize) newHeight = minSize;
            }
            if (resizeDirection.Contains("W"))
            {
                newLeft += dx;
                newWidth -= dx;
                if (newWidth < minSize) { newLeft -= (minSize - newWidth); newWidth = minSize; }
            }
            if (resizeDirection.Contains("E"))
            {
                newWidth += dx;
                if (newWidth < minSize) newWidth = minSize;
            }

            // 영역 업데이트
            selectionOverlay?.SetRect(new Rect(newLeft, newTop, newWidth, newHeight));
            currentSelectionRect = new Rect(newLeft, newTop, newWidth, newHeight);

            // 핸들 위치 업데이트
            UpdateResizeHandles();
            
            // 크기 텍스트 업데이트 (handled by SetRect inside selectionOverlay)

            // 툴바 위치 업데이트 (실시간)
            UpdateToolbarPosition();

            // 팔레트 위치 업데이트 (실시간)
            if (colorPalette != null && currentTool != "")
            {
                 // 현재 툴바 위치 기준으로 다시 계산
                 // 복잡하므로 툴바 위치 업데이트 후 간단히 재호출하거나 숨겼다가 다시 표시? 
                 // 실시간 이동이 자연스러우므로 다시 계산 로직 수행
                 // (기존 ShowColorPalette 내부 로직 재사용이 어려우므로 간단히 따라가게 처리)
                 // 여기서는 툴바가 이동했으므로 팔레트 위치도 업데이트 필요
                 // 간단히: 툴바와 팔레트의 상대 위치는 유지되거나 다시 계산됨.
                 // -> UpdateToolbarPosition에서 handled? No.
                 // -> Re-call ShowColorPalette logic or Move it manually.
                 // For now, let's keep simple: Close palette on resize start? No, user wants to modify.
                 // Just update palette position if possible.
                 double tLeft = Canvas.GetLeft(toolbarContainer);
                 double tTop = Canvas.GetTop(toolbarContainer);
                 // 팔레트 위치 재계산 로직 복제... 대신 existing logic uses stored state?
                 // Let's just update handles and toolbar for now. Palette might drift. 
                 // Fix: Close palette on resize start or move it. 
                 // Better: Hide palette during drag, show on up?
            }
        }

        private void ResizeHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isResizing)
            {
                isResizing = false;
                
                // 캡처 해제 (모든 핸들 확인)
                foreach (var h in resizeHandles) h.ReleaseMouseCapture();
                
                // 최종 변경 사항 반영 (SelectedArea 등)
                UpdateSelectedAreaFromRect();
                
                e.Handled = true;
            }
        }

        private void UpdateSizeText(Rect rect)
        {
            // 이제 CaptureSelectionOverlay가 크기 텍스트를 처리합니다
        }

        private void UpdateSelectedAreaFromRect()
        {
             // 현재 selectionRectangle 기준으로 SelectedArea 및 SelectedFrozenImage 갱신
             double left = currentSelectionRect.Left;
             double top = currentSelectionRect.Top;
             double width = currentSelectionRect.Width;
             double height = currentSelectionRect.Height;

             var dpi = VisualTreeHelper.GetDpi(this);
             int pxLeft = (int)Math.Round(left * dpi.DpiScaleX);
             int pxTop = (int)Math.Round(top * dpi.DpiScaleY);
             int pxWidth = (int)Math.Round(width * dpi.DpiScaleX);
             int pxHeight = (int)Math.Round(height * dpi.DpiScaleY);

             int globalX = (int)Math.Round(vLeft) + pxLeft;
             int globalY = (int)Math.Round(vTop) + pxTop;

             SelectedArea = new Int32Rect(globalX, globalY, pxWidth, pxHeight);

             // Frozen Image 갱신
             if (screenCapture != null && pxWidth > 0 && pxHeight > 0)
             {
                 int relX = globalX - (int)Math.Round(vLeft);
                 int relY = globalY - (int)Math.Round(vTop);

                 relX = Math.Max(0, Math.Min(relX, screenCapture.PixelWidth - 1));
                 relY = Math.Max(0, Math.Min(relY, screenCapture.PixelHeight - 1));
                 int cw = Math.Max(0, Math.Min(pxWidth, screenCapture.PixelWidth - relX));
                 int ch = Math.Max(0, Math.Min(pxHeight, screenCapture.PixelHeight - relY));

                 if (cw > 0 && ch > 0)
                 {
                     SelectedFrozenImage = new CroppedBitmap(screenCapture, new Int32Rect(relX, relY, cw, ch));
                 }
             }
        }

        private void UpdateToolbarPosition()
        {
            if (toolbarContainer == null) return;

            double selectionLeft = currentSelectionRect.Left;
            double selectionTop = currentSelectionRect.Top;
            double selectionWidth = currentSelectionRect.Width;
            double selectionHeight = currentSelectionRect.Height;

            // [레이아웃 결정 로직 개선]
            // 기본값: 하단 배치
            // 하단 공간 부족 -> 상단 배치
            // 상단 공간도 부족 -> 우측 세로 배치
            
            double toolbarH = 55; // 가로 모드 높이
            // double toolbarW = 450; // warning CS0219: 'toolbarW' 할당되었지만 사용되지 않았습니다.
            
            bool bottomBlocked = (selectionTop + selectionHeight + toolbarH + 10) > vHeight;
            bool topBlocked = (selectionTop - toolbarH - 10) < 0;

            bool shouldUseVertical = false;

            if (bottomBlocked)
            {
                if (topBlocked)
                {
                    shouldUseVertical = true; // 상하 모두 막힘 -> 세로 전환
                }
                else
                {
                    shouldUseVertical = false; // 상단은 가능 -> 가로 상단
                }
            }
            else
            {
                shouldUseVertical = false; // 하단 가능 -> 가로 하단
            }

            // 레이아웃 변경 적용
            if (shouldUseVertical != isVerticalToolbarLayout)
            {
                UpdateToolbarLayout(shouldUseVertical);
            }

            // 위치 계산
            double toolbarWidth = toolbarContainer.ActualWidth > 0 ? toolbarContainer.ActualWidth : (isVerticalToolbarLayout ? 60 : 450);
            double toolbarHeight = toolbarContainer.ActualHeight > 0 ? toolbarContainer.ActualHeight : (isVerticalToolbarLayout ? 600 : 55);
            
            double toolbarLeft, toolbarTop;
            
            if (isVerticalToolbarLayout)
            {
                // 세로: 우측
                toolbarLeft = selectionLeft + selectionWidth + 10;
                toolbarTop = selectionTop;
                
                if (toolbarLeft + toolbarWidth > vWidth) toolbarLeft = selectionLeft - toolbarWidth - 10;
                if (toolbarLeft < 10) toolbarLeft = 10;
                if (toolbarTop + toolbarHeight > vHeight) toolbarTop = vHeight - toolbarHeight - 10;
                if (toolbarTop < 10) toolbarTop = 10;
            }
            else
            {
                // 가로: 하단 or 상단
                toolbarLeft = selectionLeft;
                if (bottomBlocked)
                {
                    // 상단
                    toolbarTop = selectionTop - toolbarHeight - 10;
                }
                else
                {
                    // 하단
                    toolbarTop = selectionTop + selectionHeight + 10;
                }
                
                if (toolbarLeft + toolbarWidth > vWidth) toolbarLeft = vWidth - toolbarWidth - 10;
                if (toolbarLeft < 10) toolbarLeft = 10;
            }

            Canvas.SetLeft(toolbarContainer, toolbarLeft);
            Canvas.SetTop(toolbarContainer, toolbarTop);

            // 툴바가 아직 Canvas에 없으면 추가
            if (!canvas.Children.Contains(toolbarContainer))
            {
                canvas.Children.Add(toolbarContainer);
            }
            
            // [추가] 동적 팔레트 위치 업데이트
            UpdatePalettePosition();
        }

        // [추가] 툴바 레이아웃 동적 변경
        private void UpdateToolbarLayout(bool isVertical)
        {
            if (toolbarContainer == null || toolbarPanel == null) return;

            isVerticalToolbarLayout = isVertical;

            // 컨테이너 크기 모드 변경
            if (isVertical)
            {
                toolbarContainer.Width = 60;
                toolbarContainer.Height = double.NaN;
                toolbarPanel.Orientation = Orientation.Vertical;
            }
            else
            {
                toolbarContainer.Width = double.NaN;
                toolbarContainer.Height = 55;
                toolbarPanel.Orientation = Orientation.Horizontal;
            }

            // 구분선 스타일 변경
            foreach (var child in toolbarPanel.Children)
            {
                if (child is Border border && border.Tag as string == "Separator")
                {
                     if (isVertical)
                     {
                         border.Width = 30;
                         border.Height = 1;
                         border.Margin = new Thickness(0, 3, 0, 3);
                     }
                     else
                     {
                         border.Width = 1;
                         border.Height = 30;
                         border.Margin = new Thickness(3, 0, 3, 0);
                     }
                }
            }
        }
        // [추가] 화면 고정 로직
        private void PinImageToScreen()
        {
            try
            {
                // 1. 현재 선택 영역 확정 (그려진 요소 포함하여 이미지 생성)
                if (drawnElements.Count > 0)
                {
                    SaveDrawingsToImage();
                }
                
                // 2. 고정할 이미지 가져오기
                // SaveDrawingsToImage()가 호출되면 SelectedFrozenImage가 갱신되어있음.
                var pinnedBmp = SelectedFrozenImage;
                
                if (pinnedBmp == null) return;
                
                // 3. PinnedImageWindow 생성
                var pinnedWin = new PinnedImageWindow(pinnedBmp);
                
                // 4. 위치 설정 (실제 스크린 좌표)
                double left = currentSelectionRect.Left;
                double top = currentSelectionRect.Top;
                
                // SnippingWindow Left/Top을 더해서 절대 좌표 계산
                pinnedWin.Left = this.Left + left;
                pinnedWin.Top = this.Top + top;
                
                // 5. 창 표시
                pinnedWin.Show();
                
                // 알림 표시
                string msg = LocalizationManager.Get("PinnedToScreen");
                if (msg == "PinnedToScreen") msg = "이미지가 상단에 고정되었습니다.";
                StickerWindow.Show(msg);
                
                // 6. 현재 캡처 종료 (저장하지 않고 핀만 남김 -> DialogResult false)
                // 재캡처가 가능하도록 세션 종료
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Pin Error: " + ex.Message);
            }
        }
    }
}

