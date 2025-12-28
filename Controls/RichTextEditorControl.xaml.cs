using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Linq;
using System.Collections.Generic;
using System.IO;
using CatchCapture.Utilities;
using System.Windows.Input;
using System.Windows.Data;
using System.Text.RegularExpressions;
using System.Windows.Shapes;

namespace CatchCapture.Controls
{
    public partial class RichTextEditorControl : UserControl
    {
        private bool _isTextColorMode = false;
        private bool _isApplyingProperty = false;
        private List<FrameworkElement> _sliderPanels = new List<FrameworkElement>();
        private Color _currentTextColor = Colors.Black;
        private Color _currentHighlightColor = Colors.Transparent;

        public RichTextEditorControl()
        {
            InitializeComponent();
            
            // 15 Recommended Professional Fonts
            var recommendedFonts = new[] 
            { 
                "Inter", "Segoe UI", "Roboto", "Open Sans", "Noto Sans KR", 
                "Arial", "Helvetica", "Verdana", "Tahoma", "Georgia", 
                "Times New Roman", "Courier New", "Consolas", "Malgun Gothic", "Gulim" 
            };

            var systemFonts = Fonts.SystemFontFamilies.ToList();
            var filteredFonts = systemFonts
                .Where(f => recommendedFonts.Any(r => f.Source.Equals(r, StringComparison.OrdinalIgnoreCase) || 
                                                     f.Source.StartsWith(r + " ", StringComparison.OrdinalIgnoreCase)))
                .GroupBy(f => f.Source) // Avoid duplicates
                .Select(g => g.First())
                .OrderBy(f => f.Source)
                .Take(15) // Ensure exactly 15 or less
                .ToList();

            CboFontFamily.ItemsSource = filteredFonts;

            // Set Default Font: Segoe UI preferred, then Inter
            var defaultFont = filteredFonts.FirstOrDefault(f => f.Source.Contains("Segoe UI"))
                            ?? filteredFonts.FirstOrDefault(f => f.Source.Contains("Inter")) 
                            ?? filteredFonts.FirstOrDefault();
            
            if (defaultFont != null) CboFontFamily.SelectedItem = defaultFont;
            
            // Set Default Size: 14 (Index 2 in 10, 12, 14, 16...)
            CboFontSize.SelectedIndex = 2; 

            // Initialize Properties
            RtbEditor.FontSize = 14;
            RtbEditor.FontFamily = defaultFont ?? new FontFamily("Segoe UI");
            RtbEditor.Foreground = Brushes.Black;
            
            // Document baseline properties
            RtbEditor.Document.FontSize = 14;
            RtbEditor.Document.FontFamily = defaultFont ?? new FontFamily("Segoe UI");
            
            // Set consistent line height and paragraph spacing to prevent jumping
            RtbEditor.Document.TextAlignment = TextAlignment.Left;
            RtbEditor.Document.PagePadding = new Thickness(10);
            
            // Line height and paragraph spacing are now handled by XAML Styles in FlowDocument.Resources
            
            // Enable Undo for images and content
            RtbEditor.IsUndoEnabled = true;
            RtbEditor.UndoLimit = 100;
            
            // Auto-hide sliders when clicking elsewhere
            // Auto-hide sliders when clicking elsewhere, but check if we clicked inside a slider first
            RtbEditor.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is DependencyObject originalSource)
                {
                    foreach (var panel in _sliderPanels)
                    {
                        if (IsDescendantOrSelf(panel, originalSource))
                        {
                            continue; // Clicked inside this panel, don't hide
                        }
                        panel.Visibility = Visibility.Collapsed;
                    }
                }
            };

            // Handle Paste and Drop for images
            DataObject.AddPastingHandler(RtbEditor, OnPaste);
            RtbEditor.Drop += OnDrop;
            RtbEditor.AllowDrop = true;

            RtbEditor.SelectionChanged += RtbEditor_SelectionChanged;

