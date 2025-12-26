using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using System.Linq;
using System.Windows.Input;

namespace CatchCapture.Controls
{
    public partial class RichTextEditorControl : UserControl
    {
        private bool _isTextColorMode = false;

        public RichTextEditorControl()
        {
            InitializeComponent();
            CboFontFamily.ItemsSource = Fonts.SystemFontFamilies.OrderBy(f => f.Source);
            CboFontFamily.SelectedIndex = 0; // Default selection
        }

        // Public property to access the FlowDocument
        public FlowDocument Document => RtbEditor.Document;

        // Text Formatting
        private void BtnBold_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBold.Execute(null, RtbEditor);
        private void BtnItalic_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleItalic.Execute(null, RtbEditor);
        private void BtnUnderline_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleUnderline.Execute(null, RtbEditor);

        // Font Family
        private void CboFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RtbEditor == null || CboFontFamily.SelectedItem == null) return;
            if (CboFontFamily.SelectedItem is FontFamily fontFamily)
            {
                RtbEditor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, fontFamily);
                RtbEditor.Focus();
            }
        }

        // Font Size
        private void CboFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RtbEditor == null || CboFontSize.SelectedItem == null) return;
            if (CboFontSize.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                if (double.TryParse(item.Content.ToString(), out double size))
                {
                    RtbEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                    RtbEditor.Focus();
                }
            }
        }

        // Text Color
        private void BtnTextColor_Click(object sender, RoutedEventArgs e)
        {
            _isTextColorMode = true;
            ColorPickerPopup.PlacementTarget = BtnTextColor;
            ColorPickerPopup.IsOpen = true;
        }

        private void BtnHighlight_Click(object sender, RoutedEventArgs e)
        {
            _isTextColorMode = false;
            ColorPickerPopup.PlacementTarget = BtnHighlight;
            ColorPickerPopup.IsOpen = true;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorHex)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var brush = new SolidColorBrush(color);

                if (_isTextColorMode)
                {
                    RtbEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                }
                else
                {
                    RtbEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                }

                ColorPickerPopup.IsOpen = false;
                RtbEditor.Focus();
            }
        }

        // Alignment
        private void BtnAlignLeft_Click(object sender, RoutedEventArgs e) => EditingCommands.AlignLeft.Execute(null, RtbEditor);
        private void BtnAlignCenter_Click(object sender, RoutedEventArgs e) => EditingCommands.AlignCenter.Execute(null, RtbEditor);
        private void BtnAlignRight_Click(object sender, RoutedEventArgs e) => EditingCommands.AlignRight.Execute(null, RtbEditor);

        // Lists
        private void BtnBulletList_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBullets.Execute(null, RtbEditor);
        private void BtnNumberList_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleNumbering.Execute(null, RtbEditor);

        // Insert Image
        private void BtnInsertImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "이미지 선택",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var image = new System.Windows.Controls.Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(dialog.FileName)),
                        Stretch = Stretch.Uniform
                    };

                    CreateResizableImage(image);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
                }
            }
        }

        private void CreateResizableImage(System.Windows.Controls.Image image)
        {
            // Initial size restriction
            double maxWidth = RtbEditor.ActualWidth > 0 ? RtbEditor.ActualWidth - 60 : 600;
            if (image.Source != null)
            {
                if (image.Source.Width > maxWidth)
                {
                    image.Width = maxWidth;
                }
            }

            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 10) };
            grid.Children.Add(image);

            // Create handles
            var handles = new Grid { Visibility = Visibility.Collapsed };
            string[] positions = { "TL", "T", "TR", "L", "R", "BL", "B", "BR" };
            foreach (var pos in positions)
            {
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, Brushes.White);
                borderFactory.SetValue(Border.BorderBrushProperty, Brushes.DeepSkyBlue);
                borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));

                var thumbTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb));
                thumbTemplate.VisualTree = borderFactory;

                var thumb = new System.Windows.Controls.Primitives.Thumb
                {
                    Width = 10,
                    Height = 10,
                    Template = thumbTemplate,
                    Cursor = GetCursorForPosition(pos)
                };

                Panel.SetZIndex(thumb, 100);

                thumb.DragDelta += (s, e) =>
                {
                    double newWidth = image.Width;
                    if (pos.Contains("R")) newWidth += e.HorizontalChange;
                    if (pos.Contains("L")) newWidth -= e.HorizontalChange;
                    
                    if (newWidth > 20 && newWidth < (RtbEditor.ActualWidth - 40))
                    {
                        image.Width = newWidth;
                    }
                };

                // Alignment
                thumb.HorizontalAlignment = pos.Contains("L") ? HorizontalAlignment.Left : (pos.Contains("R") ? HorizontalAlignment.Right : HorizontalAlignment.Center);
                thumb.VerticalAlignment = pos.Contains("T") ? VerticalAlignment.Top : (pos.Contains("B") ? VerticalAlignment.Bottom : VerticalAlignment.Center);
                
                // Offset
                double offset = -4;
                thumb.Margin = new Thickness(
                    pos.Contains("L") ? offset : 0,
                    pos.Contains("T") ? offset : 0,
                    pos.Contains("R") ? offset : 0,
                    pos.Contains("B") ? offset : 0
                );

                handles.Children.Add(thumb);
            }

            grid.Children.Add(handles);

            image.MouseDown += (s, e) =>
            {
                handles.Visibility = Visibility.Visible;
                e.Handled = true;
            };

            RtbEditor.MouseDown += (s, e) =>
            {
                handles.Visibility = Visibility.Collapsed;
            };

            var container = new BlockUIContainer(grid);
            RtbEditor.Document.Blocks.Add(container);
            RtbEditor.ScrollToEnd();
        }

        private Cursor GetCursorForPosition(string pos)
        {
            switch (pos)
            {
                case "TL": case "BR": return Cursors.SizeNWSE;
                case "TR": case "BL": return Cursors.SizeNESW;
                case "T": case "B": return Cursors.SizeNS;
                case "L": case "R": return Cursors.SizeWE;
                default: return Cursors.Arrow;
            }
        }

        // Insert Link
        private void BtnInsertLink_Click(object sender, RoutedEventArgs e)
        {
            var linkText = RtbEditor.Selection.Text;
            if (string.IsNullOrEmpty(linkText))
            {
                MessageBox.Show("링크로 만들 텍스트를 먼저 선택하세요.", "안내");
                return;
            }

            var url = Microsoft.VisualBasic.Interaction.InputBox("URL을 입력하세요:", "링크 삽입", "https://");
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    var hyperlink = new Hyperlink(RtbEditor.Selection.Start, RtbEditor.Selection.End)
                    {
                        NavigateUri = new Uri(url),
                        Foreground = new SolidColorBrush(Colors.LightBlue)
                    };
                    hyperlink.RequestNavigate += (s, args) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.ToString()) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"링크를 삽입할 수 없습니다: {ex.Message}", "오류");
                }
            }
        }

        // Public method to get content as RTF or plain text
        public string GetRtfContent()
        {
            var range = new TextRange(RtbEditor.Document.ContentStart, RtbEditor.Document.ContentEnd);
            using (var stream = new System.IO.MemoryStream())
            {
                range.Save(stream, DataFormats.Rtf);
                stream.Position = 0;
                using (var reader = new System.IO.StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public string GetPlainText()
        {
            return new TextRange(RtbEditor.Document.ContentStart, RtbEditor.Document.ContentEnd).Text;
        }

        public void InsertImage(ImageSource imageSource)
        {
            try
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform
                };

                CreateResizableImage(image);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
            }
        }
    }
}
