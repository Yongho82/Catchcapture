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
        private Point startPoint;
        private Rectangle? selectionRectangle;
        private List<Point> drawingPoints = new List<Point>();
        private EditMode currentEditMode = EditMode.None;
        // Pen & Highlight settings
        private Color penColor = Colors.Black;
        private double penThickness = 3;
        private ShapeType shapeType = ShapeType.Rectangle;
        private Color shapeColor = Colors.Red;
        private List<Color> customColors = new List<Color>();

        // 공용 색상 팔레트 정의
        private static readonly Color[] SharedColorPalette = new Color[]
        {
            // 1행
            Colors.Black,
            Color.FromRgb(128, 128, 128),
            Colors.White,
            Colors.Red,
            Color.FromRgb(255, 193, 7),
            Color.FromRgb(40, 167, 69),
            // 2행
            Color.FromRgb(32, 201, 151),
            Color.FromRgb(23, 162, 184),
            Color.FromRgb(0, 123, 255),
            Color.FromRgb(108, 117, 125),
            Color.FromRgb(220, 53, 69),
            Color.FromRgb(255, 133, 27),
            // 3행
            Color.FromRgb(111, 66, 193),
            Color.FromRgb(232, 62, 140),
            Color.FromRgb(13, 110, 253),
            Color.FromRgb(25, 135, 84),
            Color.FromRgb(102, 16, 242),
            Colors.Transparent
        };
        private double shapeBorderThickness = 2;
        private bool shapeIsFilled = false;
        private double shapeFillOpacity = 0.5;
        // 기본 형광펜은 중간 투명도(약 45~50%)와 중간 두께로 시작
        private Color highlightColor = Color.FromArgb(120, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B);
        private double highlightThickness = 8;
        private Color textColor = Colors.Red;
        private double textSize = 16;
        private double eraserSize = 20;
        private int mosaicSize = 10;
        private FontWeight textFontWeight = FontWeights.Normal;
        private FontStyle textFontStyle = FontStyles.Normal;
        private string textFontFamily = "Arial";
        private bool textShadowEnabled = false;
        private bool textUnderlineEnabled = false;
        private UIElement? tempShape;
        // 라이브 스트로크 미리보기 (형광펜/펜 공용)
        private System.Windows.Shapes.Path? liveStrokePath;
        private PolyLineSegment? liveStrokeSegment;

        // 도형 버튼을 멤버 변수로 선언
        private Button? rectButton;
        private Button? ellipseButton;
        private Button? lineButton;
        private Button? arrowButton;

        private bool isDrawingShape = false;
        private bool isDragStarted = false; // 마우스 드래그가 시작되었는지

        // 로그 파일 경로 (미사용)
        // private string logFilePath = "shape_debug.log";

        public event EventHandler<ImageUpdatedEventArgs>? ImageUpdated;
        private List<CaptureImage>? allCaptures; // 클래스 멤버 변수로 추가 (20줄 부근)

        private List<CatchCapture.Models.DrawingLayer> drawingLayers = new List<CatchCapture.Models.DrawingLayer>();
        private int nextLayerId = 1;

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

            // 이벤트 핸들러 등록
            ImageCanvas.MouseLeftButtonDown += ImageCanvas_MouseLeftButtonDown;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
            ImageCanvas.MouseLeftButtonUp += ImageCanvas_MouseLeftButtonUp;
            KeyDown += PreviewWindow_KeyDown;

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
            CatchCapture.Models.LocalizationManager.LanguageChanged += PreviewWindow_LanguageChanged;
        }

        // 이미지 크기에 맞게 창 크기를 조정하는 메서드
        private void AdjustWindowSizeToFitImage()
        {
            // 이미지 크기 가져오기
            int imageWidth = currentImage.PixelWidth;
            int imageHeight = currentImage.PixelHeight;

            // 최대 창 크기 제한 (화면 크기를 벗어나지 않도록)
            double maxWindowWidth = SystemParameters.WorkArea.Width * 0.9;
            double maxWindowHeight = SystemParameters.WorkArea.Height * 0.9;

            // 최소(기본) 창 크기 설정: 툴바 아이콘 추가에 맞춰 1300x800
            double minWindowWidth = 1330;
            double minWindowHeight = 800;

            // 도구 모음과 하단 패널의 높이 계산 (대략적인 값)
            double toolbarHeight = 60; // 도구 모음 높이
            double bottomPanelHeight = 80; // 하단 패널 높이
            double windowChromeHeight = 40; // 창 테두리 및 제목 표시줄 높이

            // 필요한 콘텐츠 높이 계산
            double requiredContentHeight = imageHeight + toolbarHeight + bottomPanelHeight + windowChromeHeight;
            double requiredContentWidth = imageWidth + 60; // 좌우 여유 포함

            // 창 크기 계산 (필요한 크기와 최소/최대 범위를 고려)
            double windowWidth = Math.Min(maxWindowWidth, Math.Max(minWindowWidth, requiredContentWidth));
            double windowHeight = Math.Min(maxWindowHeight, Math.Max(minWindowHeight, requiredContentHeight));

            // 창 크기 설정
            this.Width = windowWidth;
            this.Height = windowHeight;

            // 창을 소유자 중앙에 위치 (소유자가 있는 경우)
            if (this.Owner != null)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.Width) / 2;
                this.Top = this.Owner.Top + (this.Owner.Height - this.Height) / 2;
            }
            else
            {
                // 소유자가 없는 경우 화면 중앙에 위치
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void PreviewWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // 현재 편집 모드 취소
                CancelCurrentEditMode();
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+C: 현재 이미지 복사
                ScreenCaptureUtility.CopyImageToClipboard(currentImage);
                ShowToastMessage(LocalizationManager.Get("CopyToClipboard"));
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

        // 확대 기능
        private void ZoomIn()
        {
            if (PreviewImage != null)
            {
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    // 10%씩 확대 (최대 500%)
                    double newScale = Math.Min(scaleTransform.ScaleX * 1.1, 5.0);
                    scaleTransform.ScaleX = newScale;
                    scaleTransform.ScaleY = newScale;
                    
                    // RenderTransformOrigin을 중앙으로 설정
                    PreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
                }
            }
        }

        // 축소 기능
        private void ZoomOut()
        {
            if (PreviewImage != null)
            {
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    // 10%씩 축소 (최소 10%)
                    double newScale = Math.Max(scaleTransform.ScaleX / 1.1, 0.1);
                    scaleTransform.ScaleX = newScale;
                    scaleTransform.ScaleY = newScale;
                    
                    // RenderTransformOrigin을 중앙으로 설정
                    PreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
                }
            }
        }

        // 실제 크기로 리셋
        private void ResetZoom()
        {
            if (PreviewImage != null)
            {
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleX = 1.0;
                    scaleTransform.ScaleY = 1.0;
                }
            }
        }

        // 이미지의 스케일 트랜스폼 가져오기
        private ScaleTransform? GetImageScaleTransform()
        {
            if (PreviewImage?.RenderTransform is ScaleTransform scale)
            {
                return scale;
            }
            else if (PreviewImage?.RenderTransform is TransformGroup group)
            {
                foreach (var transform in group.Children)
                {
                    if (transform is ScaleTransform scaleTransform)
                    {
                        return scaleTransform;
                    }
                }
            }
            else if (PreviewImage != null)
            {
                // 스케일 트랜스폼이 없으면 새로 생성
                var scaleTransform = new ScaleTransform(1.0, 1.0);
                if (PreviewImage.RenderTransform is TransformGroup existingGroup)
                {
                    existingGroup.Children.Add(scaleTransform);
                }
                else
                {
                    PreviewImage.RenderTransform = scaleTransform;
                }
                return scaleTransform;
            }

            return null;
        }

        private void CancelCurrentEditMode()
        {
            WriteLog($"CancelCurrentEditMode 호출: 이전 모드={currentEditMode}, tempShape={tempShape != null}");

            // 현재 편집 모드 취소
            currentEditMode = EditMode.None;

            // 선택 영역 제거
            if (selectionRectangle != null && ImageCanvas != null)
            {
                WriteLog("선택 영역 제거");
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
            }

            // 임시 도형 정리 - 헬퍼 메서드 호출
            WriteLog("CancelCurrentEditMode에서 CleanupTemporaryShape 호출");
            CleanupTemporaryShape();

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

            // 그리기 포인트 초기화
            drawingPoints.Clear();
            isDrawingShape = false;
            isDragStarted = false;
            WriteLog("그리기 상태 초기화: isDrawingShape=false, isDragStarted=false");

            // 활성 강조 해제
            SetActiveToolButton(null);
        }

        #region 도구 버튼 이벤트

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            ScreenCaptureUtility.CopyImageToClipboard(currentImage);
            ShowToastMessage(LocalizationManager.Get("CopyToClipboard"));
        }
                private async void OcrButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentImage == null) return;

            try
            {
                // 로딩 표시 (커서 변경)
                this.Cursor = Cursors.Wait;

                // OCR 실행
                string extractedText = await CatchCapture.Utilities.OcrUtility.ExtractTextFromImageAsync(currentImage);

                // 커서 복원
                this.Cursor = Cursors.Arrow;

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    MessageBox.Show(LocalizationManager.Get("NoExtractedText"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 결과창 표시
                var resultWindow = new OcrResultWindow(extractedText);
                resultWindow.Owner = this;
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show($"{LocalizationManager.Get("Error")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 자동 파일 이름 생성
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");

            // 설정 불러오기
            var settings = CatchCapture.Models.Settings.Load();
            bool isPng = settings.FileSaveFormat.Equals("PNG", StringComparison.OrdinalIgnoreCase);
            
            // 확장자 설정
            string ext = isPng ? ".png" : ".jpg";
            string defaultFileName = $"{LocalizationManager.Get("AreaCapture")} {timestamp}{ext}";

            // 필터 순서 변경 (설정된 포맷을 무조건 첫 번째로 배치)
            string filter = isPng 
                ? "PNG|*.png|JPEG|*.jpg|*.*|*.*" 
                : "JPEG|*.jpg|PNG|*.png|*.*|*.*";

            // 저장 대화 상자 표시
            var dialog = new SaveFileDialog
            {
                Title = LocalizationManager.Get("ImageSaveTitle"),
                Filter = filter,
                DefaultExt = ext,
                FileName = defaultFileName,
                FilterIndex = 1 // 항상 첫 번째 항목(설정된 포맷)이 선택됨
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ScreenCaptureUtility.SaveImageToFile(currentImage, dialog.FileName);
                    MessageBox.Show(LocalizationManager.Get("ImageSaved"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{LocalizationManager.Get("Error")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentImage == null)
                {
                    ShowToastMessage(LocalizationManager.Get("NoImageToPrint"));
                    return;
                }

                // 인쇄 미리보기 창 열기
                var printPreviewWindow = new PrintPreviewWindow(currentImage);
                printPreviewWindow.Owner = this;
                printPreviewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("PrintPreviewError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    // 편집창 닫기
                    this.Close();
                    
                    // UI가 정리될 시간을 주기 위해 짧은 대기
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    // 영역 캡처 시작
                    mainWindow.StartAreaCapture();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("RecaptureError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                // 현재 상태를 Redo 스택에 저장
                redoStack.Push(currentImage);
                var currentLayersCopy = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    LayerId = layer.LayerId,
                    Type = layer.Type,
                    Points = layer.Points?.ToArray(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased
                }).ToList();
                redoLayersStack.Push(currentLayersCopy);
                
                // Undo 스택에서 복원
                currentImage = undoStack.Pop();
                drawingLayers = undoLayersStack.Pop();
                
                UpdatePreviewImage();
                UpdateUndoRedoButtons();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count > 0)
            {
                // 현재 상태를 Undo 스택에 저장
                undoStack.Push(currentImage);
                var currentLayersCopy = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    LayerId = layer.LayerId,
                    Type = layer.Type,
                    Points = layer.Points?.ToArray(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased
                }).ToList();
                undoLayersStack.Push(currentLayersCopy);
                
                // Redo 스택에서 복원
                currentImage = redoStack.Pop();
                drawingLayers = redoLayersStack.Pop();
                
                UpdatePreviewImage();
                UpdateUndoRedoButtons();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0 || true)
            {
                if (MessageBox.Show(LocalizationManager.Get("ConfirmReset"), LocalizationManager.Get("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    SaveForUndo();
                    currentImage = originalImage;
                    UpdatePreviewImage();
                }
            }
        }

        private void CropButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Crop;
            ImageCanvas.Cursor = Cursors.Cross;
            SetActiveToolButton(CropButton);
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
            shapeType = ShapeType.Rectangle;
            shapeColor = Colors.Black;
            shapeBorderThickness = 2;
            shapeIsFilled = false;

            // 상태 초기화
            isDrawingShape = false;
            isDragStarted = false;

            WriteLog("기본 도형 그리기 모드 설정: 사각형, 검정색, 윤곽선");
            SetActiveToolButton(ShapeButton);
        }

        private void ShapeOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("ShapeOptionsButton_Click 호출 - 옵션 팝업 표시");

            // 팝업이 이미 열려있으면 닫기
            if (ToolOptionsPopup.IsOpen)
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
            highlightColor = Color.FromArgb(120, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B);
            highlightThickness = 8;
            
            // 바로 그리기 시작 (팝업 표시 안 함)
            SetActiveToolButton(HighlightToolButton);
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Pen;
            ImageCanvas.Cursor = Cursors.Pen;

            // 기본값 설정 (검정, 3px)
            penColor = Colors.Black;
            penThickness = 3;
            
            // 바로 그리기 시작 (팝업 표시 안 함)
            SetActiveToolButton(PenToolButton);  // ← 이 줄 추가
        }

        // 새로 추가: 옵션 버튼 클릭 핸들러
        private void PenOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            // 팝업이 이미 열려있으면 닫기
            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }

            // 펜 옵션 팝업 표시
            ShowPenOptionsPopup();
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
                ImageInfoText.Text = $"{LocalizationManager.Get("ImageSizePrefix")}{currentImage.PixelWidth} x {currentImage.PixelHeight}";
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

        private void SaveForUndo()
        {
            undoStack.Push(currentImage);
            redoStack.Clear();
            
            // 레이어 상태도 저장 (깊은 복사)
            var layersCopy = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
            {
                LayerId = layer.LayerId,
                Type = layer.Type,
                Points = layer.Points?.ToArray(),
                Color = layer.Color,
                Thickness = layer.Thickness,
                IsErased = layer.IsErased
            }).ToList();
            undoLayersStack.Push(layersCopy);
            redoLayersStack.Clear();
            
            UpdateUndoRedoButtons();
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

            // 임시 도형 정리
            WriteLog("UpdatePreviewImage에서 CleanupTemporaryShape 호출");
            CleanupTemporaryShape();

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

        // 활성화된 툴 버튼 강조 표시 색상 (호버 색상과 동일하게 유지)
        private static readonly Brush ActiveToolBackground = (Brush)new BrushConverter().ConvertFromString("#E8F3FF")!;
        private static readonly Brush InactiveToolBackground = Brushes.Transparent;

        private void SetActiveToolButton(FrameworkElement? active)
        {
            // 툴바의 편집 도구 버튼들만 대상으로 처리
            var toolButtons = new List<FrameworkElement?>
            {
                CropButton,
                ShapeButton,
                HighlightToolButton,
                PenToolButton,        // 펜 버튼 추가
                TextToolButton,
                MosaicToolButton,
                EraserToolButton,
            };

            foreach (var element in toolButtons)
            {
                if (element == null) continue;
                
                bool isActive = (active != null && element == active);
                var bgColor = isActive ? ActiveToolBackground : InactiveToolBackground;
                
                // Button인 경우 (CropButton, ShapeButton)
                if (element is Button b)
                {
                    b.Background = bgColor;
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
                    shapeLabel.Text = LocalizationManager.Get("ShapeLbl");
                }
            }
        }

        private void UpdateUIText()
        {
            try
            {
                // 창 제목
                this.Title = LocalizationManager.Get("ImageEditTitle");

                // 상단 툴바 텍스트
                if (RecaptureLabelText != null) RecaptureLabelText.Text = LocalizationManager.Get("Recapture");
                if (CopyLabelText != null) CopyLabelText.Text = LocalizationManager.Get("CopySelected");
                if (SaveLabelText != null) SaveLabelText.Text = LocalizationManager.Get("Save");
                if (PrintLabelText != null) PrintLabelText.Text = LocalizationManager.Get("Print");
                if (UndoLabelText != null) UndoLabelText.Text = LocalizationManager.Get("UndoLbl");
                if (RedoLabelText != null) RedoLabelText.Text = LocalizationManager.Get("RedoLbl");
                if (ResetLabelText != null) ResetLabelText.Text = LocalizationManager.Get("ResetLbl");
                if (CropLabelText != null) CropLabelText.Text = LocalizationManager.Get("Crop");
                if (RotateLabelText != null) RotateLabelText.Text = LocalizationManager.Get("Rotate");
                if (FlipHorizontalLabelText != null) FlipHorizontalLabelText.Text = LocalizationManager.Get("FlipH");
                if (FlipVerticalLabelText != null) FlipVerticalLabelText.Text = LocalizationManager.Get("FlipV");

                // TwoTierToolButton 라벨/툴팁
                if (PenToolButton != null)
                {
                    PenToolButton.Label = LocalizationManager.Get("Pen");
                    PenToolButton.ToolTipText = LocalizationManager.Get("Pen");
                }
                if (HighlightToolButton != null)
                {
                    HighlightToolButton.Label = LocalizationManager.Get("Highlighter");
                    HighlightToolButton.ToolTipText = LocalizationManager.Get("Highlighter");
                }
                if (TextToolButton != null)
                {
                    TextToolButton.Label = LocalizationManager.Get("Text");
                    TextToolButton.ToolTipText = LocalizationManager.Get("TextAdd");
                }
                if (MosaicToolButton != null)
                {
                    MosaicToolButton.Label = LocalizationManager.Get("Mosaic");
                    MosaicToolButton.ToolTipText = LocalizationManager.Get("Mosaic");
                }
                if (EraserToolButton != null)
                {
                    EraserToolButton.Label = LocalizationManager.Get("Eraser");
                    EraserToolButton.ToolTipText = LocalizationManager.Get("Eraser");
                }

                // 이미지 검색 / OCR
                if (ImageSearchLabelText != null) ImageSearchLabelText.Text = LocalizationManager.Get("ImageSearch");
                if (ImageSearchButton != null) ImageSearchButton.ToolTip = LocalizationManager.Get("ImageSearchTooltip");
                if (OcrLabelText != null) OcrLabelText.Text = LocalizationManager.Get("OCR");
                if (OcrExtractLabelText != null) OcrExtractLabelText.Text = LocalizationManager.Get("Extract");
                if (OcrButton != null) OcrButton.ToolTip = LocalizationManager.Get("OCR");

                // 하단 정보 바 및 줌 컨트롤
                if (ZoomResetButton != null)
                {
                    ZoomResetButton.Content = LocalizationManager.Get("ZoomReset");
                    ZoomResetButton.ToolTip = LocalizationManager.Get("ZoomResetTooltip");
                }
                if (ZoomOutButton != null) ZoomOutButton.ToolTip = LocalizationManager.Get("ZoomOut");
                if (ZoomInButton != null) ZoomInButton.ToolTip = LocalizationManager.Get("ZoomIn");
                if (ImageInfoText != null) ImageInfoText.Text = LocalizationManager.Get("ImageSizePrefix"); // 실제 크기는 UpdateImageInfo에서 갱신

                // 우측 패널 헤더/크기 라벨
                if (RecentCapturesTitle != null) RecentCapturesTitle.Text = LocalizationManager.Get("RecentCaptures");
                if (SizeLabelText != null) SizeLabelText.Text = LocalizationManager.Get("SizeLabel");

                // 상단 툴바 버튼 툴팁
                if (RecaptureButton != null) RecaptureButton.ToolTip = LocalizationManager.Get("AreaCapture");
                if (PrintButton != null) PrintButton.ToolTip = LocalizationManager.Get("Print");
                if (SaveButton != null) SaveButton.ToolTip = LocalizationManager.Get("Save");
                if (CopyButton != null) CopyButton.ToolTip = LocalizationManager.Get("CopyToClipboard");
                if (UndoButton != null) UndoButton.ToolTip = LocalizationManager.Get("UndoLbl");
                if (RedoButton != null) RedoButton.ToolTip = LocalizationManager.Get("RedoLbl");
                if (ResetButton != null) ResetButton.ToolTip = LocalizationManager.Get("ResetLbl");
                if (CropButton != null) CropButton.ToolTip = LocalizationManager.Get("Crop");
                if (RotateButton != null) RotateButton.ToolTip = LocalizationManager.Get("Rotate");
                if (FlipHorizontalButton != null) FlipHorizontalButton.ToolTip = LocalizationManager.Get("FlipH");
                if (FlipVerticalButton != null) FlipVerticalButton.ToolTip = LocalizationManager.Get("FlipV");
                if (ShapeButton != null) ShapeButton.ToolTip = LocalizationManager.Get("ShapeLbl");
                if (ShapeOptionsButton != null) ShapeOptionsButton.ToolTip = LocalizationManager.Get("ShapeOptions");
            }
            catch { }
        }

        private void ImageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BitmapSource? src = PreviewImage?.Source as BitmapSource;
                if (src == null) return;

                // Call MainWindow's existing Google image search logic
                var owner = this.Owner as MainWindow;
                if (owner != null)
                {
                    owner.SearchImageOnGoogle(src);
                }
            }
            catch { }
        }
        #endregion

        private void PreviewWindow_LanguageChanged(object? sender, EventArgs e)
        {
            try 
            { 
                UpdateUIText(); 
                // 팔레트(도구 옵션) 팝업이 열려 있으면 현재 모드에 맞게 재구성
                if (ToolOptionsPopup != null && ToolOptionsPopup.IsOpen)
                {
                    switch (currentEditMode)
                    {
                        case EditMode.Highlight:
                            ShowHighlightOptionsPopup();
                            break;
                        case EditMode.Text:
                            ShowTextOptions();
                            break;
                        case EditMode.Mosaic:
                            ShowMosaicOptions();
                            break;
                        case EditMode.Eraser:
                            ShowEraserOptions();
                            break;
                        case EditMode.Pen:
                            ShowPenOptionsPopup();
                            break;
                        case EditMode.Shape:
                            ShowShapeOptionsPopup();
                            break;                        
                        default:
                            // 도구 옵션이 없는 모드면 닫기
                            ToolOptionsPopup.IsOpen = false;
                            break;
                    }
                }
            } 
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { CatchCapture.Models.LocalizationManager.LanguageChanged -= PreviewWindow_LanguageChanged; } catch { }
            base.OnClosed(e);
        }
    }

    public enum EditMode
    {
        None,
        Crop,
        Pen,
        Highlight,
        Text,
        Mosaic,
        Eraser,
        Shape
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