using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using System.Linq;
using System.Windows.Input;
using System.Windows.Data;

namespace CatchCapture.Controls
{
    public partial class RichTextEditorControl : UserControl
    {
        private bool _isTextColorMode = false;
        private List<FrameworkElement> _sliderPanels = new List<FrameworkElement>();

        public RichTextEditorControl()
        {
            InitializeComponent();
            CboFontFamily.ItemsSource = Fonts.SystemFontFamilies.OrderBy(f => f.Source);
            CboFontFamily.SelectedIndex = 0; // Default selection
            
            // Enable Undo for images and content
            RtbEditor.IsUndoEnabled = true;
            RtbEditor.UndoLimit = 100;
            
            // Auto-hide sliders when clicking elsewhere
            RtbEditor.PreviewMouseLeftButtonDown += (s, e) =>
            {
                foreach (var panel in _sliderPanels)
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            };

            UpdatePlaceholderVisibility();
        }

        // Public property to access the FlowDocument
        public FlowDocument Document => RtbEditor.Document;

        // Text Formatting
        private void BtnBold_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBold.Execute(null, RtbEditor);
        private void BtnItalic_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleItalic.Execute(null, RtbEditor);
        private void BtnUnderline_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleUnderline.Execute(null, RtbEditor);

        private void BtnUndo_Click(object sender, RoutedEventArgs e) => RtbEditor.Undo();
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => RtbEditor.Redo();

        // Line Spacing
        private void BtnLineSpacingSingle_Click(object sender, RoutedEventArgs e) => SetLineHeight(1.0);
        private void BtnLineSpacing15_Click(object sender, RoutedEventArgs e) => SetLineHeight(1.5);
        private void BtnLineSpacingDouble_Click(object sender, RoutedEventArgs e) => SetLineHeight(2.0);

        private void SetLineHeight(double lineHeight)
        {
            var paragraph = RtbEditor.CaretPosition.Paragraph;
            if (paragraph != null)
            {
                paragraph.LineHeight = lineHeight;
                paragraph.Margin = new Thickness(0, 0, 0, 4); // Small bottom margin
            }
            RtbEditor.Focus();
        }

        private void BtnLineSpacing_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

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
            // Set initial width to 360px (User request)
            image.Width = 360;
            image.Stretch = Stretch.Uniform;

            var mainGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 5) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Image
            image.Margin = new Thickness(0);
            image.Cursor = Cursors.Hand;
            image.IsHitTestVisible = true;
            Grid.SetRow(image, 0);
            mainGrid.Children.Add(image);

            // Resize slider panel
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            
            // Add border/shadow to slider panel
            var sliderBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)), // Semi-transparent white
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 2, 0, 0),
                Child = sliderPanel
            };
            Grid.SetRow(sliderBorder, 1);
            sliderBorder.Visibility = Visibility.Collapsed; // Hidden initially, toggle on click
            mainGrid.Children.Add(sliderBorder);
            _sliderPanels.Add(sliderBorder);

            var sliderLabel = new TextBlock
            {
                Text = "Size ",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
            };

            var slider = new Slider
            {
                Minimum = 100,
                Maximum = 800,
                Value = 360,
                Width = 140,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center
            };

            var sizeText = new TextBlock
            {
                Text = "360px",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                MinWidth = 40
            };

            slider.ValueChanged += (s, e) =>
            {
                image.Width = slider.Value;
                sizeText.Text = $"{(int)slider.Value}px";
            };

            sliderPanel.Children.Add(sliderLabel);
            sliderPanel.Children.Add(slider);
            sliderPanel.Children.Add(sizeText);
            // sliderBorder already added to mainGrid at line 187
            


            image.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // Toggle current slider
                sliderBorder.Visibility = sliderBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                
                // Hide other sliders
                if (sliderBorder.Visibility == Visibility.Visible)
                {
                    foreach (var p in _sliderPanels)
                    {
                        if (p != sliderBorder) p.Visibility = Visibility.Collapsed;
                    }
                }
                e.Handled = true;
            };

            var container = new BlockUIContainer(mainGrid);
            RtbEditor.Document.Blocks.Add(container);
            
            // Add a new empty paragraph after the image with proper spacing
            var newPara = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = 1.5  // Default line height
            };
            RtbEditor.Document.Blocks.Add(newPara);
            RtbEditor.ScrollToEnd();
            UpdatePlaceholderVisibility();
        }

        private void RtbEditor_Loaded(object sender, RoutedEventArgs e)
        {
            // Force visible initially since the editor is empty
            if (TxtPlaceholder != null)
            {
                TxtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void RtbEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void RtbEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void RtbEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility()
        {
            if (TxtPlaceholder == null || RtbEditor == null || RtbEditor.Document == null) return;

            try
            {
                // Simple check: if there's any text or UIContainers, hide placeholder
                bool isEmpty = true;
                
                // Quick check for blocks
                if (RtbEditor.Document.Blocks.Count > 1)
                {
                    isEmpty = false;
                }
                else if (RtbEditor.Document.Blocks.Count == 1)
                {
                    var firstBlock = RtbEditor.Document.Blocks.FirstBlock;
                    
                    // Check if it's a UIContainer (image)
                    if (firstBlock is BlockUIContainer)
                    {
                        isEmpty = false;
                    }
                    else if (firstBlock is Paragraph para)
                    {
                        // Only check text if it's a paragraph
                        var textRange = new TextRange(para.ContentStart, para.ContentEnd);
                        string text = textRange.Text?.Trim('\r', '\n', ' ', '\t', '\u200B') ?? "";
                        isEmpty = string.IsNullOrWhiteSpace(text);
                    }
                }

                // Hide if has content or has focus
                TxtPlaceholder.Visibility = (isEmpty && !RtbEditor.IsFocused) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                // If there's any error, just hide the placeholder
                TxtPlaceholder.Visibility = Visibility.Collapsed;
            }
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
