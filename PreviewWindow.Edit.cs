using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 편집 기능 (partial class)
    /// SnippingWindow의 우수한 텍스트 기능 이식
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 자르기 (Crop)

        private void StartCrop()
        {
            if (selectionRectangle != null)
            {
                ImageCanvas.Children.Remove(selectionRectangle);
            }

            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);
            ImageCanvas.Children.Add(selectionRectangle);
        }

        private void UpdateCropSelection(Point currentPoint)
        {
            if (selectionRectangle == null) return;

            double x = Math.Min(startPoint.X, currentPoint.X);
            double y = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRectangle, x);
            Canvas.SetTop(selectionRectangle, y);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;
        }

        private void FinishCrop(Point endPoint)
        {
            if (selectionRectangle == null) return;

            double x = Canvas.GetLeft(selectionRectangle);
            double y = Canvas.GetTop(selectionRectangle);
            double width = selectionRectangle.Width;
            double height = selectionRectangle.Height;

            if (width > 0 && height > 0)
            {
                SaveForUndo();

                Int32Rect cropRect = new Int32Rect((int)x, (int)y, (int)width, (int)height);
                CroppedBitmap croppedBitmap = new CroppedBitmap(currentImage, cropRect);

                currentImage = croppedBitmap;
                UpdatePreviewImage();
            }

            ImageCanvas.Children.Remove(selectionRectangle);
            selectionRectangle = null;
            currentEditMode = EditMode.None;
            ImageCanvas.Cursor = Cursors.Arrow;
        }

        #endregion

        #region 텍스트 추가 (SnippingWindow 스타일)

        // 텍스트 관련 필드 (PreviewWindow.xaml.cs에 추가 필요)
        private TextBox? selectedTextBox;
        private Rectangle? textSelectionBorder;
        private Button? textDeleteButton;
        private bool isTextDragging = false;
        private Point textDragStartPoint;
        private bool textDragMoved = false; // 실제로 드래그가 발생했는지 추적

        /// <summary>
        /// 클릭 위치에 새 텍스트박스 생성
        /// </summary>
        private void AddText()
        {
            // 기존 선택 해제
            ClearTextSelection();

            // 새 텍스트박스 생성
            var textBox = new TextBox
            {
                MinWidth = 100,
                MinHeight = 30,
                FontSize = textSize,
                FontFamily = new FontFamily(textFontFamily),
                Foreground = new SolidColorBrush(textColor),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontWeight = textFontWeight,
                FontStyle = textFontStyle,
                Focusable = true, // 명시적으로 포커스 가능하도록 설정
                IsTabStop = true
            };
            
            // IME(한글 입력) 활성화
            InputMethod.SetIsInputMethodEnabled(textBox, true);
            InputMethod.SetPreferredImeState(textBox, InputMethodState.On);

            // 이미지 경계 내로 제한
            double textBoxLeft = Math.Max(0, Math.Min(startPoint.X, currentImage.PixelWidth - textBox.MinWidth));
            double textBoxTop = Math.Max(0, Math.Min(startPoint.Y, currentImage.PixelHeight - textBox.MinHeight));

            Canvas.SetLeft(textBox, textBoxLeft);
            Canvas.SetTop(textBox, textBoxTop);
            Panel.SetZIndex(textBox, 1000); // 최상위 레이어로 설정

            ImageCanvas.Children.Add(textBox);
            selectedTextBox = textBox;

            // 드래그 이벤트 등록
            textBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
            textBox.PreviewMouseMove += TextBox_PreviewMouseMove;
            textBox.PreviewMouseLeftButtonUp += TextBox_PreviewMouseLeftButtonUp;
            
            // 더블클릭 이벤트 등록 (재편집용)
            textBox.MouseDoubleClick += TextBox_MouseDoubleClick;
            
            // 포커스 이벤트 등록 (IME 활성화용)
            textBox.GotFocus += TextBox_GotFocus;

            // 편집 모드 활성화 (확정/취소 버튼 표시)
            EnableTextBoxEditing(textBox);
        }

        /// <summary>
        /// 텍스트박스 편집 모드 활성화 (확정/취소 버튼 표시)
        /// </summary>
        private void EnableTextBoxEditing(TextBox textBox, bool selectAll = false)
        {
            textBox.IsReadOnly = false;
            textBox.BorderThickness = new Thickness(2);
            textBox.BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue);

            // IME(한글 입력) 활성화 설정
            try {
                textBox.Language = System.Windows.Markup.XmlLanguage.GetLanguage("ko-KR");
            } catch { }
            InputMethod.SetIsInputMethodEnabled(textBox, true);
            InputMethod.SetPreferredImeState(textBox, InputMethodState.On);
            InputMethod.SetPreferredImeConversionMode(textBox, ImeConversionModeValues.Native);

            double left = Canvas.GetLeft(textBox);
            double top = Canvas.GetTop(textBox);

            // 확정 버튼 (✓)
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
                ToolTip = "확정 (Ctrl+Enter)"
            };

            // 취소 버튼 (✕)
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
                ToolTip = "취소 (Esc)"
            };

            // 이벤트 연결
            confirmButton.Click += (s, e) => ConfirmTextBox(textBox, confirmButton, cancelButton);
            cancelButton.Click += (s, e) =>
            {
                ImageCanvas.Children.Remove(textBox);
                ImageCanvas.Children.Remove(confirmButton);
                ImageCanvas.Children.Remove(cancelButton);
                selectedTextBox = null;
            };

            // 위치 설정 (텍스트박스 위쪽)
            double confirmLeft = left + 105;
            double confirmTop = Math.Max(top - 28, 0);
            double cancelLeft = left + 77;
            double cancelTop = Math.Max(top - 28, 0);

            Canvas.SetLeft(confirmButton, confirmLeft);
            Canvas.SetTop(confirmButton, confirmTop);
            Panel.SetZIndex(confirmButton, 1001); // 텍스트박스보다 위
            
            Canvas.SetLeft(cancelButton, cancelLeft);
            Canvas.SetTop(cancelButton, cancelTop);
            Panel.SetZIndex(cancelButton, 1001); // 텍스트박스보다 위

            ImageCanvas.Children.Add(confirmButton);
            ImageCanvas.Children.Add(cancelButton);

            // 버튼 참조 저장
            textBox.Tag = (confirmButton, cancelButton);

            // 키 이벤트 핸들러 등록
            textBox.KeyDown -= TextBox_KeyDown;
            textBox.KeyDown += TextBox_KeyDown;

            // 포커스 설정 (Dispatcher로 지연 호출하여 확실히 포커스)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 포커스를 강제로 해제했다가 다시 설정 (IME 재초기화)
                ImageCanvas.Focus(); // 임시로 다른 곳에 포커스
                
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.Focus();
                    if (selectAll)
                    {
                        textBox.SelectAll();
                    }
                    else
                    {
                        textBox.CaretIndex = textBox.Text.Length; // 커서를 텍스트 끝으로
                    }
                    
                    // IME 활성화 (한글 입력 가능하도록)
                    try {
                        InputMethod.Current.ImeState = InputMethodState.On;
                        InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Native;
                    } catch { }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle); // 우선순위 낮춤
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 텍스트박스 확정
        /// </summary>
        private void ConfirmTextBox(TextBox textBox, Button confirmButton, Button cancelButton)
        {
            if (textBox == null) return;

            // 빈 텍스트는 삭제
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                ImageCanvas.Children.Remove(textBox);
                ImageCanvas.Children.Remove(confirmButton);
                ImageCanvas.Children.Remove(cancelButton);
                selectedTextBox = null;
                return;
            }

            // 텍스트박스 확정 처리
            textBox.IsReadOnly = true;
            textBox.BorderThickness = new Thickness(0);
            textBox.Background = Brushes.Transparent;
            textBox.Cursor = Cursors.Arrow;

            // 확정/취소 버튼 제거
            ImageCanvas.Children.Remove(confirmButton);
            ImageCanvas.Children.Remove(cancelButton);
            
            // 더블클릭 이벤트 등록 (확정 후에도 더블클릭으로 재편집 가능)
            textBox.MouseDoubleClick -= TextBox_MouseDoubleClick;
            textBox.MouseDoubleClick += TextBox_MouseDoubleClick;

            selectedTextBox = null;
        }

        /// <summary>
        /// 텍스트박스 키 이벤트 처리
        /// </summary>
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 편집 중일 때
                if (!textBox.IsReadOnly)
                {
                    // Ctrl+Enter: 확정
                    if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (textBox.Tag is ValueTuple<Button, Button> tags)
                        {
                            ConfirmTextBox(textBox, tags.Item1, tags.Item2);
                            e.Handled = true;
                        }
                    }
                    // Esc: 취소
                    else if (e.Key == Key.Escape)
                    {
                        if (textBox.Tag is ValueTuple<Button, Button> tags)
                        {
                            ImageCanvas.Children.Remove(textBox);
                            ImageCanvas.Children.Remove(tags.Item1);
                            ImageCanvas.Children.Remove(tags.Item2);
                            selectedTextBox = null;
                            e.Handled = true;
                        }
                    }
                    return;
                }

                // 확정 상태일 때
                // Ctrl+Enter: 재편집
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ClearTextSelection();
                    EnableTextBoxEditing(textBox);
                    textBox.SelectAll();
                    e.Handled = true;
                }
                // Esc: 선택 해제
                else if (e.Key == Key.Escape)
                {
                    ClearTextSelection();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 텍스트박스 드래그 시작
        /// </summary>
        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 편집 중이면 드래그 불가 (텍스트 선택 허용)
                if (!textBox.IsReadOnly) return;

                // 선택 표시
                ShowTextSelection(textBox);
                selectedTextBox = textBox;

                // 드래그 시작
                isTextDragging = true;
                textDragMoved = false; // 드래그 이동 플래그 초기화
                textDragStartPoint = e.GetPosition(ImageCanvas);
                textBox.CaptureMouse();
                
                // 더블클릭 이벤트가 발생할 수 있도록 첫 번째 클릭에서는 Handled 하지 않음
                // e.Handled = true;
            }
        }

        /// <summary>
        /// 텍스트박스 드래그 중
        /// </summary>
        private void TextBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isTextDragging && sender is TextBox textBox)
            {
                Point currentPoint = e.GetPosition(ImageCanvas);

                double offsetX = currentPoint.X - textDragStartPoint.X;
                double offsetY = currentPoint.Y - textDragStartPoint.Y;

                // 실제로 이동이 발생했는지 확인 (최소 2픽셀 이상 이동)
                if (Math.Abs(offsetX) > 2 || Math.Abs(offsetY) > 2)
                {
                    textDragMoved = true;
                }

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

        /// <summary>
        /// 텍스트박스 드래그 종료
        /// </summary>
        private void TextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isTextDragging && sender is TextBox textBox)
            {
                isTextDragging = false;
                textBox.ReleaseMouseCapture();
                
                // 실제로 드래그가 발생한 경우에만 선택 해제
                // 단순 클릭(드래그 없음)인 경우 선택 유지 (삭제 버튼 사용 가능)
                if (textDragMoved)
                {
                    ClearTextSelection();
                }
                
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 텍스트박스 더블클릭 - 재편집 모드 (새 TextBox로 교체하여 IME 문제 해결)
        /// </summary>
        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox oldTextBox && oldTextBox.IsReadOnly)
            {
                e.Handled = true;
                
                // 기존 선택 UI 정리
                ClearTextSelection();
                
                // 기존 속성 백업
                string text = oldTextBox.Text;
                double left = Canvas.GetLeft(oldTextBox);
                double top = Canvas.GetTop(oldTextBox);
                var fontSize = oldTextBox.FontSize;
                var fontFamily = oldTextBox.FontFamily;
                var foreground = oldTextBox.Foreground;
                var fontWeight = oldTextBox.FontWeight;
                var fontStyle = oldTextBox.FontStyle;
                
                // 기존 텍스트박스 제거
                ImageCanvas.Children.Remove(oldTextBox);
                
                // 새 텍스트박스 생성 (완전히 새로운 객체)
                var newTextBox = new TextBox
                {
                    MinWidth = 100,
                    MinHeight = 30,
                    // Width, Height는 설정하지 않음 (자동 확장)
                    FontSize = fontSize,
                    FontFamily = fontFamily,
                    Foreground = foreground,
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue),
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontWeight = fontWeight,
                    FontStyle = fontStyle,
                    Focusable = true,
                    IsTabStop = true,
                    Text = text // 기존 텍스트 복원
                };

                // IME(한글 입력) 활성화
                try {
                    newTextBox.Language = System.Windows.Markup.XmlLanguage.GetLanguage("ko-KR");
                } catch { }
                InputMethod.SetIsInputMethodEnabled(newTextBox, true);
                InputMethod.SetPreferredImeState(newTextBox, InputMethodState.On);
                InputMethod.SetPreferredImeConversionMode(newTextBox, ImeConversionModeValues.Native);

                // 위치 설정
                Canvas.SetLeft(newTextBox, left);
                Canvas.SetTop(newTextBox, top);
                Panel.SetZIndex(newTextBox, 1000);

                ImageCanvas.Children.Add(newTextBox);
                selectedTextBox = newTextBox;

                // 이벤트 등록
                newTextBox.PreviewMouseLeftButtonDown += TextBox_PreviewMouseLeftButtonDown;
                newTextBox.PreviewMouseMove += TextBox_PreviewMouseMove;
                newTextBox.PreviewMouseLeftButtonUp += TextBox_PreviewMouseLeftButtonUp;
                newTextBox.MouseDoubleClick += TextBox_MouseDoubleClick;
                newTextBox.GotFocus += TextBox_GotFocus;

                // 편집 모드 활성화 (전체 선택)
                EnableTextBoxEditing(newTextBox, true);
            }
        }
        
        /// <summary>
        /// 텍스트박스 포커스 받을 때 IME 활성화
        /// </summary>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsReadOnly)
            {
                // IME(한글 입력) 강제 활성화
                try {
                    textBox.Language = System.Windows.Markup.XmlLanguage.GetLanguage("ko-KR");
                } catch { }
                InputMethod.SetIsInputMethodEnabled(textBox, true);
                InputMethod.SetPreferredImeState(textBox, InputMethodState.On);
                InputMethod.SetPreferredImeConversionMode(textBox, ImeConversionModeValues.Native);
                
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        InputMethod.Current.ImeState = InputMethodState.On;
                        InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Native;
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        /// <summary>
        /// 텍스트박스 선택 표시 (점선 테두리 + 삭제 버튼)
        /// </summary>
        private void ShowTextSelection(TextBox textBox)
        {
            ClearTextSelection();

            double left = Canvas.GetLeft(textBox);
            double top = Canvas.GetTop(textBox);
            double width = textBox.ActualWidth > 0 ? textBox.ActualWidth : textBox.MinWidth;
            double height = textBox.ActualHeight > 0 ? textBox.ActualHeight : textBox.MinHeight;

            // 점선 테두리
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
            ImageCanvas.Children.Add(textSelectionBorder);

            // 삭제 버튼 (🗑️)
            textDeleteButton = new Button
            {
                Content = "🗑️",
                Width = 24,
                Height = 24,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "삭제"
            };

            textDeleteButton.Click += (s, e) =>
            {
                ImageCanvas.Children.Remove(textBox);
                ImageCanvas.Children.Remove(textDeleteButton);
                ClearTextSelection();
                selectedTextBox = null;
            };

            Canvas.SetLeft(textDeleteButton, left + width - 20);
            Canvas.SetTop(textDeleteButton, top - 28);
            ImageCanvas.Children.Add(textDeleteButton);
        }

        /// <summary>
        /// 텍스트박스 선택 해제
        /// </summary>
        private void ClearTextSelection()
        {
            // 테두리 제거
            if (textSelectionBorder != null && ImageCanvas.Children.Contains(textSelectionBorder))
            {
                ImageCanvas.Children.Remove(textSelectionBorder);
                textSelectionBorder = null;
            }

            // 삭제 버튼 제거
            if (textDeleteButton != null && ImageCanvas.Children.Contains(textDeleteButton))
            {
                ImageCanvas.Children.Remove(textDeleteButton);
                textDeleteButton = null;
            }

            // 선택된 텍스트박스 읽기 전용으로 전환
            if (selectedTextBox != null)
            {
                selectedTextBox.IsReadOnly = true;
                selectedTextBox.BorderThickness = new Thickness(0);
                selectedTextBox.Background = Brushes.Transparent;
            }
        }

        #endregion

        #region 마법봉 (배경 제거)

        // 마법봉 설정
        private int magicWandTolerance = 32; // 색상 허용 오차 (0-255)
        private bool magicWandContiguous = true; // 연속 영역만 선택
        
        // 마법봉 드래그 관련
        private bool isMagicWandDragging = false;
        private Point magicWandStartPoint;
        private Rectangle? magicWandSelectionRect;
        private Border? magicWandCursor; // 마법봉 커서 (마우스 따라다니는 아이콘)

        /// <summary>
        /// 마법봉 커서 표시/업데이트
        /// </summary>
        public void UpdateMagicWandCursor(Point point)
        {
            if (currentEditMode != EditMode.MagicWand)
            {
                HideMagicWandCursor();
                return;
            }

            if (magicWandCursor == null)
            {
                magicWandCursor = new Border
                {
                    Width = 32,
                    Height = 32,
                    Background = Brushes.Transparent,
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = "✨",
                        FontSize = 20,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Panel.SetZIndex(magicWandCursor, 9999);
                ImageCanvas.Children.Add(magicWandCursor);
            }

            Canvas.SetLeft(magicWandCursor, point.X - 16);
            Canvas.SetTop(magicWandCursor, point.Y - 16);
        }

        /// <summary>
        /// 마법봉 커서 숨기기
        /// </summary>
        public void HideMagicWandCursor()
        {
            if (magicWandCursor != null)
            {
                ImageCanvas.Children.Remove(magicWandCursor);
                magicWandCursor = null;
            }
        }

        /// <summary>
        /// 마법봉 선택 시작 (드래그 준비)
        /// </summary>
        private void StartMagicWandSelection()
        {
            isMagicWandDragging = true;
            magicWandStartPoint = startPoint;
            
            // 마법봉 커서 숨기기 (클릭 방해 방지)
            HideMagicWandCursor();
            
            // 선택 영역 미리보기 사각형 생성
            magicWandSelectionRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 255)),
                IsHitTestVisible = false // 클릭 이벤트 통과
            };
            
            Canvas.SetLeft(magicWandSelectionRect, startPoint.X);
            Canvas.SetTop(magicWandSelectionRect, startPoint.Y);
            magicWandSelectionRect.Width = 0;
            magicWandSelectionRect.Height = 0;
            
            ImageCanvas.Children.Add(magicWandSelectionRect);
        }

        /// <summary>
        /// 마법봉 드래그 중 (선택 영역 업데이트)
        /// </summary>
        public void UpdateMagicWandSelection(Point currentPoint)
        {
            if (!isMagicWandDragging || magicWandSelectionRect == null) return;
            
            double x = Math.Min(magicWandStartPoint.X, currentPoint.X);
            double y = Math.Min(magicWandStartPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - magicWandStartPoint.X);
            double height = Math.Abs(currentPoint.Y - magicWandStartPoint.Y);
            
            Canvas.SetLeft(magicWandSelectionRect, x);
            Canvas.SetTop(magicWandSelectionRect, y);
            magicWandSelectionRect.Width = width;
            magicWandSelectionRect.Height = height;
        }

        /// <summary>
        /// 마법봉 선택 완료 (드래그 끝)
        /// </summary>
        public void FinishMagicWandSelection(Point endPoint)
        {
            if (!isMagicWandDragging) return;
            
            isMagicWandDragging = false;
            
            // 선택 영역 제거
            if (magicWandSelectionRect != null)
            {
                ImageCanvas.Children.Remove(magicWandSelectionRect);
                magicWandSelectionRect = null;
            }
            
            // 드래그 거리 계산
            double dragDistance = Math.Sqrt(
                Math.Pow(endPoint.X - magicWandStartPoint.X, 2) + 
                Math.Pow(endPoint.Y - magicWandStartPoint.Y, 2));
            
            if (dragDistance < 5)
            {
                // 드래그 없음 → 포인트 클릭 마법봉
                ApplyMagicWand();
            }
            else
            {
                // 드래그 있음 → 사각형 영역 내 배경 제거
                int x1 = (int)Math.Min(magicWandStartPoint.X, endPoint.X);
                int y1 = (int)Math.Min(magicWandStartPoint.Y, endPoint.Y);
                int x2 = (int)Math.Max(magicWandStartPoint.X, endPoint.X);
                int y2 = (int)Math.Max(magicWandStartPoint.Y, endPoint.Y);
                
                ApplyMagicWandInRegion(x1, y1, x2, y2);
            }
        }

        /// <summary>
        /// 지정된 영역 내의 배경 제거 (드래그 영역)
        /// </summary>
        private void ApplyMagicWandInRegion(int x1, int y1, int x2, int y2)
        {
            if (currentImage == null) return;
            
            try
            {
                // 클릭 위치로 기준 색상 결정 (영역의 첫 번째 픽셀)
                int refX = Math.Clamp(x1, 0, currentImage.PixelWidth - 1);
                int refY = Math.Clamp(y1, 0, currentImage.PixelHeight - 1);
                
                // Undo 스택에 현재 상태 저장 (이미지 + 레이어)
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
                redoStack.Clear();
                redoLayersStack.Clear();

                // BitmapSource를 WriteableBitmap으로 변환
                WriteableBitmap writeable = new WriteableBitmap(currentImage);
                
                int width = writeable.PixelWidth;
                int height = writeable.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                writeable.CopyPixels(pixels, stride, 0);

                // 기준 색상 가져오기
                int refIndex = (refY * stride) + (refX * 4);
                byte targetB = pixels[refIndex];
                byte targetG = pixels[refIndex + 1];
                byte targetR = pixels[refIndex + 2];

                // 영역 내에서만 비슷한 색상 제거
                for (int y = Math.Max(0, y1); y < Math.Min(height, y2); y++)
                {
                    for (int x = Math.Max(0, x1); x < Math.Min(width, x2); x++)
                    {
                        int index = (y * stride) + (x * 4);
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];

                        if (IsColorSimilar(r, g, b, targetR, targetG, targetB, magicWandTolerance))
                        {
                            pixels[index + 3] = 0; // Alpha = 0 (투명)
                        }
                    }
                }

                // 수정된 픽셀 적용
                writeable.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

                currentImage = writeable;
                PreviewImage.Source = currentImage;
                UpdateUndoRedoButtons();
            }
            catch (Exception ex)
            {
                WriteLog($"ApplyMagicWandInRegion 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 마법봉 허용 오차 설정
        /// </summary>
        public void SetMagicWandTolerance(int tolerance)
        {
            magicWandTolerance = Math.Clamp(tolerance, 0, 255);
        }

        /// <summary>
        /// 마법봉 연속 영역 모드 설정
        /// </summary>
        public void SetMagicWandContiguous(bool contiguous)
        {
            magicWandContiguous = contiguous;
        }

        /// <summary>
        /// 마법봉으로 클릭한 영역의 배경 제거
        /// </summary>
        public void ApplyMagicWand()
        {
            if (currentImage == null) return;

            try
            {
                // magicWandStartPoint 사용 (드래그 시작점)
                Point clickPoint = magicWandStartPoint;
                
                // 클릭 좌표가 이미지 범위 내인지 확인
                int x = (int)clickPoint.X;
                int y = (int)clickPoint.Y;
                
                WriteLog($"ApplyMagicWand: 클릭 좌표({x}, {y}), 이미지 크기({currentImage.PixelWidth}, {currentImage.PixelHeight})");
                
                if (x < 0 || x >= currentImage.PixelWidth || y < 0 || y >= currentImage.PixelHeight)
                {
                    WriteLog($"ApplyMagicWand: 좌표가 이미지 범위 밖 - 무시");
                    return;
                }

                // Undo 스택에 현재 상태 저장 (이미지 + 레이어)
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
                redoStack.Clear();
                redoLayersStack.Clear();

                // BitmapSource를 WriteableBitmap으로 변환
                WriteableBitmap writeable = new WriteableBitmap(currentImage);
                
                // 픽셀 데이터 추출
                int width = writeable.PixelWidth;
                int height = writeable.PixelHeight;
                int stride = width * 4; // BGRA
                byte[] pixels = new byte[height * stride];
                writeable.CopyPixels(pixels, stride, 0);

                // 클릭한 픽셀의 색상 가져오기
                int clickIndex = (y * stride) + (x * 4);
                byte targetB = pixels[clickIndex];
                byte targetG = pixels[clickIndex + 1];
                byte targetR = pixels[clickIndex + 2];

                if (magicWandContiguous)
                {
                    // 연속 영역만 제거 (Flood Fill)
                    FloodFillRemove(pixels, width, height, stride, x, y, targetR, targetG, targetB);
                }
                else
                {
                    // 이미지 전체에서 비슷한 색상 제거
                    RemoveSimilarColors(pixels, width, height, stride, targetR, targetG, targetB);
                }

                // 수정된 픽셀을 WriteableBitmap에 적용
                writeable.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

                // 결과 이미지 업데이트 및 화면 갱신
                currentImage = writeable;
                PreviewImage.Source = currentImage;
                UpdateUndoRedoButtons();
            }
            catch (Exception ex)
            {
                WriteLog($"ApplyMagicWand 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Flood Fill 알고리즘으로 연속된 영역 투명하게 처리
        /// </summary>
        private void FloodFillRemove(byte[] pixels, int width, int height, int stride,
            int startX, int startY, byte targetR, byte targetG, byte targetB)
        {
            bool[,] visited = new bool[width, height];
            Queue<(int x, int y)> queue = new Queue<(int, int)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                // 범위 체크
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                // 이미 방문한 픽셀
                if (visited[x, y])
                    continue;

                visited[x, y] = true;

                int index = (y * stride) + (x * 4);
                byte b = pixels[index];
                byte g = pixels[index + 1];
                byte r = pixels[index + 2];
                byte a = pixels[index + 3];

                // 이미 투명한 픽셀은 건너뜀
                if (a == 0)
                    continue;

                // 색상 비교 (허용 오차 내인지 확인)
                if (IsColorSimilar(r, g, b, targetR, targetG, targetB, magicWandTolerance))
                {
                    // 투명하게 처리
                    pixels[index + 3] = 0; // Alpha = 0

                    // 4방향 이웃 추가
                    queue.Enqueue((x + 1, y));
                    queue.Enqueue((x - 1, y));
                    queue.Enqueue((x, y + 1));
                    queue.Enqueue((x, y - 1));
                }
            }
        }

        private void MagicWandButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.MagicWand;
            ImageCanvas.Cursor = Cursors.None; // 커스텀 커서 사용 (마법봉 아이콘이 따라다님)
            SetActiveToolButton(MagicWandToolButton);
        }

        private void MagicWandOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowMagicWandOptions();
        }

        /// <summary>
        /// 이미지 전체에서 비슷한 색상을 모두 투명하게 처리
        /// </summary>
        private void RemoveSimilarColors(byte[] pixels, int width, int height, int stride,
            byte targetR, byte targetG, byte targetB)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + (x * 4);
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];

                    if (IsColorSimilar(r, g, b, targetR, targetG, targetB, magicWandTolerance))
                    {
                        pixels[index + 3] = 0; // Alpha = 0
                    }
                }
            }
        }

        /// <summary>
        /// 두 색상이 허용 오차 내에서 비슷한지 확인
        /// </summary>
        private bool IsColorSimilar(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, int tolerance)
        {
            int diffR = Math.Abs(r1 - r2);
            int diffG = Math.Abs(g1 - g2);
            int diffB = Math.Abs(b1 - b2);

            return diffR <= tolerance && diffG <= tolerance && diffB <= tolerance;
        }

        #endregion
    }
}
