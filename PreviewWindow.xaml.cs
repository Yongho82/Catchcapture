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
        // 기본 형광펜은 중간 투명도(약 45~50%)와 중간 두께로 시작
        private Color highlightColor = Color.FromArgb(120, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B);
        private double highlightThickness = 8;
        // 형광펜 두께 미리보기용 보조 요소
        private Canvas? thicknessPreviewCanvas;
        private Rectangle? thicknessPreviewRect;
        private Color textColor = Colors.Red;
        private double textSize = 16;
        private double eraserSize = 10;
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

        // 로그 파일 경로
        private string logFilePath = "shape_debug.log";

        public event EventHandler<ImageUpdatedEventArgs>? ImageUpdated;
        private List<CaptureImage>? allCaptures; // 클래스 멤버 변수로 추가 (20줄 부근)

        public PreviewWindow(BitmapSource image, int index, List<CaptureImage>? captures = null)
        {
            InitializeComponent();

            // 로그 파일 초기화
            try
            {
                File.WriteAllText(logFilePath, $"===== 로그 시작: {DateTime.Now} =====\r\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"로그 파일 생성 실패: {ex.Message}");
            }

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

            // 최소(기본) 창 크기 설정: 요청대로 1200x800
            double minWindowWidth = 1200;
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
                MessageBox.Show("이미지가 클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show("이미지가 클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
            string defaultFileName = $"캡처 {timestamp}{ext}";

            // 필터 순서 변경 (설정된 포맷을 무조건 첫 번째로 배치)
            string filter = isPng 
                ? "PNG 이미지|*.png|JPEG 이미지|*.jpg|모든 파일|*.*" 
                : "JPEG 이미지|*.jpg|PNG 이미지|*.png|모든 파일|*.*";

            // 저장 대화 상자 표시
            var dialog = new SaveFileDialog
            {
                Title = "이미지 저장",
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
                    MessageBox.Show("이미지가 저장되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count > 0)
            {
                redoStack.Push(currentImage);
                currentImage = undoStack.Pop();
                UpdatePreviewImage();
                UpdateUndoRedoButtons();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            if (redoStack.Count > 0)
            {
                undoStack.Push(currentImage);
                currentImage = redoStack.Pop();
                UpdatePreviewImage();
                UpdateUndoRedoButtons();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("모든 편집 내용을 취소하고 원본 이미지로 되돌리시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SaveForUndo();
                currentImage = originalImage;
                UpdatePreviewImage();
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
            ImageCanvas.Cursor = Cursors.None;

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
                ImageInfoText.Text = $"이미지 사이즈 : {currentImage.PixelWidth} x {currentImage.PixelHeight}";
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
            string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(logMessage);

            try
            {
                File.AppendAllText(logFilePath, logMessage + "\r\n");
            }
            catch
            {
                // 로그 저장 실패해도 계속 진행
            }
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
            if (ShapeOptionsButton != null)
            {
                ShapeOptionsButton.Background = (active == ShapeButton) ? ActiveToolBackground : InactiveToolBackground;
            }
        }
        #endregion
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
                return new GridLength((1 - animationClock.CurrentProgress.Value) * (fromVal - toVal) + toVal, GridUnitType.Pixel);
            else
                return new GridLength(animationClock.CurrentProgress.Value * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
        }
    }
}