            BuildColorPalette();
            UpdateColorIndicators();

            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        private void UpdateUIText()
        {
            // Color Picker
            if (ColorPickerTitle != null) ColorPickerTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("ColorPickerTitle") ?? "색상";
            if (BtnClearColor != null) BtnClearColor.Content = CatchCapture.Resources.LocalizationManager.GetString("Transparent") ?? "투명";
            if (BtnCustomColor != null) BtnCustomColor.Content = "+ " + (CatchCapture.Resources.LocalizationManager.GetString("Add") ?? "추가");

            // Tooltips
            if (CboFontFamily != null) CboFontFamily.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("FontFamily") ?? "글꼴";
            if (CboFontSize != null) CboFontSize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("FontSize") ?? "글자 크기";
            if (BtnBold != null) BtnBold.ToolTip = (CatchCapture.Resources.LocalizationManager.GetString("Bold") ?? "굵게") + " (Ctrl+B)";
            if (BtnItalic != null) BtnItalic.ToolTip = (CatchCapture.Resources.LocalizationManager.GetString("Italic") ?? "기울임") + " (Ctrl+I)";
            if (BtnUnderline != null) BtnUnderline.ToolTip = (CatchCapture.Resources.LocalizationManager.GetString("Underline") ?? "밑줄") + " (Ctrl+U)";
            if (BtnTextColor != null) BtnTextColor.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("TextColor") ?? "글자 색상";
            if (BtnHighlight != null) BtnHighlight.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("HighlightColor") ?? "배경 색상 (형광펜)";
            if (BtnAlignLeft != null) BtnAlignLeft.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("AlignLeft") ?? "왼쪽 정렬";
            if (BtnAlignCenter != null) BtnAlignCenter.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("AlignCenter") ?? "가운데 정렬";
            if (BtnAlignRight != null) BtnAlignRight.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("AlignRight") ?? "오른쪽 정렬";
            if (BtnBulletList != null) BtnBulletList.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("BulletList") ?? "글머리 기호";
            if (BtnNumberList != null) BtnNumberList.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("NumberList") ?? "번호 매기기";
            if (BtnInsertImage != null) BtnInsertImage.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("InsertImage") ?? "이미지 삽입";
            if (BtnCaptureAdd != null) BtnCaptureAdd.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("InsertCapture") ?? "캡처 추가";
            if (BtnInsertLink != null) BtnInsertLink.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("InsertLink") ?? "링크 삽입";
            if (BtnUndo != null) BtnUndo.ToolTip = (CatchCapture.Resources.LocalizationManager.GetString("Undo") ?? "실행 취소") + " (Ctrl+Z)";
            if (BtnRedo != null) BtnRedo.ToolTip = (CatchCapture.Resources.LocalizationManager.GetString("Redo") ?? "다시 실행") + " (Ctrl+Y)";
            if (BtnLineSpacing != null) BtnLineSpacing.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("LineSpacing") ?? "줄 간격";
        }

