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

        public PreviewWindow(BitmapSource image, int index)
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

            // 창이 로드된 후 하이라이트 모드 활성화
            this.Loaded += (s, e) =>
            {
                // 약간의 지연 후 하이라이트 모드 활성화 (UI가 완전히 로드된 후)
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
                // 현재 스케일 가져오기
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    // 10%씩 확대 (최대 500%)
                    double newScale = Math.Min(scaleTransform.ScaleX * 1.1, 5.0);
                    scaleTransform.ScaleX = newScale;
                    scaleTransform.ScaleY = newScale;
                }
            }
        }

        // 축소 기능
        private void ZoomOut()
        {
            if (PreviewImage != null)
            {
                // 현재 스케일 가져오기
                var scaleTransform = GetImageScaleTransform();
                if (scaleTransform != null)
                {
                    // 10%씩 축소 (최소 10%)
                    double newScale = Math.Max(scaleTransform.ScaleX * 0.9, 0.1);
                    scaleTransform.ScaleX = newScale;
                    scaleTransform.ScaleY = newScale;
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

        #region 이벤트 핸들러

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ImageCanvas == null) return;

            Point clickPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseDown: 위치({clickPoint.X:F1}, {clickPoint.Y:F1}), 편집모드={currentEditMode}, isDrawingShape={isDrawingShape}");

            // 도형 모드에서의 처리
            if (currentEditMode == EditMode.Shape)
            {
                // 클릭 위치 저장
                startPoint = clickPoint;
                // 드래그는 아직 시작되지 않음
                isDragStarted = false;
                isDrawingShape = true;
                WriteLog($"도형 그리기 대기: 시작점({startPoint.X:F1}, {startPoint.Y:F1}), 도형타입={shapeType}");
                e.Handled = true;
                return;
            }

            // 다른 모드 처리
            startPoint = clickPoint;

            switch (currentEditMode)
            {
                case EditMode.Crop:
                    StartCrop();
                    break;
                case EditMode.Highlight:
                case EditMode.Pen:
                    StartDrawing();
                    break;
                case EditMode.Text:
                    AddText();
                    break;
                case EditMode.Mosaic:
                    StartMosaic();
                    break;
                case EditMode.Eraser:
                    StartEraser();
                    break;
            }
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 왼쪽 버튼이 눌려 있지 않으면 아무것도 하지 않음
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // 현재 마우스 위치
            Point currentPoint = e.GetPosition(ImageCanvas);

            // 도형 그리기 처리
            if (currentEditMode == EditMode.Shape && isDrawingShape)
            {
                // 드래그 거리 계산
                double dragDistance = Math.Sqrt(
                    Math.Pow(currentPoint.X - startPoint.X, 2) +
                    Math.Pow(currentPoint.Y - startPoint.Y, 2));

                // 드래그가 시작되지 않았고, 거리가 충분하면 드래그 시작
                if (!isDragStarted && dragDistance > 5)
                {
                    isDragStarted = true;
                    WriteLog($"드래그 시작 감지: 거리={dragDistance:F1}");
                }

                // 이미 드래그가 시작되었다면 도형 그리기
                if (isDragStarted)
                {
                    WriteLog($"MouseMove(도형): 위치({currentPoint.X:F1}, {currentPoint.Y:F1}), tempShape={tempShape != null}");

                    // 임시 도형이 없으면 새로 생성
                    if (tempShape == null)
                    {
                        tempShape = CreateShape(startPoint, currentPoint);
                        if (tempShape != null)
                        {
                            ImageCanvas.Children.Add(tempShape);
                            WriteLog($"임시 도형 생성 및 추가됨: 타입={tempShape.GetType().Name}, 크기({Math.Abs(currentPoint.X - startPoint.X):F1}x{Math.Abs(currentPoint.Y - startPoint.Y):F1})");
                        }
                    }
                    else
                    {
                        // 기존 도형 속성 업데이트
                        UpdateShapeProperties(tempShape, startPoint, currentPoint);
                        WriteLog($"임시 도형 업데이트: 크기({Math.Abs(currentPoint.X - startPoint.X):F1}x{Math.Abs(currentPoint.Y - startPoint.Y):F1})");
                    }

                    e.Handled = true;
                }
                return;
            }

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    UpdateCropSelection(currentPoint);
                    break;
                case EditMode.Highlight:
                case EditMode.Pen:
                    UpdateDrawing(currentPoint);
                    break;
                case EditMode.Mosaic:
                    UpdateMosaic(currentPoint);
                    break;
                case EditMode.Eraser:
                    UpdateEraser(currentPoint);
                    break;
            }
        }

        private void ImageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 편집 모드가 없으면 아무것도 하지 않음
            if (currentEditMode == EditMode.None) return;

            // 마우스를 뗀 위치
            Point endPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseUp: 위치({endPoint.X:F1}, {endPoint.Y:F1}), 편집모드={currentEditMode}, isDrawingShape={isDrawingShape}, isDragStarted={isDragStarted}");

            // 도형 그리기 완료
            if (currentEditMode == EditMode.Shape && isDrawingShape)
            {
                // 드래그한 경우에만 도형 적용
                if (isDragStarted)
                {
                    WriteLog($"도형 그리기 완료 시도: tempShape={tempShape != null}");
                    ApplyShape(endPoint);
                }
                else
                {
                    // 클릭만 한 경우(드래그 없음)
                    WriteLog("드래그 없이 클릭만 감지됨. 도형 그리기 취소");

                    // 기존에 생성된 임시 도형이 있다면 유지
                    if (tempShape != null)
                    {
                        WriteLog("기존 tempShape 유지");
                    }
                }

                // 상태 초기화
                isDrawingShape = false;
                isDragStarted = false;

                e.Handled = true;
                return;
            }

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    FinishCrop(endPoint);
                    break;
                case EditMode.Highlight:
                case EditMode.Pen:
                    FinishDrawing();
                    break;
                case EditMode.Mosaic:
                    FinishMosaic(endPoint);
                    break;
                case EditMode.Eraser:
                    FinishEraser();
                    break;
            }
        }

        #endregion

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
            string defaultFileName = $"캡처 {timestamp}.jpg";

            // 저장 대화 상자 표시
            var dialog = new SaveFileDialog
            {
                Title = "이미지 저장",
                Filter = "JPEG 이미지|*.jpg|PNG 이미지|*.png|모든 파일|*.*",
                DefaultExt = ".jpg",
                FileName = defaultFileName
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

            ShowHighlightOptionsPopup();
            SetActiveToolButton(HighlightButton);
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Pen;
            ImageCanvas.Cursor = Cursors.Pen;

            ShowPenOptionsPopup();
            SetActiveToolButton(PenButton);
        }

        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Text;
            ImageCanvas.Cursor = Cursors.IBeam;

            ShowTextOptions();
            SetActiveToolButton(TextButton);
        }

        private void MosaicButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Mosaic;
            ImageCanvas.Cursor = Cursors.Cross;

            ShowMosaicOptions();
            SetActiveToolButton(MosaicButton);
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Eraser;
            ImageCanvas.Cursor = Cursors.None;

            ShowEraserOptions();
            SetActiveToolButton(EraserButton);
        }

        private void CloseOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            // 옵션 패널 닫기
            EditToolPanel.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 편집 도구 구현

        private void StartCrop()
        {
            if (selectionRectangle != null)
            {
                ImageCanvas.Children.Remove(selectionRectangle);
            }

            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.LightBlue,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 173, 216, 230))
            };

            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);

            ImageCanvas.Children.Add(selectionRectangle);
        }

        private void UpdateCropSelection(Point currentPoint)
        {
            if (selectionRectangle == null) return;

            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRectangle, left);
            Canvas.SetTop(selectionRectangle, top);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;
        }

        private void FinishCrop(Point endPoint)
        {
            if (selectionRectangle == null) return;

            double left = Math.Min(startPoint.X, endPoint.X);
            double top = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                MessageBox.Show("선택된 영역이 너무 작습니다. 다시 선택해주세요.", "알림");
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
                return;
            }

            Int32Rect cropRect = new Int32Rect((int)left, (int)top, (int)width, (int)height);

            SaveForUndo();
            currentImage = ImageEditUtility.CropImage(currentImage, cropRect);
            UpdatePreviewImage();

            ImageCanvas.Children.Remove(selectionRectangle);
            selectionRectangle = null;
            // 연속 자르기를 위해 크롭 모드 유지
            currentEditMode = EditMode.Crop;
            ImageCanvas.Cursor = Cursors.Cross;
        }

        private void StartDrawing()
        {
            drawingPoints.Clear();
            drawingPoints.Add(startPoint);

            // 라이브 경로 초기화 (펜/형광펜 모두 동일 방식으로 미리보기)
            Color strokeColor;
            double thickness;
            double opacity = 1.0;
            if (currentEditMode == EditMode.Pen)
            {
                strokeColor = penColor;
                thickness = penThickness;
                opacity = 1.0;
            }
            else // Highlight
            {
                // 미리보기도 최종 렌더와 동일하게 브러시 알파를 사용
                strokeColor = Color.FromArgb(highlightColor.A, highlightColor.R, highlightColor.G, highlightColor.B);
                thickness = highlightThickness;
                opacity = 1.0;
            }

            var fig = new PathFigure { StartPoint = startPoint, IsClosed = false, IsFilled = false };
            liveStrokeSegment = new PolyLineSegment();
            fig.Segments.Add(liveStrokeSegment);
            var geom = new PathGeometry();
            geom.Figures.Add(fig);

            liveStrokePath = new System.Windows.Shapes.Path
            {
                Data = geom,
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = opacity
            };

            ImageCanvas.Children.Add(liveStrokePath);
        }

        private void UpdateDrawing(Point currentPoint)
        {
            drawingPoints.Add(currentPoint);

            // 라이브 경로 갱신 (연속 경로여서 끊김 현상 방지)
            if (liveStrokeSegment != null)
            {
                liveStrokeSegment.Points.Add(currentPoint);
            }
        }

        private void FinishDrawing()
        {
            if (drawingPoints.Count < 2) return;

            SaveForUndo();
            if (currentEditMode == EditMode.Pen)
            {
                currentImage = ImageEditUtility.ApplyPen(currentImage, drawingPoints.ToArray(), penColor, penThickness);
            }
            else
            {
                currentImage = ImageEditUtility.ApplyHighlight(currentImage, drawingPoints.ToArray(), highlightColor, highlightThickness);
            }

            // 라이브 경로 제거
            if (liveStrokePath != null)
            {
                ImageCanvas.Children.Remove(liveStrokePath);
                liveStrokePath = null;
                liveStrokeSegment = null;
            }

            UpdatePreviewImage();

            drawingPoints.Clear();
            // keep mode
            ImageCanvas.Cursor = Cursors.Pen;
        }

        private void AddText()
        {
            TextBox textBox = new TextBox
            {
                Width = 200,
                Height = 30,
                AcceptsReturn = false,
                FontSize = textSize
            };

            Canvas.SetLeft(textBox, startPoint.X);
            Canvas.SetTop(textBox, startPoint.Y);

            ImageCanvas.Children.Add(textBox);
            textBox.Focus();

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    string text = textBox.Text.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        SaveForUndo();

                        // 향상된 텍스트 서식 옵션 사용
                        TextFormatOptions options = new TextFormatOptions
                        {
                            TextColor = textColor,
                            FontSize = textSize,
                            FontWeight = textFontWeight,
                            FontStyle = textFontStyle,
                            FontFamily = textFontFamily,
                            ApplyShadow = textShadowEnabled,
                            TextAlignment = TextAlignment.Left
                        };

                        if (textShadowEnabled)
                        {
                            options.ShadowColor = Color.FromArgb(100, 0, 0, 0);
                            options.ShadowOffset = new Vector(2, 2);
                        }

                        if (textUnderlineEnabled)
                        {
                            options.TextDecoration = new TextDecorationCollection();
                            options.TextDecoration.Add(TextDecorations.Underline);
                        }

                        currentImage = ImageEditUtility.AddEnhancedText(currentImage, text, startPoint, options);
                        UpdatePreviewImage();
                    }

                    ImageCanvas.Children.Remove(textBox);
                    // 텍스트 입력 완료 후에도 텍스트 모드 유지
                    currentEditMode = EditMode.Text;
                    ImageCanvas.Cursor = Cursors.IBeam;
                }
                else if (e.Key == Key.Escape)
                {
                    ImageCanvas.Children.Remove(textBox);
                    // ESC 키는 현재 입력만 취소하고 텍스트 모드는 유지
                    currentEditMode = EditMode.Text;
                    ImageCanvas.Cursor = Cursors.IBeam;
                }
            };
        }

        private void StartMosaic()
        {
            if (selectionRectangle != null)
            {
                ImageCanvas.Children.Remove(selectionRectangle);
            }

            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
            };

            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);

            ImageCanvas.Children.Add(selectionRectangle);
        }

        private void UpdateMosaic(Point currentPoint)
        {
            if (selectionRectangle == null) return;

            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRectangle, left);
            Canvas.SetTop(selectionRectangle, top);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;
        }

        private void FinishMosaic(Point endPoint)
        {
            if (selectionRectangle == null) return;

            double left = Math.Min(startPoint.X, endPoint.X);
            double top = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                MessageBox.Show("선택된 영역이 너무 작습니다. 다시 선택해주세요.", "알림");
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
                return;
            }

            Int32Rect mosaicRect = new Int32Rect((int)left, (int)top, (int)width, (int)height);

            SaveForUndo();
            currentImage = ImageEditUtility.ApplyMosaic(currentImage, mosaicRect, mosaicSize);
            UpdatePreviewImage();

            ImageCanvas.Children.Remove(selectionRectangle);
            selectionRectangle = null;

            // 모자이크 적용 후에도 모드 유지
            currentEditMode = EditMode.Mosaic;
            ImageCanvas.Cursor = Cursors.Cross;
        }

        private void StartEraser()
        {
            drawingPoints.Clear();
            drawingPoints.Add(startPoint);
        }

        private void UpdateEraser(Point currentPoint)
        {
            drawingPoints.Add(currentPoint);

            // 임시 지우개 효과 표시
            Ellipse eraser = new Ellipse
            {
                Width = eraserSize * 2,
                Height = eraserSize * 2,
                Fill = Brushes.White
            };

            Canvas.SetLeft(eraser, currentPoint.X - eraserSize);
            Canvas.SetTop(eraser, currentPoint.Y - eraserSize);

            ImageCanvas.Children.Add(eraser);
        }

        private void FinishEraser()
        {
            if (drawingPoints.Count < 1) return;

            SaveForUndo();
            currentImage = ImageEditUtility.ApplyEraser(currentImage, drawingPoints.ToArray(), eraserSize);
            UpdatePreviewImage();

            // 임시 지우개 효과 제거
            List<UIElement> elementsToRemove = new List<UIElement>();
            foreach (UIElement element in ImageCanvas.Children)
            {
                if (element is Ellipse)
                {
                    elementsToRemove.Add(element);
                }
            }

            foreach (UIElement element in elementsToRemove)
            {
                ImageCanvas.Children.Remove(element);
            }

            drawingPoints.Clear();
        }

        private UIElement? CreateShape(Point start, Point current)
        {
            if (ImageCanvas == null) return null;

            WriteLog($"CreateShape: 시작({start.X:F1}, {start.Y:F1}), 현재({current.X:F1}, {current.Y:F1}), 도형타입={shapeType}");

            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            // 도형 유형에 따라 다른 도형 생성 - 단순화
            UIElement? result = null;
            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    var rectangle = new Rectangle
                    {
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness,
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb(128, shapeColor.R, shapeColor.G, shapeColor.B)) : null
                    };

                    Canvas.SetLeft(rectangle, left);
                    Canvas.SetTop(rectangle, top);
                    rectangle.Width = width;
                    rectangle.Height = height;

                    result = rectangle;
                    WriteLog($"사각형 생성됨: 위치({left:F1}, {top:F1}), 크기({width:F1}x{height:F1})");
                    break;

                case ShapeType.Ellipse:
                    var ellipse = new Ellipse
                    {
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness,
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb(128, shapeColor.R, shapeColor.G, shapeColor.B)) : null
                    };

                    Canvas.SetLeft(ellipse, left);
                    Canvas.SetTop(ellipse, top);
                    ellipse.Width = width;
                    ellipse.Height = height;

                    result = ellipse;
                    WriteLog($"타원 생성됨: 위치({left:F1}, {top:F1}), 크기({width:F1}x{height:F1})");
                    break;

                case ShapeType.Line:
                    var line = new Line
                    {
                        X1 = start.X,
                        Y1 = start.Y,
                        X2 = current.X,
                        Y2 = current.Y,
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };

                    result = line;
                    WriteLog($"선 생성됨: 시작({start.X:F1}, {start.Y:F1}), 끝({current.X:F1}, {current.Y:F1})");
                    break;

                case ShapeType.Arrow:
                    result = CreateArrow(start, current);
                    WriteLog($"화살표 생성됨: 시작({start.X:F1}, {start.Y:F1}), 끝({current.X:F1}, {current.Y:F1})");
                    break;

                default:
                    WriteLog("알 수 없는 도형 타입");
                    return null;
            }

            return result;
        }

        private System.Windows.Shapes.Path CreateArrow(Point start, Point end)
        {
            double arrowLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            double arrowHeadWidth = Math.Min(10, arrowLength / 3); // 화살표 머리 너비 조정

            // 화살표 각도 계산
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowHeadAngle1 = angle + Math.PI / 6; // 30도
            double arrowHeadAngle2 = angle - Math.PI / 6; // -30도

            // 화살표 머리 끝점 계산
            Point arrowHead1 = new Point(
                end.X - arrowHeadWidth * Math.Cos(arrowHeadAngle1),
                end.Y - arrowHeadWidth * Math.Sin(arrowHeadAngle1));

            Point arrowHead2 = new Point(
                end.X - arrowHeadWidth * Math.Cos(arrowHeadAngle2),
                end.Y - arrowHeadWidth * Math.Sin(arrowHeadAngle2));

            // 경로 생성
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure { StartPoint = start };

            // 선 추가
            pathFigure.Segments.Add(new LineSegment(end, true));

            // 화살표 머리 추가
            pathFigure.Segments.Add(new LineSegment(arrowHead1, true));
            pathFigure.Segments.Add(new LineSegment(end, true));
            pathFigure.Segments.Add(new LineSegment(arrowHead2, true));

            pathGeometry.Figures.Add(pathFigure);

            // Path 생성 및 반환
            var path = new System.Windows.Shapes.Path
            {
                Data = pathGeometry,
                Stroke = new SolidColorBrush(shapeColor),
                StrokeThickness = shapeBorderThickness
            };

            return path;
        }

        private void ApplyShape(Point endPoint)
        {
            WriteLog($"ApplyShape 시작: 끝점({endPoint.X:F1}, {endPoint.Y:F1}), tempShape={tempShape != null}");

            // 임시 도형 없이 바로 이미지에 도형 적용
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);
            WriteLog($"도형 크기: 너비={width:F1}, 높이={height:F1}, 도형타입={shapeType}");

            try
            {
                // 도형을 이미지에 적용
                WriteLog("SaveForUndo 호출 및 도형 적용 시작");
                SaveForUndo();
                currentImage = ImageEditUtility.ApplyShape(
                    currentImage,
                    startPoint,
                    endPoint,
                    shapeType,
                    shapeColor,
                    shapeBorderThickness,
                    shapeIsFilled
                );
                WriteLog("도형이 이미지에 성공적으로 적용됨");

                // 이미지 업데이트 및 임시 도형 정리
                WriteLog("임시 도형 정리 및 이미지 업데이트 시작");
                CleanupTemporaryShape();

                // 자식 요소 수를 로그로 기록
                int childCount = ImageCanvas.Children.Count;
                WriteLog($"UpdatePreviewImage 전 ImageCanvas.Children.Count={childCount}");

                UpdatePreviewImage();

                // 도형 그리기 모드 유지
                currentEditMode = EditMode.Shape;
                ImageCanvas.Cursor = Cursors.Cross;
                WriteLog("도형 모드 유지: currentEditMode=Shape");
            }
            catch (Exception ex)
            {
                string errorMsg = $"도형 적용 중 오류가 발생했습니다: {ex.Message}";
                WriteLog($"오류: {errorMsg}");
                MessageBox.Show(errorMsg, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 임시 도형 정리 헬퍼 메서드 추가
        private void CleanupTemporaryShape()
        {
            WriteLog($"CleanupTemporaryShape 호출: tempShape={tempShape != null}");

            if (tempShape != null && ImageCanvas != null)
            {
                try
                {
                    WriteLog($"tempShape 제거 시도: 타입={tempShape.GetType().Name}");
                    ImageCanvas.Children.Remove(tempShape);
                    WriteLog("tempShape 성공적으로 제거됨");
                }
                catch (Exception ex)
                {
                    WriteLog($"tempShape 제거 실패: {ex.Message}");
                    // 예외 무시
                }
                tempShape = null;
                WriteLog("tempShape = null 설정");
            }
            else
            {
                WriteLog("tempShape가 이미 null이거나 ImageCanvas가 null임");
            }
        }

        private void UpdateShapeProperties(UIElement shape, Point start, Point current)
        {
            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            // 도형 유형에 따라 속성 업데이트
            if (shape is Rectangle rect)
            {
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Width = width;
                rect.Height = height;
            }
            else if (shape is Ellipse ellipse)
            {
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
                ellipse.Width = width;
                ellipse.Height = height;
            }
            else if (shape is Line line)
            {
                line.X1 = start.X;
                line.Y1 = start.Y;
                line.X2 = current.X;
                line.Y2 = current.Y;
            }
            else if (shape is System.Windows.Shapes.Path path && shapeType == ShapeType.Arrow)
            {
                // 화살표는 새로 생성하는 것이 더 간단하고 정확합니다
                int index = ImageCanvas.Children.IndexOf(shape);
                if (index != -1)
                {
                    ImageCanvas.Children.RemoveAt(index);
                    var newArrow = CreateArrow(start, current);
                    ImageCanvas.Children.Insert(index, newArrow);
                    tempShape = newArrow;
                }
            }
        }

        #endregion

        #region 도구 옵션 패널 / 팝업

        private void ShowHighlightOptionsPopup()
        {
            // Build popup content under the highlight icon
            ToolOptionsPopupContent.Children.Clear();

            // 색상 선택
            var colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(colorLabel);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ToolOptionsPopupContent.Children.Add(colorPanel);

            Color[] colors = new Color[]
            {
                Colors.Yellow, Colors.Orange, Colors.Red, Colors.Blue, Colors.Green, Colors.White, Colors.Black, Color.FromRgb(255, 0, 255)
            };

            foreach (Color color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = (color.R == highlightColor.R && color.G == highlightColor.G && color.B == highlightColor.B) ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };

                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    highlightColor = Color.FromArgb(highlightColor.A, color.R, color.G, color.B);
                    // 테두리 업데이트
                    foreach (var child in colorPanel.Children)
                    {
                        if (child is Border b && b.Background is SolidColorBrush sc)
                        {
                            var c = sc.Color;
                            b.BorderBrush = (c.R == highlightColor.R && c.G == highlightColor.G && c.B == highlightColor.B) ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };

                colorPanel.Children.Add(colorBorder);
            }

            // 두께 선택 라벨
            var thicknessLabel = new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(thicknessLabel);

            Slider thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 20,
                Value = highlightThickness,
                Width = 120,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            thicknessSlider.ValueChanged += (s, e) =>
            {
                highlightThickness = thicknessSlider.Value;
                // 미리보기 갱신
                if (thicknessPreviewRect != null && thicknessPreviewCanvas != null)
                {
                    thicknessPreviewRect.Height = Math.Max(1, highlightThickness);
                    // 가운데 정렬
                    var canvasHeight = thicknessPreviewCanvas.ActualHeight > 0 ? thicknessPreviewCanvas.ActualHeight : 28;
                    Canvas.SetTop(thicknessPreviewRect, (canvasHeight - thicknessPreviewRect.Height) / 2);
                }
            };

            ToolOptionsPopupContent.Children.Add(thicknessSlider);

            // 두께 미리보기 (슬라이더 옆)
            var previewWrapper = new Border { Width = 70, Height = 28, Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent };
            thicknessPreviewCanvas = new Canvas { Width = 70, Height = 28, Background = Brushes.Transparent };
            thicknessPreviewRect = new Rectangle { Width = 60, Height = Math.Max(1, highlightThickness), Fill = new SolidColorBrush(highlightColor), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(thicknessPreviewRect, 5);
            Canvas.SetTop(thicknessPreviewRect, (28 - thicknessPreviewRect.Height) / 2);
            thicknessPreviewCanvas.Children.Add(thicknessPreviewRect);
            previewWrapper.Child = thicknessPreviewCanvas;
            ToolOptionsPopupContent.Children.Add(previewWrapper);

            // 두께 프리셋 (1, 3, 5, 8, 12px)
            var presets = new[] { 1, 3, 5, 8, 12 };
            var presetPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 0, 0, 0) };
            foreach (var p in presets)
            {
                var sampleCanvas = new Canvas { Width = 80, Height = 18, Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand };
                var r = new Rectangle { Width = 60, Height = p, Fill = new SolidColorBrush(highlightColor), RadiusX = 2, RadiusY = 2 };
                Canvas.SetLeft(r, 4);
                Canvas.SetTop(r, (18 - p) / 2.0);
                sampleCanvas.Children.Add(r);
                var label = new TextBlock { Text = $"{p}px", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(66, 0, 0, 0) };
                sampleCanvas.Children.Add(label);
                int pv = p;
                sampleCanvas.MouseLeftButtonDown += (s, e) => { thicknessSlider.Value = pv; };
                presetPanel.Children.Add(sampleCanvas);
            }
            ToolOptionsPopupContent.Children.Add(presetPanel);

            // 투명도 선택 (두께 옆)
            var opacityLabel = new TextBlock { Text = "투명도:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(opacityLabel);

            Slider opacitySlider = new Slider
            {
                Minimum = 10,
                Maximum = 100,
                // 기본은 중간 단계 투명함
                Value = Math.Max(10, (int)Math.Round((highlightColor.A / 255.0) * 100)),
                Width = 120,
                IsSnapToTickEnabled = true,
                TickFrequency = 5,
                VerticalAlignment = VerticalAlignment.Center
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                byte a = (byte)Math.Max(26, Math.Min(255, Math.Round(opacitySlider.Value / 100.0 * 255)));
                highlightColor = Color.FromArgb(a, highlightColor.R, highlightColor.G, highlightColor.B);
                // 미리보기 색상 업데이트
                if (thicknessPreviewRect != null)
                {
                    thicknessPreviewRect.Fill = new SolidColorBrush(highlightColor);
                }
                // 팝업 내 프리셋 샘플들도 동일 알파 적용
                foreach (var child in ToolOptionsPopupContent.Children)
                {
                    if (child is StackPanel sp)
                    {
                        foreach (var sub in sp.Children)
                        {
                            if (sub is Canvas sc)
                            {
                                foreach (var elem in sc.Children)
                                {
                                    if (elem is Rectangle rr)
                                        rr.Fill = new SolidColorBrush(highlightColor);
                                }
                            }
                        }
                    }
                }
            };
            ToolOptionsPopupContent.Children.Add(opacitySlider);

            // Open under icon
            ToolOptionsPopup.PlacementTarget = HighlightButton;
            ToolOptionsPopup.IsOpen = true;
        }

        private void ShowPenOptionsPopup()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 색상
            var colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(colorLabel);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ToolOptionsPopupContent.Children.Add(colorPanel);

            Color[] colors = new Color[]
            {
                Colors.Black, Colors.Gray, Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Purple
            };
            foreach (var c in colors)
            {
                Border swatch = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(c),
                    BorderBrush = (c == penColor) ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };
                swatch.MouseLeftButtonDown += (s, e) =>
                {
                    penColor = c;
                    foreach (var child in colorPanel.Children)
                    {
                        if (child is Border b && b.Background is SolidColorBrush sc)
                        {
                            b.BorderBrush = (sc.Color == penColor) ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };
                colorPanel.Children.Add(swatch);
            }

            // 두께
            var thicknessLabel = new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(thicknessLabel);

            Slider thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 20,
                Value = penThickness,
                Width = 120,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center
            };
            thicknessSlider.ValueChanged += (s, e) => { penThickness = thicknessSlider.Value; };
            ToolOptionsPopupContent.Children.Add(thicknessSlider);

            ToolOptionsPopup.PlacementTarget = PenButton;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 도구 옵션 패널

        private void ShowTextOptions()
        {
            // 패널 제목 설정
            ToolTitleText.Text = "텍스트 도구 옵션";

            EditToolContent.Children.Clear();

            // 색상 선택
            Border colorLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center };
            colorLabelWrapper.Child = colorLabel;
            EditToolContent.Children.Add(colorLabelWrapper);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Color[] colors = { Colors.Red, Colors.Blue, Colors.Green, Colors.Black, Colors.White };
            foreach (Color color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = color == textColor ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };

                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    textColor = color;
                    foreach (UIElement element in colorPanel.Children)
                    {
                        if (element is Border border)
                        {
                            border.BorderBrush = border == s ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };

                colorPanel.Children.Add(colorBorder);
            }

            EditToolContent.Children.Add(colorPanel);

            // 크기 선택
            Border sizeLabelWrapper = new Border { Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock sizeLabel = new TextBlock { Text = "크기:", VerticalAlignment = VerticalAlignment.Center };
            sizeLabelWrapper.Child = sizeLabel;
            EditToolContent.Children.Add(sizeLabelWrapper);

            Slider sizeSlider = new Slider
            {
                Minimum = 8,
                Maximum = 48,
                Value = textSize,
                Width = 120,
                IsSnapToTickEnabled = true,
                TickFrequency = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                textSize = sizeSlider.Value;
            };

            EditToolContent.Children.Add(sizeSlider);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator
            {
                Margin = new Thickness(10, 0, 10, 0),
                Height = 20
            });

            // 폰트 스타일 옵션
            StackPanel fontStylePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };

            // 굵게 버튼
            CheckBox boldCheckBox = new CheckBox
            {
                Content = "굵게",
                IsChecked = textFontWeight == FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            boldCheckBox.Checked += (s, e) => { textFontWeight = FontWeights.Bold; };
            boldCheckBox.Unchecked += (s, e) => { textFontWeight = FontWeights.Normal; };
            fontStylePanel.Children.Add(boldCheckBox);

            // 기울임 버튼
            CheckBox italicCheckBox = new CheckBox
            {
                Content = "기울임",
                IsChecked = textFontStyle == FontStyles.Italic,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            italicCheckBox.Checked += (s, e) => { textFontStyle = FontStyles.Italic; };
            italicCheckBox.Unchecked += (s, e) => { textFontStyle = FontStyles.Normal; };
            fontStylePanel.Children.Add(italicCheckBox);

            // 밑줄 버튼
            CheckBox underlineCheckBox = new CheckBox
            {
                Content = "밑줄",
                IsChecked = textUnderlineEnabled,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            underlineCheckBox.Checked += (s, e) => { textUnderlineEnabled = true; };
            underlineCheckBox.Unchecked += (s, e) => { textUnderlineEnabled = false; };
            fontStylePanel.Children.Add(underlineCheckBox);

            // 그림자 버튼
            CheckBox shadowCheckBox = new CheckBox
            {
                Content = "그림자",
                IsChecked = textShadowEnabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            shadowCheckBox.Checked += (s, e) => { textShadowEnabled = true; };
            shadowCheckBox.Unchecked += (s, e) => { textShadowEnabled = false; };
            fontStylePanel.Children.Add(shadowCheckBox);

            EditToolContent.Children.Add(fontStylePanel);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator
            {
                Margin = new Thickness(10, 0, 10, 0),
                Height = 20
            });

            // 폰트 선택 콤보박스
            Border fontLabelWrapper = new Border { Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock fontLabel = new TextBlock { Text = "폰트:", VerticalAlignment = VerticalAlignment.Center };
            fontLabelWrapper.Child = fontLabel;
            EditToolContent.Children.Add(fontLabelWrapper);

            ComboBox fontComboBox = new ComboBox
            {
                Width = 100,
                SelectedItem = textFontFamily,
                VerticalAlignment = VerticalAlignment.Center
            };

            string[] commonFonts = { "Arial", "Verdana", "Tahoma", "Times New Roman", "Courier New" };
            foreach (string font in commonFonts)
            {
                fontComboBox.Items.Add(font);
            }

            if (!fontComboBox.Items.Contains(textFontFamily))
            {
                fontComboBox.Items.Add(textFontFamily);
            }

            fontComboBox.SelectedItem = textFontFamily;
            fontComboBox.SelectionChanged += (s, e) =>
            {
                if (fontComboBox.SelectedItem != null)
                {
                    textFontFamily = fontComboBox.SelectedItem.ToString() ?? "Arial";
                }
            };

            EditToolContent.Children.Add(fontComboBox);

            EditToolPanel.Visibility = Visibility.Visible;
        }

        private void ShowMosaicOptions()
        {
            // 패널 제목 설정
            ToolTitleText.Text = "모자이크 도구 옵션";

            EditToolContent.Children.Clear();

            // 크기 선택
            Border sizeLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock sizeLabel = new TextBlock { Text = "모자이크 크기:", VerticalAlignment = VerticalAlignment.Center };
            sizeLabelWrapper.Child = sizeLabel;
            EditToolContent.Children.Add(sizeLabelWrapper);

            Slider sizeSlider = new Slider
            {
                Minimum = 3,
                Maximum = 30,
                Value = mosaicSize,
                Width = 150,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                mosaicSize = (int)sizeSlider.Value;
            };

            EditToolContent.Children.Add(sizeSlider);

            EditToolPanel.Visibility = Visibility.Visible;
        }

        private void ShowEraserOptions()
        {
            // 패널 제목 설정
            ToolTitleText.Text = "지우개 도구 옵션";

            EditToolContent.Children.Clear();

            // 크기 선택
            Border sizeLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock sizeLabel = new TextBlock { Text = "지우개 크기:", VerticalAlignment = VerticalAlignment.Center };
            sizeLabelWrapper.Child = sizeLabel;
            EditToolContent.Children.Add(sizeLabelWrapper);

            Slider sizeSlider = new Slider
            {
                Minimum = 5,
                Maximum = 50,
                Value = eraserSize,
                Width = 150,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                eraserSize = sizeSlider.Value;
            };

            EditToolContent.Children.Add(sizeSlider);

            // 현재 크기 미리보기
            Border previewWrapper = new Border { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Border eraserPreview = new Border
            {
                Width = eraserSize,
                Height = eraserSize,
                Background = Brushes.LightGray,
                CornerRadius = new CornerRadius(eraserSize / 2)
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                eraserPreview.Width = eraserSize;
                eraserPreview.Height = eraserSize;
                eraserPreview.CornerRadius = new CornerRadius(eraserSize / 2);
            };

            previewWrapper.Child = eraserPreview;
            EditToolContent.Children.Add(previewWrapper);

            EditToolPanel.Visibility = Visibility.Visible;
        }

        private void ShowShapeOptionsPopup_OLD()
        {
            // 팝업 내용 초기화
            ToolOptionsPopupContent.Children.Clear();
            ToolOptionsPopupContent.Orientation = Orientation.Vertical;

            // 버튼 리스트 초기화
            shapeTypeButtons.Clear();
            fillButtons.Clear();
            colorButtons.Clear();

            // 메인 컨테이너 (적절한 크기로)
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = 240,
                Margin = new Thickness(6)
            };

            // 1. 도형 종류 섹션
            var shapeTypePanel = CreateShapeTypeSection();
            mainPanel.Children.Add(shapeTypePanel);

            // 2. 선 스타일 섹션 (도형 바로 아래, 간격 더 줄임)
            var lineStylePanel = CreateLineStyleSection();
            lineStylePanel.Margin = new Thickness(0, 4, 0, 0);
            mainPanel.Children.Add(lineStylePanel);

            // 구분선 (더 얇고 간격 더 줄임)
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 6, 0, 6)
            };
            mainPanel.Children.Add(separator);

            // 3. 색상 팔레트 섹션
            var colorPanel = CreateColorPaletteSection();
            mainPanel.Children.Add(colorPanel);

            ToolOptionsPopupContent.Children.Add(mainPanel);

            // 팝업 위치 설정 (ShapeButton 아래)
            ToolOptionsPopup.PlacementTarget = ShapeButton;
            ToolOptionsPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ToolOptionsPopup.HorizontalOffset = 0;
            ToolOptionsPopup.VerticalOffset = 5;

            // 팝업 열기
            ToolOptionsPopup.IsOpen = true;
        }

        private void ShowShapeOptions_OLD()
        {
            // 패널 제목 설정
            ToolTitleText.Text = "도형 도구 옵션";

            EditToolContent.Children.Clear();

            // 도형 유형 선택
            Border typeLabelWrapper = new Border { Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock typeLabel = new TextBlock { Text = "도형 유형:", VerticalAlignment = VerticalAlignment.Center };
            typeLabelWrapper.Child = typeLabel;
            EditToolContent.Children.Add(typeLabelWrapper);

            StackPanel shapeTypesPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };

            // 사각형 버튼
            rectButton = new Button
            {
                Content = "□",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Rectangle ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0) // 상하 중앙 정렬 조정
            };

            rectButton.Click += (s, e) =>
            {
                shapeType = ShapeType.Rectangle;
                UpdateShapeTypeButtons();
            };
            shapeTypesPanel.Children.Add(rectButton);

            // 타원 버튼
            ellipseButton = new Button
            {
                Content = "○",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Ellipse ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0) // 상하 중앙 정렬 조정
            };

            ellipseButton.Click += (s, e) =>
            {
                shapeType = ShapeType.Ellipse;
                UpdateShapeTypeButtons();
            };
            shapeTypesPanel.Children.Add(ellipseButton);

            // 선 버튼
            lineButton = new Button
            {
                Content = "−",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Line ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0) // 상하 중앙 정렬 조정
            };

            lineButton.Click += (s, e) =>
            {
                shapeType = ShapeType.Line;
                UpdateShapeTypeButtons();
            };
            shapeTypesPanel.Children.Add(lineButton);

            // 화살표 버튼
            arrowButton = new Button
            {
                Content = "→",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Arrow ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0) // 상하 중앙 정렬 조정
            };

            arrowButton.Click += (s, e) =>
            {
                shapeType = ShapeType.Arrow;
                UpdateShapeTypeButtons();
            };
            shapeTypesPanel.Children.Add(arrowButton);

            EditToolContent.Children.Add(shapeTypesPanel);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator { Margin = new Thickness(5, 0, 5, 0), Width = 1, Height = 20 });

            // 색상 선택
            Border colorLabelWrapper = new Border { Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center };
            colorLabelWrapper.Child = colorLabel;
            EditToolContent.Children.Add(colorLabelWrapper);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Color[] colors = new Color[]
            {
                Colors.Black, Colors.Red, Colors.Blue, Colors.Green,
                Colors.Yellow, Colors.Orange, Colors.Purple, Colors.White
            };

            foreach (Color color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = color.Equals(shapeColor) ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };

                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    shapeColor = color;
                    foreach (UIElement element in colorPanel.Children)
                    {
                        if (element is Border border)
                        {
                            border.BorderBrush = border == s ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };

                colorPanel.Children.Add(colorBorder);
            }

            EditToolContent.Children.Add(colorPanel);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator { Margin = new Thickness(5, 0, 5, 0), Width = 1, Height = 20 });

            // 두께 선택
            Border thicknessLabelWrapper = new Border { Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock thicknessLabel = new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center };
            thicknessLabelWrapper.Child = thicknessLabel;
            EditToolContent.Children.Add(thicknessLabelWrapper);

            Slider thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 10,
                Value = shapeBorderThickness,
                Width = 100,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };

            thicknessSlider.ValueChanged += (s, e) =>
            {
                shapeBorderThickness = thicknessSlider.Value;
            };

            EditToolContent.Children.Add(thicknessSlider);

            // 채우기 옵션
            CheckBox fillCheckBox = new CheckBox
            {
                Content = "채우기",
                IsChecked = shapeIsFilled,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            fillCheckBox.Checked += (s, e) => { shapeIsFilled = true; };
            fillCheckBox.Unchecked += (s, e) => { shapeIsFilled = false; };

            EditToolContent.Children.Add(fillCheckBox);

            EditToolPanel.Visibility = Visibility.Visible;
        }

        private void UpdateShapeTypeButtons()
        {
            if (rectButton != null)
                rectButton.Background = shapeType == ShapeType.Rectangle ? Brushes.LightBlue : Brushes.Transparent;

            if (ellipseButton != null)
                ellipseButton.Background = shapeType == ShapeType.Ellipse ? Brushes.LightBlue : Brushes.Transparent;

            if (lineButton != null)
                lineButton.Background = shapeType == ShapeType.Line ? Brushes.LightBlue : Brushes.Transparent;

            if (arrowButton != null)
                arrowButton.Background = shapeType == ShapeType.Arrow ? Brushes.LightBlue : Brushes.Transparent;
        }

        #endregion

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

        private void SetActiveToolButton(Button? active)
        {
            // 툴바의 편집 도구 버튼들만 대상으로 처리
            var toolButtons = new List<Button?>
            {
                CropButton,
                ShapeButton,
                HighlightButton,
                TextButton,
                MosaicButton,
                EraserButton,
                PenButton
            };

            foreach (var b in toolButtons)
            {
                if (b == null) continue;
                b.Background = (active != null && b == active) ? ActiveToolBackground : InactiveToolBackground;
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
}