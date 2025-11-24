using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

            // 색상 선택
            var colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(colorLabel);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ToolOptionsPopupContent.Children.Add(colorPanel);

            foreach (Color color in SharedColorPalette)
            {
                if (color == Colors.Transparent) continue;

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
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            thicknessSlider.ValueChanged += (s, e) => highlightThickness = e.NewValue;
            ToolOptionsPopupContent.Children.Add(thicknessSlider);

            TextBlock thicknessValue = new TextBlock
            {
                Text = $"{highlightThickness:F0}px",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            thicknessSlider.ValueChanged += (s, e) => thicknessValue.Text = $"{e.NewValue:F0}px";
            ToolOptionsPopupContent.Children.Add(thicknessValue);

            // 투명도 선택 라벨
            var opacityLabel = new TextBlock { Text = "투명도:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(opacityLabel);

            Slider opacitySlider = new Slider
            {
                Minimum = 10,
                Maximum = 255,
                Value = highlightColor.A,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                highlightColor = Color.FromArgb((byte)e.NewValue, highlightColor.R, highlightColor.G, highlightColor.B);
            };
            ToolOptionsPopupContent.Children.Add(opacitySlider);

            TextBlock opacityValue = new TextBlock
            {
                Text = $"{(highlightColor.A / 255.0 * 100):F0}%",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            opacitySlider.ValueChanged += (s, e) => opacityValue.Text = $"{(e.NewValue / 255.0 * 100):F0}%";
            ToolOptionsPopupContent.Children.Add(opacityValue);

            // 팝업 위치 설정
            var highlightButton = this.FindName("HighlightButton") as FrameworkElement;
            if (highlightButton != null)
            {
                ToolOptionsPopup.PlacementTarget = highlightButton;
            }
            else
            {
                ToolOptionsPopup.PlacementTarget = this;
            }

            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 텍스트 옵션

        private void ShowTextOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 폰트 크기
            var sizeLabel = new TextBlock { Text = "크기:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(sizeLabel);

            Slider sizeSlider = new Slider
            {
                Minimum = 8,
                Maximum = 72,
                Value = textSize,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            sizeSlider.ValueChanged += (s, e) => textSize = e.NewValue;
            ToolOptionsPopupContent.Children.Add(sizeSlider);

            TextBlock sizeValue = new TextBlock
            {
                Text = $"{textSize:F0}px",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            sizeSlider.ValueChanged += (s, e) => sizeValue.Text = $"{e.NewValue:F0}px";
            ToolOptionsPopupContent.Children.Add(sizeValue);

            // 색상 선택
            var colorLabel = new TextBlock { Text = "색상:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(colorLabel);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ToolOptionsPopupContent.Children.Add(colorPanel);

            foreach (Color color in SharedColorPalette)
            {
                if (color == Colors.Transparent) continue;
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = (color == textColor) ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };

                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    textColor = color;
                    foreach (var child in colorPanel.Children)
                    {
                        if (child is Border b)
                        {
                            b.BorderBrush = (b.Background is SolidColorBrush sc && sc.Color == textColor) ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };

                colorPanel.Children.Add(colorBorder);
            }

            // 폰트 스타일
            var styleLabel = new TextBlock { Text = "스타일:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            ToolOptionsPopupContent.Children.Add(styleLabel);

            CheckBox boldCheckBox = new CheckBox
            {
                Content = "굵게",
                IsChecked = textFontWeight == FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            boldCheckBox.Checked += (s, e) => textFontWeight = FontWeights.Bold;
            boldCheckBox.Unchecked += (s, e) => textFontWeight = FontWeights.Normal;
            ToolOptionsPopupContent.Children.Add(boldCheckBox);

            CheckBox italicCheckBox = new CheckBox
            {
                Content = "기울임",
                IsChecked = textFontStyle == FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            italicCheckBox.Checked += (s, e) => textFontStyle = FontStyles.Italic;
            italicCheckBox.Unchecked += (s, e) => textFontStyle = FontStyles.Normal;
            ToolOptionsPopupContent.Children.Add(italicCheckBox);

            CheckBox underlineCheckBox = new CheckBox
            {
                Content = "밑줄",
                IsChecked = textUnderlineEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            underlineCheckBox.Checked += (s, e) => textUnderlineEnabled = true;
            underlineCheckBox.Unchecked += (s, e) => textUnderlineEnabled = false;
            ToolOptionsPopupContent.Children.Add(underlineCheckBox);

            CheckBox shadowCheckBox = new CheckBox
            {
                Content = "그림자",
                IsChecked = textShadowEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            shadowCheckBox.Checked += (s, e) => textShadowEnabled = true;
            shadowCheckBox.Unchecked += (s, e) => textShadowEnabled = false;
            ToolOptionsPopupContent.Children.Add(shadowCheckBox);

            // 팝업 위치 설정
            var textButton = this.FindName("TextButton") as FrameworkElement;
            if (textButton != null)
            {
                ToolOptionsPopup.PlacementTarget = textButton;
            }
            else
            {
                ToolOptionsPopup.PlacementTarget = this;
            }

            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 모자이크 옵션

        private void ShowMosaicOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            var sizeLabel = new TextBlock { Text = "픽셀 크기:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
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

            ToolOptionsPopup.PlacementTarget = this.FindName("MosaicButton") as FrameworkElement ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 지우개 옵션

        private void ShowEraserOptions()
        {
            ToolOptionsPopupContent.Children.Clear();

            var sizeLabel = new TextBlock { Text = "크기:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
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

            ToolOptionsPopup.PlacementTarget = this.FindName("EraserButton") as FrameworkElement ?? this;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion
    }
}
