using CatchCapture.Utilities;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls.Primitives;
using CatchCapture.Models;
using System.Windows.Media.Effects;
using LocalizationManager = CatchCapture.Resources.LocalizationManager; // 상단 추가

namespace CatchCapture
{
    public partial class PreviewWindow : Window
    {
        // 도형 관련 변수 정리
        private BitmapSource originalImage;
        private BitmapSource currentImage;
        private int imageIndex;
        private Stack<BitmapSource> undoStack = new Stack<BitmapSource>();
        private Stack<BitmapSource> redoStack = new Stack<BitmapSource>();
        private Stack<List<CatchCapture.Models.DrawingLayer>> undoLayersStack = new Stack<List<CatchCapture.Models.DrawingLayer>>();
        private Stack<List<CatchCapture.Models.DrawingLayer>> redoLayersStack = new Stack<List<CatchCapture.Models.DrawingLayer>>();
        private Stack<BitmapSource> undoOriginalStack = new Stack<BitmapSource>();
        private Stack<BitmapSource> redoOriginalStack = new Stack<BitmapSource>();
        private Point startPoint;
        private Rectangle? selectionRectangle;
        private List<Point> drawingPoints = new List<Point>();
        private EditMode currentEditMode = EditMode.None;
        // Pen & Highlight settings
        // Fields removed as requested (using SharedCanvasEditor)
        // selectedColor, penThickness, shapeType, etc. are now managed by _editorManager
        // 라이브 스트로크 미리보기 - _editorManager에서 관리
        // 로그 파일 경로 (미사용)
        // private string logFilePath = "shape_debug.log";

        public event EventHandler<ImageUpdatedEventArgs>? ImageUpdated;
        private List<CaptureImage>? allCaptures; // 클래스 멤버 변수로 추가 (20줄 부근)

        private List<UIElement> drawnElements = new List<UIElement>();
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
        private Stack<UIElement> _editorUndoStack = new Stack<UIElement>();
        private CatchCapture.Controls.ToolOptionsControl _toolOptionsControl;

        private List<CatchCapture.Models.DrawingLayer> drawingLayers = new List<CatchCapture.Models.DrawingLayer>();
        private Dictionary<int, List<CatchCapture.Models.DrawingLayer>> captureDrawingLayers = new Dictionary<int, List<CatchCapture.Models.DrawingLayer>>();
        private int nextLayerId = 1;

        private string? _sourceApp;
        private string? _sourceTitle;

        public bool RequestMainWindowMinimize { get; private set; } = false;

