using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CatchCapture.Models;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 도구 옵션 팝업 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 형광펜 옵션

        private void ShowHighlightOptionsPopup()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 메인 그리드 (좌: 색상, 중: 구분선, 우: 두께+투명도)
            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 두께+투명도

            // --- 1. 왼쪽: 색상 섹션 ---
            StackPanel colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            
            TextBlock colorLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Color"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            colorSection.Children.Add(colorLabel);

            // 색상 그리드 (WrapPanel)
            WrapPanel colorGrid = new WrapPanel { Width = 150 };
            
            foreach (var c in SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateHighlightColorSwatch(c, colorGrid));
            }

            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);

            // --- 2. 가운데: 구분선 ---
            Border separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 5, 15, 5)
            };
            Grid.SetColumn(separator, 1);
            mainGrid.Children.Add(separator);

            // --- 3. 오른쪽: 두께+투명도 섹션 ---
            StackPanel rightSection = new StackPanel();

            // 두께 라벨
            TextBlock thicknessLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Thickness"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            rightSection.Children.Add(thicknessLabel);

            // 두께 프리셋 리스트
            StackPanel thicknessList = new StackPanel();
            int[] presets = new int[] { 1, 3, 5, 8, 12 };

            foreach (var p in presets)
            {
                Grid item = new Grid 
                { 
                    Margin = new Thickness(0, 0, 0, 8), 
                    Cursor = Cursors.Hand, 
                    Background = Brushes.Transparent 
                };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Border line = new Border
                {
                    Height = p,
                    Width = 30,
                    Background = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(line, 0);
                item.Children.Add(line);

                TextBlock text = new TextBlock
                {
                    Text = $"{p}px",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(text, 1);
                item.Children.Add(text);

                if (highlightThickness == p)
                {
                    item.Background = new SolidColorBrush(Color.FromRgb(240, 240, 255));
                    text.Foreground = Brushes.Black;
                    text.FontWeight = FontWeights.Bold;
                }

                int thickness = p;
                item.MouseLeftButtonDown += (s, e) =>
                {
                    highlightThickness = thickness;
                    ShowHighlightOptionsPopup();
                };

                thicknessList.Children.Add(item);
            }
            rightSection.Children.Add(thicknessList);

            // 투명도 섹션
            rightSection.Children.Add(new Border { Height = 10 }); // 간격

            TextBlock opacityLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Opacity"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 4) 
            };
            rightSection.Children.Add(opacityLabel);

            Slider opacitySlider = new Slider
            {
                Minimum = 10,
                Maximum = 255,
                Value = highlightColor.A,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            TextBlock opacityValue = new TextBlock
            {
                Text = $"{(highlightColor.A / 255.0 * 100):F0}%",
                FontSize = 11,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };

            opacitySlider.ValueChanged += (s, e) =>
            {
                highlightColor = Color.FromArgb((byte)e.NewValue, highlightColor.R, highlightColor.G, highlightColor.B);
                opacityValue.Text = $"{(e.NewValue / 255.0 * 100):F0}%";
            };

            rightSection.Children.Add(opacitySlider);
            rightSection.Children.Add(opacityValue);

            Grid.SetColumn(rightSection, 2);
            mainGrid.Children.Add(rightSection);

            ToolOptionsPopupContent.Children.Add(mainGrid);

            var highlightButton = this.FindName("HighlightToolButton") as FrameworkElement;
            ToolOptionsPopup.PlacementTarget = highlightButton ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        private Border CreateHighlightColorSwatch(Color c, WrapPanel parentPanel)
        {
            Border swatch = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(c),
                BorderBrush = (c.R == highlightColor.R && c.G == highlightColor.G && c.B == highlightColor.B) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness((c.R == highlightColor.R && c.G == highlightColor.G && c.B == highlightColor.B) ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };

            if (c.R == highlightColor.R && c.G == highlightColor.G && c.B == highlightColor.B)
            {
                swatch.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }

            swatch.MouseLeftButtonDown += (s, e) =>
            {
                highlightColor = Color.FromArgb(highlightColor.A, c.R, c.G, c.B);
                UpdateHighlightColorSelection(parentPanel);
            };

            return swatch;
        }

        private void UpdateHighlightColorSelection(WrapPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                {
                    bool isSelected = (sc.Color.R == highlightColor.R && sc.Color.G == highlightColor.G && sc.Color.B == highlightColor.B);
                    b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    b.BorderThickness = new Thickness(isSelected ? 2 : 1);
                    b.Effect = isSelected ? new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 2,
                        ShadowDepth = 0,
                        Opacity = 0.5
                    } : null;
                }
            }
        }

        #endregion

        #region 텍스트 옵션

        private void ShowTextOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 메인 그리드 (좌: 색상, 중: 구분선, 우: 크기+스타일)
            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // --- 1. 왼쪽: 색상 섹션 ---
            StackPanel colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            
            TextBlock colorLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Color"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            colorSection.Children.Add(colorLabel);

            WrapPanel colorGrid = new WrapPanel { Width = 150 };
            
            foreach (var c in SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateTextColorSwatch(c, colorGrid));
            }

            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);

            // --- 2. 가운데: 구분선 ---
            Border separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 5, 15, 5)
            };
            Grid.SetColumn(separator, 1);
            mainGrid.Children.Add(separator);

            // --- 3. 오른쪽: 크기+스타일 섹션 ---
            StackPanel rightSection = new StackPanel();

            // 폰트 선택
            TextBlock fontLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Font"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 4) 
            };
            rightSection.Children.Add(fontLabel);

            ComboBox fontComboBox = new ComboBox
            {
                Width = 120,
                Height = 25,
                Margin = new Thickness(0, 0, 0, 10),
                // 주요 폰트만 표시 (시스템 전체 폰트는 너무 많아서 로딩 지연 가능성)
                ItemsSource = new List<string> { 
                    "맑은 고딕", "굴림", "돋움", "바탕", "궁서", 
                    "Arial", "Segoe UI", "Verdana", "Tahoma", "Times New Roman", 
                    "Consolas", "Impact", "Comic Sans MS" 
                },
                SelectedItem = textFontFamily
            };
            
            fontComboBox.SelectionChanged += (s, e) =>
            {
                if (fontComboBox.SelectedItem is string fontName)
                {
                    textFontFamily = fontName;
                }
            };
            rightSection.Children.Add(fontComboBox);

            // 크기 라벨
            TextBlock sizeLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("SizeLabel"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 4) 
            };
            rightSection.Children.Add(sizeLabel);

            Slider sizeSlider = new Slider
            {
                Minimum = 8,
                Maximum = 72,
                Value = textSize,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            TextBlock sizeValue = new TextBlock
            {
                Text = $"{textSize:F0}px",
                FontSize = 11,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                textSize = e.NewValue;
                sizeValue.Text = $"{e.NewValue:F0}px";
            };

            rightSection.Children.Add(sizeSlider);
            rightSection.Children.Add(sizeValue);

            // 스타일 섹션
            rightSection.Children.Add(new Border { Height = 10 });

            TextBlock styleLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Style"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 4) 
            };
            rightSection.Children.Add(styleLabel);

            StackPanel stylePanel = new StackPanel { Orientation = Orientation.Vertical };

            CheckBox boldCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Bold"),
                IsChecked = textFontWeight == FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 2)
            };
            boldCheckBox.Checked += (s, e) => textFontWeight = FontWeights.Bold;
            boldCheckBox.Unchecked += (s, e) => textFontWeight = FontWeights.Normal;
            stylePanel.Children.Add(boldCheckBox);

            CheckBox italicCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Italic"),
                IsChecked = textFontStyle == FontStyles.Italic,
                Margin = new Thickness(0, 2, 0, 2)
            };
            italicCheckBox.Checked += (s, e) => textFontStyle = FontStyles.Italic;
            italicCheckBox.Unchecked += (s, e) => textFontStyle = FontStyles.Normal;
            stylePanel.Children.Add(italicCheckBox);

            CheckBox underlineCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Underline"),
                IsChecked = textUnderlineEnabled,
                Margin = new Thickness(0, 2, 0, 2)
            };
            underlineCheckBox.Checked += (s, e) => textUnderlineEnabled = true;
            underlineCheckBox.Unchecked += (s, e) => textUnderlineEnabled = false;
            stylePanel.Children.Add(underlineCheckBox);

            CheckBox shadowCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Shadow"),
                IsChecked = textShadowEnabled,
                Margin = new Thickness(0, 2, 0, 2)
            };
            shadowCheckBox.Checked += (s, e) => textShadowEnabled = true;
            shadowCheckBox.Unchecked += (s, e) => textShadowEnabled = false;
            stylePanel.Children.Add(shadowCheckBox);

            rightSection.Children.Add(stylePanel);

            Grid.SetColumn(rightSection, 2);
            mainGrid.Children.Add(rightSection);

            ToolOptionsPopupContent.Children.Add(mainGrid);

            var textButton = this.FindName("TextToolButton") as FrameworkElement;
            ToolOptionsPopup.PlacementTarget = textButton ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        private Border CreateTextColorSwatch(Color c, WrapPanel parentPanel)
        {
            Border swatch = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(c),
                BorderBrush = (c == textColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness((c == textColor) ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };

            if (c == textColor)
            {
                swatch.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }

            swatch.MouseLeftButtonDown += (s, e) =>
            {
                textColor = c;
                UpdateTextColorSelection(parentPanel);
            };

            return swatch;
        }

        private void UpdateTextColorSelection(WrapPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                {
                    bool isSelected = (sc.Color == textColor);
                    b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    b.BorderThickness = new Thickness(isSelected ? 2 : 1);
                    b.Effect = isSelected ? new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 2,
                        ShadowDepth = 0,
                        Opacity = 0.5
                    } : null;
                }
            }
        }

        #endregion

        #region 펜 옵션

        // NOTE: Renamed to avoid duplicating existing ShowPenOptionsPopup in PreviewWindow.Tools.cs
        private void BuildPenOptionsPopup()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 메인 그리드 (좌: 색상, 중: 구분선, 우: 두께)
            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 두께

            // --- 1. 왼쪽: 색상 섹션 ---
            StackPanel colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };

            TextBlock colorLabel = new TextBlock
            {
                Text = LocalizationManager.Get("Color"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            colorSection.Children.Add(colorLabel);

            WrapPanel colorGrid = new WrapPanel { Width = 150 };
            foreach (var c in SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                var swatch = new Border
                {
                    Width = 20,
                    Height = 20,
                    Background = new SolidColorBrush(c),
                    BorderBrush = (c == penColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderThickness = new Thickness((c == penColor) ? 2 : 1),
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand
                };
                swatch.MouseLeftButtonDown += (s, e) =>
                {
                    penColor = c;
                    foreach (var child in colorGrid.Children)
                    {
                        if (child is Border b && b.Background is SolidColorBrush sc)
                        {
                            bool isSelected = (sc.Color == penColor);
                            b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                            b.BorderThickness = new Thickness(isSelected ? 2 : 1);
                        }
                    }
                };
                colorGrid.Children.Add(swatch);
            }
            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);

            // --- 2. 가운데: 구분선 ---
            Border separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 5, 15, 5)
            };
            Grid.SetColumn(separator, 1);
            mainGrid.Children.Add(separator);

            // --- 3. 오른쪽: 두께 섹션 ---
            StackPanel thicknessSection = new StackPanel();
            TextBlock thicknessLabel = new TextBlock
            {
                Text = LocalizationManager.Get("Thickness"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            thicknessSection.Children.Add(thicknessLabel);

            StackPanel thicknessList = new StackPanel();
            int[] presets = new int[] { 1, 3, 5, 8, 12 };
            foreach (var p in presets)
            {
                Grid item = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Border line = new Border
                {
                    Height = p,
                    Width = 30,
                    Background = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(line, 0);
                item.Children.Add(line);

                TextBlock text = new TextBlock
                {
                    Text = $"{p}px",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(text, 1);
                item.Children.Add(text);

                int thickness = p;
                item.MouseLeftButtonDown += (s, e) =>
                {
                    penThickness = thickness;
                    foreach (var child in thicknessList.Children)
                    {
                        if (child is Grid g) g.Background = Brushes.Transparent;
                    }
                    item.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
                };

                thicknessList.Children.Add(item);
            }
            thicknessSection.Children.Add(thicknessList);
            Grid.SetColumn(thicknessSection, 2);
            mainGrid.Children.Add(thicknessSection);

            ToolOptionsPopupContent.Children.Add(mainGrid);

            var penButton = this.FindName("PenToolButton") as FrameworkElement;
            ToolOptionsPopup.PlacementTarget = penButton ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 모자이크 옵션

        private void ShowMosaicOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            var sizeLabel = new TextBlock { Text = LocalizationManager.Get("PixelSizeLabel"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(sizeLabel);

            Slider sizeSlider = new Slider
            {
                Minimum = 5,
                Maximum = 50,
                Value = mosaicSize,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };
            sizeSlider.ValueChanged += (s, e) => mosaicSize = (int)e.NewValue;
            ToolOptionsPopupContent.Children.Add(sizeSlider);

            TextBlock sizeValue = new TextBlock
            {
                Text = $"{mosaicSize}px",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            sizeSlider.ValueChanged += (s, e) => sizeValue.Text = $"{(int)e.NewValue}px";
            ToolOptionsPopupContent.Children.Add(sizeValue);

            ToolOptionsPopup.PlacementTarget = this.FindName("MosaicToolButton") as FrameworkElement ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 지우개 옵션

        private void ShowEraserOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            var sizeLabel = new TextBlock { Text = LocalizationManager.Get("SizeLabel") + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(sizeLabel);

            Slider sizeSlider = new Slider
            {
                Minimum = 5,
                Maximum = 100,
                Value = eraserSize,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };
            sizeSlider.ValueChanged += (s, e) => eraserSize = (int)e.NewValue;
            ToolOptionsPopupContent.Children.Add(sizeSlider);

            TextBlock sizeValue = new TextBlock
            {
                Text = $"{eraserSize}px",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            sizeSlider.ValueChanged += (s, e) => sizeValue.Text = $"{(int)e.NewValue}px";
            ToolOptionsPopupContent.Children.Add(sizeValue);

            ToolOptionsPopup.PlacementTarget = this.FindName("EraserToolButton") as FrameworkElement ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 옵션 버튼 클릭 핸들러

        private void HighlightOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowHighlightOptionsPopup();
        }

        private void TextOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowTextOptions();
        }

        private void MosaicOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowMosaicOptions();
        }

        private void EraserOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowEraserOptions();
        }

        #endregion
    }
}
