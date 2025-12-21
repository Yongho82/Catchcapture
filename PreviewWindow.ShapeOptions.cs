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

        private void ShowShapeOptions()
        {
            // 패널 제목 설정
            ToolTitleText.Text = LocalizationManager.Get("ShapeOptions");

            EditToolContent.Children.Clear();

            // 도형 유형 선택
            Border typeLabelWrapper = new Border { Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock typeLabel = new TextBlock { Text = LocalizationManager.Get("ShapeType") + ":", VerticalAlignment = VerticalAlignment.Center };
            typeLabelWrapper.Child = typeLabel;
            EditToolContent.Children.Add(typeLabelWrapper);

            StackPanel shapeTypesPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };

            // 사각형 버튼
            rectButton = new Button
            {
                Content = "□",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Rectangle ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0)
            };
            rectButton.Click += (s, e) => { shapeType = ShapeType.Rectangle; UpdateShapeTypeButtons(); };
            shapeTypesPanel.Children.Add(rectButton);

            // 타원 버튼
            ellipseButton = new Button
            {
                Content = "○",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Ellipse ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0)
            };
            ellipseButton.Click += (s, e) => { shapeType = ShapeType.Ellipse; UpdateShapeTypeButtons(); };
            shapeTypesPanel.Children.Add(ellipseButton);

            // 선 버튼
            lineButton = new Button
            {
                Content = "−",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Line ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0)
            };
            lineButton.Click += (s, e) => { shapeType = ShapeType.Line; UpdateShapeTypeButtons(); };
            shapeTypesPanel.Children.Add(lineButton);

            // 화살표 버튼
            arrowButton = new Button
            {
                Content = "→",
                FontSize = 16,
                Width = 26,
                Height = 26,
                Margin = new Thickness(2, 0, 2, 0),
                Background = shapeType == ShapeType.Arrow ? Brushes.LightBlue : Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -3, 0, 0)
            };
            arrowButton.Click += (s, e) => { shapeType = ShapeType.Arrow; UpdateShapeTypeButtons(); };
            shapeTypesPanel.Children.Add(arrowButton);

            EditToolContent.Children.Add(shapeTypesPanel);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator { Margin = new Thickness(5, 0, 5, 0), Width = 1, Height = 20 });

            // 색상 선택
            Border colorLabelWrapper = new Border { Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock colorLabel = new TextBlock { Text = LocalizationManager.Get("Color") + ":", VerticalAlignment = VerticalAlignment.Center };
            colorLabelWrapper.Child = colorLabel;
            EditToolContent.Children.Add(colorLabelWrapper);

            StackPanel colorPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Color[] colors = new Color[]
            {
                Colors.Black, Colors.Red, Colors.Blue, Colors.Green,
                Colors.Yellow, Colors.Orange, Colors.Purple, Colors.White
            };
            foreach (Color color in colors)
            {
                Border colorBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = new SolidColorBrush(color),
                    BorderBrush = color.Equals(shapeColor) ? Brushes.Black : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(2)
                };
                colorBorder.MouseLeftButtonDown += (s, e) =>
                {
                    shapeColor = color;
                    foreach (UIElement element in colorPanel.Children)
                    {
                        if (element is Border border)
                        {
                            border.BorderBrush = border == s ? Brushes.Black : Brushes.Transparent;
                        }
                    }
                };
                colorPanel.Children.Add(colorBorder);
            }
            EditToolContent.Children.Add(colorPanel);

            // 구분선 추가
            EditToolContent.Children.Add(new Separator { Margin = new Thickness(5, 0, 5, 0), Width = 1, Height = 20 });

            // 두께 선택
            Border thicknessLabelWrapper = new Border { Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            TextBlock thicknessLabel = new TextBlock { Text = LocalizationManager.Get("Thickness") + ":", VerticalAlignment = VerticalAlignment.Center };
            thicknessLabelWrapper.Child = thicknessLabel;
            EditToolContent.Children.Add(thicknessLabelWrapper);

            Slider thicknessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 10,
                Value = shapeBorderThickness,
                Width = 100,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };
            thicknessSlider.ValueChanged += (s, e) => { shapeBorderThickness = thicknessSlider.Value; ApplyPropertyChangesToSelectedObject(); };
            EditToolContent.Children.Add(thicknessSlider);

            // 채우기 옵션
            CheckBox fillCheckBox = new CheckBox
            {
                Content = LocalizationManager.Get("Fill"),
                IsChecked = shapeIsFilled,
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            fillCheckBox.Checked += (s, e) => { shapeIsFilled = true; };
            fillCheckBox.Unchecked += (s, e) => { shapeIsFilled = false; };
            EditToolContent.Children.Add(fillCheckBox);

            EditToolPanel.Visibility = Visibility.Visible;
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

            // 색상 팔레트 (3행 6열, 더 많은 색상과 좁은 간격)
            var colorGrid = new UniformGrid
            {
                Rows = 3,
                Columns = 6,
                Margin = new Thickness(0, 4, 0, 0)
            };

            foreach (var color in SharedColorPalette)
            {
                var colorBtn = CreateColorButton(color);
                colorGrid.Children.Add(colorBtn);
            }

            panel.Children.Add(colorGrid);
            return panel;
        }

        private Button CreateColorButton(Color color)
        {
            // 투명색(지우개)인 경우 특별 처리
            bool isTransparent = color == Colors.Transparent;

            var button = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0.2),
                BorderThickness = new Thickness(0),
                Style = null,
                Tag = color,
                Cursor = Cursors.Hand
            };

            // 버튼 템플릿 생성
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "ButtonBorder";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetValue(Border.PaddingProperty, new Thickness(4));

            // 선택된 색상인지 확인하여 배경 설정
            bool isSelected = shapeColor == color;
            factory.SetValue(Border.BackgroundProperty, isSelected ? new SolidColorBrush(Color.FromRgb(173, 216, 255)) : Brushes.Transparent);

            // 내부 색상 원 생성
            var innerFactory = new FrameworkElementFactory(typeof(Border));
            innerFactory.SetValue(Border.WidthProperty, 16.0);
            innerFactory.SetValue(Border.HeightProperty, 16.0);
            innerFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            innerFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            innerFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

            if (isTransparent)
            {
                innerFactory.SetValue(Border.BackgroundProperty, Brushes.White);
                innerFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200)));
                innerFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                textFactory.SetValue(TextBlock.TextProperty, "✕");
                textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
                textFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 100, 100)));
                textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                innerFactory.AppendChild(textFactory);
            }
            else
            {
                innerFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(color));
                innerFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(220, 220, 220)));
                innerFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            }

            factory.AppendChild(innerFactory);
            template.VisualTree = factory;

            // 트리거 추가 (호버 효과)
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            if (!isSelected)
            {
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 248, 255)), "ButtonBorder"));
            }
            template.Triggers.Add(hoverTrigger);

            button.Template = template;

            // 리스트에 추가
            colorButtons.Add(button);

            button.Click += (s, e) => { shapeColor = color; UpdateColorButtonsInPopup(); ApplyPropertyChangesToSelectedObject(); };
            return button;
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

        private void UpdateColorButtonsInPopup()
        {
            foreach (var button in colorButtons)
            {
                var buttonColor = (Color)button.Tag;
                bool isSelected = buttonColor == shapeColor;
                bool isTransparent = buttonColor == Colors.Transparent;

                var template = new ControlTemplate(typeof(Button));
                var factory = new FrameworkElementFactory(typeof(Border));
                factory.Name = "ButtonBorder";
                factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                factory.SetValue(Border.PaddingProperty, new Thickness(4));
                factory.SetValue(Border.BackgroundProperty, isSelected ? new SolidColorBrush(Color.FromRgb(173, 216, 255)) : Brushes.Transparent);

                var innerFactory = new FrameworkElementFactory(typeof(Border));
                innerFactory.SetValue(Border.WidthProperty, 16.0);
                innerFactory.SetValue(Border.HeightProperty, 16.0);
                innerFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                innerFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                innerFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

                if (isTransparent)
                {
                    innerFactory.SetValue(Border.BackgroundProperty, Brushes.White);
                    innerFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200)));
                    innerFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                    var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                    textFactory.SetValue(TextBlock.TextProperty, "✕");
                    textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
                    textFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 100, 100)));
                    textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                    innerFactory.AppendChild(textFactory);
                }
                else
                {
                    innerFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(buttonColor));
                    innerFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(220, 220, 220)));
                    innerFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                }

                factory.AppendChild(innerFactory);
                template.VisualTree = factory;

                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                if (!isSelected)
                {
                    hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 248, 255)), "ButtonBorder"));
                }
                template.Triggers.Add(hoverTrigger);

                button.Template = template;
            }
        }
        private void UpdateShapeTypeButtons()
        {
            if (rectButton != null)
                rectButton.Background = (shapeType == ShapeType.Rectangle) ? GetActiveToolBrush() : Brushes.Transparent;

            if (ellipseButton != null)
                ellipseButton.Background = (shapeType == ShapeType.Ellipse) ? GetActiveToolBrush() : Brushes.Transparent;

            if (lineButton != null)
                lineButton.Background = (shapeType == ShapeType.Line) ? GetActiveToolBrush() : Brushes.Transparent;

            if (arrowButton != null)
                arrowButton.Background = (shapeType == ShapeType.Arrow) ? GetActiveToolBrush() : Brushes.Transparent;
        }
    }
}
