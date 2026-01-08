using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using CatchCapture.Models; // For Color Palette
using CatchCapture.Utilities; // For SharedCanvasEditor
using ResLoc = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture.Controls
{
    public partial class ToolOptionsControl : UserControl
    {
        private SharedCanvasEditor? _editor;
        private string _currentMode = "";
        private List<Color> _customColors = new List<Color>();



        public ToolOptionsControl()
        {
            InitializeComponent();
            InitializeEvents();
            BuildColorPalette();
            UpdateLocalization();
            ResLoc.LanguageChanged += (s, e) => UpdateLocalization();
        }

        public void Initialize(SharedCanvasEditor editor)
        {
            _editor = editor;
            if (IsLoaded)
            {
                LoadValuesFromEditor();
            }
            else
            {
                Loaded += (s, e) => LoadValuesFromEditor();
            }
        }

        public void SetMode(string toolName)
        {
            _currentMode = toolName;
            
            // Hide all first
            PenOptions.Visibility = Visibility.Collapsed;
            PenOpacityPanel.Visibility = Visibility.Collapsed; // 형광펜만 켬
            TextOptions.Visibility = Visibility.Collapsed;
            ShapeOptions.Visibility = Visibility.Collapsed;
            NumberingTabs.Visibility = Visibility.Collapsed;
            NumberingBadgeOptions.Visibility = Visibility.Collapsed;
            MosaicOptions.Visibility = Visibility.Collapsed;
            EraserOptions.Visibility = Visibility.Collapsed;
            MagicWandOptions.Visibility = Visibility.Collapsed; // [추가] 마법봉 옵션 숨기기
            ColorSection.Visibility = Visibility.Visible;
            Separator.Visibility = Visibility.Visible;

            switch (toolName)
            {
                case "펜":
                    PenOptions.Visibility = Visibility.Visible;
                    if (_editor != null) PenSizeSlider.Value = _editor.PenThickness;
                    // 펜은 불투명도 조절 없음 (보통)
                    break;
                case "형광펜":
                    PenOptions.Visibility = Visibility.Visible;
                    PenOpacityPanel.Visibility = Visibility.Visible;
                    if (_editor != null)
                    {
                        PenSizeSlider.Value = _editor.HighlightThickness;
                        PenOpacitySlider.Value = _editor.HighlightOpacity;
                    }
                    break;
                case "텍스트":
                    TextOptions.Visibility = Visibility.Visible;
                    if (_editor != null)
                    {
                        FontComboBox.SelectedItem = _editor.TextFontFamily;
                        FontSizeComboBox.SelectedItem = (int)_editor.TextFontSize;
                    }
                    break;
                case "도형":
                    ShapeOptions.Visibility = Visibility.Visible;
                    // Load Shape State
                    LoadShapeState();
                    break;
                case "넘버링":
                    ColorLabel.Visibility = Visibility.Collapsed;
                    NumberingTabs.Visibility = Visibility.Visible;
                    if (_editor != null)
                    {
                        FontSizeComboBox.SelectedItem = (int)_editor.NumberingTextSize;
                        FontComboBox.SelectedItem = _editor.TextFontFamily;
                        NumBadgeSlider.Value = _editor.NumberingBadgeSize;
                        UpdateNumberingTabSelection();
                    }
                    else
                    {
                        // 에디터가 없어도 기본 탭 선택 로직은 실행
                        UpdateNumberingTabSelection();
                    }
                    break;
                case "모자이크":
                    MosaicOptions.Visibility = Visibility.Visible;
                    ColorSection.Visibility = Visibility.Collapsed; // 모자이크는 색상 없음
                    Separator.Visibility = Visibility.Collapsed;
                    if (_editor != null) 
                    {
                        MosaicSlider.Value = _editor.MosaicIntensity;
                        BlurCheck.IsChecked = _editor.UseBlur;
                    }
                    break;
                case "지우개":
                    EraserOptions.Visibility = Visibility.Visible;
                    ColorSection.Visibility = Visibility.Collapsed;
                    Separator.Visibility = Visibility.Collapsed;
                    if (_editor != null) EraserSlider.Value = _editor.EraserSize;
                    break;
                case "마법봉":
                    MagicWandOptions.Visibility = Visibility.Visible;
                    ColorSection.Visibility = Visibility.Collapsed;
                    Separator.Visibility = Visibility.Collapsed;
                    if (_editor != null)
                    {
                        MagicWandToleranceSlider.Value = _editor.MagicWandTolerance;
                        MagicWandContiguousCheck.IsChecked = _editor.MagicWandContiguous;
                    }
                    break;
            }
            LoadEditorValues();
        }

        public void LoadEditorValues()
        {
            if (_editor == null) return;

            // [추가] 에디터의 현재 값을 UI에 반영 (텍스트/넘버링 선택 시 연동)
            
            // 폰트 크기
            double currentSize = (_currentMode == "넘버링") ? _editor.NumberingTextSize : _editor.TextFontSize;
            bool found = false;
            foreach (var item in FontSizeComboBox.Items)
            {
                if (double.TryParse(item.ToString(), out double sz) && Math.Abs(sz - currentSize) < 0.1)
                {
                    FontSizeComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }
            if (!found) FontSizeComboBox.Text = currentSize.ToString();

            // 폰트 종류
            FontComboBox.SelectedItem = _editor.TextFontFamily;

            // 스타일 버튼
            if (BoldBtn != null) BoldBtn.IsChecked = (_editor.TextFontWeight == FontWeights.Bold);
            if (ItalicBtn != null) ItalicBtn.IsChecked = (_editor.TextFontStyle == FontStyles.Italic);
            if (UnderlineBtn != null) UnderlineBtn.IsChecked = _editor.TextUnderlineEnabled;
            if (ShadowBtn != null) ShadowBtn.IsChecked = _editor.TextShadowEnabled;

            // 색상
            UpdateColorSelection(_editor.SelectedColor);

            // 펜/형광펜 두께 및 투명도
            if (_currentMode == "형광펜")
            {
                PenSizeSlider.Value = _editor.HighlightThickness;
                PenOpacitySlider.Value = _editor.HighlightOpacity;
            }
            else if (_currentMode == "펜")
            {
                PenSizeSlider.Value = _editor.PenThickness;
            }

            // 도형 상태
            if (_currentMode == "도형") LoadShapeState();

            ShapeThicknessSlider.Value = _editor.ShapeBorderThickness;

            // 넘버링
            if (_currentMode == "넘버링")
            {
                NumBadgeSlider.Value = _editor.NumberingBadgeSize;
                UpdateNumberingTabSelection();
            }

            // 마법봉
            MagicWandToleranceSlider.Value = _editor.MagicWandTolerance;
            MagicWandContiguousCheck.IsChecked = _editor.MagicWandContiguous;

            // 모자이크
            MosaicSlider.Value = _editor.MosaicIntensity;
            BlurCheck.IsChecked = _editor.UseBlur;
        }

        private void UpdateNumberingTabSelection()
        {
            if (_editor == null) return;
            bool isBadge = (NumBadgeTab.IsChecked == true);
            
            // 배지/텍스트 옵션 패널 노출 제어
            NumberingBadgeOptions.Visibility = isBadge ? Visibility.Visible : Visibility.Collapsed;
            TextOptions.Visibility = isBadge ? Visibility.Collapsed : Visibility.Visible;
            
            // 탭에 맞는 현재 색상을 팔레트에서 하이라이트
            UpdateColorSelection(isBadge ? _editor.NumberingBadgeColor : _editor.NumberingNoteColor);
        }

        private void InitializeEvents()
        {
            // Pen/Highlight
            PenSizeSlider.ValueChanged += (s, e) =>
            {
                if (_editor == null) return;
                if (_currentMode == "형광펜") _editor.HighlightThickness = e.NewValue;
                else _editor.PenThickness = e.NewValue;
                PenSizeValue.Text = $"{(int)e.NewValue}px";
            };
            PenOpacitySlider.ValueChanged += (s, e) =>
            {
                 if (_editor == null) return;
                 if (_currentMode == "형광펜") _editor.HighlightOpacity = e.NewValue;
                 PenOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
            };

            // Text Sizes ComboBox
            int[] sizes = { 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 };
            foreach (var sz in sizes) FontSizeComboBox.Items.Add(sz);
            
            FontSizeComboBox.SelectionChanged += (s, e) => {
                if (_editor == null || FontSizeComboBox.SelectedItem == null) return;
                try
                {
                    double sz = Convert.ToDouble(FontSizeComboBox.SelectedItem);
                    if (_currentMode == "넘버링") 
                    {
                        _editor.NumberingTextSize = sz;
                    }
                    else _editor.TextFontSize = sz;

                    // [추가] 선택된 객체가 있으면 즉시 적용
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
                catch { }
            };

            // Text Size Slider (Legacy support if needed, otherwise hidden)
            TextSizeSlider.ValueChanged += (s, e) =>
            {
                if (_editor == null) return;
                if (_currentMode == "넘버링") _editor.NumberingTextSize = e.NewValue;
                else _editor.TextFontSize = e.NewValue;
            };
            
            // Fonts
            string[] fonts = { 
                "Malgun Gothic", "Gulim", "Dotum", "Batang", "Gungsuh", 
                "Arial", "Segoe UI", "Verdana", "Tahoma", "Times New Roman", 
                "Consolas", "Impact", "Comic Sans MS" 
            };
            foreach (var f in fonts) FontComboBox.Items.Add(f);
            
            FontComboBox.SelectionChanged += (s, e) =>
            {
                if (_editor == null || FontComboBox.SelectedItem == null) return;
                string? f = FontComboBox.SelectedItem?.ToString();
                if (f != null) {
                    _editor.TextFontFamily = f;
                    // [추가] 선택된 객체가 있으면 즉시 적용
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
            };

            // Text Styles (ToggleButtons) - with null checks
            if (BoldBtn != null) BoldBtn.Click += (s, e) => UpdateTextStyle();
            if (ItalicBtn != null) ItalicBtn.Click += (s, e) => UpdateTextStyle();
            if (UnderlineBtn != null) UnderlineBtn.Click += (s, e) => UpdateTextStyle();
            if (ShadowBtn != null) ShadowBtn.Click += (s, e) => UpdateTextStyle();

            // Shapes
            ShapeRect.Checked += (s, e) => { if (_editor!=null) _editor.CurrentShapeType = ShapeType.Rectangle; };
            ShapeCircle.Checked += (s, e) => { if (_editor!=null) _editor.CurrentShapeType = ShapeType.Ellipse; };
            ShapeLine.Checked += (s, e) => { if (_editor!=null) _editor.CurrentShapeType = ShapeType.Line; };
            ShapeArrow.Checked += (s, e) => { if (_editor!=null) _editor.CurrentShapeType = ShapeType.Arrow; };
            
            ShapeOutline.Checked += (s, e) => { 
                if (_editor!=null) { 
                    _editor.ShapeIsFilled = false; 
                    ShapeOpacityPanel.Visibility = Visibility.Collapsed;
                    ShapeThicknessPanel.Visibility = Visibility.Visible;
                }
            };
            ShapeFill.Checked += (s, e) => { 
                if (_editor!=null) { 
                    _editor.ShapeIsFilled = true; 
                    ShapeOpacityPanel.Visibility = Visibility.Visible;
                    ShapeOpacityPanel.IsEnabled = true; 
                    ShapeOpacityPanel.Opacity = 1.0;
                    ShapeThicknessPanel.Visibility = Visibility.Collapsed;
                }
            };
            
            ShapeThicknessSlider.ValueChanged += (s, e) =>
            {
                if (_editor == null) return;
                _editor.ShapeBorderThickness = e.NewValue;
                ShapeThicknessValue.Text = $"{(int)e.NewValue}px";
            };

            ShapeOpacitySlider.ValueChanged += (s, e) =>
            {
                if (_editor == null) return;
                _editor.ShapeFillOpacity = e.NewValue;
                ShapeOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
            };

            // Numbering
            NumBadgeTab.Checked += (s, e) => UpdateNumberingTabSelection();
            NumTextTab.Checked += (s, e) => UpdateNumberingTabSelection();

            NumBadgeSlider.ValueChanged += (s, e) => {
                if (_editor == null) return;
                _editor.NumberingBadgeSize = e.NewValue;
                NumBadgeValue.Text = $"{(int)e.NewValue}px";
                _editor.ApplyCurrentTextSettingsToSelectedObject();
            };

            // Mosaic
            MosaicSlider.ValueChanged += (s,e) => {
                if (_editor == null) return;
                _editor.MosaicIntensity = e.NewValue;
                MosaicValue.Text = $"{(int)e.NewValue}";
            };
            BlurCheck.Checked += (s, e) => {
                if (_editor != null) _editor.UseBlur = true;
            };
            BlurCheck.Unchecked += (s, e) => {
                if (_editor != null) _editor.UseBlur = false;
            };
            
            // Eraser
            EraserSlider.ValueChanged += (s,e) => {
                if (_editor == null) return;
                _editor.EraserSize = e.NewValue;
                EraserValue.Text = $"{(int)e.NewValue}px";
            };

            // Magic Wand
            MagicWandToleranceSlider.ValueChanged += (s, e) => {
                if (_editor == null) return;
                _editor.MagicWandTolerance = (int)e.NewValue;
                MagicWandToleranceValue.Text = $"{(int)e.NewValue}";
            };
            MagicWandContiguousCheck.Checked += (s, e) => {
                if (_editor != null) _editor.MagicWandContiguous = true;
            };
            MagicWandContiguousCheck.Unchecked += (s, e) => {
                if (_editor != null) _editor.MagicWandContiguous = false;
            };

            // Paragraph Spacing Button
            if (ParaSpacingBtn != null) ParaSpacingBtn.Click += (s, e) => {
                if (ParaSpacingBtn.ContextMenu != null)
                {
                    ParaSpacingBtn.ContextMenu.PlacementTarget = ParaSpacingBtn;
                    ParaSpacingBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    ParaSpacingBtn.ContextMenu.IsOpen = true;
                }
            };
            
            // Paragraph Spacing (Generalized for Numbering and Text)
            if (ParaSpacingNarrow != null) ParaSpacingNarrow.Click += (s, e) => {
                UncheckAllParaSpacing();
                ParaSpacingNarrow.IsChecked = true;
                if (_editor != null) {
                    _editor.LineHeightMultiplier = 1.15; // 좁게
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
            };
            if (ParaSpacingNormal != null) ParaSpacingNormal.Click += (s, e) => {
                UncheckAllParaSpacing();
                ParaSpacingNormal.IsChecked = true;
                if (_editor != null) {
                    _editor.LineHeightMultiplier = 1.5; // 보통 (1.5줄)
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
            };
            if (ParaSpacingWide != null) ParaSpacingWide.Click += (s, e) => {
                UncheckAllParaSpacing();
                ParaSpacingWide.IsChecked = true;
                if (_editor != null) {
                    _editor.LineHeightMultiplier = 2.0; // 넓게 (2줄)
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
            };
        }

        private void UncheckAllParaSpacing()
        {
            if (ParaSpacingNarrow != null) ParaSpacingNarrow.IsChecked = false;
            if (ParaSpacingNormal != null) ParaSpacingNormal.IsChecked = false;
            if (ParaSpacingWide != null) ParaSpacingWide.IsChecked = false;
        }

        
        private void UpdateTextStyle()
        {
            if (_editor == null || BoldBtn == null || ItalicBtn == null || UnderlineBtn == null || ShadowBtn == null) return;
            _editor.TextFontWeight = (BoldBtn.IsChecked == true) ? FontWeights.Bold : FontWeights.Normal;
            _editor.TextFontStyle = (ItalicBtn.IsChecked == true) ? FontStyles.Italic : FontStyles.Normal;
            _editor.TextUnderlineEnabled = (UnderlineBtn.IsChecked == true);
            _editor.TextShadowEnabled = (ShadowBtn.IsChecked == true);

            // [추가] 선택된 객체가 있으면 즉시 적용
            _editor.ApplyCurrentTextSettingsToSelectedObject();
        }

        private void BuildColorPalette()
        {
            ColorGrid.Children.Clear();
            // Shared Palette
            foreach (var c in UIConstants.SharedColorPalette)
            {
                if (c != Colors.Transparent) 
                    ColorGrid.Children.Add(CreateColorSwatch(c));
            }
            
            // Custom Colors from Settings
            var settings = Settings.Load();
            foreach (var hexColor in settings.CustomPaletteColors)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hexColor);
                    ColorGrid.Children.Add(CreateColorSwatch(color));
                }
                catch { }
            }

            // Add '+' button
             var addButton = new Button
             {
                 Content = new TextBlock { Text = "+", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, -4, 0, 0), Foreground = Brushes.Black },
                 Width = 20, Height = 20, Margin = new Thickness(2),
                 BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                 Background = Brushes.White,
                 Padding = new Thickness(0)
             };
             addButton.Click += AddCustomColor;
             ColorGrid.Children.Add(addButton);
        }

        private Border CreateColorSwatch(Color c)
        {
            var border = new Border
            {
                Width = 20, Height = 20, Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(c),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Cursor = Cursors.Hand
            };
            
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (_editor != null) {
                    if (_currentMode == "넘버링")
                    {
                        if (NumBadgeTab.IsChecked == true) _editor.NumberingBadgeColor = c;
                        else _editor.NumberingNoteColor = c;
                    }
                    else
                    {
                        _editor.SelectedColor = c;
                    }
                    // [추가] 선택된 객체가 있으면 즉시 적용
                    _editor.ApplyCurrentTextSettingsToSelectedObject();
                }
                UpdateColorSelection(c);
            };
            
            return border;
        }

        private void UpdateColorSelection(Color selected)
        {
            foreach (var child in ColorGrid.Children)
            {
                if (child is Border b && b.Background is SolidColorBrush sc)
                {
                    bool isSel = (sc.Color == selected);
                    b.BorderThickness = new Thickness(isSel ? 2 : 1);
                    b.BorderBrush = isSel ? Brushes.Black : Brushes.Gray;
                    b.Effect = isSel ? new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 5, Color = Colors.Gray } : null;
                }
            }
        }
        
        private void AddCustomColor(object sender, RoutedEventArgs e)
        {
             var dlg = new System.Windows.Forms.ColorDialog();
             if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
             {
                  var newColor = Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                  
                  // Add to persistent settings
                  var settings = Settings.Load();
                  string hex = newColor.ToString(); // #AARRGGBB
                  if (!settings.CustomPaletteColors.Contains(hex))
                  {
                      settings.CustomPaletteColors.Add(hex);
                      settings.Save();
                  }

                  // Add to UI (before the + button)
                  ColorGrid.Children.Insert(ColorGrid.Children.Count - 1, CreateColorSwatch(newColor));
                  
                  if (_editor != null) {
                        if (_currentMode == "넘버링")
                        {
                            if (NumBadgeTab.IsChecked == true) _editor.NumberingBadgeColor = newColor;
                            else _editor.NumberingNoteColor = newColor;
                        }
                        else
                        {
                             _editor.SelectedColor = newColor;
                        }
                        _editor.ApplyCurrentTextSettingsToSelectedObject();
                  }
                  UpdateColorSelection(newColor);
             }
        }

        private void LoadValuesFromEditor()
        {
            if (_editor == null) return;
            UpdateColorSelection(_editor.SelectedColor);
            
            // Text Styles
            if (BoldBtn != null) BoldBtn.IsChecked = _editor.TextFontWeight == FontWeights.Bold;
            if (ItalicBtn != null) ItalicBtn.IsChecked = _editor.TextFontStyle == FontStyles.Italic;
            if (UnderlineBtn != null) UnderlineBtn.IsChecked = _editor.TextUnderlineEnabled;
            if (ShadowBtn != null) ShadowBtn.IsChecked = _editor.TextShadowEnabled;

            // Font & Size ComboBoxes
            if (FontComboBox != null) FontComboBox.SelectedItem = _editor.TextFontFamily;
            if (FontSizeComboBox != null) FontSizeComboBox.SelectedItem = (int)_editor.TextFontSize;
        }
        
        private void LoadShapeState()
        {
            if (_editor == null) return;
            // Shapes
            switch (_editor.CurrentShapeType)
            {
                case ShapeType.Rectangle: ShapeRect.IsChecked = true; break;
                case ShapeType.Ellipse: ShapeCircle.IsChecked = true; break;
                case ShapeType.Line: ShapeLine.IsChecked = true; break;
                case ShapeType.Arrow: ShapeArrow.IsChecked = true; break;
            }
             ShapeOutline.IsChecked = !_editor.ShapeIsFilled;
             ShapeFill.IsChecked = _editor.ShapeIsFilled;
             ShapeOpacitySlider.Value = _editor.ShapeFillOpacity;
             
             if (_editor.ShapeIsFilled)
             {
                 ShapeOpacityPanel.Visibility = Visibility.Visible;
                 ShapeOpacityPanel.IsEnabled = true;
                 ShapeOpacityPanel.Opacity = 1.0;
                 ShapeThicknessPanel.Visibility = Visibility.Collapsed;
             }
             else
             {
                 ShapeOpacityPanel.Visibility = Visibility.Collapsed;
                 ShapeThicknessPanel.Visibility = Visibility.Visible;
             }
        }

        private void UpdateLocalization()
        {
            // Set text from LocalizationManager
            ColorLabel.Text = ResLoc.GetString("Color");
            PenSizeLabel.Text = ResLoc.GetString("Thickness");
            PenOpacityLabel.Text = ResLoc.GetString("Opacity");
            FontLabel.Text = ResLoc.GetString("Font");
            TextSizeLabel.Text = ResLoc.GetString("SizeLabel");
            StyleLabel.Text = ResLoc.GetString("Style");
            BoldCheck.Content = BoldBtn.ToolTip = ResLoc.GetString("Bold");
            ItalicCheck.Content = ItalicBtn.ToolTip = ResLoc.GetString("Italic");
            UnderlineCheck.Content = UnderlineBtn.ToolTip = ResLoc.GetString("Underline");
            ShadowCheck.Content = ShadowBtn.ToolTip = ResLoc.GetString("Shadow");
            ShapeLabel.Text = ResLoc.GetString("Shape");
            ShapeOutline.Content = ResLoc.GetString("Outline");
            ShapeFill.Content = ResLoc.GetString("Fill");
            ShapeThicknessLabel.Text = ResLoc.GetString("Thickness");
            ShapeOpacityLabel.Text = ResLoc.GetString("FillOpacity");
            NumBadgeTab.Content = ResLoc.GetString("Badge");
            NumTextTab.Content = ResLoc.GetString("TextNote");
            NumBadgeLabel.Text = ResLoc.GetString("BadgeSize");

            MosaicLabel.Text = ResLoc.GetString("Intensity");
            BlurCheck.Content = ResLoc.GetString("Blur");
            EraserLabel.Text = ResLoc.GetString("Size");
            MagicWandLabel.Text = ResLoc.GetString("MagicWandToleranceLabel");
            MagicWandContiguousCheck.Content = ResLoc.GetString("MagicWandContiguousOnly");
        }
    }
}
