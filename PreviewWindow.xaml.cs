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

namespace CatchCapture
{
    public partial class PreviewWindow : Window
    {
        private BitmapSource originalImage;
        private BitmapSource currentImage;
        private int imageIndex;
        private Stack<BitmapSource> undoStack = new Stack<BitmapSource>();
        private Stack<BitmapSource> redoStack = new Stack<BitmapSource>();
        private Point startPoint;
        private Rectangle? selectionRectangle;
        private List<Point> drawingPoints = new List<Point>();
        private EditMode currentEditMode = EditMode.None;
        private Color highlightColor = Colors.Yellow;
        private double highlightThickness = 5;
        private Color textColor = Colors.Red;
        private double textSize = 16;
        private double eraserSize = 10;
        private int mosaicSize = 10;

        public event EventHandler<ImageUpdatedEventArgs>? ImageUpdated;

        public PreviewWindow(BitmapSource image, int index)
        {
            InitializeComponent();

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
        }

        private void CancelCurrentEditMode()
        {
            // 현재 편집 모드 취소
            currentEditMode = EditMode.None;
            
            // 선택 영역 제거
            if (selectionRectangle != null)
            {
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
            }
            
            // 도구 패널 숨김
            EditToolPanel.Visibility = Visibility.Collapsed;
            
            // 마우스 커서 복원
            ImageCanvas.Cursor = Cursors.Arrow;
            
            // 그리기 포인트 초기화
            drawingPoints.Clear();
        }

        #region 이벤트 핸들러

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(ImageCanvas);
            
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    StartCrop();
                    break;
                case EditMode.Highlight:
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
            if (e.LeftButton != MouseButtonState.Pressed) return;
            
            Point currentPoint = e.GetPosition(ImageCanvas);
            
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    UpdateCropSelection(currentPoint);
                    break;
                case EditMode.Highlight:
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
            if (currentEditMode == EditMode.None) return;
            
            Point endPoint = e.GetPosition(ImageCanvas);
            
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    FinishCrop(endPoint);
                    break;
                case EditMode.Highlight:
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
            // 저장 대화 상자 표시
            var dialog = new SaveFileDialog
            {
                Title = "이미지 저장",
                Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|모든 파일|*.*",
                DefaultExt = ".png"
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

        private void HighlightButton_Click(object? sender, RoutedEventArgs? e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Highlight;
            ImageCanvas.Cursor = Cursors.Pen;
            
            ShowHighlightOptions();
        }

        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Text;
            ImageCanvas.Cursor = Cursors.IBeam;
            
            ShowTextOptions();
        }

        private void MosaicButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Mosaic;
            ImageCanvas.Cursor = Cursors.Cross;
            
            ShowMosaicOptions();
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.Eraser;
            ImageCanvas.Cursor = Cursors.None;
            
            ShowEraserOptions();
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
            currentEditMode = EditMode.None;
        }

        private void StartDrawing()
        {
            drawingPoints.Clear();
            drawingPoints.Add(startPoint);
        }

        private void UpdateDrawing(Point currentPoint)
        {
            drawingPoints.Add(currentPoint);
            
            // 임시 선 그리기
            if (drawingPoints.Count > 1)
            {
                Line line = new Line
                {
                    X1 = drawingPoints[drawingPoints.Count - 2].X,
                    Y1 = drawingPoints[drawingPoints.Count - 2].Y,
                    X2 = currentPoint.X,
                    Y2 = currentPoint.Y,
                    Stroke = new SolidColorBrush(highlightColor),
                    StrokeThickness = highlightThickness,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round
                };
                
                ImageCanvas.Children.Add(line);
            }
        }

        private void FinishDrawing()
        {
            if (drawingPoints.Count < 2) return;
            
            SaveForUndo();
            currentImage = ImageEditUtility.ApplyHighlight(currentImage, drawingPoints.ToArray(), highlightColor, highlightThickness);
            UpdatePreviewImage();
            
            // 임시 선 제거
            List<UIElement> elementsToRemove = new List<UIElement>();
            foreach (UIElement element in ImageCanvas.Children)
            {
                if (element is Line)
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
                        currentImage = ImageEditUtility.AddText(currentImage, text, startPoint, textColor, textSize);
                        UpdatePreviewImage();
                    }
                    
                    ImageCanvas.Children.Remove(textBox);
                    currentEditMode = EditMode.None;
                }
                else if (e.Key == Key.Escape)
                {
                    ImageCanvas.Children.Remove(textBox);
                    currentEditMode = EditMode.None;
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
            currentEditMode = EditMode.None;
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

        #endregion

        #region 도구 옵션 패널

        private void ShowHighlightOptions()
        {
            // 패널 제목 설정
            ToolTitleText.Text = "형광펜 도구 옵션";
            
            EditToolContent.Children.Clear();
            
            // 색상 선택
            Border colorLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center };
            colorLabelWrapper.Child = colorLabel;
            EditToolContent.Children.Add(colorLabelWrapper);
            
            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };
            
            Color[] colors = { Colors.Yellow, Colors.Red, Colors.Blue, Colors.Green, Colors.Black };
            foreach (Color color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = color == highlightColor ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };
                
                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    highlightColor = color;
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
            
            // 두께 선택
            Border thicknessLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock thicknessLabel = new TextBlock { Text = "두께:", VerticalAlignment = VerticalAlignment.Center };
            thicknessLabelWrapper.Child = thicknessLabel;
            EditToolContent.Children.Add(thicknessLabelWrapper);
            
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
            };
            
            EditToolContent.Children.Add(thicknessSlider);
            
            EditToolPanel.Visibility = Visibility.Visible;
        }

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
            
            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center };
            
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
            Border sizeLabelWrapper = new Border { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
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
            PreviewImage.Source = currentImage;
            PreviewImage.Width = currentImage.PixelWidth;
            PreviewImage.Height = currentImage.PixelHeight;
            
            ImageCanvas.Width = currentImage.PixelWidth;
            ImageCanvas.Height = currentImage.PixelHeight;
            
            // 이미지 업데이트 이벤트 발생
            ImageUpdated?.Invoke(this, new ImageUpdatedEventArgs(imageIndex, currentImage));
        }

        #endregion
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

    public enum EditMode
    {
        None,
        Crop,
        Highlight,
        Text,
        Mosaic,
        Eraser
    }
} 