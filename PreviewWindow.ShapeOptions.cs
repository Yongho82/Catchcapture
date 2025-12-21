using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input; 
using System.Windows.Controls.Primitives; 
using CatchCapture.Utilities;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class PreviewWindow
    {
        // 팝업 내 버튼들 참조 저장용 (도형 옵션 전용)
        private List<Button> shapeTypeButtons = new List<Button>();
        private List<Button> fillButtons = new List<Button>();
        private List<Button> colorButtons = new List<Button>();

        private void ShowShapeOptionsPopup()
        {
            // 팝업 내용 초기화
            ToolOptionsPopupContent.Children.Clear();
            ToolOptionsPopupContent.Orientation = Orientation.Vertical;

            // 버튼 리스트 초기화
            shapeTypeButtons.Clear();
            fillButtons.Clear();
            colorButtons.Clear();

            // 메인 컨테이너 (적절한 크기로)
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = 240,
                Margin = new Thickness(6)
            };

            // 1. 도형 종류 섹션
            var shapeTypePanel = CreateShapeTypeSection();
            mainPanel.Children.Add(shapeTypePanel);

            // 2. 선 스타일 섹션 (도형 바로 아래, 간격 더 줄임)
            var lineStylePanel = CreateLineStyleSection();
            lineStylePanel.Margin = new Thickness(0, 4, 0, 0);
            mainPanel.Children.Add(lineStylePanel);

            // 구분선 (더 얇고 간격 더 줄임)
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Margin = new Thickness(0, 6, 0, 6)
            };
            mainPanel.Children.Add(separator);

            // 3. 색상 팔레트 섹션
            var colorPanel = CreateColorPaletteSection();
            mainPanel.Children.Add(colorPanel);

            ToolOptionsPopupContent.Children.Add(mainPanel);

            // 팝업 위치 설정 (ShapeButton 아래)
            ToolOptionsPopup.PlacementTarget = ShapeButton;
            ToolOptionsPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ToolOptionsPopup.HorizontalOffset = 0;
            ToolOptionsPopup.VerticalOffset = 5;

            // 팝업 열기
            ToolOptionsPopup.IsOpen = true;
        }



        private StackPanel CreateShapeTypeSection()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };

            // 제목
            var title = new TextBlock
            {
                Text = LocalizationManager.Get("ShapeLbl"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            panel.Children.Add(title);

            // 도형 버튼들 (1행 배치, 더 컴팩트하게)
            var shapesPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var rectBtn = CreateShapeButton("▢", ShapeType.Rectangle);
            var ellipseBtn = CreateShapeButton("○", ShapeType.Ellipse);
            var lineBtn = CreateShapeButton("╱", ShapeType.Line);
            var arrowBtn = CreateShapeButton("↗", ShapeType.Arrow);

            shapesPanel.Children.Add(rectBtn);
            shapesPanel.Children.Add(ellipseBtn);
            shapesPanel.Children.Add(lineBtn);
            shapesPanel.Children.Add(arrowBtn);

            panel.Children.Add(shapesPanel);
            return panel;
        }

        private Button CreateShapeButton(string content, ShapeType type)
        {
            var button = new Button
            {
                Content = content,
                Width = 40,
                Height = 32,
                Margin = new Thickness(3, 2, 3, 2),
                FontSize = 14,
                Background = shapeType == type ? new SolidColorBrush(Color.FromRgb(72, 152, 255)) : Brushes.White,
                Foreground = shapeType == type ? Brushes.White : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = null,
                Tag = type
            };

            // 리스트에 추가
            shapeTypeButtons.Add(button);

            // 호버 효과
            button.MouseEnter += (s, e) => { if (shapeType != type) button.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)); };
            button.MouseLeave += (s, e) => { if (shapeType != type) button.Background = Brushes.White; };

            button.Click += (s, e) =>
            {
                shapeType = type;
                UpdateShapeTypeButtonsInPopup();
                ApplyPropertyChangesToSelectedObject();

                // 도형 그리기 모드 설정 (선택 모드였더라도 새 도형을 선택하면 도형 모드로 전환)
                if (currentEditMode != EditMode.Shape)
                {
                    CancelCurrentEditMode();
                    currentEditMode = EditMode.Shape;
                    ImageCanvas.Cursor = Cursors.Cross;
                    SetActiveToolButton(ShapeButton);
                    
                    // 팝업 닫기 (새 도형 모드로 전환할 때만)
                    ToolOptionsPopup.IsOpen = false;
                }
            };

            return button;
        }

        private StackPanel CreateLineStyleSection()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };

            // 제목
            var title = new TextBlock
            {
                Text = LocalizationManager.Get("LineStyle"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            panel.Children.Add(title);

            // 채우기 옵션 (더 컴팩트하게)
            var fillPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var outlineBtn = CreateFillButton(LocalizationManager.Get("Outline"), false);
            var fillBtn = CreateFillButton(LocalizationManager.Get("Fill"), true);
            fillPanel.Children.Add(outlineBtn);
            fillPanel.Children.Add(fillBtn);
            panel.Children.Add(fillPanel);

            // [추가] 채우기 투명도 슬라이더 (채우기 모드일 때만 활성화)
            var opacityPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Margin = new Thickness(0, 8, 0, 0),
                IsEnabled = shapeIsFilled // 초기 상태 설정
            };
            
            var opacityLabel = new TextBlock 
            { 
                Text = LocalizationManager.Get("FillOpacity"), 
                FontSize = 10, 
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            
            var opacitySlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = shapeFillOpacity * 100,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center,
                IsSnapToTickEnabled = true,
                TickFrequency = 10
            };
            
            var opacityValueText = new TextBlock 
            { 
                Text = $"{(int)(shapeFillOpacity * 100)}%", 
                FontSize = 10, 
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Width = 30
            };

            opacitySlider.ValueChanged += (s, e) => 
            {
                shapeFillOpacity = opacitySlider.Value / 100.0;
                opacityValueText.Text = $"{(int)opacitySlider.Value}%";
                ApplyPropertyChangesToSelectedObject();
            };

            // 채우기 버튼 클릭 시 슬라이더 활성화/비활성화 연동을 위해 태그에 저장
            fillBtn.Tag = true; // 이미 true로 설정되어 있지만 명시적으로 확인
            // 기존 클릭 이벤트에 로직 추가가 어려우므로, 여기서 이벤트 핸들러를 추가하거나
            // UpdateFillButtonsInPopup 메서드에서 opacityPanel의 IsEnabled를 제어해야 함.
            // 간단하게 구현하기 위해 opacityPanel을 멤버 변수로 빼거나, 
            // fillBtn 클릭 이벤트에서 panel을 찾아서 제어하는 방식이 필요함.
            
            // 가장 쉬운 방법: fillBtn 클릭 시 갱신되는 UpdateFillButtonsInPopup에서 제어하도록
            // opacityPanel에 이름을 주거나 태그를 줘서 찾을 수 있게 함.
            opacityPanel.Tag = "OpacityPanel"; 

            opacityPanel.Children.Add(opacityLabel);
            opacityPanel.Children.Add(opacitySlider);
            opacityPanel.Children.Add(opacityValueText);
            
            panel.Children.Add(opacityPanel);

            return panel;
        }
        private Button CreateLineStyleButton(string content, bool isDashed, double thickness, int row, int col)
        {
            var button = new Button
            {
                Content = content,
                Width = 60,
                Height = 30,
                Margin = new Thickness(2),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            button.Click += (s, e) => { shapeBorderThickness = thickness; /* 점선/파선 스타일 적용 로직 추가 가능 */ };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, col);
            return button;
        }

        private Button CreateFillButton(string text, bool isFilled)
        {
            var button = new Button
            {
                Content = text,
                Width = 65,
                Height = 28,
                Margin = new Thickness(2, 0, 2, 0),
                FontSize = 10,
                Background = shapeIsFilled == isFilled ? new SolidColorBrush(Color.FromRgb(72, 152, 255)) : Brushes.White,
                Foreground = shapeIsFilled == isFilled ? Brushes.White : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Style = null,
                Tag = isFilled
            };

            // 리스트에 추가
            fillButtons.Add(button);

            // 호버 효과
            button.MouseEnter += (s, e) => { if (shapeIsFilled != isFilled) button.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)); };
            button.MouseLeave += (s, e) => { if (shapeIsFilled != isFilled) button.Background = Brushes.White; };

            button.Click += (s, e) => { shapeIsFilled = isFilled; UpdateFillButtonsInPopup(); ApplyPropertyChangesToSelectedObject(); };
            return button;
        }

        private StackPanel CreateColorPaletteSection()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };

            // 색상 팔레트 (WrapPanel 사용으로 다른 도구와 통일)
            var colorGrid = new WrapPanel { Width = 150 };

            foreach (var color in UIConstants.SharedColorPalette)
            {
                if (color == Colors.Transparent) continue;
                colorGrid.Children.Add(CreateColorSwatch(color, colorGrid));
            }

            foreach (var color in customColors)
            {
                colorGrid.Children.Add(CreateColorSwatch(color, colorGrid));
            }

            // [+] 버튼 (사용자 정의 색상 추가)
            Button addButton = new Button
            {
                Width = 20, Height = 20, Margin = new Thickness(2),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                Content = new TextBlock { Text = "+", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Cursor = Cursors.Hand
            };
            
            // 둥근 모서리 템플릿 적용
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;
            addButton.Template = template;

            addButton.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    if (!customColors.Contains(newColor)) customColors.Add(newColor);
                    colorGrid.Children.Remove(addButton);
                    colorGrid.Children.Add(CreateColorSwatch(newColor, colorGrid));
                    colorGrid.Children.Add(addButton);
                    selectedColor = newColor;
                    UpdateColorSelection(colorGrid);
                    ApplyPropertyChangesToSelectedObject();
                }
            };
            colorGrid.Children.Add(addButton);
            panel.Children.Add(colorGrid);
            return panel;
        }


        private void UpdateShapeTypeButtonsInPopup()
        {
            foreach (var button in shapeTypeButtons)
            {
                var buttonType = (ShapeType)button.Tag;
                if (buttonType == shapeType)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(72, 152, 255));
                    button.Foreground = Brushes.White;
                }
                else
                {
                    button.Background = Brushes.White;
                    button.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
            }
        }

        private void UpdateFillButtonsInPopup()
        {
            foreach (var button in fillButtons)
            {
                var buttonIsFilled = (bool)button.Tag;
                if (buttonIsFilled == shapeIsFilled)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(72, 152, 255));
                    button.Foreground = Brushes.White;
                }
                else
                {
                    button.Background = Brushes.White;
                    button.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
            }

            // [추가] 투명도 슬라이더 패널 활성화/비활성화
            // ToolOptionsPopupContent에서 OpacityPanel을 찾아서 제어
            if (ToolOptionsPopupContent.Children.Count > 0 && ToolOptionsPopupContent.Children[0] is StackPanel mainPanel)
            {
                foreach (var child in mainPanel.Children)
                {
                    if (child is StackPanel sectionPanel)
                    {
                        foreach (var subChild in sectionPanel.Children)
                        {
                            if (subChild is StackPanel opacityPanel && opacityPanel.Tag as string == "OpacityPanel")
                            {
                                opacityPanel.IsEnabled = shapeIsFilled;
                                opacityPanel.Opacity = shapeIsFilled ? 1.0 : 0.5;
                            }
                        }
                    }
                }
            }
        }

    }
}
