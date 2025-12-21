using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
            colorSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Color"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });

            WrapPanel colorGrid = new WrapPanel { Width = 150 };
            foreach (var c in UIConstants.SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateHighlightColorSwatch(c, colorGrid));
            }
            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);

            // --- 2. 중간: 구분선 ---
            Border sep = new Border { Width = 1, Background = (Brush)Application.Current.Resources["ThemeBorder"], Margin = new Thickness(0, 5, 15, 5) };
            Grid.SetColumn(sep, 1);
            mainGrid.Children.Add(sep);

            // --- 3. 오른쪽: 두께 + 투명도 섹션 ---
            StackPanel rightSection = new StackPanel { Width = 100 };
            
            // 두께
            rightSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Thickness"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            Slider thicknessSlider = new Slider { Minimum = 1, Maximum = 50, Value = highlightThickness, Margin = new Thickness(0, 0, 0, 5) };
            thicknessSlider.ValueChanged += (s, e) => { highlightThickness = e.NewValue; };
            rightSection.Children.Add(thicknessSlider);

            // 투명도
            rightSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Opacity"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8) });
            Slider opacitySlider = new Slider { Minimum = 0, Maximum = 1, Value = highlightOpacity, Margin = new Thickness(0, 0, 0, 5) };
            TextBlock opacityVal = new TextBlock { Text = $"{(int)(highlightOpacity * 100)}%", HorizontalAlignment = HorizontalAlignment.Center };
            opacitySlider.ValueChanged += (s, e) => { highlightOpacity = e.NewValue; opacityVal.Text = $"{(int)(e.NewValue * 100)}%"; };
            rightSection.Children.Add(opacitySlider);
            rightSection.Children.Add(opacityVal);

            Grid.SetColumn(rightSection, 2);
            mainGrid.Children.Add(rightSection);

            ToolOptionsPopupContent.Children.Add(mainGrid);
            ToolOptionsPopup.PlacementTarget = HighlightToolButton;
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
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };

            swatch.BorderBrush = (c == selectedColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
            swatch.BorderThickness = new Thickness((c == selectedColor) ? 2 : 1);

            // 선택 표시용 그림자 효과
            if (c == selectedColor)
            {
                swatch.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }

            swatch.MouseLeftButtonDown += (s, e) =>
            {
                selectedColor = c;
                UpdateColorSelection(parentPanel);
            };

            return swatch;
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
            
            foreach (var c in UIConstants.SharedColorPalette)
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
                Background = (Brush)Application.Current.Resources["ThemeBorder"],
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
                    ApplyPropertyChangesToSelectedObject();
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
                Foreground = (Brush)Application.Current.Resources["ThemeForeground"],
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };

            sizeSlider.ValueChanged += (s, e) =>
            {
                textSize = e.NewValue;
                sizeValue.Text = $"{e.NewValue:F0}px";
                ApplyPropertyChangesToSelectedObject();
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
            boldCheckBox.Checked += (s, e) => { textFontWeight = FontWeights.Bold; ApplyPropertyChangesToSelectedObject(); };
            boldCheckBox.Unchecked += (s, e) => { textFontWeight = FontWeights.Normal; ApplyPropertyChangesToSelectedObject(); };
            stylePanel.Children.Add(boldCheckBox);

            CheckBox italicCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Italic"),
                IsChecked = textFontStyle == FontStyles.Italic,
                Margin = new Thickness(0, 2, 0, 2)
            };
            italicCheckBox.Checked += (s, e) => { textFontStyle = FontStyles.Italic; ApplyPropertyChangesToSelectedObject(); };
            italicCheckBox.Unchecked += (s, e) => { textFontStyle = FontStyles.Normal; ApplyPropertyChangesToSelectedObject(); };
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
                BorderBrush = (c == selectedColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness((c == selectedColor) ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };

            if (c == selectedColor)
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
                selectedColor = c;
                UpdateColorSelection(parentPanel);
                ApplyPropertyChangesToSelectedObject();
            };

            return swatch;
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
            if (currentEditMode != EditMode.Highlight)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Highlight;
                SetActiveToolButton(HighlightToolButton);
            }

            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowHighlightOptionsPopup();
        }

        private void TextOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Text)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Text;
                SetActiveToolButton(TextToolButton);
            }

            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowTextOptions();
        }

        private void MosaicOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Mosaic)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Mosaic;
                SetActiveToolButton(MosaicToolButton);
            }

            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowMosaicOptions();
        }

        private void EraserOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Eraser)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Eraser;
                SetActiveToolButton(EraserToolButton);
            }

            if (ToolOptionsPopup.IsOpen)
            {
                ToolOptionsPopup.IsOpen = false;
                return;
            }
            ShowEraserOptions();
        }

        #endregion

        #region 마법봉 옵션

        private void ShowMagicWandOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            StackPanel mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // 허용 오차 라벨
            var toleranceLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("MagicWandToleranceLabel"), 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            };
            mainPanel.Children.Add(toleranceLabel);

            // 허용 오차 슬라이더
            StackPanel tolerancePanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            Slider toleranceSlider = new Slider
            {
                Minimum = 0,
                Maximum = 128,
                Value = magicWandTolerance,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock toleranceValue = new TextBlock
            {
                Text = $"{magicWandTolerance}",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Width = 30
            };

            toleranceSlider.ValueChanged += (s, e) => 
            {
                SetMagicWandTolerance((int)e.NewValue);
                toleranceValue.Text = $"{(int)e.NewValue}";
            };

            tolerancePanel.Children.Add(toleranceSlider);
            tolerancePanel.Children.Add(toleranceValue);
            mainPanel.Children.Add(tolerancePanel);

            // 연속 영역 체크박스
            CheckBox contiguousCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("MagicWandContiguousOnly"),
                IsChecked = magicWandContiguous,
                Margin = new Thickness(0, 10, 0, 0)
            };
            contiguousCheckBox.Checked += (s, e) => SetMagicWandContiguous(true);
            contiguousCheckBox.Unchecked += (s, e) => SetMagicWandContiguous(false);
            mainPanel.Children.Add(contiguousCheckBox);

            // 설명 텍스트
            TextBlock descText = new TextBlock
            {
                Text = LocalizationManager.Get("MagicWandDesc"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["ThemeForeground"],
                Opacity = 0.6,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(descText);

            ToolOptionsPopupContent.Children.Add(mainPanel);

            ToolOptionsPopup.PlacementTarget = this.FindName("MagicWandToolButton") as FrameworkElement ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion
        #region 넘버링 옵션

        private void ShowNumberingOptionsPopup()
        {
            ToolOptionsPopupContent.Children.Clear();

            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 크기

            // 1. 색상
            StackPanel colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            colorSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Color"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            WrapPanel colorGrid = new WrapPanel { Width = 150 };
            foreach (var c in UIConstants.SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateColorSwatch(c, colorGrid, (color) => { selectedColor = color; UpdateColorSelection(colorGrid, selectedColor); }));
            }
            colorSection.Children.Add(colorGrid);
            Grid.SetColumn(colorSection, 0);
            mainGrid.Children.Add(colorSection);

            // 2. 구분선
            Border sep = new Border { Width = 1, Background = (Brush)Application.Current.Resources["ThemeBorder"], Margin = new Thickness(0, 5, 15, 5) };
            Grid.SetColumn(sep, 1);
            mainGrid.Children.Add(sep);

            // 3. 크기
            StackPanel sizeSection = new StackPanel();
            sizeSection.Children.Add(new TextBlock { Text = LocalizationManager.Get("Size"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            
            // _editorManager의 NumberingBadgeSize 사용
            double currentSize = _editorManager.NumberingBadgeSize;
            
            Slider sizeSlider = new Slider { Minimum = 10, Maximum = 60, Value = currentSize, Width = 100, Margin = new Thickness(0, 0, 0, 5) };
            TextBlock sizeVal = new TextBlock { Text = $"{(int)currentSize}px", HorizontalAlignment = HorizontalAlignment.Center };
            
            sizeSlider.ValueChanged += (s, e) => { 
                double val = e.NewValue;
                _editorManager.NumberingBadgeSize = val;
                _editorManager.NumberingTextSize = val * 0.5; // 50% 비율 유지
                sizeVal.Text = $"{(int)val}px"; 
            };
            
            sizeSection.Children.Add(sizeSlider);
            sizeSection.Children.Add(sizeVal);
            Grid.SetColumn(sizeSection, 2);
            mainGrid.Children.Add(sizeSection);

            ToolOptionsPopupContent.Children.Add(mainGrid);
            ToolOptionsPopup.PlacementTarget = NumberingToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        private Border CreateColorSwatch(Color c, WrapPanel parent, Action<Color> onSelected)
        {
            var swatch = new Border { Width = 20, Height = 20, Background = new SolidColorBrush(c), Margin = new Thickness(2), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand };
            UpdateSwatchSelection(swatch, c == selectedColor);
            swatch.MouseLeftButtonDown += (s, e) => { onSelected(c); };
            return swatch;
        }

        private void UpdateSwatchSelection(Border b, bool isSelected)
        {
            b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
            b.BorderThickness = new Thickness(isSelected ? 2 : 1);
            b.Effect = isSelected ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 0, Opacity = 0.5 } : null;
        }

        private void UpdateColorSelection(WrapPanel panel, Color current)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                    UpdateSwatchSelection(b, sc.Color == current);
            }
        }

        #endregion
    }
}
