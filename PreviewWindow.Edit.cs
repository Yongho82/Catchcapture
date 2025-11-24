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

        #region 텍스트 추가

        private void AddText()
        {
            TextBox textBox = new TextBox
            {
                Width = 200,
                Height = 30,
                AcceptsReturn = false,
                FontSize = textSize,
                Foreground = new SolidColorBrush(textColor),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                FontWeight = textFontWeight,
                FontStyle = textFontStyle,
                FontFamily = new FontFamily(textFontFamily)
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

                        RenderTargetBitmap rtb = new RenderTargetBitmap(
                            (int)currentImage.PixelWidth, (int)currentImage.PixelHeight,
                            96, 96, PixelFormats.Pbgra32);

                        DrawingVisual dv = new DrawingVisual();
                        using (DrawingContext dc = dv.RenderOpen())
                        {
                            dc.DrawImage(currentImage, new Rect(0, 0, currentImage.PixelWidth, currentImage.PixelHeight));

                            FormattedText formattedText = new FormattedText(
                                text,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, FontStretches.Normal),
                                textBox.FontSize,
                                textBox.Foreground,
                                VisualTreeHelper.GetDpi(this).PixelsPerDip);

                            if (textShadowEnabled)
                            {
                                FormattedText shadowText = new FormattedText(
                                    text,
                                    CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, FontStretches.Normal),
                                    textBox.FontSize,
                                    Brushes.Black,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                                dc.DrawText(shadowText, new Point(startPoint.X + 2, startPoint.Y + 2));
                            }

                            dc.DrawText(formattedText, startPoint);

                            if (textUnderlineEnabled)
                            {
                                Pen underlinePen = new Pen(textBox.Foreground, 1);
                                dc.DrawLine(underlinePen,
                                    new Point(startPoint.X, startPoint.Y + formattedText.Height),
                                    new Point(startPoint.X + formattedText.Width, startPoint.Y + formattedText.Height));
                            }
                        }

                        rtb.Render(dv);
                        currentImage = rtb;
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

        #endregion
    }
}
