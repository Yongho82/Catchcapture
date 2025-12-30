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
        private Canvas? _drawingCanvas;
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
        private CatchCapture.Controls.ToolOptionsControl? _toolOptionsControl;
        private string currentTool = ""; 
        private List<UIElement> drawnElements = new List<UIElement>();
        private Stack<UIElement> undoStack = new Stack<UIElement>();
        private Stack<UIElement> redoStack = new Stack<UIElement>();
 
        private Button? activeToolButton; 
        private Border? magnifierBorder;

        private Image? magnifierImage;
        private const double MagnifierSize = 150; // 돋보기 크기
        private const double MagnificationFactor = 3.0; // 확대 배율
        // 넘버링 도구 관련
        private int numberingNext = 1; // 배지 번호 자동 증가
        // 십자선 관련 필드 추가
        private Line? crosshairHorizontal;
        private Line? crosshairVertical;
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

        private string? _sourceApp;
        private string? _sourceTitle;

        public string? SourceApp => _sourceApp;
        public string? SourceTitle => _sourceTitle;

        public bool RequestMainWindowMinimize { get; private set; } = false;
        
        // Flag to request MainWindow to open NoteInputWindow after this window closes
        public bool RequestSaveToNote { get; private set; } = false;

        public SnippingWindow(bool showGuideText = false, BitmapSource? cachedScreenshot = null, string? sourceApp = null, string? sourceTitle = null)
        {
            var settings = Settings.Load();
            showMagnifier = settings.ShowMagnifier;

            // Use provided metadata or capture it now (fallback)
            if (!string.IsNullOrEmpty(sourceApp) && !string.IsNullOrEmpty(sourceTitle))
            {
                _sourceApp = sourceApp;
                _sourceTitle = sourceTitle;
            }
            else
            {
                var meta = ScreenCaptureUtility.GetActiveWindowMetadata();
                _sourceApp = meta.AppName;
                _sourceTitle = meta.Title;
            }

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

            screenImage = new Image();
            Panel.SetZIndex(screenImage, -1);
            canvas.Children.Add(screenImage);

            // [추가] 드로잉 전용 캔버스 (영역 제한용 Clip 적용)
            _drawingCanvas = new Canvas();
            _drawingCanvas.Width = vWidth;
            _drawingCanvas.Height = vHeight;
            canvas.Children.Add(_drawingCanvas);

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

            _editorManager = new SharedCanvasEditor(_drawingCanvas ?? canvas, drawnElements, undoStack);
            _editorManager.MosaicRequired += (rect) => ApplyMosaic(rect);
            _editorManager.ElementAdded += OnElementAdded;
            _editorManager.ActionOccurred += () => redoStack.Clear();

            // 언어 변경 이벤트 구독: 즉시편집 UI 텍스트 런타임 갱신
            try { LocalizationManager.LanguageChanged += OnLanguageChanged; } catch { }
        }

        private void SnippingWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 즉시편집 모드에서 엔터키 입력 시 확정 처리
            if (instantEditMode && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                // 텍스트 박스 편집 중이고 줄바꿈을 허용하는 경우 윈도우를 닫지 않음
                var focused = Keyboard.FocusedElement;
                if (focused is TextBox tb && !tb.IsReadOnly && tb.AcceptsReturn)
                {
                    // TextBox가 Enter를 직접 처리하도록 둠
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

            // C for Copy Color (Only without modifiers to avoid conflict with Ctrl+C)
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.None)
            {
                CopyColorToClipboard(closeWindow: true);
                e.Handled = true;
                return; // Added return to be explicit
            }
            
            // 즉시편집 모드일 때만 나머지 단축키 활성화
            if (!instantEditMode) return;
            
            // Ctrl + Z: 실행 취소
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UndoLastAction();
                e.Handled = true;
            }
            // Ctrl + Y: 다시 실행
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                RedoLastAction();
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
                        // [메모리 최적화] CroppedBitmap은 원본(3모니터 전체)을 참조하므로, 
                        // 작은 영역을 잘라도 35MB+가 메모리에 남음. "Deep Copy"를 수행하여 연결 고리 끊기.
                        var tempCrop = new CroppedBitmap(screenCapture, cropRect);
                        
                        var visual = new DrawingVisual();
                        using (var ctx = visual.RenderOpen())
                        {
                            ctx.DrawImage(tempCrop, new Rect(0, 0, cw, ch));
                        }
                        
                        var rtb = new RenderTargetBitmap(cw, ch, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(visual);
                        rtb.Freeze(); // 불변 객체로 만들어 성능 향상 및 스레드 공유 가능

                        SelectedFrozenImage = rtb;
                    }
                }
            }
            catch { /* ignore crop errors */ }

            // ★ 선택한 영역의 중심점에서 창 메타데이터 캡처 (실제 캡처된 창 정보)
            try
            {
                int centerX = globalX + pxWidth / 2;
                int centerY = globalY + pxHeight / 2;
                var capturedMeta = ScreenCaptureUtility.GetWindowAtPoint(centerX, centerY);
                if (capturedMeta.AppName != "Unknown" && capturedMeta.Title != "Unknown")
                {
                    _sourceApp = capturedMeta.AppName;
                    _sourceTitle = capturedMeta.Title;
                }
            }
            catch { /* fallback to initial metadata */ }

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
                
                ScreenCaptureUtility.CopyTextToClipboard(textToCopy);
                
                // Show Sticker (Toast) 
                string msg = ResLoc.GetString("CopiedToClipboard");
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

        // 미리 선택된 영역 설정 (창캡처/단위캡처용)
        public void SetPreselectedArea(Rect screenRect)
        {
            if (!instantEditMode) return;
            
            // 화면 좌표를 캔버스 좌표로 변환
            double canvasX = screenRect.X - vLeft;
            double canvasY = screenRect.Y - vTop;
            
            currentSelectionRect = new Rect(canvasX, canvasY, screenRect.Width, screenRect.Height);
            
            // DPI 변환
            var dpi = VisualTreeHelper.GetDpi(this);
            int pxLeft = (int)Math.Round(canvasX * dpi.DpiScaleX);
            int pxTop = (int)Math.Round(canvasY * dpi.DpiScaleY);
            int pxWidth = (int)Math.Round(screenRect.Width * dpi.DpiScaleX);
            int pxHeight = (int)Math.Round(screenRect.Height * dpi.DpiScaleY);

            int globalX = (int)Math.Round(vLeft) + pxLeft;
            int globalY = (int)Math.Round(vTop) + pxTop;

            SelectedArea = new Int32Rect(globalX, globalY, pxWidth, pxHeight);

            // 선택 영역 이미지 생성
            try
            {
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
                        var cropRect = new Int32Rect(relX, relY, cw, ch);
                        SelectedFrozenImage = new CroppedBitmap(screenCapture, cropRect);
                    }
                }
            }
            catch { }

            // 선택 영역 시각화
            if (selectionOverlay != null)
            {
                selectionOverlay.SetRect(currentSelectionRect);
                selectionOverlay.SetVisibility(Visibility.Visible);
            }

            // 즉시편집 툴바 표시
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ShowEditToolbar();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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
            
            // [수정] 툴바 위치 계산 로직 통합
            var (isVertical, tLeft, tTop) = CalculateToolbarPosition();
            isVerticalToolbarLayout = isVertical;
            
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
            
            // 팔레트는 동적 계산으로 변경되었으므로 내부 함수 제거

            
            // 선택 버튼
            var selectButton = CreateToolButton("cursor.png", ResLoc.GetString("Select"), GetLocalizedTooltip("Select"));
            selectButton.Tag = "선택";
            selectButton.Click += (s, e) => ToggleToolPalette("선택", selectButton);

            var penButton = CreateToolButton("pen.png", ResLoc.GetString("Pen"), GetLocalizedTooltip("Pen"));
            penButton.Tag = "펜";
            penButton.Click += (s, e) => ToggleToolPalette("펜", penButton);
            
            var highlighterButton = CreateToolButton("highlight.png", ResLoc.GetString("Highlighter"), GetLocalizedTooltip("Highlighter"));
            highlighterButton.Tag = "형광펜";
            highlighterButton.Click += (s, e) => ToggleToolPalette("형광펜", highlighterButton);
            
            var textButton = CreateToolButton("text.png", ResLoc.GetString("Text"), GetLocalizedTooltip("TextAdd"));
            textButton.Tag = "텍스트";
            textButton.Click += (s, e) => ToggleToolPalette("텍스트", textButton);
            
            var shapeButton = CreateToolButton("shape.png", ResLoc.GetString("ShapeLbl"), GetLocalizedTooltip("ShapeOptions"));
            shapeButton.Tag = "도형";
            shapeButton.Click += (s, e) => ToggleToolPalette("도형", shapeButton);
            
            var numberingButton = CreateToolButton("numbering.png", ResLoc.GetString("Numbering"), GetLocalizedTooltip("Numbering"));
            numberingButton.Tag = "넘버링";
            numberingButton.Click += (s, e) => ToggleToolPalette("넘버링", numberingButton);
            
            var mosaicButton = CreateToolButton("mosaic.png", ResLoc.GetString("Mosaic"), GetLocalizedTooltip("Mosaic"));
            mosaicButton.Tag = "모자이크";
            mosaicButton.Click += (s, e) => ToggleToolPalette("모자이크", mosaicButton);
            
            var eraserButton = CreateToolButton("eraser.png", ResLoc.GetString("Eraser"), GetLocalizedTooltip("Eraser"));
            eraserButton.Tag = "지우개";
            eraserButton.Click += (s, e) => ToggleToolPalette("지우개", eraserButton);

            // 이미지 검색 버튼
            var imageSearchButton = CreateToolButton("img_find.png", ResLoc.GetString("ImageSearch"), GetLocalizedTooltip("ImageSearch"));
            imageSearchButton.Click += async (s, e) => 
            { 
                await PerformImageSearch();
            };

            // OCR 버튼
            var ocrButton = CreateToolButton("extract_text.png", ResLoc.GetString("OcrCapture") ?? "OCR", GetLocalizedTooltip("OcrCapture"));
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
                // 모든 그린 요소 제거 (드로잉 전용 캔버스에서 제거)
                foreach (var element in drawnElements.ToList())
                {
                    (_drawingCanvas ?? canvas).Children.Remove(element);
                    InteractiveEditor.RemoveInteractiveElement(_drawingCanvas ?? canvas, element);
                }
                drawnElements.Clear();
                undoStack.Clear();
                
                // 툴바와 팔레트 제거
                if (toolbarContainer != null)
                {
                    canvas.Children.Remove(toolbarContainer);
                    toolbarContainer = null;
                }
                HideColorPalette(); // Hide the new tool options control
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
            
            var doneViewbox = new Viewbox
            {
                Width = 43,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = doneLabel
            };
            
            doneStackPanel.Children.Add(doneText);
            doneStackPanel.Children.Add(doneViewbox);
            
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

            // 다시 실행 버튼
            var redoButton = CreateActionButton("redo.png", LocalizationManager.Get("RedoLbl"), $"{LocalizationManager.Get("RedoLbl")} (Ctrl+Y)");
            redoButton.Click += (s, e) => RedoLastAction();
            
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
            
            // 노트저장 버튼
            var saveNoteButton = CreateActionButton("my_note.png", LocalizationManager.Get("NoteSave"), LocalizationManager.Get("SaveNoteTooltip"));
            saveNoteButton.Click += (s, e) => SaveToNote();

            // 복사 버튼
            var copyButton = CreateActionButton("copy_selected.png", ResLoc.GetString("CopySelected"), $"{GetLocalizedTooltip("CopyToClipboard")} (Ctrl+C)");
            copyButton.Click += (s, e) => CopyToClipboard();
            
            // 저장 버튼
            var saveButton = CreateActionButton("save_selected.png", ResLoc.GetString("Save"), $"{GetLocalizedTooltip("Save")} (Ctrl+S)");
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
            toolbarPanel.Children.Add(redoButton);
            toolbarPanel.Children.Add(resetButton);
            toolbarPanel.Children.Add(separator3);
            toolbarPanel.Children.Add(saveNoteButton);
            toolbarPanel.Children.Add(copyButton);
            toolbarPanel.Children.Add(saveButton);
            
            // 툴바 위치 설정 (초기화)
            UpdateToolbarPosition();

            // [추가] 드로잉 영역 클리핑 설정
            if (_drawingCanvas != null)
                _drawingCanvas.Clip = new RectangleGeometry(currentSelectionRect);

            // 도구 옵션 컨트롤 초기화
            _toolOptionsControl = new CatchCapture.Controls.ToolOptionsControl();
            _toolOptionsControl.Initialize(_editorManager);
            _toolOptionsControl.Visibility = Visibility.Collapsed;
            canvas.Children.Add(_toolOptionsControl);
            Panel.SetZIndex(_toolOptionsControl, 3000);

            // 펜을 기본 도구로 선택 (첫 실행 시에만, 팔레트는 열지 않음)
            if (string.IsNullOrEmpty(currentTool))
            {
                EnableDrawingMode("펜");
                SetActiveToolButton(penButton);
            }
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

        private string GetLocalizedTooltip(string key)
        {
            string tooltipKey = key + "Tooltip";
            string tt = ResLoc.GetString(tooltipKey);
            if (tt == tooltipKey) // Not found
            {
                return ResLoc.GetString(key);
            }
            return tt;
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
        
        private void HideColorPalette()
        {
            if (_toolOptionsControl != null)
            {
                _toolOptionsControl.Visibility = Visibility.Collapsed;
            }
        }

        private void OnElementAdded(UIElement element)
        {
            // [제거] 개별 요소 Clip 대신 _drawingCanvas Clip 사용
            redoStack.Clear();
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

        private void EnableDrawingMode(string tool = "펜")
        {
            currentTool = tool;
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
                UpdateObjectResizeHandles(InteractiveEditor.GetElementBounds(selectedObject));
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
            if (_editorManager != null) _editorManager.SelectedObject = element;

            // 선택 강조 효과 (점선 테두리)
            UpdateObjectSelectionUI();
            CreateObjectResizeHandles();

            // [추가] 선택된 객체의 설정을 팔레트에 반영
            SyncSelectedObjectToEditor();
        }

        private void SyncSelectedObjectToEditor()
        {
            if (selectedObject == null || _editorManager == null) return;

            if (selectedObject is TextBox tb)
            {
                _editorManager.TextFontSize = tb.FontSize;
                _editorManager.TextFontFamily = tb.FontFamily.Source;
                _editorManager.TextFontWeight = tb.FontWeight;
                _editorManager.TextFontStyle = tb.FontStyle;
                _editorManager.TextUnderlineEnabled = tb.TextDecorations == TextDecorations.Underline;
                // Shadow sync is harder without custom logic, but let's assume standard
            }
            else if (selectedObject is Canvas group && group.Children.Count >= 1 && group.Children[0] is Border badge)
            {
                // Numbering Badge sync
                _editorManager.NumberingBadgeSize = badge.Width;
                if (group.Children.Count >= 2 && group.Children[1] is TextBox note)
                {
                    _editorManager.NumberingTextSize = note.FontSize;
                }
            }

            // UI 갱신
            _toolOptionsControl?.LoadEditorValues();
        }

        private void DeselectObject()
        {
            if (selectedObject != null)
            {
                // [수정] 텍스트박스인 경우 SharedCanvasEditor 내부 텍스트박스로의 접근이 제한적이므로
                // 일단은 전용 선택 해제 로직 없이 일반 선택 해제만 진행
                // 추후 SharedCanvasEditor에서 선택 해제 로직을 통합 관리할 수도 있음
                
                selectedObject = null;
                if (_editorManager != null) _editorManager.SelectedObject = null;
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

                // 구글 번역 요청 시 즉시편집 오버레이 종료
                if (resultWindow.RequestGoogleTranslate)
                {
                    this.Close();
                }
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

                // 공용 유틸리티 사용하여 구글 이미지 검색 실행 (3.5초 + 더블 탭)
                CatchCapture.Utilities.GoogleSearchUtility.SearchImage(imageToSearch);
                
                // 안내 메시지 표시 (이미 창이 닫히기 전 잠깐 보여줌)
                try { 
                    new GuideWindow(ResLoc.GetString("SearchingOnGoogle"), TimeSpan.FromSeconds(3)).Show(); 
                } catch { }

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
            (_drawingCanvas ?? canvas).Children.Remove(lastElement);
            
            // Redo 스택에 저장
            redoStack.Push(lastElement);

            // [수정] InteractiveEditor를 통해 관련 부속 요소들도 함께 제거
            InteractiveEditor.RemoveInteractiveElement(_drawingCanvas ?? canvas, lastElement);

            // [추가] 번호 도구의 경우 다음 번호를 다시 계산
            _editorManager?.RecalculateNextNumber();
        }

        private void RedoLastAction()
        {
            if (redoStack.Count == 0) return;

            var element = redoStack.Pop();
            
            // 다시 추가
            (_drawingCanvas ?? canvas).Children.Add(element);
            drawnElements.Add(element);

            // 번호 도구의 경우 다음 번호 재계산
            _editorManager?.RecalculateNextNumber();
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
                    (_drawingCanvas ?? canvas).Children.Remove(element);
                    
                    // InteractiveEditor를 통해 관련 부속 요소 제거
                    InteractiveEditor.RemoveInteractiveElement(_drawingCanvas ?? canvas, element);
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
                    ScreenCaptureUtility.CopyImageToClipboard(SelectedFrozenImage);
                    
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
        // 복사 완료 스티커 표시 메서드
        private void ShowCopyCompleteSticker()
        {
            CatchCapture.Utilities.StickerWindow.Show(ResLoc.GetString("CopiedToClipboard"));
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
                UpdateObjectResizeHandles(InteractiveEditor.GetElementBounds(selectedObject));
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
                        (_drawingCanvas ?? canvas).Children.Remove(toRemove);
                        drawnElements.Remove(toRemove);
                        
                        // [추가] 부속 요소들(텍스트 버튼 등)도 함께 제거
                        InteractiveEditor.RemoveInteractiveElement(_drawingCanvas ?? canvas, toRemove);

                        // [추가] 번호 도구의 경우 다음 번호 재계산
                        _editorManager?.RecalculateNextNumber();
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
                double scale = 1.0 / Math.Max(1, _editorManager.MosaicIntensity);
                
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
                
                (_drawingCanvas ?? canvas).Children.Add(mosaicImage);
                drawnElements.Add(mosaicImage);
            }
            catch { /* 오류 무시 */ }
        }
        // CreateShapeOptionButton이 더 이상 사용되지 않음 (ToolOptionsControl로 통합)

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

        // 툴바/팔레트를 재생성하면서 현재 상태를 보존
        private void RebuildInstantEditUIPreservingState()
        {
            // 기존 툴바/팔레트 제거
            HideColorPalette();
            if (toolbarContainer != null)
            {
                try { canvas.Children.Remove(toolbarContainer); } catch { }
                toolbarContainer = null;
                toolbarPanel = null;
            }
             if (_toolOptionsControl != null)
            {
                try { canvas.Children.Remove(_toolOptionsControl); } catch { }
                _toolOptionsControl = null;
            }

            // 툴바 재생성
            ShowEditToolbar();
            
            // 현재 도구 다시 반영
            if (_toolOptionsControl != null)
            {
                _toolOptionsControl.SetMode(currentTool);
                // 모드에 따른 추가 설정 (필요시)
                switch (currentTool)
                {
                    case "선택": EnableSelectMode(); break;
                    case "텍스트": EnableTextMode(); break;
                    case "도형": EnableShapeMode(); break;
                    case "모자이크": EnableMosaicMode(); break;
                    case "지우개": EnableEraserMode(); break;
                    case "넘버링": EnableNumberingMode(); break;
                    default: EnableDrawingMode(currentTool); break;
                }

                // 버튼 하이라이트 복원
                var btn = toolbarPanel?.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == currentTool);
                if (btn != null) SetActiveToolButton(btn);

                // 옵션 팔레트 복원 (지우개/선택 제외)
                ShowPaletteAtCurrentPosition();
            }
        }
        
        private void ToggleToolPalette(string toolName, Button button)
        {
            if (currentTool == toolName)
            {
                if (_toolOptionsControl != null && _toolOptionsControl.Visibility == Visibility.Visible)
                {
                    HideColorPalette();
                }
                else
                {
                    if (toolName != "선택" && toolName != "지우개")
                        ShowToolOptions(toolName);
                }
            }
            else
            {
                currentTool = toolName;
                SetActiveToolButton(button);
                
                switch (toolName)
                {
                    case "선택": EnableSelectMode(); HideColorPalette(); return; 
                    case "펜": EnableDrawingMode(); break;
                    case "형광펜": EnableDrawingMode("형광펜"); break; 
                    case "텍스트": EnableTextMode(); break;
                    case "도형": EnableShapeMode(); break;
                    case "넘버링": EnableNumberingMode(); break;
                    case "모자이크": EnableMosaicMode(); break;
                    case "지우개": EnableEraserMode(); break;
                }
                
                ShowToolOptions(toolName);
            }
        }

        private void ShowToolOptions(string toolName)
        {
            if (_toolOptionsControl == null) return;
            
            // 레이아웃 계산을 위해 잠시 Hidden 처리
            if (_toolOptionsControl.Visibility != Visibility.Visible)
                _toolOptionsControl.Visibility = Visibility.Hidden;

            _toolOptionsControl.SetMode(toolName);
            
            // 강제 레이아웃 갱신하여 ActualWidth 확보
            _toolOptionsControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _toolOptionsControl.Arrange(new Rect(new Point(0, 0), _toolOptionsControl.DesiredSize));
            _toolOptionsControl.UpdateLayout();
            
            Point pos = CalculatePalettePosition();
            Canvas.SetLeft(_toolOptionsControl, pos.X);
            Canvas.SetTop(_toolOptionsControl, pos.Y);
            _toolOptionsControl.Visibility = Visibility.Visible;
        }

        private void ShowPaletteAtCurrentPosition()
        {
             if (currentTool != "" && currentTool != "선택")
                 ShowToolOptions(currentTool);
        }

        private (bool isVertical, double left, double top) CalculateToolbarPosition()
        {
            // currentSelectionRect는 캔버스 기준 좌표 (0,0부터 시작)
            double sLeft = currentSelectionRect.Left;
            double sTop = currentSelectionRect.Top;
            double sWidth = currentSelectionRect.Width;
            double sHeight = currentSelectionRect.Height;

            // 가로 툴바 크기
            double hWidth = 460; 
            double hHeight = 65; 
            double margin = 10;
            
            // 캔버스(가상 스크린) 크기 - currentSelectionRect는 이미 캔버스 좌표계
            double canvasWidth = vWidth;
            double canvasHeight = vHeight;

            // 하단/상단 여유 공간 계산 (캔버스 기준)
            double bottomSpace = canvasHeight - (sTop + sHeight);
            double topSpace = sTop;

            // 하단 또는 상단에 툴바를 배치할 수 있는지 확인
            bool canFitBottom = bottomSpace >= (hHeight + margin);
            bool canFitTop = topSpace >= (hHeight + margin);

            // [추가] 선택 영역이 매우 크면서 한쪽 여유가 부족하면 세로 모드 선호
            // 조건: 높이 1000px 이상 AND (하단 여유 < 50px OR 상단 여유 < 50px)
            bool isTallSelection = sHeight >= 1000 && (bottomSpace < 50 || topSpace < 50);

            // [추가 요청] 화면 오른쪽 끝에서 드래그 시 툴바가 잘리는 문제 해결
            // 오른쪽 끝에 붙어있으면 강제로 왼쪽 세로 모드 적용 (공간이 있다면)
            bool isAtRightEdge = (sLeft + sWidth) > (canvasWidth - 50);
            if (isAtRightEdge)
            {
                double vtWidth = 70;
                double vtHeight = 580;
                
                // 왼쪽에 공간이 있고, 화면 높이가 툴바보다 큰 경우에만 강제 적용
                if ((sLeft - vtWidth - 10) >= 0 && canvasHeight >= vtHeight) 
                {
                     double top = sTop + (sHeight - vtHeight) / 2;
                     if (top + vtHeight > canvasHeight - 10) top = canvasHeight - vtHeight - 10;
                     if (top < 10) top = 10;
                     
                     return (true, sLeft - vtWidth - 10, top);
                }
            }

            // 키 큰 선택 영역이면 무조건 세로 모드
            if (isTallSelection)
            {
                double vtWidth = 70;
                double vtHeight = 580; 
                
                bool canFitRight = (sLeft + sWidth + vtWidth + 15) <= canvasWidth;
                bool canFitLeft = (sLeft - vtWidth - 15) >= 0;

                double top = sTop + (sHeight - vtHeight) / 2;
                if (top + vtHeight > canvasHeight - 10) top = canvasHeight - vtHeight - 10;
                if (top < 10) top = 10;

                string position = "";
                double finalLeft = 0;
                
                if (canFitRight)
                {
                    position = "VERTICAL RIGHT (Tall Selection)";
                    finalLeft = sLeft + sWidth + 10;
                }
                else if (canFitLeft)
                {
                    position = "VERTICAL LEFT (Tall Selection)";
                    finalLeft = sLeft - vtWidth - 10;
                }
                else
                {
                    position = "VERTICAL INSIDE (Tall Selection)";
                    finalLeft = sLeft + sWidth - vtWidth - 15;
                    if (finalLeft < sLeft) finalLeft = sLeft + 5;
                }

                try
                {
                    string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture_Toolbar_Debug.txt");
                    System.IO.File.AppendAllText(logPath, $"  Mode: {position} at ({finalLeft:F1}, {top:F1})\n\n");
                }
                catch { }
                
                return (true, finalLeft, top);
            }

            // 우선순위: 하단 > 상단 > 세로(우측)
            if (canFitBottom)
            {
                // 하단 배치 - 텍스트박스 가림 방지를 위해 마진 확대
                double left = sLeft;
                if (left + hWidth > canvasWidth - 10) left = canvasWidth - hWidth - 10;
                if (left < 10) left = 10;
                
                double finalTop = sTop + sHeight + 12; // 가로형 팔레트로 변경되어 다시 12px로 조정
                
                try
                {
                    string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture_Toolbar_Debug.txt");
                    System.IO.File.AppendAllText(logPath, $"  Mode: HORIZONTAL BOTTOM at ({left:F1}, {finalTop:F1})\n\n");
                }
                catch { }
                
                return (false, left, finalTop);
            }
            else if (canFitTop)
            {
                // 상단 배치
                double left = sLeft;
                if (left + hWidth > canvasWidth - 10) left = canvasWidth - hWidth - 10;
                if (left < 10) left = 10;
                
                try
                {
                    string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture_Toolbar_Debug.txt");
                    System.IO.File.AppendAllText(logPath, $"  Mode: HORIZONTAL TOP at ({left:F1}, {sTop - hHeight - 15:F1})\n\n");
                }
                catch { }
                
                return (false, left, sTop - hHeight - 15);
            }
            else
            {
                // 세로 레이아웃 (상하 모두 부족)
                double vtWidth = 70;
                double vtHeight = 580; 
                
                bool canFitRight = (sLeft + sWidth + vtWidth + 15) <= canvasWidth;
                bool canFitLeft = (sLeft - vtWidth - 15) >= 0;

                // 세로 위치는 선택 영역 중앙 정렬 시도
                double top = sTop + (sHeight - vtHeight) / 2;
                if (top + vtHeight > canvasHeight - 10) top = canvasHeight - vtHeight - 10;
                if (top < 10) top = 10;

                string position = "";
                double finalLeft = 0;
                
                if (canFitRight)
                {
                    position = "VERTICAL RIGHT";
                    finalLeft = sLeft + sWidth + 10;
                }
                else if (canFitLeft)
                {
                    position = "VERTICAL LEFT";
                    finalLeft = sLeft - vtWidth - 10;
                }
                else
                {
                    position = "VERTICAL INSIDE";
                    finalLeft = sLeft + sWidth - vtWidth - 15;
                    if (finalLeft < sLeft) finalLeft = sLeft + 5;
                }

                try
                {
                    string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture_Toolbar_Debug.txt");
                    System.IO.File.AppendAllText(logPath, $"  Mode: {position} at ({finalLeft:F1}, {top:F1})\n\n");
                }
                catch { }
                
                return (true, finalLeft, top);
            }
        }

        private Point CalculatePalettePosition()
        {
            if (toolbarContainer == null) return new Point(0,0);
            
            double tLeft = Canvas.GetLeft(toolbarContainer);
            double tTop = Canvas.GetTop(toolbarContainer);
            double tWidth = toolbarContainer.ActualWidth;
            double tHeight = toolbarContainer.ActualHeight;
            if (double.IsNaN(tWidth) || tWidth < 10) tWidth = isVerticalToolbarLayout ? 60 : 450;
            if (double.IsNaN(tHeight)) tHeight = isVerticalToolbarLayout ? 550 : 55;
            
            // 팔레트 예상 크기 (실제 크기 반영 노력)
            double paletteHeight = 150; 
            double paletteWidth = 360;  // 기본값 증가 (300 -> 360)
            
            if (_toolOptionsControl != null && _toolOptionsControl.ActualWidth > 10)
            {
                paletteWidth = _toolOptionsControl.ActualWidth;
            }

            double margin = 10;
            
            if (isVerticalToolbarLayout)
            {
                 // 세로 레이아웃
                 double selectionLeft = currentSelectionRect.Left;
                 // 툴바가 선택 영역 오른쪽에 있으면 팔레트는 더 오른쪽
                 if (tLeft > selectionLeft)
                 {
                     double targetX = tLeft + tWidth + margin;
                     // 화면 밖으로 나가면 왼쪽으로
                     if (targetX + paletteWidth > vWidth) return new Point(tLeft - paletteWidth - margin, tTop);
                     return new Point(targetX, tTop);
                 }
                 else
                 {
                     return new Point(tLeft - paletteWidth - margin, tTop);
                 }
            }
            else
            {
                // 가로 레이아웃: 하단 여유 공간을 감지하여 팔레트 위치 결정
                // 툴바 하단부터 캔버스 끝까지의 여유 공간 계산
                double spaceBelow = vHeight - (tTop + tHeight);
                double spaceAbove = tTop - currentSelectionRect.Top;
                
                // 하단에 팔레트를 배치할 충분한 공간이 있는지 확인
                bool canFitBelow = spaceBelow >= (paletteHeight + margin);
                
                if (canFitBelow)
                {
                    // 하단에 공간이 있으면 툴바 아래에 팔레트 배치
                    return new Point(tLeft, tTop + tHeight + margin);
                }
                else
                {
                    // 하단에 공간이 없으면 툴바 위에 팔레트 배치
                    return new Point(tLeft, tTop - paletteHeight - margin);
                }
            }
        }
        
        private void UpdatePalettePosition()
        {
            if (_toolOptionsControl != null && _toolOptionsControl.Visibility == Visibility.Visible)
            {
                Point pos = CalculatePalettePosition();
                Canvas.SetLeft(_toolOptionsControl, pos.X);
                Canvas.SetTop(_toolOptionsControl, pos.Y);
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

            // 드로잉 영역 클리핑 업데이트
            if (_drawingCanvas != null)
                _drawingCanvas.Clip = new RectangleGeometry(currentSelectionRect);

            // 핸들 위치 업데이트
            UpdateResizeHandles();
            
            // 크기 텍스트 업데이트 (handled by SetRect inside selectionOverlay)

            // 툴바 위치 업데이트 (실시간)
            UpdateToolbarPosition();

            // 팔레트 위치 업데이트 (실시간)
            UpdatePalettePosition();
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
             // 드로잉 영역 클리핑 업데이트
             if (_drawingCanvas != null)
                 _drawingCanvas.Clip = new RectangleGeometry(currentSelectionRect);

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

            var (isVertical, tLeft, tTop) = CalculateToolbarPosition();

            // 상하 공간 부족 시 레이아웃 변경
            if (isVertical != isVerticalToolbarLayout)
            {
                UpdateToolbarLayout(isVertical);
            }

            Canvas.SetLeft(toolbarContainer, tLeft);
            Canvas.SetTop(toolbarContainer, tTop);
            
            // 툴바가 아직 Canvas에 없으면 추가
            if (!canvas.Children.Contains(toolbarContainer))
            {
                canvas.Children.Add(toolbarContainer);
            }
            
            // 팔레트 위치도 함께 업데이트
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
                CatchCapture.CustomMessageBox.Show("Pin Error: " + ex.Message);
            }
        }
        private void SaveToNote()
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
                    var settings = Settings.Load();
                    if (!string.IsNullOrEmpty(settings.NotePassword) && !App.IsNoteAuthenticated)
                    {
                        var lockWin = new NoteLockCheckWindow(settings.NotePassword, settings.NotePasswordHint);
                        lockWin.Owner = this;
                        if (lockWin.ShowDialog() != true)
                        {
                            return;
                        }
                    }

                    // 스니핑 창을 먼저 닫고 MainWindow에서 노트 입력창을 열도록 플래그 설정
                    RequestSaveToNote = true;
                    RequestMainWindowMinimize = true;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"노트 저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

