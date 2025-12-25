using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using System.Linq;

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
            }
        }

        // Font Size
        private void CboFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RtbEditor == null || CboFontSize.SelectedItem == null) return;
            var item = CboFontSize.SelectedItem as ComboBoxItem;
            if (double.TryParse(item.Content.ToString(), out double size))
            {
                RtbEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
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
                        MaxWidth = 600,
                        Stretch = Stretch.Uniform
                    };

                    var container = new BlockUIContainer(image);
                    RtbEditor.Document.Blocks.Add(container);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
                }
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
                    MaxWidth = 600,
                    Stretch = Stretch.Uniform
                };

                var container = new BlockUIContainer(image);
                RtbEditor.Document.Blocks.Add(container);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
            }
        }
    }
}