        public PreviewWindow(BitmapSource image, int index, List<CaptureImage>? captures = null)
        {
            InitializeComponent();

            // Debug logging disabled
            // (no file writes)

            WriteLog("PreviewWindow 생성됨");

            originalImage = image;
            currentImage = image;
            imageIndex = index;
            allCaptures = captures;

            // 이미지 표시
            PreviewImage.Source = currentImage;
            PreviewImage.Width = currentImage.PixelWidth;
            PreviewImage.Height = currentImage.PixelHeight;

            // 캔버스 크기 설정
            ImageCanvas.Width = currentImage.PixelWidth;
            ImageCanvas.Height = currentImage.PixelHeight;

            // 이미지 크기에 맞게 창 크기 조정 (최대 크기 제한 적용)
            AdjustWindowSizeToFitImage();

            // Store metadata from the current capture if available
            if (allCaptures != null && index >= 0 && index < allCaptures.Count)
            {
                _sourceApp = allCaptures[index].SourceApp;
                _sourceTitle = allCaptures[index].SourceTitle;
            }

            // 이벤트 핸들러 등록
            ImageCanvas.MouseLeftButtonDown += ImageCanvas_MouseLeftButtonDown;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
            ImageCanvas.MouseLeftButtonUp += ImageCanvas_MouseLeftButtonUp;
            ImageCanvas.MouseLeave += ImageCanvas_MouseLeave;
            PreviewKeyDown += PreviewWindow_KeyDown;
            this.PreviewMouseWheel += PreviewWindow_PreviewMouseWheel;
            // 캡처 리스트 표시
            if (allCaptures != null && allCaptures.Count > 0)
            {
                LoadCaptureList();
            }

            // UI 텍스트 로컬라이즈
            UpdateUIText();

            // 이미지 정보 업데이트
            UpdateImageInfo();  // 이 줄 추가
            // 창이 로드된 후 하이라이트 모드 활성화
            this.Loaded += (s, e) =>
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        HighlightButton_Click(null, null);
                    }));
            };
            // Ensure toolbar texts finalize after template creation
            this.Loaded += (s, e) => { try { UpdateUIText(); } catch { } };
            // Live language switch
            LocalizationManager.LanguageChanged += PreviewWindow_LanguageChanged;

            _editorManager = new SharedCanvasEditor(ImageCanvas, drawnElements, _editorUndoStack);
            _editorManager.ActionOccurred += () => { SyncEditorToLayers(); UpdateUndoRedoButtons(); };
            _editorManager.ElementAdded += (element) => {
                SaveForUndo();
                if (element is FrameworkElement fe) CreateLayerForElement(fe);
            };
            _editorManager.MosaicRequired += (rect) => ApplyMosaic(rect);

            // 공용 도구 옵션 컨트롤 초기화
            _toolOptionsControl = new CatchCapture.Controls.ToolOptionsControl();
            _toolOptionsControl.Initialize(_editorManager);
        }



        private void ApplyMosaic(Rect rect)
        {
            SaveForUndo();
            int intensity = (int)(_editorManager?.MosaicIntensity ?? 15);
            currentImage = ImageEditUtility.ApplyMosaic(currentImage, new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), intensity);
            UpdatePreviewImage();
        }

        private void CreateLayerForElement(FrameworkElement element)
        {
            if (element.Tag is CatchCapture.Models.DrawingLayer) return;

            CatchCapture.Models.DrawingLayer? layer = null;

            if (element is Polyline polyline)
            {
                layer = new CatchCapture.Models.DrawingLayer
                {
                    Type = polyline.Opacity < 1.0 ? DrawingLayerType.Highlight : DrawingLayerType.Pen,
                    Points = polyline.Points.ToArray(),
                    Color = ((SolidColorBrush)polyline.Stroke).Color,
                    Thickness = polyline.StrokeThickness,
                    IsInteractive = true,
                    LayerId = nextLayerId++
                };
            }
            else if (element is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer existingLayer)
            {
                // Already tagged (e.g. Shape created via ShapeDrawingHelper might already have a layer in some flows)
                return;
            }
            else if (element is Shape shape)
            {
                // 도형 레이어 생성
                layer = new CatchCapture.Models.DrawingLayer
                {
                    Type = DrawingLayerType.Shape,
                    ShapeType = _editorManager.CurrentShapeType, // 현재 설정된 타입 사용
                    StartPoint = startPoint,
                    EndPoint = new Point(Canvas.GetLeft(shape) + shape.Width, Canvas.GetTop(shape) + shape.Height), // 대략적 계산
                    Color = _editorManager.SelectedColor,
                    Thickness = _editorManager.ShapeBorderThickness,
                    IsFilled = _editorManager.ShapeIsFilled,
                    FillOpacity = _editorManager.ShapeFillOpacity,
                    IsInteractive = true,
                    LayerId = nextLayerId++
                };
            }
            else if (element is Canvas groupCanvas)
            {
                // 넘버링 등은 일단 Shape나 Custom으로 처리하거나 별도 타입 추가 가능
                // 여기서는 일단 Shape 기본값으로 저장 (추후 확장)
            }

            if (layer != null)
            {
                element.Tag = layer;
                drawingLayers.Add(layer);
            }
        }

        private void SyncEditorToLayers()
        {
            // 수정된 요소들의 상태를 레이어에 반영
            foreach (var element in drawnElements)
            {
                if (element is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    var bounds = InteractiveEditor.GetElementBounds(element);
                    if (layer.Type == DrawingLayerType.Shape)
                    {
                        layer.StartPoint = new Point(bounds.Left, bounds.Top);
                        layer.EndPoint = new Point(bounds.Right, bounds.Bottom);
                    }
                    else if (layer.Type == DrawingLayerType.Pen || layer.Type == DrawingLayerType.Highlight)
                    {
                        if (element is Polyline p) layer.Points = p.Points.ToArray();
                    }
                }
            }
        }

        // 이미지 크기에 맞게 창 크기를 조정하고 필요시 줌 조정
        private void AdjustWindowSizeToFitImage()
        {
            if (currentImage == null) return;

            // 이미지 원래 크기
            double imageWidth = currentImage.PixelWidth;
            double imageHeight = currentImage.PixelHeight;

            // 화면 작업 영역 크기
            double workAreaWidth = SystemParameters.WorkArea.Width;
            double workAreaHeight = SystemParameters.WorkArea.Height;

            // 최대 창 크기 제한 (화면의 90%)
            double maxWindowWidth = workAreaWidth * 0.95;
            double maxWindowHeight = workAreaHeight * 0.95;

            // 최소 창 크기 설정
            double minWindowWidth = 1450;
            double minWindowHeight = 800;

            // UI 요소 크기 예상치
            double toolbarHeight = 80; // 상단 툴바
            double bottomPanelHeight = 30; // 하단 상태바 등
            double windowChromeHeight = 40; // 타이틀바
            double rightPanelWidth = 320; // 우측 캡처 리스트 패널 예상 너비
            double padding = 40; // 여백

            // 이미지를 100%로 보여줄 때 필요한 창 크기 
            // (이미지 너비 + 우측 패널 + 여백)
            double requiredContentWidth = imageWidth + rightPanelWidth + padding;
            double requiredContentHeight = imageHeight + toolbarHeight + bottomPanelHeight + windowChromeHeight;

            // 창 크기 결정 (최소/최대 범위 내)
            double targetWindowWidth = Math.Min(maxWindowWidth, Math.Max(minWindowWidth, requiredContentWidth));
            double targetWindowHeight = Math.Min(maxWindowHeight, Math.Max(minWindowHeight, requiredContentHeight));

            // 창 크기 설정
            this.Width = targetWindowWidth;
            this.Height = targetWindowHeight;

            // 창 위치 설정 (중앙)
            if (this.Owner != null)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.Width) / 2;
                this.Top = this.Owner.Top + (this.Owner.Height - this.Height) / 2;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // --- 줌(Scale) 조정 ---
            // 창 크기가 결정되었으니, 이미지 표시 영역(Canvas)에 할당될 실제 공간 계산
            // 창 너비 - 우측패널 - 여백
            double availableWidth = targetWindowWidth - rightPanelWidth - padding;
            // 창 높이 - 상단툴바 - 하단패널 - 타이틀바
            double availableHeight = targetWindowHeight - toolbarHeight - bottomPanelHeight - windowChromeHeight;

            // 공간이 유효한지 체크
            if (availableWidth > 0 && availableHeight > 0)
            {
                double scaleX = availableWidth / imageWidth;
                double scaleY = availableHeight / imageHeight;
                
                // 둘 중 더 작은 비율을 선택 (이미지 전체가 들어오도록)
                double scale = Math.Min(scaleX, scaleY);
                
                // 이미지가 공간보다 크면 축소 (1.0보다 작을 때만 적용)
                // 너무 작게 축소되는 것 방지 (예: 최소 10%)
                if (scale < 1.0)
                {
                    scale = Math.Max(scale, 0.1); 
                    
                    var scaleTransform = GetImageScaleTransform();
                    if (scaleTransform != null)
                    {
                        scaleTransform.ScaleX = scale;
                        scaleTransform.ScaleY = scale;
                        UpdateImageInfo();
                    }
                }
                else
                {
                     // 공간이 충분하면 100%로 리셋
                     ResetZoom();
                }
            }
        }

        private void PreviewWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // 텍스트 박스 입력 중일 때는 ESC를 눌러도 창이 닫히지 않도록 방지
                if (e.OriginalSource is TextBox) return;

                if (currentEditMode == EditMode.Crop)
                {
                    CancelCrop();
                }
                else
                {
                    // 현재 편집 모드가 없는 경우에만 편집창 닫기 (사용자 요청)
                    this.Close();
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (currentEditMode == EditMode.Crop)
                {
                    ConfirmCrop();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+C: 현재 이미지 복사
                ScreenCaptureUtility.CopyImageToClipboard(currentImage);
                ShowToastMessage(LocalizationManager.GetString("CopyToClipboard"));
                e.Handled = true;
            }
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+Z: 실행 취소
                if (UndoButton?.IsEnabled == true)
                {
                    UndoButton_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+Y: 다시 실행
                if (RedoButton?.IsEnabled == true)
                {
                    RedoButton_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+S: 저장
                SaveButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                // Ctrl+Shift++: 확대
                ZoomIn();
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+-: 축소
                ZoomOut();
                e.Handled = true;
            }
            else if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+0: 실제 크기로
                ResetZoom();
                e.Handled = true;
            }
        }

        private void PreviewWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl 키가 눌린 상태에서만 작동
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    ZoomIn(); // 휠 올리면 확대
                }
                else
                {
                    ZoomOut(); // 휠 내리면 축소
                }
                
                // 스크롤 이벤트가 ScrollViewer로 전파되어 스크롤되는 것을 방지
                e.Handled = true;
            }
        }

        // 확대 기능
        private void ZoomIn()
        {
            var scaleTransform = GetImageScaleTransform();
            if (scaleTransform != null)
            {
                // 10%씩 확대 (최대 500%)
                double newScale = Math.Min(scaleTransform.ScaleX * 1.1, 5.0);
                scaleTransform.ScaleX = newScale;
                scaleTransform.ScaleY = newScale;
                
                UpdateImageInfo(); // 줌 레벨 텍스트 업데이트
            }
        }

        // 축소 기능
        private void ZoomOut()
        {
            var scaleTransform = GetImageScaleTransform();
            if (scaleTransform != null)
            {
                // 10%씩 축소 (최소 10%)
                double newScale = Math.Max(scaleTransform.ScaleX / 1.1, 0.1);
                scaleTransform.ScaleX = newScale;
                scaleTransform.ScaleY = newScale;
                
                UpdateImageInfo(); // 줌 레벨 텍스트 업데이트
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 그려진 요소(Visible)가 하나라도 있다면 닫을 때 합성하여 부모 리스트 업데이트 (자동 저장)
            if (drawnElements != null && drawnElements.Any(el => el.Visibility == Visibility.Visible))
            {
                var finalImage = GetCombinedImage();
                if (finalImage != null)
                {
                    ImageUpdated?.Invoke(this, new ImageUpdatedEventArgs(imageIndex, finalImage));
                }
            }
            
            // ★ 메모리 최적화: 창 닫을 때 리소스 정리
            CleanupResources();
            
            base.OnClosing(e);
        }

        /// <summary>
        /// ★ 메모리 최적화: 창 닫을 때 모든 리소스 정리
        /// </summary>
        private void CleanupResources()
        {
            // Undo/Redo 스택 정리
            undoStack.Clear();
            redoStack.Clear();
            undoOriginalStack.Clear();
            redoOriginalStack.Clear();
            undoLayersStack.Clear();
            redoLayersStack.Clear();
            
            // 그려진 요소 정리
            drawnElements.Clear();
            drawingLayers.Clear();
            
            // 이벤트 핸들러 해제
            LocalizationManager.LanguageChanged -= PreviewWindow_LanguageChanged;
            
            // 명시적 GC 요청 (대용량 이미지 메모리 회수)
            GC.Collect(0, GCCollectionMode.Optimized);
        }

        // 실제 크기로 리셋
        private void ResetZoom()
        {
            var scaleTransform = GetImageScaleTransform();
            if (scaleTransform != null)
            {
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
                
                UpdateImageInfo(); // 줌 레벨 텍스트 업데이트
            }
        }

        // 이미지의 스케일 트랜스폼 가져오기
        private ScaleTransform? GetImageScaleTransform()
        {
            // XAML에 정의된 CanvasScaleTransform 반환 (LayoutTransform 사용)
            return CanvasScaleTransform;
        }
        
        private void CancelCurrentEditMode()
        {
            WriteLog($"CancelCurrentEditMode 호출: 이전 모드={currentEditMode}");

            // 현재 편집 모드 취소
            currentEditMode = EditMode.None;

            // 선택 영역 제거
            if (selectionRectangle != null && ImageCanvas != null)
            {
                WriteLog("선택 영역 제거");
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
            }

            CleanupCropUI(); // Crop UI 정리

            // 도구 패널 숨김
            if (EditToolPanel != null)
            {
                WriteLog("도구 패널 숨김");
                EditToolPanel.Visibility = Visibility.Collapsed;
            }

            // 마우스 커서 복원
            if (ImageCanvas != null)
            {
                WriteLog("마우스 커서 복원: Arrow");
                ImageCanvas.Cursor = Cursors.Arrow;
            }

            // 그리기 포인트 초기화 - _editorManager에서 관리됨
            drawingPoints.Clear();
            WriteLog("그리기 상태 초기화");

            // 활성 강조 해제
            SetActiveToolButton(null);
        }

        #region 도구 버튼 이벤트

        /// <summary>
        /// drawnElements를 직접 렌더링하여 합성 이미지 생성 (SnippingWindow 방식)
        /// DPI 스케일링을 고려하여 정확하게 렌더링
        /// </summary>
        private BitmapSource GetCombinedImage()
        {
            try
            {
                // DPI 정보 획득
                double dpiX = currentImage.DpiX;
                double dpiY = currentImage.DpiY;
                int pixelWidth = currentImage.PixelWidth;
                int pixelHeight = currentImage.PixelHeight;

                // 논리적 크기 계산 (WPF 좌표계)
                double logicWidth = currentImage.Width;
                double logicHeight = currentImage.Height;

                if (logicWidth <= 0 || logicHeight <= 0)
                {
                    // Fallback if Width/Height metadata is missing
                    logicWidth = pixelWidth * 96.0 / dpiX;
                    logicHeight = pixelHeight * 96.0 / dpiY;
                }

                var renderBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();

                using (var dc = drawingVisual.RenderOpen())
                {
                    // 1. 배경 이미지 (논리 좌표 크기로 그리기)
                    dc.DrawImage(currentImage, new Rect(0, 0, logicWidth, logicHeight));

                    // 2. 그려진 요소들 렌더링
                    foreach (var element in drawnElements)
                    {
                        if (element.Visibility != Visibility.Visible) continue;

                        try
                        {
                            if (element is Polyline polyline)
                            {
                                var pen = new Pen(polyline.Stroke, polyline.StrokeThickness)
                                {
                                    StartLineCap = PenLineCap.Round,
                                    EndLineCap = PenLineCap.Round,
                                    LineJoin = PenLineJoin.Round
                                };
                                
                                if (polyline.Opacity < 1.0) dc.PushOpacity(polyline.Opacity);
                                
                                // Polyline Points 확인
                                if (polyline.Points != null && polyline.Points.Count > 1)
                                {
                                    for (int i = 0; i < polyline.Points.Count - 1; i++)
                                    {
                                        dc.DrawLine(pen, polyline.Points[i], polyline.Points[i + 1]);
                                    }
                                }
                                
                                if (polyline.Opacity < 1.0) dc.Pop();
                            }
                            else if (element is Line line)
                            {
                                dc.DrawLine(new Pen(line.Stroke, line.StrokeThickness), 
                                    new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
                            }
                            else if (element is Shape shape)
                            {
                                double left = Canvas.GetLeft(shape);
                                double top = Canvas.GetTop(shape);
                                if (double.IsNaN(left)) left = 0;
                                if (double.IsNaN(top)) top = 0;

                                dc.PushTransform(new TranslateTransform(left, top));
                                
                                if (shape is Rectangle rect)
                                {
                                    double w = double.IsNaN(rect.Width) ? rect.ActualWidth : rect.Width;
                                    double h = double.IsNaN(rect.Height) ? rect.ActualHeight : rect.Height;
                                    // 안전장치
                                    if (w <= 0 || double.IsNaN(w)) w = 0;
                                    if (h <= 0 || double.IsNaN(h)) h = 0;

                                    dc.DrawRectangle(rect.Fill, new Pen(rect.Stroke, rect.StrokeThickness), new Rect(0, 0, w, h));
                                }
                                else if (shape is Ellipse ellipse)
                                {
                                    double w = double.IsNaN(ellipse.Width) ? ellipse.ActualWidth : ellipse.Width;
                                    double h = double.IsNaN(ellipse.Height) ? ellipse.ActualHeight : ellipse.Height;
                                    if (w <= 0 || double.IsNaN(w)) w = 0;
                                    if (h <= 0 || double.IsNaN(h)) h = 0;

                                    dc.DrawEllipse(ellipse.Fill, new Pen(ellipse.Stroke, ellipse.StrokeThickness), 
                                        new Point(w/2, h/2), w/2, h/2);
                                }
                                dc.Pop();
                            }
                            else if (element is TextBox textBox)
                            {
                                if (string.IsNullOrWhiteSpace(textBox.Text)) continue;

                                double left = Canvas.GetLeft(textBox);
                                double top = Canvas.GetTop(textBox);
                                if (double.IsNaN(left)) left = 0;
                                if (double.IsNaN(top)) top = 0;

                                var formattedText = new FormattedText(
                                    textBox.Text,
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                                    textBox.FontSize,
                                    textBox.Foreground,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                                dc.DrawText(formattedText, new Point(left + textBox.Padding.Left, top + textBox.Padding.Top));
                            }
                            else if (element is Canvas groupCanvas)
                            {
                                double gLeft = Canvas.GetLeft(groupCanvas);
                                double gTop = Canvas.GetTop(groupCanvas);
                                if (double.IsNaN(gLeft)) gLeft = 0;
                                if (double.IsNaN(gTop)) gTop = 0;
                                
                                dc.PushTransform(new TranslateTransform(gLeft, gTop));
                                
                                foreach(var child in groupCanvas.Children)
                                {
                                    if (child is Border border)
                                    {
                                        double bLeft = Canvas.GetLeft(border);
                                        double bTop = Canvas.GetTop(border);
                                        if(double.IsNaN(bLeft)) bLeft = 0;
                                        if(double.IsNaN(bTop)) bTop = 0;

                                        double bw = double.IsNaN(border.Width) ? border.ActualWidth : border.Width;
                                        double bh = double.IsNaN(border.Height) ? border.ActualHeight : border.Height;
                                        
                                        var ellipseGeo = new EllipseGeometry(new Point(bLeft + bw/2, bTop + bh/2), bw/2, bh/2);
                                        dc.DrawGeometry(border.Background, null, ellipseGeo);
                                        
                                        if (border.Child is TextBlock tb)
                                        {
                                            var ft = new FormattedText(
                                                tb.Text,
                                                System.Globalization.CultureInfo.CurrentCulture,
                                                FlowDirection.LeftToRight,
                                                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                                                tb.FontSize,
                                                tb.Foreground,
                                                VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                            dc.DrawText(ft, new Point(bLeft + (bw - ft.Width)/2, bTop + (bh - ft.Height)/2));
                                        }
                                    }
                                    else if (child is TextBox tb)
                                    {
                                        if (string.IsNullOrWhiteSpace(tb.Text)) continue;
                                        double tLeft = Canvas.GetLeft(tb);
                                        double tTop = Canvas.GetTop(tb);
                                        if (double.IsNaN(tLeft)) tLeft = 0;
                                        if (double.IsNaN(tTop)) tTop = 0;
                                        
                                        var ft = new FormattedText(
                                            tb.Text,
                                            System.Globalization.CultureInfo.CurrentCulture,
                                            FlowDirection.LeftToRight,
                                            new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                                            tb.FontSize,
                                            tb.Foreground,
                                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                        dc.DrawText(ft, new Point(tLeft + tb.Padding.Left, tTop + tb.Padding.Top));
                                    }
                                }
                                dc.Pop();
                            }
                        }
                        catch (Exception innerEx)
                        {
                            // 특정 요소 렌더링 실패해도 계속 진행
                             System.Diagnostics.Debug.WriteLine($"Error rendering element {element}: {innerEx.Message}");
                        }
                    }
                }
                renderBitmap.Render(drawingVisual);
                return renderBitmap;
            }
            catch (Exception ex)
            {
                // 치명적 오류 시 메시지 표시 및 원본 반환
                 System.Diagnostics.Debug.WriteLine($"이미지 합성 중 오류: {ex.Message}");
                return currentImage; 
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var finalImage = GetCombinedImage();
            ScreenCaptureUtility.CopyImageToClipboard(finalImage);
            ShowToastMessage(LocalizationManager.GetString("CopyToClipboard"));
        }

        private async void OcrButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentImage == null) return;

            try
            {
                // 로딩 표시 (커서 변경)
                this.Cursor = Cursors.Wait;

                // OCR 실행 (튜플 반환)
                var result = await CatchCapture.Utilities.OcrUtility.ExtractTextFromImageAsync(currentImage);

                // 커서 복원
                this.Cursor = Cursors.Arrow;

                if (string.IsNullOrWhiteSpace(result.Text))
                {
                    CustomMessageBox.Show(LocalizationManager.GetString("NoExtractedText"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 결과창 표시 (경고 여부 전달)
                var resultWindow = new OcrResultWindow(result.Text, result.ShowWarning);
                resultWindow.Owner = this;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                CustomMessageBox.Show($"OCR 처리 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // 토스트 메시지 표시 메서드
        private void ShowToastMessage(string message)
        {
            if (ToastBorder == null || ToastText == null) return;

            ToastText.Text = message;
            
            // 애니메이션으로 표시
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
            fadeOut.BeginTime = TimeSpan.FromSeconds(2.0); // 2초 후 사라짐

            var storyboard = new System.Windows.Media.Animation.Storyboard();
            storyboard.Children.Add(fadeIn);
            storyboard.Children.Add(fadeOut);
            
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, ToastBorder);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            System.Windows.Media.Animation.Storyboard.SetTarget(fadeOut, ToastBorder);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

            storyboard.Begin();
        }
        private void BakeDrawnElements()
        {
            // 변경 전 상태 저장
            SaveForUndo();
            
            // 모든 레이어를 강제로 Interactive = false로 변경하여 이미지에 구워지도록 함
            foreach (var layer in drawingLayers)
            {
                layer.IsInteractive = false;
            }
            
            // 화면의 편집 요소 제거
            // 지우개 커서 등 일시적인 요소도 포함될 수 있으므로 Children 전체를 비우는 것이 깔끔함
            // 단, 다른 로직에 영향이 없도록 조심 (현재 구조상 안전해 보임)
            ImageCanvas.Children.Clear();
            drawnElements.Clear();
            DeselectObject();

            // 전체 레이어 재렌더링
            currentImage = CatchCapture.Utilities.LayerRenderer.RenderLayers(originalImage, drawingLayers);
            UpdatePreviewImage();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var finalImage = GetCombinedImage();
            // 자동 파일 이름 생성
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");

            // 설정 불러오기
            var settings = CatchCapture.Models.Settings.Load();
            bool isPng = settings.FileSaveFormat.Equals("PNG", StringComparison.OrdinalIgnoreCase);
            
            // 확장자 설정
            string ext = isPng ? ".png" : ".jpg";
            string defaultFileName = $"{LocalizationManager.GetString("AreaCapture")} {timestamp}{ext}";

            // 필터 순서 변경 (설정된 포맷을 무조건 첫 번째로 배치)
            string filter = isPng 
                ? "PNG|*.png|JPEG|*.jpg|*.*|*.*" 
                : "JPEG|*.jpg|PNG|*.png|*.*|*.*";

            // 저장 대화 상자 표시
            var dialog = new SaveFileDialog
            {
                Title = LocalizationManager.GetString("ImageSaveTitle"),
                Filter = filter,
                DefaultExt = ext,
                FileName = defaultFileName,
                FilterIndex = 1 // 항상 첫 번째 항목(설정된 포맷)이 선택됨
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ScreenCaptureUtility.SaveImageToFile(finalImage, dialog.FileName);
                    CustomMessageBox.Show(LocalizationManager.GetString("ImageSaved"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"{LocalizationManager.GetString("Error")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentImage == null)
                {
                    ShowToastMessage(LocalizationManager.GetString("NoImageToPrint"));
                    return;
                }

                // 인쇄 미리보기 창 열기
                var printPreviewWindow = new PrintPreviewWindow(currentImage);
                printPreviewWindow.Owner = this;
                printPreviewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"{LocalizationManager.GetString("PrintPreviewError")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveNoteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var finalImage = GetCombinedImage();
                if (finalImage != null)
                {
                    var settings = CatchCapture.Models.Settings.Load();
                    if (!string.IsNullOrEmpty(settings.NotePassword) && !App.IsNoteAuthenticated)
                    {
                        var lockWin = new NoteLockCheckWindow(settings.NotePassword, settings.NotePasswordHint);
                        lockWin.Owner = this;
                        if (lockWin.ShowDialog() != true)
                        {
                            return;
                        }
                    }

                    var noteWin = new NoteInputWindow(finalImage, _sourceApp, _sourceTitle);
                    noteWin.Owner = this;
                    if (noteWin.ShowDialog() == true)
                    {
                        RequestMainWindowMinimize = true;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"노트 저장 중 오류가 발생했습니다: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RecaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // MainWindow 찾기
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                
                if (mainWindow != null)
                {
                    if (mainWindow.simpleModeWindow != null && mainWindow.simpleModeWindow.IsVisible)
                    {
                        mainWindow._wasSimpleModeVisibleBeforeRecapture = true;
                        mainWindow.simpleModeWindow._suppressActivatedExpand = true;
                        mainWindow.simpleModeWindow.Hide();
                    }
                    if (mainWindow.trayModeWindow != null && mainWindow.trayModeWindow.IsVisible)
                    {
                        mainWindow.trayModeWindow.Hide();
                    }

                    // 편집창 닫기
                    this.Close();
                    
                    // UI가 완전히 사라질 시간을 충분히 줌 (100ms → 300ms)
                    await System.Threading.Tasks.Task.Delay(150);
                    
                    // 영역 캡처 시작
                    mainWindow.StartAreaCapture();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"{LocalizationManager.GetString("RecaptureError")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                // 현재 상태를 Redo 스택에 저장
                redoStack.Push(currentImage);
                redoOriginalStack.Push(originalImage);
                redoLayersStack.Push(drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    LayerId = layer.LayerId,
                    Type = layer.Type,
                    Points = layer.Points?.ToArray(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased,
                    IsInteractive = layer.IsInteractive,
                    StartPoint = layer.StartPoint,
                    EndPoint = layer.EndPoint,
                    ShapeType = layer.ShapeType,
                    IsFilled = layer.IsFilled,
                    FillOpacity = layer.FillOpacity,
                    Text = layer.Text,
                    TextPosition = layer.TextPosition,
                    FontSize = layer.FontSize,
                    FontWeight = layer.FontWeight,
                    FontStyle = layer.FontStyle,
                    FontFamily = layer.FontFamily,
                    HasShadow = layer.HasShadow,
                    HasUnderline = layer.HasUnderline
                }).ToList());
                
                // Undo 스택에서 이전 상태 복원
                currentImage = undoStack.Pop();
                originalImage = undoOriginalStack.Pop();
                drawingLayers = undoLayersStack.Pop();
                
                SyncDrawnElementsFromLayers(); // ← 인터랙티브 요소 동기화
                _editorManager?.RecalculateNextNumber(); // 넘버링 번호 동기화
                UpdateUndoRedoButtons();
                UpdatePreviewImage();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count > 0)
            {
                // 현재 상태를 Undo 스택에 저장
                undoStack.Push(currentImage);
                if (undoOriginalStack.Count > 0) 
                     undoOriginalStack.Push(originalImage);
                else 
                     // 만약 스택이 비어있으면 현재 originalImage가 계속 유효했음. 
                     // 하지만 undoOriginalStack과 짝을 맞추기 위해, Redo 시 복원할 Undo 스택에는 
                     // Redo 전의 state(현재 state)를 넣어야 함. 
                     // 여기 로직은 약간 복잡하므로 단순화:
                     undoOriginalStack.Push(originalImage);

                undoLayersStack.Push(drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    LayerId = layer.LayerId,
                    Type = layer.Type,
                    Points = layer.Points?.ToArray(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased,
                    IsInteractive = layer.IsInteractive,
                    StartPoint = layer.StartPoint,
                    EndPoint = layer.EndPoint,
                    ShapeType = layer.ShapeType,
                    IsFilled = layer.IsFilled,
                    FillOpacity = layer.FillOpacity,
                    Text = layer.Text,
                    TextPosition = layer.TextPosition,
                    FontSize = layer.FontSize,
                    FontWeight = layer.FontWeight,
                    FontStyle = layer.FontStyle,
                    FontFamily = layer.FontFamily,
                    HasShadow = layer.HasShadow,
                    HasUnderline = layer.HasUnderline
                }).ToList());
                
                // Redo 스택에서 복원
                currentImage = redoStack.Pop();
                if (redoOriginalStack.Count > 0)
                    originalImage = redoOriginalStack.Pop();
                drawingLayers = redoLayersStack.Pop();
                
                SyncDrawnElementsFromLayers();
                _editorManager?.RecalculateNextNumber(); // 넘버링 번호 동기화
                UpdateUndoRedoButtons();
                UpdatePreviewImage();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show(LocalizationManager.GetString("ConfirmReset"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // 현재 상태를 Undo 스택에 저장
                undoStack.Push(currentImage);
                undoOriginalStack.Push(originalImage);
                
                var layersCopy = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    LayerId = layer.LayerId,
                    Type = layer.Type,
                    Points = layer.Points?.ToArray(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased,
                    IsInteractive = layer.IsInteractive,
                    StartPoint = layer.StartPoint,
                    EndPoint = layer.EndPoint,
                    ShapeType = layer.ShapeType,
                    IsFilled = layer.IsFilled,
                    FillOpacity = layer.FillOpacity,
                    Text = layer.Text,
                    TextPosition = layer.TextPosition,
                    FontSize = layer.FontSize,
                    FontWeight = layer.FontWeight,
                    FontStyle = layer.FontStyle,
                    FontFamily = layer.FontFamily,
                    HasShadow = layer.HasShadow,
                    HasUnderline = layer.HasUnderline
                }).ToList();
                undoLayersStack.Push(layersCopy);
                
                // Redo 스택 초기화 (새로운 작업이므로)
                redoStack.Clear();
                redoOriginalStack.Clear();
                redoLayersStack.Clear();
                
                // 원본으로 리셋
                currentImage = originalImage;
                drawingLayers.Clear(); // 레이어 초기화
                _editorManager?.ResetNumbering(); // 넘버링 번호 초기화
                SyncDrawnElementsFromLayers();
                
                UpdateUndoRedoButtons();
                UpdatePreviewImage();
            }
        }

        private void CropButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Crop;
            ImageCanvas.Cursor = Cursors.Arrow;
            SetActiveToolButton(CropButton);
            InitializeCropMode();
        }

        private void RotateButton_Click(object sender, RoutedEventArgs e)
        {
            SaveForUndo();
            currentImage = ImageEditUtility.RotateImage(currentImage);
            UpdatePreviewImage();
        }

        private void FlipHorizontalButton_Click(object sender, RoutedEventArgs e)
        {
            SaveForUndo();
            currentImage = ImageEditUtility.FlipImage(currentImage, true);
            UpdatePreviewImage();
        }

        private void FlipVerticalButton_Click(object sender, RoutedEventArgs e)
        {
            SaveForUndo();
            currentImage = ImageEditUtility.FlipImage(currentImage, false);
            UpdatePreviewImage();
        }

        private void ShapeButton_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("ShapeButton_Click 호출 - 기본 도형 그리기 모드");

            // 현재 편집 모드 취소
            CancelCurrentEditMode();

            // 기본 설정으로 도형 그리기 모드 설정
            currentEditMode = EditMode.Shape;
            ImageCanvas.Cursor = Cursors.Cross;

            // 기본값 설정 (사각형, 검정색, 윤곽선)
            // 기본값 설정 (사각형, 검정색, 윤곽선) - EditorManager 사용 
            // _editorManager.CurrentShapeType = ShapeType.Rectangle;
            // _editorManager.SelectedColor = Colors.Black;
            // _editorManager.ShapeBorderThickness = 2;
            // _editorManager.ShapeIsFilled = false;

            WriteLog("기본 도형 그리기 모드 설정: 사각형, 검정색, 윤곽선");
            SetActiveToolButton(ShapeButton);
        }

        private void ShapeOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("ShapeOptionsButton_Click 호출 - 옵션 팝업 표시");

            if (currentEditMode != EditMode.Shape)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Shape;
                SetActiveToolButton(ShapeButton);
            }

            // 팝업이 이미 열려있으면 닫기
            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == ShapeToolGrid)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }

            // 도형 옵션 팝업 표시
            ShowShapeOptionsPopup();
            WriteLog("도형 옵션 팝업 표시됨");
        }

        private void HighlightButton_Click(object? sender, RoutedEventArgs? e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Highlight;
            ImageCanvas.Cursor = Cursors.Pen;

            // 기본값 설정 (노란색, 중간 투명도, 8px)
            // 기본값 설정 (노란색, 중간 투명도, 8px) - EditorManager 사용
            // selectedColor = Colors.Yellow;
            // highlightThickness = 8;
            
            // 바로 그리기 시작 (팝업 표시 안 함)
            SetActiveToolButton(HighlightToolButton);
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Pen;
            ImageCanvas.Cursor = Cursors.Pen;

            // 기본값 설정 (검정, 3px)
            // 기본값 설정 (검정, 3px) - EditorManager 사용
            // selectedColor = Colors.Black;
            // penThickness = 3;
            
            // 바로 그리기 시작 (팝업 표시 안 함)
            SetActiveToolButton(PenToolButton);  // ← 이 줄 추가
        }


        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Text;
            ImageCanvas.Cursor = Cursors.IBeam;

            SetActiveToolButton(TextToolButton);
        }

        private void MosaicButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Mosaic;
            ImageCanvas.Cursor = Cursors.Cross;

            SetActiveToolButton(MosaicToolButton);
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Eraser;
            ImageCanvas.Cursor = Cursors.Arrow;  // 또는 Cursors.Cross (십자 모양)

            SetActiveToolButton(EraserToolButton);
        }

        private void CloseOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            // 옵션 패널 닫기
            EditToolPanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
            UpdateImageInfo();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
            UpdateImageInfo();
        }

        private void ZoomResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
            UpdateImageInfo();
        }

        private bool isPanelVisible = true;

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (CaptureListBorder == null || ToggleButton == null) return;

            if (isPanelVisible)
            {
                // 숨기기
                CaptureListBorder.Visibility = Visibility.Collapsed;
                // 화살표를 > 방향으로 (닫힘 - 열기 유도)
                ToggleButton.RenderTransform = new RotateTransform(180);
                ToggleButton.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            else
            {
                // 보이기
                CaptureListBorder.Visibility = Visibility.Visible;
                // 화살표를 < 방향으로 (열림 - 닫기 유도)
                ToggleButton.RenderTransform = new RotateTransform(0);
            }
            
            isPanelVisible = !isPanelVisible;
        }

        private void UpdateImageInfo()
        {
            if (ImageInfoText != null && currentImage != null)
            {
                ImageInfoText.Text = $"{LocalizationManager.GetString("ImageSizePrefix")}{currentImage.PixelWidth} x {currentImage.PixelHeight}";
            }

            if (ZoomLevelText != null)
            {
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    int zoomPercent = (int)(scaleTransform.ScaleX * 100);
                    ZoomLevelText.Text = $"{zoomPercent} %";
                }
            }
        }

        #region 유틸리티 메서드

        // ★ 메모리 최적화: Undo 스택 최대 개수 제한
        private const int MAX_UNDO_STACK_SIZE = 10;

        private void SaveForUndo()
        {
            undoStack.Push(currentImage);
            redoStack.Clear();

            undoOriginalStack.Push(originalImage);
            redoOriginalStack.Clear();
            
            // ★ 메모리 최적화: 스택 크기 제한
            TrimUndoStacks();
            
            // 레이어 상태도 저장 (깊은 복사)
            var layersCopy = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
            {
                LayerId = layer.LayerId,
                Type = layer.Type,
                Points = layer.Points?.ToArray(),
                Color = layer.Color,
                Thickness = layer.Thickness,
                IsErased = layer.IsErased,
                IsInteractive = layer.IsInteractive,
                StartPoint = layer.StartPoint,
                EndPoint = layer.EndPoint,
                ShapeType = layer.ShapeType,
                IsFilled = layer.IsFilled,
                FillOpacity = layer.FillOpacity,
                Text = layer.Text,
                TextPosition = layer.TextPosition,
                FontSize = layer.FontSize,
                FontWeight = layer.FontWeight,
                FontStyle = layer.FontStyle,
                FontFamily = layer.FontFamily,
                HasShadow = layer.HasShadow,
                HasUnderline = layer.HasUnderline
            }).ToList();
            undoLayersStack.Push(layersCopy);
            redoLayersStack.Clear();
            
            UpdateUndoRedoButtons();
        }

        /// <summary>
        /// ★ 메모리 최적화: Undo/Redo 스택 크기를 제한하여 메모리 사용량 절감
        /// </summary>
        private void TrimUndoStacks()
        {
            // Undo 스택 크기 제한
            while (undoStack.Count > MAX_UNDO_STACK_SIZE)
            {
                // 가장 오래된 항목 제거 (Stack을 List로 변환 후 처리)
                var items = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 0; i < MAX_UNDO_STACK_SIZE; i++)
                {
                    undoStack.Push(items[MAX_UNDO_STACK_SIZE - 1 - i]);
                }
                break;
            }
            
            while (undoOriginalStack.Count > MAX_UNDO_STACK_SIZE)
            {
                var items = undoOriginalStack.ToArray();
                undoOriginalStack.Clear();
                for (int i = 0; i < MAX_UNDO_STACK_SIZE; i++)
                {
                    undoOriginalStack.Push(items[MAX_UNDO_STACK_SIZE - 1 - i]);
                }
                break;
            }
            
            while (undoLayersStack.Count > MAX_UNDO_STACK_SIZE)
            {
                var items = undoLayersStack.ToArray();
                undoLayersStack.Clear();
                for (int i = 0; i < MAX_UNDO_STACK_SIZE; i++)
                {
                    undoLayersStack.Push(items[MAX_UNDO_STACK_SIZE - 1 - i]);
                }
                break;
            }
            
            // 명시적 GC 힌트 (대용량 이미지 메모리 회수 유도)
            if (undoStack.Count == MAX_UNDO_STACK_SIZE)
            {
                GC.Collect(0, GCCollectionMode.Optimized);
            }
        }

        private void SyncDrawnElementsFromLayers()
        {
            if (ImageCanvas == null || drawnElements == null) return;

            // 기존 WPF 요소 제거
            foreach (var element in drawnElements)
            {
                ImageCanvas.Children.Remove(element);
            }
            drawnElements.Clear();
            DeselectObject();

            // 인터랙티브 레이어로부터 WPF 요소 재생성
            foreach (var layer in drawingLayers.Where(l => l.IsInteractive && !l.IsErased))
            {
                UIElement? element = null;

                if (layer.Type == CatchCapture.Models.DrawingLayerType.Shape && layer.ShapeType.HasValue)
                {
                    element = ShapeDrawingHelper.CreateShape(
                        layer.ShapeType.Value,
                        layer.StartPoint ?? new Point(0,0),
                        layer.EndPoint ?? new Point(0,0),
                        layer.Color,
                        layer.Thickness,
                        layer.IsFilled,
                        layer.FillOpacity
                    );
                }
                else if (layer.Type == CatchCapture.Models.DrawingLayerType.Text)
                {
                    element = RecreateTextBoxFromLayer(layer);
                }
                else if (layer.Type == CatchCapture.Models.DrawingLayerType.Pen || layer.Type == CatchCapture.Models.DrawingLayerType.Highlight)
                {
                    var polyline = new Polyline
                    {
                        Stroke = new SolidColorBrush(layer.Color),
                        StrokeThickness = layer.Thickness,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Points = new PointCollection(layer.Points ?? Enumerable.Empty<Point>())
                    };
                    if (layer.Type == CatchCapture.Models.DrawingLayerType.Highlight)
                    {
                        polyline.Opacity = 0.5;
                    }
                    element = polyline;
                }

                if (element is FrameworkElement fe)
                {
                    fe.Tag = layer;
                    drawnElements.Add(element);
                    ImageCanvas.Children.Add(element);
                }
            }
        }

        private TextBox RecreateTextBoxFromLayer(CatchCapture.Models.DrawingLayer layer)
        {
            var textBox = new TextBox
            {
                Text = layer.Text,
                FontSize = layer.FontSize,
                FontFamily = new FontFamily(layer.FontFamily),
                Foreground = new SolidColorBrush(layer.Color),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontWeight = layer.FontWeight,
                FontStyle = layer.FontStyle,
                IsReadOnly = true,
                Cursor = Cursors.Arrow,
                Tag = layer
            };

            Canvas.SetLeft(textBox, layer.TextPosition?.X ?? 0);
            Canvas.SetTop(textBox, layer.TextPosition?.Y ?? 0);
            
            // 이벤트 재연결
            textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            textBox.PreviewMouseMove += TextBox_PreviewMouseMove;
            textBox.PreviewMouseLeftButtonUp += TextBox_PreviewMouseLeftButtonUp;
            textBox.MouseDoubleClick += TextBox_MouseDoubleClick;
            textBox.GotFocus += TextBox_GotFocus;

            return textBox;
        }

        private void UpdateUndoRedoButtons()
        {
            UndoButton.IsEnabled = undoStack.Count > 0;
            RedoButton.IsEnabled = redoStack.Count > 0;
        }

        private void UpdatePreviewImage()
        {
            WriteLog("UpdatePreviewImage 시작");
            if (PreviewImage == null || ImageCanvas == null)
            {
                WriteLog("UpdatePreviewImage 중단: PreviewImage 또는 ImageCanvas가 null임");
                return;
            }


            WriteLog($"이미지 소스 업데이트: {currentImage.PixelWidth}x{currentImage.PixelHeight}");
            PreviewImage.Source = currentImage;
            PreviewImage.Width = currentImage.PixelWidth;
            PreviewImage.Height = currentImage.PixelHeight;

            ImageCanvas.Width = currentImage.PixelWidth;
            ImageCanvas.Height = currentImage.PixelHeight;

            // 이미지 업데이트 이벤트 발생
            ImageUpdated?.Invoke(this, new ImageUpdatedEventArgs(imageIndex, currentImage));
            WriteLog("UpdatePreviewImage 완료");
            // 이미지 정보 업데이트
            UpdateImageInfo();  // 이 줄 추가

            // 자식 요소 갯수
            WriteLog($"UpdatePreviewImage 완료 후 자식 요소 수: {ImageCanvas.Children.Count}");
            var childCount = 0;
            foreach (var child in ImageCanvas.Children)
            {
                childCount++;
                WriteLog($"  - {childCount}번째 자식: {child.GetType().Name}");
            }
        }

        // 로그 기록 헬퍼 메서드
        private void WriteLog(string message)
        {
            // Debug logging disabled
        }

        // 활성화된 툴 버튼 강조 표시 색상 (테마에 따라 동적으로 결정됨)
        private Brush GetActiveToolBrush()
        {
            try
            {
                // 테마의 테두리 색상을 가져옴
                var borderBrush = Application.Current.Resources["ThemeBorder"] as SolidColorBrush;
                if (borderBrush != null)
                {
                    var color = borderBrush.Color;
                    // 약간 더 진하게 혹은 투명하게 조정하여 강조 효과 생성
                    return new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
                }
            }
            catch { }
            return new SolidColorBrush(Color.FromRgb(232, 243, 255)); // 기본값 (연한 파랑)
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Select;
            SetActiveToolButton(SelectButton);
            ImageCanvas.Cursor = Cursors.Arrow;
        }

        private void NumberingButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Numbering;
            SetActiveToolButton(NumberingToolButton);
            ImageCanvas.Cursor = Cursors.Arrow;
        }

        private void NumberingOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Numbering)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Numbering;
                SetActiveToolButton(NumberingToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == NumberingToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowNumberingOptionsPopup();
        }

        private void SetActiveToolButton(UIElement? active)
        {
            DeselectObject();
            var InactiveToolBackground = Brushes.Transparent;

            // 모든 툴 버튼 목록 (TwoTierToolButton과 일반 Button 혼용)
            var toolButtons = new List<UIElement?>
            {
                RecaptureButton,
                PrintButton,
                SaveButton,
                CopyButton,
                UndoButton,
                RedoButton,
                ResetButton,
                CropButton,
                RotateButton,
                FlipHorizontalButton,
                FlipVerticalButton,
                SelectButton,
                ShapeButton,
                HighlightToolButton,
                PenToolButton,        // 펜 버튼 추가
                TextToolButton,
                MosaicToolButton,
                EraserToolButton,
                MagicWandToolButton,
                NumberingToolButton
            };

            foreach (var element in toolButtons)
            {
                if (element == null) continue;
                
                bool isActive = (active != null && element == active);
                var bgColor = isActive ? GetActiveToolBrush() : InactiveToolBackground;
                
                // Button인 경우 (CropButton, ShapeButton, ImageSearch, Share, OCR)
                if (element is Button b)
                {
                    b.Background = bgColor;
                    // 선택된 경우 약간의 투명도 조절로 강조
                    b.Opacity = isActive ? 1.0 : 0.9;
                }
                // TwoTierToolButton인 경우 (펜, 형광펜, 텍스트, 모자이크, 지우개)
                else if (element is CatchCapture.Controls.TwoTierToolButton ttb)
                {
                    // 내부 메인 버튼과 옵션 버튼 모두 색상 변경
                    ttb.SetButtonsBackground(bgColor);
                }
            }
            
            // 도형 버튼의 옵션 버튼도 색상 변경
            if (ShapeButton != null)
            {
                ShapeButton.ApplyTemplate();
                var shapeLabel = ShapeButton.Template?.FindName("ShapeLabelText", ShapeButton) as TextBlock;
                if (shapeLabel != null)
                {
                    shapeLabel.Text = LocalizationManager.GetString("ShapeLbl");
                }
            }
        }


        private void ImageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BitmapSource? src = PreviewImage?.Source as BitmapSource;
                if (src == null) return;

                // 공용 유틸리티 사용하여 구글 이미지 검색 실행 (3.5초 + 더블 탭)
                CatchCapture.Utilities.GoogleSearchUtility.SearchImage(src);
                // 안내 메시지 표시
                try { new GuideWindow(LocalizationManager.GetString("SearchingOnGoogle"), TimeSpan.FromSeconds(3)) { Owner = this }.Show(); } catch { }
            }
            catch { }
        }
        #endregion

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BitmapSource? src = PreviewImage?.Source as BitmapSource;
                if (src == null) return;

                ShareImage(src);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"공유 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ShareImage 메서드 추가 (MainWindow와 동일)
        public void ShareImage(BitmapSource image)
        {
            try
            {
                // 1. 임시 파일로 저장
                string tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture");
                if (!System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.CreateDirectory(tempFolder);
                }
                
                string tempPath = System.IO.Path.Combine(tempFolder, $"share_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                
                // PNG로 저장
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                {
                    encoder.Save(fs);
                }
                
                // 2. Windows Share 대화상자 호출 (Windows 10 이상)
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                
                // DataTransferManager 가져오기
                var dataTransferManager = Windows.ApplicationModel.DataTransfer.DataTransferManagerInterop.GetForWindow(hwnd);
                
                dataTransferManager.DataRequested += async (s, args) =>
                {
                    var request = args.Request;
                    request.Data.Properties.Title = "이미지 공유";
                    request.Data.Properties.Description = "캐치캡처 스크린샷";
                    
                    var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempPath);
                    request.Data.SetStorageItems(new[] { storageFile });
                    request.Data.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(storageFile));
                };
                
                Windows.ApplicationModel.DataTransfer.DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
            }
            catch (Exception ex)
            {
                // Windows Share가 지원되지 않는 경우 클립보드 복사로 대체
                try
                {
                    ScreenCaptureUtility.CopyImageToClipboard(image);
                    ShowToastMessage("클립보드에 복사됨");
                }
                catch
                {
                    CustomMessageBox.Show($"공유 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void PreviewWindow_LanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateUIText();
            });
        }
        private void UpdateUIText()
        {
            // Title
            this.Title = LocalizationManager.GetString("AppTitle");
            // Toolbar Buttons Texts & Tooltips
            // Recapture
            if(RecaptureLabelText != null) RecaptureLabelText.Text = LocalizationManager.GetString("Recapture");
            if(RecaptureButton != null) RecaptureButton.ToolTip = LocalizationManager.GetString("AreaCapture");
            // Print
            if(PrintLabelText != null) PrintLabelText.Text = LocalizationManager.GetString("Print");
            if(PrintButton != null) PrintButton.ToolTip = LocalizationManager.GetString("Print");
            // Save
            if(SaveLabelText != null) SaveLabelText.Text = LocalizationManager.GetString("Save");
            if(SaveButton != null) SaveButton.ToolTip = LocalizationManager.GetString("Save");
            // Copy
            if(CopyLabelText != null) CopyLabelText.Text = LocalizationManager.GetString("Copy");
            if(CopyButton != null) CopyButton.ToolTip = LocalizationManager.GetString("CopyToClipboard");
            // Undo/Redo/Reset
            if(UndoLabelText != null) UndoLabelText.Text = LocalizationManager.GetString("Undo");
            if(UndoButton != null) UndoButton.ToolTip = LocalizationManager.GetString("Undo");
            if(RedoLabelText != null) RedoLabelText.Text = LocalizationManager.GetString("Redo");
            if(RedoButton != null) RedoButton.ToolTip = LocalizationManager.GetString("Redo");
            if(ResetLabelText != null) ResetLabelText.Text = LocalizationManager.GetString("Reset");
            if(ResetButton != null) ResetButton.ToolTip = LocalizationManager.GetString("ResetToOriginal");
            // Crop/Rotate/Flip
            if(CropLabelText != null) CropLabelText.Text = LocalizationManager.GetString("Crop");
            if(CropButton != null) CropButton.ToolTip = LocalizationManager.GetString("Crop");
            if(RotateLabelText != null) RotateLabelText.Text = LocalizationManager.GetString("Rotate");
            if(RotateButton != null) RotateButton.ToolTip = LocalizationManager.GetString("Rotate");
            if(FlipHorizontalLabelText != null) FlipHorizontalLabelText.Text = LocalizationManager.GetString("FlipHorizontal");
            if(FlipHorizontalButton != null) FlipHorizontalButton.ToolTip = LocalizationManager.GetString("FlipHorizontal");
            if(FlipVerticalLabelText != null) FlipVerticalLabelText.Text = LocalizationManager.GetString("FlipVertical");
            if(FlipVerticalButton != null) FlipVerticalButton.ToolTip = LocalizationManager.GetString("FlipVertical");
            // Shape
            if (ShapeButton != null)
            {
                var shapeLabel = ShapeButton.Template?.FindName("ShapeLabelText", ShapeButton) as TextBlock;
                if (shapeLabel != null) shapeLabel.Text = LocalizationManager.GetString("Shape");
            }
            if(ShapeButton != null) ShapeButton.ToolTip = LocalizationManager.GetString("DrawShapeTooltip");
            if(ShapeOptionsButton != null) ShapeOptionsButton.ToolTip = LocalizationManager.GetString("ShapeOptions");
            // Tools
            if (PenToolButton != null)
            {
                PenToolButton.Label = LocalizationManager.GetString("Pen");
                PenToolButton.ToolTipText = LocalizationManager.GetString("Pen");
            }
            if (HighlightToolButton != null)
            {
                HighlightToolButton.Label = LocalizationManager.GetString("Highlighter");
                HighlightToolButton.ToolTipText = GetLocalizedTooltip("Highlighter");
            }
            if (TextToolButton != null)
            {
                TextToolButton.Label = LocalizationManager.GetString("Text");
                TextToolButton.ToolTipText = LocalizationManager.GetString("AddText");
            }
            if (MosaicToolButton != null)
            {
                MosaicToolButton.Label = LocalizationManager.GetString("Mosaic");
                MosaicToolButton.ToolTipText = LocalizationManager.GetString("Mosaic");
            }
            if (EraserToolButton != null)
            {
                EraserToolButton.Label = LocalizationManager.GetString("Eraser");
                EraserToolButton.ToolTipText = LocalizationManager.GetString("Eraser");
            }

            if (MagicWandToolButton != null)
            {
                MagicWandToolButton.Label = LocalizationManager.GetString("BackgroundRemoval");
                MagicWandToolButton.ToolTipText = LocalizationManager.GetString("BackgroundRemovalTooltip");
            }

            if (NumberingToolButton != null)
            {
                NumberingToolButton.Label = LocalizationManager.GetString("Numbering") ?? "Nombre";
                NumberingToolButton.ToolTipText = GetLocalizedTooltip("Numbering");
            }

            // Select
            if(SelectLabelText != null) SelectLabelText.Text = LocalizationManager.GetString("Select");
            if(SelectButton != null) SelectButton.ToolTip = LocalizationManager.GetString("SelectTooltip");
            // Image Search & Share & OCR
            if(ImageSearchLabelText != null) ImageSearchLabelText.Text = LocalizationManager.GetString("ImageSearch");
            if(ImageSearchButton != null) ImageSearchButton.ToolTip = LocalizationManager.GetString("ImageSearch");
            if(ShareLabelText != null) ShareLabelText.Text = LocalizationManager.GetString("Share");
            if(ShareButton != null) ShareButton.ToolTip = LocalizationManager.GetString("Share");
            if (OcrExtractLabelText != null) OcrExtractLabelText.Text = LocalizationManager.GetString("Extract");
            if (OcrButton != null) OcrButton.ToolTip = GetLocalizedTooltip("OcrCapture"); // Use OcrCaptureTooltip with fallback
            // Panels
            if(ToolTitleText != null) ToolTitleText.Text = LocalizationManager.GetString("ToolOptions");
            if(RecentCapturesTitle != null) RecentCapturesTitle.Text = LocalizationManager.GetString("RecentCaptures");
            if(SizeLabelText != null) SizeLabelText.Text = LocalizationManager.GetString("Size");
            
            // Zoom Reset
            if (ZoomResetButton != null)
                ZoomResetButton.Content = LocalizationManager.GetString("OriginalSize");
            UpdateImageInfo();
        }

        private string GetLocalizedTooltip(string key)
        {
            string tooltipKey = key + "Tooltip";
            string tt = LocalizationManager.GetString(tooltipKey);
            if (tt == tooltipKey) // Not found
            {
                return LocalizationManager.GetString(key);
            }
            return tt;
        }

        protected override void OnClosed(EventArgs e)
        {
            try 
            { 
                // [메모리 최적화] 정적 이벤트 핸들러 해제
                LocalizationManager.LanguageChanged -= PreviewWindow_LanguageChanged; 
                
                // 이미지 캔버스 이벤트 해제
                if (ImageCanvas != null)
                {
                    ImageCanvas.MouseLeftButtonDown -= ImageCanvas_MouseLeftButtonDown;
                    ImageCanvas.MouseMove -= ImageCanvas_MouseMove;
                    ImageCanvas.MouseLeftButtonUp -= ImageCanvas_MouseLeftButtonUp;
                    ImageCanvas.MouseLeave -= ImageCanvas_MouseLeave;
                }

                // [메모리 최적화] 대용량 비트맵 스택 비우기
                if (undoStack != null) undoStack.Clear();
                if (redoStack != null) redoStack.Clear();
                if (undoOriginalStack != null) undoOriginalStack.Clear();
                if (redoOriginalStack != null) redoOriginalStack.Clear();
                if (undoLayersStack != null) undoLayersStack.Clear();
                if (redoLayersStack != null) redoLayersStack.Clear();
                if (_editorUndoStack != null) _editorUndoStack.Clear();
                
                // 메인 이미지 참조 해제
                originalImage = null!;
                currentImage = null!;
                if (PreviewImage != null) PreviewImage.Source = null;
                
                // 그 외 참조 해제
                allCaptures = null;
                _editorManager = null!;

                // ★ 메모리 강제 정리: 편집창이 닫히면 30MB+ 이미지들이 더 이상 필요 없음
                // 즉시 회수하여 메인 프로그램 메모리를 가볍게 유지
                GC.Collect(0, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();

            } 
            catch { }
            
            base.OnClosed(e);
        }
    }

    public enum EditMode
    {
        None,
        Select,
        Crop,
        Pen,
        Highlight,
        Text,
        Mosaic,
        Eraser,
        Shape,
        MagicWand,
        Numbering
    }

    public class ImageUpdatedEventArgs : EventArgs
    {
        public int Index { get; }
        public BitmapSource NewImage { get; }

        public ImageUpdatedEventArgs(int index, BitmapSource newImage)
        {
            Index = index;
            NewImage = newImage;
        }
    }
    public class GridLengthAnimation : System.Windows.Media.Animation.AnimationTimeline
    {
        public GridLength? From { get; set; }
        public GridLength? To { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override System.Windows.Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock animationClock)
        {
            double fromVal = ((GridLength)(From ?? (GridLength)defaultOriginValue)).Value;
            double toVal = ((GridLength)(To ?? (GridLength)defaultDestinationValue)).Value;

            if (fromVal > toVal)
                return new GridLength((1 - (animationClock.CurrentProgress ?? 0)) * (fromVal - toVal) + toVal, GridUnitType.Pixel);
            else
                return new GridLength((animationClock.CurrentProgress ?? 0) * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
        }
    }
}