        private void BuildColorPalette()
        {
            ColorGrid.Children.Clear();
            foreach (var color in CatchCapture.Models.UIConstants.SharedColorPalette)
            {
                if (color == Colors.Transparent) continue;

                var border = new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = color.ToString()
                };

                border.MouseLeftButtonDown += (s, e) =>
                {
                    ApplySelectedColor(color);
                    ColorPickerPopup.IsOpen = false;
                };

                // Hover effect
                border.MouseEnter += (s, e) => border.BorderBrush = Brushes.SkyBlue;
                border.MouseLeave += (s, e) => border.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));

                ColorGrid.Children.Add(border);
            }
        }

        private void ApplySelectedColor(Color color)
        {
            var brush = new SolidColorBrush(color);
            if (_isTextColorMode)
            {
                _currentTextColor = color;
                ApplyPropertyToSelection(TextElement.ForegroundProperty, brush);
            }
            else
            {
                _currentHighlightColor = color;
                ApplyPropertyToSelection(TextElement.BackgroundProperty, color == Colors.Transparent ? null : brush);
            }
            UpdateColorIndicators();
        }

        private void UpdateColorIndicators()
        {
            TextColorBar.Background = new SolidColorBrush(_currentTextColor);
            
            if (_currentHighlightColor == Colors.Transparent)
            {
                HighlightColorBar.Background = Brushes.Transparent;
                HighlightColorBar.BorderThickness = new Thickness(1);
            }
            else
            {
                HighlightColorBar.Background = new SolidColorBrush(_currentHighlightColor);
                HighlightColorBar.BorderThickness = new Thickness(0);
            }
        }

        private void RtbEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isApplyingProperty) return;

            UpdateToolBarStates();
        }

        private void UpdateToolBarStates()
        {
            if (RtbEditor == null || _isApplyingProperty) return;

            // During typing (caret position, no selection), 
            // don't let the toolbar sync back from the editor.
            // This prevents the Korean IME from overriding user-selected style with old context.
            if (RtbEditor.IsFocused && RtbEditor.Selection.IsEmpty) return;

            _isApplyingProperty = true;
            try
            {
                // Update Bold/Italic/Underline
                var fontWeight = RtbEditor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
                BtnBold.IsChecked = (fontWeight != DependencyProperty.UnsetValue && fontWeight is FontWeight fw && fw == FontWeights.Bold);

                var fontStyle = RtbEditor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                BtnItalic.IsChecked = (fontStyle != DependencyProperty.UnsetValue && fontStyle is FontStyle fs && fs == FontStyles.Italic);

                var textDecorations = RtbEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
                BtnUnderline.IsChecked = (textDecorations != DependencyProperty.UnsetValue && textDecorations is TextDecorationCollection tdc && tdc.Count > 0);

                // Update Color indicators from selection
                var foreground = RtbEditor.Selection.GetPropertyValue(TextElement.ForegroundProperty);
                if (foreground is SolidColorBrush fBrush)
                {
                    _currentTextColor = fBrush.Color;
                }

                var background = RtbEditor.Selection.GetPropertyValue(TextElement.BackgroundProperty);
                if (background is SolidColorBrush bBrush)
                {
                    _currentHighlightColor = bBrush.Color;
                }
                else
                {
                    _currentHighlightColor = Colors.Transparent;
                }

                UpdateColorIndicators();

                // Update Font ComboBox
                var fontFamily = RtbEditor.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
                if (fontFamily is FontFamily ff && CboFontFamily.ItemsSource is List<FontFamily> fonts)
                {
                    var match = fonts.FirstOrDefault(x => x.Source == ff.Source);
                    if (match != null && CboFontFamily.SelectedItem != match)
                    {
                        CboFontFamily.SelectedItem = match;
                    }
                }

                // Update Size ComboBox
                var fontSize = RtbEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                if (fontSize is double sz)
                {
                    int szInt = (int)Math.Round(sz);
                    foreach (ComboBoxItem item in CboFontSize.Items)
                    {
                        if (double.TryParse(item.Content?.ToString(), out double s) && (int)Math.Round(s) == szInt)
                        {
                            if (CboFontSize.SelectedItem != item)
                            {
                                CboFontSize.SelectedItem = item;
                            }
                            break;
                        }
                    }
                }
            }
            finally
            {
                _isApplyingProperty = false;
            }
        }

        private bool IsDescendantOrSelf(DependencyObject parent, DependencyObject node)
        {
            if (parent == null || node == null) return false;
            if (parent == node) return true;

            try
            {
                var current = node;
                while (current != null)
                {
                    if (current == parent) return true;
                    if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    else
                    {
                        current = LogicalTreeHelper.GetParent(current);
                    }
                }
            }
            catch { } // Ignore visual tree errors
            return false;
        }


        // Public property to access the FlowDocument
        public FlowDocument Document => RtbEditor.Document;


        // Text Formatting
        private void BtnBold_Click(object sender, RoutedEventArgs e) 
        {
            var val = RtbEditor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            var target = (val != DependencyProperty.UnsetValue && val is FontWeight fw && fw == FontWeights.Bold) 
                ? FontWeights.Normal : FontWeights.Bold;
            ApplyPropertyToSelection(TextElement.FontWeightProperty, target);
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e) 
        {
            var val = RtbEditor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            var target = (val != DependencyProperty.UnsetValue && val is FontStyle fs && fs == FontStyles.Italic) 
                ? FontStyles.Normal : FontStyles.Italic;
            ApplyPropertyToSelection(TextElement.FontStyleProperty, target);
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e) 
        {
            var currentValue = RtbEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            bool isUnderlined = (currentValue != DependencyProperty.UnsetValue && currentValue is TextDecorationCollection tdc && tdc.Count > 0);
            
            var target = isUnderlined ? null : TextDecorations.Underline;
            ApplyPropertyToSelection(Inline.TextDecorationsProperty, target);
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e) => RtbEditor.Undo();
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => RtbEditor.Redo();

        // Line Spacing
        private void BtnLineSpacingSingle_Click(object sender, RoutedEventArgs e) => SetLineHeight(1.0);
        private void BtnLineSpacing15_Click(object sender, RoutedEventArgs e) => SetLineHeight(1.5);
        private void BtnLineSpacingDouble_Click(object sender, RoutedEventArgs e) => SetLineHeight(2.0);

        private void SetLineHeight(double multiplier)
        {
            if (RtbEditor == null) return;

            RtbEditor.BeginChange();
            try
            {
                var textRange = RtbEditor.Selection;
                var currentPos = textRange.Start;

                while (currentPos != null && currentPos.CompareTo(textRange.End) <= 0)
                {
                    var paragraph = currentPos.Paragraph;
                    if (paragraph != null)
                    {
                        double currentFontSize = (double)paragraph.GetValue(TextElement.FontSizeProperty);
                        if (currentFontSize <= 0) currentFontSize = 14; // Fallback

                        paragraph.LineHeight = currentFontSize * multiplier;
                        paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                        // Use slightly larger margin for better readability between paragraphs
                        paragraph.Margin = new Thickness(0, 0, 0, currentFontSize * 0.5); 
                    }
                    
                    // Move to start of next paragraph
                    var next = currentPos.GetNextContextPosition(LogicalDirection.Forward);
                    if (next == null || next.CompareTo(textRange.End) > 0) break;
                    currentPos = next;
                }
            }
            finally
            {
                RtbEditor.EndChange();
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
                ApplyPropertyToSelection(TextElement.FontFamilyProperty, fontFamily);
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
                    ApplyPropertyToSelection(TextElement.FontSizeProperty, size);
                }
            }
        }

        private void ApplyPropertyToSelection(DependencyProperty property, object? value)
        {
            if (RtbEditor == null) return;

            try
            {
                _isApplyingProperty = true;
                RtbEditor.Focus();
                
                if (RtbEditor.Selection.IsEmpty)
                {
                    // Use a Zero-Width Space (ZWS) as an anchor for the style.
                    RtbEditor.BeginChange();
                    try
                    {
                        // 1. Capture ALL current context properties to avoid losing them
                        var curFont = RtbEditor.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
                        var curSize = RtbEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                        var curColor = RtbEditor.Selection.GetPropertyValue(TextElement.ForegroundProperty);
                        var curWeight = RtbEditor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
                        var curStyle = RtbEditor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                        var curDecor = RtbEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);

                        // 2. Insert ZWS anchor
                        RtbEditor.Selection.Text = "\u200B";
                        
                        // 3. Select the ZWS
                        var end = RtbEditor.Selection.End;
                        var start = end.GetPositionAtOffset(-1, LogicalDirection.Backward);
                        if (start != null)
                        {
                            RtbEditor.Selection.Select(start, end);

                            // 4. Re-apply current context to the ZWS anchor (locking it in)
                            if (curFont != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, curFont);
                            if (curSize != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, curSize);
                            if (curColor != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, curColor);
                            if (curWeight != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, curWeight);
                            if (curStyle != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, curStyle);
                            if (curDecor != DependencyProperty.UnsetValue) RtbEditor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, curDecor);
                            
                            // 5. Apply the NEW property (user's selection)
                            if (value is SolidColorBrush b && b.Color == Colors.Black) value = Brushes.Black;
                            RtbEditor.Selection.ApplyPropertyValue(property, value);
                            
                            // 6. Move caret after the ZWS anchor
                            RtbEditor.Selection.Select(RtbEditor.Selection.End, RtbEditor.Selection.End);
                        }
                    }
                    finally
                    {
                        RtbEditor.EndChange();
                    }
                }
                else
                {
                    // Normal selection application
                    if (value is SolidColorBrush b && b.Color == Colors.Black) value = Brushes.Black;
                    RtbEditor.Selection.ApplyPropertyValue(property, value);
                }

                RtbEditor.Focus();
                Keyboard.Focus(RtbEditor);
            }
            finally
            {
                _isApplyingProperty = false;
            }
        }



        // Text Color
        private void BtnTextColor_Click(object sender, RoutedEventArgs e)
        {
            _isTextColorMode = true;
            _isTextColorMode = true;
            ColorPickerTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("TextColor") ?? "글자 색상";
            BtnClearColor.Visibility = Visibility.Collapsed; // Text color doesn't usually have "transparent"
            ColorPickerPopup.PlacementTarget = BtnTextColor;
            ColorPickerPopup.IsOpen = true;
        }

        private void BtnHighlight_Click(object sender, RoutedEventArgs e)
        {
            _isTextColorMode = false;
            _isTextColorMode = false;
            ColorPickerTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("HighlightColor") ?? "배경 색상";
            BtnClearColor.Visibility = Visibility.Visible; // Background can be cleared
            ColorPickerPopup.PlacementTarget = BtnHighlight;
            ColorPickerPopup.IsOpen = true;
        }

        private void BtnClearColor_Click(object sender, RoutedEventArgs e)
        {
            ApplySelectedColor(Colors.Transparent);
            ColorPickerPopup.IsOpen = false;
        }

        private void AddCustomColor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                
                // Add to palette temporarily
                var border = new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = Brushes.SkyBlue,
                    BorderThickness = new Thickness(2),
                    Cursor = Cursors.Hand
                };
                border.MouseLeftButtonDown += (s, ev) => { ApplySelectedColor(color); ColorPickerPopup.IsOpen = false; };
                ColorGrid.Children.Add(border);

                ApplySelectedColor(color);
                ColorPickerPopup.IsOpen = false;
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            // This is matched to the old XAML buttons. 
            // New logic uses BuildColorPalette and MouseLeftButtonDown on Borders.
            if (sender is Button btn && btn.Tag is string colorHex)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    ApplySelectedColor(color);
                    ColorPickerPopup.IsOpen = false;
                }
                catch { }
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
                    CatchCapture.CustomMessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
                }
            }
        }

        // Capture Add - Event for parent window to handle
        public event Action? CaptureRequested;

        private void BtnCaptureAdd_Click(object sender, RoutedEventArgs e)
        {
            CaptureRequested?.Invoke();
        }

        /// <summary>
        /// Public method to insert a captured image directly into the editor
        /// </summary>
        public void InsertCapturedImage(BitmapSource capturedImage)
        {
            if (capturedImage == null) return;
            
            try
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = capturedImage,
                    Stretch = Stretch.Uniform
                };

                CreateResizableImage(image);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"캡처 이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
            }
        }

        private void CreateResizableImage(System.Windows.Controls.Image image)
        {
            // Set initial width (Use actual size if smaller than 360, otherwise default to 360)
            double initialWidth = 360;
            try
            {
                if (image.Source is BitmapSource bs)
                {
                    // Use PixelWidth (actual pixels)
                    double w = bs.PixelWidth;
                    if (w > 0 && w < 360)
                    {
                        initialWidth = w;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting image width: {ex.Message}");
            }

            if (initialWidth < 50) initialWidth = 50;
            
            // Round to integer to avoid XAML serialization issues with long decimals
            initialWidth = Math.Round(initialWidth);

            image.Width = initialWidth;
            image.Stretch = Stretch.Uniform;

            var mainGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 5), VerticalAlignment = VerticalAlignment.Top };

            // Image
            image.Margin = new Thickness(0);
            image.Cursor = Cursors.Hand;
            image.IsHitTestVisible = true;
            Panel.SetZIndex(image, 1);
            mainGrid.Children.Add(image);

            // Resize slider panel - overlay at bottom of image
            var sliderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            
            // Add border/shadow to slider panel
            var sliderBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), // Very transparent black
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), // Rounded pill style
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(10, 0, 10, 15),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = sliderPanel,
                Visibility = Visibility.Collapsed
            };
            
            Panel.SetZIndex(sliderBorder, 2); // Ensure it's on top of image for clicks
            
            // Handle bubbled event to prevent RichTextBox from interfering with slider drag
            sliderBorder.MouseLeftButtonDown += (s, e) => e.Handled = true;
            
            mainGrid.Children.Add(sliderBorder); // [Fix] Add sliderBorder to Grid

            var sliderLabel = new TextBlock
            {
                Text = "Size ",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = Brushes.White
            };

            var slider = new Slider
            {
                Minimum = 20,
                Maximum = 1200,
                Value = initialWidth, // Use calculated initial width
                Width = 140,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 10
            };

            var sizeText = new TextBlock
            {
                Text = $"{(int)initialWidth}px",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = Brushes.White,
                MinWidth = 40
            };

            var separator = new Border 
            { 
                Width = 1, Height = 12, Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 
                Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
            };

            var btnEdit = new Button
            {
                Content = "수정", FontSize = 11,
                Background = Brushes.Transparent, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0)
            };

            sliderPanel.Children.Add(sliderLabel);
            sliderPanel.Children.Add(slider);
            sliderPanel.Children.Add(sizeText);
            sliderPanel.Children.Add(separator);
            sliderPanel.Children.Add(btnEdit);
            
            var container = new BlockUIContainer(mainGrid);
            HookImageEvents(image, sliderBorder, container);
            
            RtbEditor.BeginChange();
            try
            {
                InsertBlockAtCaret(container);
            }
            finally
            {
                RtbEditor.EndChange();
            }

            RtbEditor.Focus();
        }

        private void InsertBlockAtCaret(Block block)
        {
            var curPos = RtbEditor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            var p = curPos.Paragraph;

            if (p != null)
            {
                // 중간에 있으면 문단 나누기
                if (curPos.CompareTo(p.ContentStart) != 0 && curPos.CompareTo(p.ContentEnd) != 0)
                {
                    TextPointer next = curPos.InsertParagraphBreak();
                    if (next.Paragraph != null)
                        RtbEditor.Document.Blocks.InsertBefore(next.Paragraph, block);
                    else
                        RtbEditor.Document.Blocks.Add(block);
                    
                    RtbEditor.CaretPosition = next;
                }
                else if (curPos.CompareTo(p.ContentStart) == 0)
                {
                    RtbEditor.Document.Blocks.InsertBefore(p, block);
                    // 이미지가 맨 앞이면 에디터 포커싱 유지를 위해 커서 조정
                    RtbEditor.CaretPosition = block.ElementEnd.GetNextInsertionPosition(LogicalDirection.Forward) ?? block.ElementEnd;
                }
                else
                {
                    RtbEditor.Document.Blocks.InsertAfter(p, block);
                    // 이미지 뒤에 빈 문단이 없으면 추가
                    if (block.NextBlock == null)
                    {
                        var newPara = new Paragraph { Margin = new Thickness(0), LineHeight = 1.5 };
                        RtbEditor.Document.Blocks.InsertAfter(block, newPara);
                        RtbEditor.CaretPosition = newPara.ContentStart;
                    }
                    else
                    {
                        RtbEditor.CaretPosition = block.ElementEnd.GetNextInsertionPosition(LogicalDirection.Forward) ?? block.ElementEnd;
                    }
                }
            }
            else
            {
                RtbEditor.Document.Blocks.Add(block);
                var newPara = new Paragraph { Margin = new Thickness(0), LineHeight = 1.5 };
                RtbEditor.Document.Blocks.Add(newPara);
                RtbEditor.CaretPosition = newPara.ContentStart;
            }
        }

        private void RtbEditor_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void RtbEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void RtbEditor_GotFocus(object sender, RoutedEventArgs e)
        {
        }

        private void RtbEditor_LostFocus(object sender, RoutedEventArgs e)
        {
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
                CatchCapture.CustomMessageBox.Show("링크로 만들 텍스트를 먼저 선택하세요.", "안내");
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
                    CatchCapture.CustomMessageBox.Show($"링크를 삽입할 수 없습니다: {ex.Message}", "오류");
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

        public string GetXaml()
        {
            try
            {
                // Save the whole document for full fidelity (FlowDocument root)
                return System.Windows.Markup.XamlWriter.Save(RtbEditor.Document);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "";
            }
        }

        public void SetXaml(string xamlString)
        {
            if (string.IsNullOrEmpty(xamlString)) return;
            try
            {
                using (var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xamlString)))
                {
                    var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Load(ms);
                    RtbEditor.Document = flowDocument;
                    RestoreImageBehaviors();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SetXaml Error: " + ex.Message);
            }
        }

        private void RestoreImageBehaviors()
        {
            _sliderPanels.Clear();
            foreach (var block in RtbEditor.Document.Blocks)
            {
                if (block is BlockUIContainer container && container.Child is Grid mainGrid)
                {
                    // Check if it's our resizable image structure
                    if (mainGrid.Children.Count >= 2 && 
                        mainGrid.Children[0] is System.Windows.Controls.Image image && 
                        mainGrid.Children[1] is Border sliderBorder)
                    {
                        HookImageEvents(image, sliderBorder, container);
                    }
                }
            }
        }

        private void HookImageEvents(System.Windows.Controls.Image image, Border sliderBorder, BlockUIContainer container)
        {
            // Use Tag to prevent duplicate hooking
            if (sliderBorder.Tag?.ToString() == "Hooked") return;
            sliderBorder.Tag = "Hooked";

            if (!_sliderPanels.Contains(sliderBorder))
                _sliderPanels.Add(sliderBorder);
            
            // Find slider and size text
            Slider? slider = null;
            TextBlock? sizeText = null;
            if (sliderBorder.Child is StackPanel sp)
            {
                slider = sp.Children.OfType<Slider>().FirstOrDefault();
                sizeText = sp.Children.OfType<TextBlock>().FirstOrDefault(t => t.Text.EndsWith("px"));

                // Hook Edit Button (find by Content since Name causes XAML collision)
                var btnEdit = sp.Children.OfType<Button>().FirstOrDefault(b => b.Content?.ToString() == "수정");
                if (btnEdit != null)
                {
                    btnEdit.Click += (s, e) => EditImage(image);
                }
            }

            if (slider != null && sizeText != null)
            {
                slider.ValueChanged += (s, e) =>
                {
                    // Round to integer to avoid XAML serialization issues
                    double v = Math.Round(slider.Value);
                    image.Width = v;
                    sizeText.Text = $"{(int)v}px";
                    // Force slider value to be exact integer (avoid floating point in XAML)
                    if (Math.Abs(slider.Value - v) > 0.001)
                    {
                        slider.Value = v;
                    }
                };
            }

            sliderBorder.MouseLeftButtonDown += (s, e) => e.Handled = true;

            image.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    EditImage(image);
                    e.Handled = true;
                    return;
                }

                // 1. Toggle current slider
                sliderBorder.Visibility = sliderBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                
                // 2. Hide other sliders
                if (sliderBorder.Visibility == Visibility.Visible)
                {
                    foreach (var p in _sliderPanels)
                    {
                        if (p != sliderBorder) p.Visibility = Visibility.Collapsed;
                    }
                }
                
                // 3. FORCE SELECTION of the container so Delete/Backspace works
                RtbEditor.Focus();
                // Select the container range
                RtbEditor.Selection.Select(container.ElementStart, container.ElementEnd);
                
                e.Handled = true;
            };
        }

        private void EditImage(System.Windows.Controls.Image image)
        {
            var bitmap = image.Source as BitmapSource;
            if (bitmap != null)
            {
                try
                {
                    var previewWin = new PreviewWindow(bitmap, 0);
                    previewWin.Owner = Window.GetWindow(this);
                    previewWin.ImageUpdated += (sw, args) =>
                    {
                        image.Source = args.NewImage;
                    };
                    previewWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening preview window: {ex.Message}");
                }
            }
        }

        public void InsertImage(ImageSource imageSource)
        {
            try
            {
                ImageSource finalSource = imageSource;
                
                // If it's a BitmapSource (not file-based), save to temp file for Undo compatibility
                if (imageSource is BitmapSource bitmapSource && !(imageSource is BitmapImage))
                {
                    string tempPath = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(), 
                        $"catchcapture_temp_{Guid.NewGuid()}.png");
                    
                    using (var fileStream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }
                    
                    // Load as file-based BitmapImage (serializable for Undo)
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(tempPath);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    finalSource = bitmapImage;
                }
                
                var image = new System.Windows.Controls.Image
                {
                    Source = finalSource,
                    Stretch = Stretch.Uniform
                };

                CreateResizableImage(image);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"이미지를 삽입할 수 없습니다: {ex.Message}", "오류");
            }
        }

        public void InsertMediaFile(string filePath, BitmapSource? thumbnail = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    CatchCapture.CustomMessageBox.Show("파일을 찾을 수 없습니다.", "오류");
                    return;
                }

                // 파일 확장자 확인
                string ext = System.IO.Path.GetExtension(filePath).ToLower();
                bool isAudio = ext == ".mp3";
                
                // 컨테이너 Grid 생성
                var grid = new Grid
                {
                    Width = 300,
                    Height = 200,
                    Background = Brushes.Black,
                    Cursor = Cursors.Hand,
                    Tag = filePath // 기존 Tag도 유지 (실시간 동작용)
                };

                // XAML 직렬화 시 Tag가 유실될 수 있으므로, 숨김 텍스트 블록에 경로 저장
                var pathHolder = new TextBlock
                {
                    Text = filePath,
                    Visibility = Visibility.Collapsed,
                    Name = "FilePathHolder"
                };
                grid.Children.Add(pathHolder);

                if (isAudio)
                {
                    // 오디오 파일 - 스피커 아이콘 표시
                    var speakerIcon = new TextBlock
                    {
                        Text = "🔊",
                        FontSize = 60,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(speakerIcon);
                }
                else if (thumbnail != null)
                {
                    // 동영상 썸네일 표시
                    var image = new System.Windows.Controls.Image
                    {
                        Source = thumbnail,
                        Stretch = Stretch.UniformToFill
                    };
                    grid.Children.Add(image);

                    // 재생 버튼 오버레이
                    var playButtonBg = new System.Windows.Shapes.Ellipse
                    {
                        Width = 50,
                        Height = 50,
                        Fill = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(playButtonBg);

                    var playIcon = new TextBlock
                    {
                        Text = "▶",
                        FontSize = 24,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(3, 0, 0, 0)
                    };
                    grid.Children.Add(playIcon);
                }

                // 포맷 레이블 (우측 상단)
                var formatLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 67, 97, 238)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 8, 8, 0)
                };
                var formatText = new TextBlock
                {
                    Text = ext.ToUpper().Replace(".", ""),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                formatLabel.Child = formatText;
                grid.Children.Add(formatLabel);

                // 더블클릭 이벤트 - 윈도우 기본 프로그램으로 열기
                grid.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        try
                        {
                            var path = grid.Tag as string;
                            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = path,
                                    UseShellExecute = true
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            CatchCapture.CustomMessageBox.Show($"파일 열기 실패: {ex.Message}", "오류");
                        }
                        e.Handled = true;
                    }
                };

                // Border로 감싸기 (테두리 제거)
                var border = new Border
                {
                    Child = grid,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 5, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // BlockUIContainer에 추가
                var container = new BlockUIContainer(border);
                
                // 현재 커서 위치에 삽입
                var caretPosition = RtbEditor.CaretPosition;
                
                // BlockUIContainer를 Document에 직접 추가
                RtbEditor.Document.Blocks.Add(container);
                
                // 미디어 다음에 빈 단락 추가하여 커서 위치 확보
                var nextParagraph = new Paragraph();
                RtbEditor.Document.Blocks.Add(nextParagraph);
                
                // 커서를 다음 단락의 시작으로 이동
                RtbEditor.CaretPosition = nextParagraph.ContentStart;
                RtbEditor.Focus();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"미디어 파일을 삽입할 수 없습니다: {ex.Message}", "오류");
            }
        }

        public void InitializeWithImage(ImageSource imageSource)
        {
            _isApplyingProperty = true;
            try
            {
                // Clear existing content using selection (preserves Undo capability)
                RtbEditor.SelectAll();
                RtbEditor.Selection.Text = "";
                
                // Insert image
                InsertImage(imageSource);

                // Set default line height for the following text
                if (RtbEditor.Document.Blocks.LastBlock is Paragraph p)
                {
                    p.LineHeight = 15 * 1.5;
                }
            }
            finally
            {
                _isApplyingProperty = false;
            }
        }

        public List<BitmapSource> GetAllImages()
        {
            var images = new List<BitmapSource>();
            var imageControls = new List<System.Windows.Controls.Image>();
            
            foreach (var block in RtbEditor.Document.Blocks)
            {
                if (block is BlockUIContainer container)
                {
                    // Explicitly handle Grid which is our standard container
                    if (container.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is System.Windows.Controls.Image img) imageControls.Add(img);
                        }
                    }
                    else
                    {
                        FindImagesRecursive(container.Child, imageControls);
                    }
                }
            }
            foreach (var img in imageControls)
            {
                if (img.Source is BitmapSource bs)
                {
                    images.Add(bs);
                }
            }
            
            return images;
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Bitmap))
            {
                var bitmap = e.DataObject.GetData(DataFormats.Bitmap) as BitmapSource;
                if (bitmap != null)
                {
                    InsertImage(bitmap);
                    e.CancelCommand();
                }
            }
            else if (e.DataObject.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.DataObject.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        if (IsImageFile(file))
                        {
                            try { InsertImage(new BitmapImage(new Uri(file))); } catch { }
                        }
                    }
                    e.CancelCommand();
                }
            }
            else if (e.DataObject.GetDataPresent(DataFormats.Html))
            {
                var html = e.DataObject.GetData(DataFormats.Html) as string;
                if (!string.IsNullOrEmpty(html))
                {
                    // 이미지 태그가 있는지 확인
                    var imgRegex = new Regex(@"(<img[^>]+>)", RegexOptions.IgnoreCase);
                    if (imgRegex.IsMatch(html))
                    {
                        // 이미지와 텍스트가 섞여있으므로 직접 처리
                        e.CancelCommand();

                        // 1. Fragment만 추출 (선택적)
                        int startIdx = html.IndexOf("<!--StartFragment-->");
                        int endIdx = html.LastIndexOf("<!--EndFragment-->");
                        string content = html;
                        if (startIdx >= 0 && endIdx > startIdx)
                        {
                            content = html.Substring(startIdx + 20, endIdx - (startIdx + 20));
                        }

                        // 2. 이미지 태그를 기준으로 분할하여 순서대로 삽입
                        var parts = imgRegex.Split(content);
                        foreach (var part in parts)
                        {
                            if (string.IsNullOrEmpty(part)) continue;

                            if (part.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
                            {
                                // 이미지 주소 추출
                                var srcMatch = Regex.Match(part, @"src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                                if (srcMatch.Success)
                                {
                                    string url = srcMatch.Groups[1].Value;
                                    if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(url);
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            InsertImage(bitmap);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            else
                            {
                                // 텍스트 삽입
                                InsertCleanText(part);
                            }
                        }
                    }
                }
            }
        }

        private void InsertCleanText(string html)
        {
            if (string.IsNullOrEmpty(html)) return;

            // HTML 태그 제거 및 구조 보존
            string text = Regex.Replace(html, @"<(br|p|div|tr|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", "");
            text = System.Net.WebUtility.HtmlDecode(text).Trim('\r', '\n', ' ');

            if (!string.IsNullOrEmpty(text))
            {
                // 현재 선택 영역에 텍스트 삽입 (RichTextBox의 Selection은 삽입 후 자동으로 뒤로 이동함)
                RtbEditor.Selection.Text = text;
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (IsImageFile(file))
                        {
                            try { InsertImage(new BitmapImage(new Uri(file))); } catch { }
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLower();
            return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(ext);
        }
        public void UpdateImageSources(List<string> relativePaths)
        {
            var allImages = new List<System.Windows.Controls.Image>();
            foreach (var block in RtbEditor.Document.Blocks)
            {
                if (block is BlockUIContainer container)
                {
                    // Explicitly handle Grid which is our standard container
                    if (container.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is System.Windows.Controls.Image img) allImages.Add(img);
                        }
                    }
                    else
                    {
                        FindImagesRecursive(container.Child, allImages);
                    }
                }
            }

            string imgDir = DatabaseManager.Instance.GetImageFolderPath();
            for (int i = 0; i < allImages.Count && i < relativePaths.Count; i++)
            {
                string fullPath = System.IO.Path.Combine(imgDir, relativePaths[i]);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        allImages[i].Source = bitmap;
                    }
                    catch { }
                }
            }
        }

        private void FindImagesRecursive(UIElement element, List<System.Windows.Controls.Image> result)
        {
            if (element is System.Windows.Controls.Image img)
            {
                result.Add(img);
            }
            else if (element is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    FindImagesRecursive(child, result);
                }
            }
            else if (element is Border border && border.Child != null)
            {
                FindImagesRecursive(border.Child, result);
            }
            else if (element is ContentControl cc && cc.Content is UIElement content)
            {
                FindImagesRecursive(content, result);
            }
        }
    }
}
