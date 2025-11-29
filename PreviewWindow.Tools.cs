using System;
using System.Collections.Generic;
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
    /// PreviewWindow의 도구 옵션 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 펜 도구 옵션

        /// <summary>
        /// 펜 옵션 팝업 표시
        /// </summary>
        private void ShowPenOptionsPopup()
        {
            ToolOptionsPopupContent.Children.Clear();

            // 메인 그리드 (좌: 색상, 중: 구분선, 우: 두께)
            Grid mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 색상
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 구분선
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 두께

            // --- 1. 왼쪽: 색상 섹션 ---
            StackPanel colorSection = new StackPanel { Margin = new Thickness(0, 0, 15, 0) };
            
            // 라벨
            TextBlock colorLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Color"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            colorSection.Children.Add(colorLabel);

            // 색상 그리드 (WrapPanel)
            WrapPanel colorGrid = new WrapPanel { Width = 130 }; // 5개씩 배치될 정도의 너비
            
            // 기본 색상들 (공용 팔레트 사용, 투명 제외)
            foreach (var c in SharedColorPalette)
            {
                if (c == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
            }

            // 사용자 정의 색상 추가
            foreach (var c in customColors)
            {
                colorGrid.Children.Add(CreateColorSwatch(c, colorGrid));
            }

            // [+] 버튼 (색상 추가)
            Button addButton = new Button
            {
                Width = 20, Height = 20, Margin = new Thickness(2),
                Background = Brushes.White, 
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)), 
                BorderThickness = new Thickness(1),
                Content = new TextBlock 
                { 
                    Text = "+", 
                    FontWeight = FontWeights.Bold, 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center 
                },
                Cursor = Cursors.Hand,
                ToolTip = LocalizationManager.Get("AddColor")
            };
            
            // 둥근 모서리 스타일 적용
            ControlTemplate buttonTemplate = new ControlTemplate(typeof(Button));
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            buttonTemplate.VisualTree = borderFactory;
            addButton.Template = buttonTemplate;

            addButton.Click += (s, e) => 
            {
                var dialog = new System.Windows.Forms.ColorDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    
                    // customColors 리스트에 추가 (중복 방지)
                    if (!customColors.Contains(newColor))
                    {
                        customColors.Add(newColor);
                    }

                    // 1. [+] 버튼을 잠시 제거
                    colorGrid.Children.Remove(addButton);

                    // 2. 새 색상 버튼 추가
                    var newSwatch = CreateColorSwatch(newColor, colorGrid);
                    colorGrid.Children.Add(newSwatch);

                    // 3. [+] 버튼 다시 추가 (맨 뒤로)
                    colorGrid.Children.Add(addButton);
                    
                    // 4. 바로 선택 처리
                    penColor = newColor;
                    UpdateColorSelection(colorGrid);
                }
            };
            colorGrid.Children.Add(addButton);
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

            // 라벨
            TextBlock thicknessLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("Thickness"), 
                FontWeight = FontWeights.SemiBold, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            thicknessSection.Children.Add(thicknessLabel);

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
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // 선
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });    // 텍스트

                // 선 (시각적 표시)
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

                // 텍스트 (예: 3px)
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

                // 선택 표시 (배경색)
                if (penThickness == p)
                {
                    item.Background = new SolidColorBrush(Color.FromRgb(240, 240, 255)); // 연한 파랑
                    text.Foreground = Brushes.Black;
                    text.FontWeight = FontWeights.Bold;
                }

                // 클릭 이벤트
                int thickness = p; // 클로저 문제 방지
                item.MouseLeftButtonDown += (s, e) =>
                {
                    penThickness = thickness;
                    // 다시 그리기 (선택 상태 업데이트)
                    ShowPenOptionsPopup(); 
                };

                thicknessList.Children.Add(item);
            }
            thicknessSection.Children.Add(thicknessList);

            Grid.SetColumn(thicknessSection, 2);
            mainGrid.Children.Add(thicknessSection);

            // 팝업에 추가
            ToolOptionsPopupContent.Children.Add(mainGrid);

            // 팝업 위치 설정
            var penToolButton = this.FindName("PenToolButton") as FrameworkElement;
            if (penToolButton != null)
            {
                ToolOptionsPopup.PlacementTarget = penToolButton;
            }
            else
            {
                ToolOptionsPopup.PlacementTarget = this;
            }

            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        /// <summary>
        /// 색상 버튼 생성 헬퍼
        /// </summary>
        private Border CreateColorSwatch(Color c, WrapPanel parentPanel)
        {
            Border swatch = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(c),
                BorderBrush = (c == penColor) ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(c == penColor ? 2 : 1),
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4), // 둥근 모서리
                Cursor = Cursors.Hand
            };

            // 선택 표시용 그림자 효과
            if (c == penColor)
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
                penColor = c;
                UpdateColorSelection(parentPanel);
            };

            return swatch;
        }

        /// <summary>
        /// 색상 선택 상태 업데이트 헬퍼
        /// </summary>
        private void UpdateColorSelection(WrapPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                {
                    bool isSelected = (sc.Color == penColor);
                    b.BorderBrush = isSelected ? Brushes.Black : new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    b.BorderThickness = new Thickness(isSelected ? 2 : 1);
                    b.Effect = isSelected ? new DropShadowEffect
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
    }
}
