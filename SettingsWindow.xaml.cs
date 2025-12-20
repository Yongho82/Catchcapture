using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CatchCapture.Models;
using System.Diagnostics;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture
{
    public partial class SettingsWindow : Window
    {
        private Settings _settings = null!;
        private string _originalThemeMode = string.Empty;
        private string _originalThemeBg = string.Empty;
        private string _originalThemeFg = string.Empty;
        private int[] _customColors = new int[16]; // 색상 대화상자의 사용자 지정 색상 저장

        public SettingsWindow()
        {
            try
            {
                InitializeComponent();
                _settings = Settings.Load();
                UpdateUIText(); // 다국어 텍스트 적용
                // 언어 변경 이벤트 구독 (닫힐 때 해제)
                CatchCapture.Models.LocalizationManager.LanguageChanged += OnLanguageChanged;
                InitKeyComboBoxes(); // 콤보박스 초기화
                InitLanguageComboBox(); // 언어 콤보 아이템 채우기
                LoadCapturePage();
                LoadMenuEditPage();
                LoadSystemPage();
                LoadHotkeysPage();
                
                // Store original theme for cancel revert
                _originalThemeMode = _settings.ThemeMode ?? "General";
                _originalThemeBg = _settings.ThemeBackgroundColor ?? "#FFFFFF";
                _originalThemeFg = _settings.ThemeTextColor ?? "#333333";
                
                LoadThemePage();
                HighlightNav(NavCapture, "Capture");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsWindow Constructor Error: {ex.Message}");
                // 생성자에서 오류 발생 시 최소한 창은 열리거나 오류 메시지를 보여줘야 함
                System.Windows.MessageBox.Show("설정 창을 로드하는 중 오류가 발생했습니다: " + ex.Message);
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // 모든 텍스트 갱신
            UpdateUIText();
            // 메뉴 편집 항목 표시 이름도 갱신
            try
            {
                if (_menuItems != null)
                {
                    foreach (var item in _menuItems)
                    {
                        item.DisplayName = GetMenuItemDisplayName(item.Key);
                    }
                    MenuItemsListBox.Items.Refresh();
                }
                UpdateAvailableMenus();
            }
            catch { }
        }

private void UpdateUIText()
        {
            try
            {
                // 창 제목
                this.Title = LocalizationManager.GetString("Settings");
                
                // 사이드바
                if (SidebarGeneralText != null) SidebarGeneralText.Text = LocalizationManager.GetString("General");
                if (NavCapture != null) NavCapture.Content = LocalizationManager.GetString("CaptureSettings");
                if (NavMenuEdit != null) NavMenuEdit.Content = LocalizationManager.GetString("MenuEdit");
                if (NavSystem != null) NavSystem.Content = LocalizationManager.GetString("SystemSettings");
                if (NavHotkey != null) NavHotkey.Content = LocalizationManager.GetString("HotkeySettings");
                
                // 메뉴 편집 페이지
                if (MenuEditSectionTitle != null) MenuEditSectionTitle.Text = LocalizationManager.GetString("MenuEdit");
                if (MenuEditGuideText != null) MenuEditGuideText.Text = LocalizationManager.GetString("MenuEditGuide");
                if (AddMenuButton != null) AddMenuButton.Content = "+ " + (LocalizationManager.GetString("Add") ?? "추가"); 
                
                // 앱 이름과 하단 정보
                if (SidebarAppNameText != null) SidebarAppNameText.Text = LocalizationManager.GetString("AppTitle");
                if (VersionText != null) VersionText.Text = $"{LocalizationManager.GetString("Version")} 1.0.0";
                if (WebsiteIcon != null) WebsiteIcon.ToolTip = LocalizationManager.GetString("VisitHomepage");
                if (RestoreDefaultsText != null) RestoreDefaultsText.Text = LocalizationManager.GetString("RestoreDefaults");
                if (PrivacyPolicyText != null) PrivacyPolicyText.Text = LocalizationManager.GetString("PrivacyPolicy");
                
                // 캡처 페이지
                if (CaptureSectionTitle != null) CaptureSectionTitle.Text = LocalizationManager.GetString("CaptureSettings");
                if (SaveSettingsGroup != null) SaveSettingsGroup.Header = LocalizationManager.GetString("SaveSettings");
                if (SavePathText != null) SavePathText.Text = LocalizationManager.GetString("SavePath");
                if (BtnBrowse != null) BtnBrowse.Content = LocalizationManager.GetString("Change");
                if (FileFormatText != null) FileFormatText.Text = LocalizationManager.GetString("FileFormat");
                if (QualityText != null) QualityText.Text = LocalizationManager.GetString("Quality");
                if (OptionsGroup != null) OptionsGroup.Header = LocalizationManager.GetString("Options");
                if (ChkAutoSave != null) ChkAutoSave.Content = LocalizationManager.GetString("AutoSaveCapture");
                if (ChkShowPreview != null) ChkShowPreview.Content = LocalizationManager.GetString("ShowPreviewAfterCapture");
                if (ChkShowMagnifier != null) ChkShowMagnifier.Content = LocalizationManager.GetString("ShowMagnifier") ?? "영역 캡처 시 돋보기 표시";
                
                // 단축키 페이지
                if (HotkeySectionTitle != null) HotkeySectionTitle.Text = LocalizationManager.GetString("HotkeySettings");
                if (PrintScreenGroup != null) PrintScreenGroup.Header = LocalizationManager.GetString("UsePrintScreen");
                if (ChkUsePrintScreen != null) ChkUsePrintScreen.Content = LocalizationManager.GetString("UsePrintScreen");
                
                // Print Screen 액션 항목
                if (CboPrintScreenAction != null)
                {
                    string? currentActionTag = null;
                    if (CboPrintScreenAction.SelectedItem is ComboBoxItem selectedAction)
                    {
                        currentActionTag = selectedAction.Tag as string ?? null;
                    }
                    var items = new[]
                    {
                        (key: "AreaCapture", tag: "영역 캡처"),
                        (key: "DelayCapture", tag: "지연 캡처"),
                        (key: "RealTimeCapture", tag: "실시간 캡처"),
                        (key: "MultiCapture", tag: "다중 캡처"),
                        (key: "FullScreen", tag: "전체화면"),
                        (key: "DesignatedCapture", tag: "지정 캡처"),
                        (key: "WindowCapture", tag: "창 캡처"),
                        (key: "ElementCapture", tag: "단위 캡처"),
                        (key: "ScrollCapture", tag: "스크롤 캡처"),
                        (key: "OcrCapture", tag: "OCR 캡처"),
                        (key: "ScreenRecording", tag: "동영상 녹화"),
                    };
                    
                    CboPrintScreenAction.Items.Clear();
                    foreach (var (key, tag) in items)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = LocalizationManager.GetString(key),
                            Tag = tag
                        };
                        CboPrintScreenAction.Items.Add(item);
                        
                        if (tag == currentActionTag)
                        {
                            CboPrintScreenAction.SelectedItem = item;
                        }
                    }
                    if (CboPrintScreenAction.SelectedItem == null && CboPrintScreenAction.Items.Count > 0)
                    {
                         CboPrintScreenAction.SelectedIndex = 0;
                    }
                }
                
                // 단축키 체크박스 라벨
                if (HkRegionEnabled != null) HkRegionEnabled.Content = LocalizationManager.GetString("AreaCapture");
                if (HkDelayEnabled != null) HkDelayEnabled.Content = LocalizationManager.GetString("DelayCapture");
                if (HkRealTimeEnabled != null) HkRealTimeEnabled.Content = LocalizationManager.GetString("RealTimeCapture");
                if (HkMultiEnabled != null) HkMultiEnabled.Content = LocalizationManager.GetString("MultiCapture");
                if (HkFullEnabled != null) HkFullEnabled.Content = LocalizationManager.GetString("FullScreen");
                if (HkDesignatedEnabled != null) HkDesignatedEnabled.Content = LocalizationManager.GetString("DesignatedCapture");
                if (HkWindowCaptureEnabled != null) HkWindowCaptureEnabled.Content = LocalizationManager.GetString("WindowCapture");
                if (HkElementCaptureEnabled != null) HkElementCaptureEnabled.Content = LocalizationManager.GetString("ElementCapture");
                if (HkScrollCaptureEnabled != null) HkScrollCaptureEnabled.Content = LocalizationManager.GetString("ScrollCapture");
                if (HkOcrCaptureEnabled != null) HkOcrCaptureEnabled.Content = LocalizationManager.GetString("OcrCapture");
                if (HkScreenRecordEnabled != null) HkScreenRecordEnabled.Content = LocalizationManager.GetString("ScreenRecording");
                if (HkSimpleModeEnabled != null) HkSimpleModeEnabled.Content = LocalizationManager.GetString("SimpleMode");
                if (HkTrayModeEnabled != null) HkTrayModeEnabled.Content = LocalizationManager.GetString("TrayMode");
                if (HkSaveAllEnabled != null) HkSaveAllEnabled.Content = LocalizationManager.GetString("SaveAll");
                if (HkDeleteAllEnabled != null) HkDeleteAllEnabled.Content = LocalizationManager.GetString("DeleteAll");
                if (HkOpenSettingsEnabled != null) HkOpenSettingsEnabled.Content = LocalizationManager.GetString("OpenSettings");
                if (HkOpenEditorEnabled != null) HkOpenEditorEnabled.Content = LocalizationManager.GetString("OpenEditor");
                
                // 시스템 페이지
                if (SystemSectionTitle != null) SystemSectionTitle.Text = LocalizationManager.GetString("SystemSettings");
                if (StartupGroup != null) StartupGroup.Header = LocalizationManager.GetString("StartupMode");
                if (StartWithWindowsCheckBox != null) StartWithWindowsCheckBox.Content = LocalizationManager.GetString("StartWithWindows");
                if (StartupModeText != null) StartupModeText.Text = LocalizationManager.GetString("StartupMode");
                if (StartupModeTrayRadio != null) StartupModeTrayRadio.Content = LocalizationManager.GetString("StartInTray");
                if (StartupModeNormalRadio != null) StartupModeNormalRadio.Content = LocalizationManager.GetString("StartInNormal");
                if (StartupModeSimpleRadio != null) StartupModeSimpleRadio.Content = LocalizationManager.GetString("StartInSimple");
                if (StartupModeNotice != null) StartupModeNotice.Text = LocalizationManager.GetString("StartupModeNotice");
                
                // 언어 페이지
                if (LanguageGroup != null) LanguageGroup.Header = LocalizationManager.GetString("LanguageSettings");
                if (LanguageLabelText != null) LanguageLabelText.Text = LocalizationManager.GetString("LanguageLabel");
                if (LanguageRestartNotice != null) LanguageRestartNotice.Text = LocalizationManager.GetString("RestartRequired");

                // 테마 페이지
                if (NavTheme != null) NavTheme.Content = LocalizationManager.GetString("ThemeSettings");
                if (ThemeSectionTitle != null) ThemeSectionTitle.Text = LocalizationManager.GetString("ThemeSettings");
                if (ThemePresetGroup != null) ThemePresetGroup.Header = LocalizationManager.GetString("ThemePreset");
                if (ThemeGeneral != null) ThemeGeneral.Content = LocalizationManager.GetString("ThemeGeneral");
                if (ThemeDark != null) ThemeDark.Content = LocalizationManager.GetString("ThemeDark");
                if (ThemeLight != null) ThemeLight.Content = LocalizationManager.GetString("ThemeLight");
                if (ThemeBlue != null) ThemeBlue.Content = LocalizationManager.GetString("ThemeBlue");
                if (ThemeCustom != null) ThemeCustom.Content = LocalizationManager.GetString("ThemeCustom");
                if (ThemeCustomGroup != null) ThemeCustomGroup.Header = LocalizationManager.GetString("ThemeCustom");
                if (ThemeBgColorLabel != null) ThemeBgColorLabel.Text = LocalizationManager.GetString("ThemeBgColor");
                if (ThemeTextColorLabel != null) ThemeTextColorLabel.Text = LocalizationManager.GetString("ThemeTextColor");
                if (ThemeColorGuide != null) ThemeColorGuide.Text = LocalizationManager.GetString("ThemeColorGuide");

                // 하단 버튼
                if (CancelButton != null) CancelButton.Content = LocalizationManager.GetString("Cancel");
                if (ApplyButton != null) ApplyButton.Content = LocalizationManager.GetString("Apply");
                if (SaveButton != null) SaveButton.Content = LocalizationManager.GetString("Save");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUIText error: {ex.Message}");
                // 에러가 발생해도 프로그램이 크래시되지 않도록 합니다
            }
        }

        private void InitKeyComboBoxes()
        {
            var keys = new System.Collections.Generic.List<string>();
            // A-Z
            for (char c = 'A'; c <= 'Z'; c++) keys.Add(c.ToString());
            // 0-9
            for (char c = '0'; c <= '9'; c++) keys.Add(c.ToString());
            // F1-F12
            for (int i = 1; i <= 12; i++) keys.Add($"F{i}");

            // 여기에 HkRealTimeKey와 HkMultiKey를 추가했습니다
            var boxes = new[] { 
                HkRegionKey, HkDelayKey, HkRealTimeKey, HkMultiKey, HkFullKey, HkDesignatedKey,
                HkWindowCaptureKey, HkElementCaptureKey, HkScrollCaptureKey,
                HkOcrCaptureKey, HkScreenRecordKey,
                HkSimpleModeKey, HkTrayModeKey,
                HkSaveAllKey, HkDeleteAllKey, HkOpenSettingsKey, HkOpenEditorKey 
            };

            foreach (var box in boxes)
            {
                box.ItemsSource = keys;
            }
        }

private void InitLanguageComboBox()
        {
            if (LanguageComboBox == null) return;
            LanguageComboBox.Items.Clear();
            var langs = new[]
            {
                (Code: "ko", Name: "한국어"),
                (Code: "en", Name: "English"),
                (Code: "zh", Name: "简体中文"),
                (Code: "ja", Name: "日本語"),
                (Code: "es", Name: "Español"),
                (Code: "de", Name: "Deutsch"),
                (Code: "fr", Name: "Français")
            };
            foreach (var (code, name) in langs)
            {
                LanguageComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            }
            // SelectionChanged 핸들러 연결 (중복 연결 방지)
            LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        }

        private void HighlightNav(Button btn, string tag)
        {
            NavCapture.FontWeight = tag == "Capture" ? FontWeights.Bold : FontWeights.Normal;
            NavTheme.FontWeight = tag == "Theme" ? FontWeights.Bold : FontWeights.Normal;
            NavMenuEdit.FontWeight = tag == "MenuEdit" ? FontWeights.Bold : FontWeights.Normal;
            NavSystem.FontWeight = tag == "System" ? FontWeights.Bold : FontWeights.Normal;
            NavHotkey.FontWeight = tag == "Hotkey" ? FontWeights.Bold : FontWeights.Normal;
            
            PageCapture.Visibility = tag == "Capture" ? Visibility.Visible : Visibility.Collapsed;
            PageTheme.Visibility = tag == "Theme" ? Visibility.Visible : Visibility.Collapsed;
            PageMenuEdit.Visibility = tag == "MenuEdit" ? Visibility.Visible : Visibility.Collapsed;
            PageSystem.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
            PageHotkey.Visibility = tag == "Hotkey" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadThemePage()
        {
            if (ThemeDark != null && _settings.ThemeMode == "Dark") ThemeDark.IsChecked = true;
            else if (ThemeLight != null && _settings.ThemeMode == "Light") ThemeLight.IsChecked = true;
            else if (ThemeBlue != null && _settings.ThemeMode == "Blue") ThemeBlue.IsChecked = true;
            else if (ThemeCustom != null && _settings.ThemeMode == "Custom") ThemeCustom.IsChecked = true;
            else if (ThemeGeneral != null) ThemeGeneral.IsChecked = true;

            UpdateColorPreviews();
        }

        private void UpdateColorPreviews()
        {
            try
            {
                if (PreviewThemeBgColor != null)
                {
                    string bgColor = string.IsNullOrEmpty(_settings.ThemeBackgroundColor) ? "#FFFFFF" : _settings.ThemeBackgroundColor;
                    PreviewThemeBgColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));
                }
            }
            catch { if (PreviewThemeBgColor != null) PreviewThemeBgColor.Background = Brushes.White; }

            try
            {
                if (PreviewThemeTextColor != null)
                {
                    string textColor = string.IsNullOrEmpty(_settings.ThemeTextColor) ? "#333333" : _settings.ThemeTextColor;
                    PreviewThemeTextColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
                }
            }
            catch { if (PreviewThemeTextColor != null) PreviewThemeTextColor.Background = Brushes.Black; }
            
            // 사용자 지정 모드일 때만 색상 선택 가능하도록 활성화
            if (ThemeCustomGroup != null && ThemeCustom != null)
            {
                ThemeCustomGroup.IsEnabled = ThemeCustom.IsChecked == true;
                ThemeCustomGroup.Opacity = ThemeCustom.IsChecked == true ? 1.0 : 0.5;
            }

            // Apply real-time preview to the whole app
            App.ApplyTheme(_settings);
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true && rb.Tag is string info)
            {
                _settings.ThemeMode = info;
                if (info == "General")
                {
                    _settings.ThemeBackgroundColor = "#FFFFFF";
                    _settings.ThemeTextColor = "#333333";
                }
                else if (info == "Dark")
                {
                    _settings.ThemeBackgroundColor = "#2D2D2D";
                    _settings.ThemeTextColor = "#FFFFFF";
                }
                else if (info == "Light")
                {
                    _settings.ThemeBackgroundColor = "#F5F5F7";
                    _settings.ThemeTextColor = "#333333";
                }
                else if (info == "Blue")
                {
                    _settings.ThemeBackgroundColor = "#E3F2FD";
                    _settings.ThemeTextColor = "#0d47a1";
                }
                else if (info == "Custom")
                {
                    // If switching to custom, ensure we have at least some default colors
                    if (string.IsNullOrEmpty(_settings.ThemeBackgroundColor)) _settings.ThemeBackgroundColor = "#FFFFFF";
                    if (string.IsNullOrEmpty(_settings.ThemeTextColor)) _settings.ThemeTextColor = "#333333";
                }
                
                UpdateColorPreviews();
            }
        }

        private void ThemeColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
        }

        private void PreviewColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string type)
            {
                var dialog = new System.Windows.Forms.ColorDialog();
                dialog.FullOpen = true;
                
                // 팔레트 상태 복원 (깊은 복사)
                if (_customColors != null) dialog.CustomColors = (int[])_customColors.Clone();
                
                string currentHex = type == "Bg" ? (_settings.ThemeBackgroundColor ?? "#FFFFFF") : (_settings.ThemeTextColor ?? "#333333");
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(currentHex);
                    dialog.Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B);
                }
                catch { }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // 팔레트 상태 저장 (깊은 복사)
                    _customColors = (int[])dialog.CustomColors.Clone();
                    
                    string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                    
                    if (type == "Bg") _settings.ThemeBackgroundColor = hex;
                    else _settings.ThemeTextColor = hex;
                    
                    // 무조건 사용자 지정(Custom) 모드로 전환
                    if (ThemeCustom != null) 
                    {
                        ThemeCustom.IsChecked = true;
                        _settings.ThemeMode = "Custom";
                    }
                    
                    UpdateColorPreviews();
                    // App.ApplyTheme는 UpdateColorPreviews 내부에서 자동으로 호출됨
                }
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                HighlightNav(btn, tag);
            }
        }

        private void LoadCapturePage()
        {
            if (CboFormat != null)
            {
                string fmt = _settings.FileSaveFormat ?? "PNG";
                foreach (ComboBoxItem item in CboFormat.Items)
                {
                    if (item != null && item.Content?.ToString()?.Equals(fmt, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CboFormat.SelectedItem = item;
                        break;
                    }
                }
                if (CboFormat.SelectedItem == null && CboFormat.Items.Count > 0) CboFormat.SelectedIndex = 0;
            }

            if (CboQuality != null)
            {
                foreach (ComboBoxItem item in CboQuality.Items)
                {
                    if (item != null && item.Tag?.ToString() == _settings.ImageQuality.ToString())
                    {
                        CboQuality.SelectedItem = item;
                        break;
                    }
                }
                if (CboQuality.SelectedItem == null && CboQuality.Items.Count > 0) CboQuality.SelectedIndex = 0;
            }

            var defaultInstallFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            if (TxtFolder != null) TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? defaultInstallFolder
                : _settings.DefaultSaveFolder;
            if (ChkAutoSave != null) ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
            if (ChkShowPreview != null) ChkShowPreview.IsChecked = _settings.ShowPreviewAfterCapture;
            if (ChkShowMagnifier != null) ChkShowMagnifier.IsChecked = _settings.ShowMagnifier;
            
            // Print Screen key
            ChkUsePrintScreen.IsChecked = _settings.UsePrintScreenKey;
            foreach (ComboBoxItem item in CboPrintScreenAction.Items)
            {
                if (item.Tag?.ToString() == _settings.PrintScreenAction)
                {
                    CboPrintScreenAction.SelectedItem = item;
                    break;
                }
            }
            if (CboPrintScreenAction.SelectedItem == null)
                CboPrintScreenAction.SelectedIndex = 0;
        }

        private void LoadSystemPage()
        {
            if (StartWithWindowsCheckBox != null) StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            
            if (StartupModeTrayRadio != null && _settings.StartupMode == "Tray") StartupModeTrayRadio.IsChecked = true;
            else if (StartupModeSimpleRadio != null && _settings.StartupMode == "Simple") StartupModeSimpleRadio.IsChecked = true;
            else if (StartupModeNormalRadio != null) StartupModeNormalRadio.IsChecked = true;

            // 언어 설정
            string currentLang = _settings.Language ?? "ko";
            if (LanguageComboBox != null)
            {
                foreach (ComboBoxItem item in LanguageComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentLang)
                    {
                        LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }
                if (LanguageComboBox.SelectedItem == null)
                    LanguageComboBox.SelectedIndex = 0; // 기본값: 한국어
            }
        }

        private static void EnsureDefaultKey(ToggleHotkey hk, string defaultKey)
        {
            if (string.IsNullOrWhiteSpace(hk.Key))
                hk.Key = defaultKey;
        }

        private void LoadHotkeysPage()
        {
            var hk = _settings.Hotkeys;

            // Fill defaults if empty
            EnsureDefaultKey(hk.RegionCapture, "A");
            EnsureDefaultKey(hk.DelayCapture, "K");
            EnsureDefaultKey(hk.RealTimeCapture, "R");      // 추가
            EnsureDefaultKey(hk.MultiCapture, "M");         // 추가
            EnsureDefaultKey(hk.FullScreen, "F");
            EnsureDefaultKey(hk.DesignatedCapture, "W");    // G → W 변경
            EnsureDefaultKey(hk.WindowCapture, "C");        // W → C 변경
            EnsureDefaultKey(hk.ElementCapture, "E");
            EnsureDefaultKey(hk.ScrollCapture, "S");        // R → S 변경
            EnsureDefaultKey(hk.OcrCapture, "O");
            EnsureDefaultKey(hk.ScreenRecord, "V");
            EnsureDefaultKey(hk.SimpleMode, "Q");           // M → Q 변경
            EnsureDefaultKey(hk.TrayMode, "T");
            EnsureDefaultKey(hk.SaveAll, "Z");              // S → Z 변경
            EnsureDefaultKey(hk.DeleteAll, "X");
            EnsureDefaultKey(hk.OpenSettings, "O");
            EnsureDefaultKey(hk.OpenEditor, "E");

            // Bind to UI
            BindHotkey(hk.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            BindHotkey(hk.DelayCapture, HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey);
            BindHotkey(hk.RealTimeCapture, HkRealTimeEnabled, HkRealTimeCtrl, HkRealTimeShift, HkRealTimeAlt, HkRealTimeWin, HkRealTimeKey);
            BindHotkey(hk.MultiCapture, HkMultiEnabled, HkMultiCtrl, HkMultiShift, HkMultiAlt, HkMultiWin, HkMultiKey);
            BindHotkey(hk.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            BindHotkey(hk.DesignatedCapture, HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey);
            BindHotkey(hk.WindowCapture, HkWindowCaptureEnabled, HkWindowCaptureCtrl, HkWindowCaptureShift, HkWindowCaptureAlt, HkWindowCaptureWin, HkWindowCaptureKey);
            BindHotkey(hk.ElementCapture, HkElementCaptureEnabled, HkElementCaptureCtrl, HkElementCaptureShift, HkElementCaptureAlt, HkElementCaptureWin, HkElementCaptureKey);
            BindHotkey(hk.ScrollCapture, HkScrollCaptureEnabled, HkScrollCaptureCtrl, HkScrollCaptureShift, HkScrollCaptureAlt, HkScrollCaptureWin, HkScrollCaptureKey);
            BindHotkey(hk.OcrCapture, HkOcrCaptureEnabled, HkOcrCaptureCtrl, HkOcrCaptureShift, HkOcrCaptureAlt, HkOcrCaptureWin, HkOcrCaptureKey);
            BindHotkey(hk.ScreenRecord, HkScreenRecordEnabled, HkScreenRecordCtrl, HkScreenRecordShift, HkScreenRecordAlt, HkScreenRecordWin, HkScreenRecordKey);
            BindHotkey(hk.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            BindHotkey(hk.TrayMode, HkTrayModeEnabled, HkTrayModeCtrl, HkTrayModeShift, HkTrayModeAlt, HkTrayModeWin, HkTrayModeKey);
            BindHotkey(hk.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            BindHotkey(hk.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            BindHotkey(hk.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
            BindHotkey(hk.OpenEditor, HkOpenEditorEnabled, HkOpenEditorCtrl, HkOpenEditorShift, HkOpenEditorAlt, HkOpenEditorWin, HkOpenEditorKey);
        }

        private static void BindHotkey(ToggleHotkey src, CheckBox? en, CheckBox? ctrl, CheckBox? shift, CheckBox? alt, CheckBox? win, ComboBox? key)
        {
            if (src == null) return;
            if (en != null) en.IsChecked = src.Enabled;
            if (ctrl != null) ctrl.IsChecked = src.Ctrl;
            if (shift != null) shift.IsChecked = src.Shift;
            if (alt != null) alt.IsChecked = src.Alt;
            if (win != null) win.IsChecked = src.Win;
            if (key != null)
            {
                string val = (src.Key ?? string.Empty).Trim().ToUpperInvariant();
                foreach (var item in key.Items)
                {
                    if (item != null && item.ToString() == val)
                    {
                        key.SelectedItem = item;
                        break;
                    }
                }
                if (key.SelectedItem == null && !string.IsNullOrEmpty(val))
                {
                    key.Text = val;
                }
            }
        }

        private static void ReadHotkey(ToggleHotkey dst, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, ComboBox key)
        {
            dst.Enabled = en.IsChecked == true;
            dst.Ctrl = ctrl.IsChecked == true;
            dst.Shift = shift.IsChecked == true;
            dst.Alt = alt.IsChecked == true;
            dst.Win = win.IsChecked == true;
            dst.Key = (key.SelectedItem as string ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtFolder.Text = dlg.SelectedPath;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            HarvestSettings();
            Settings.Save(_settings);
            
            // Re-store original theme so cancel doesn't revert to old values if we clicked Apply before
            _originalThemeMode = _settings.ThemeMode;
            _originalThemeBg = _settings.ThemeBackgroundColor;
            _originalThemeFg = _settings.ThemeTextColor;

            DialogResult = true;
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            HarvestSettings();
            Settings.Save(_settings);

            // Re-store original theme so cancel doesn't revert to old values
            _originalThemeMode = _settings.ThemeMode;
            _originalThemeBg = _settings.ThemeBackgroundColor;
            _originalThemeFg = _settings.ThemeTextColor;

            // Apply theme real-time (already happening but let's be safe)
            App.ApplyTheme(_settings);
            
            // Feedback for Apply
            // CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("SettingsSaved"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HarvestSettings()
        {
            // Capture options
            if (CboFormat.SelectedItem is ComboBoxItem item)
            {
                _settings.FileSaveFormat = item.Content?.ToString() ?? "PNG";
            }
            
            if (CboQuality.SelectedItem is ComboBoxItem qualityItem)
            {
                if (int.TryParse(qualityItem.Tag?.ToString(), out int quality))
                {
                    _settings.ImageQuality = quality;
                }
                else
                {
                    _settings.ImageQuality = 100;
                }
            }
            else
            {
                _settings.ImageQuality = 100;
            }

            var desiredFolder = (TxtFolder.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(desiredFolder))
            {
                desiredFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            }
            _settings.DefaultSaveFolder = desiredFolder;
            _settings.AutoSaveCapture = ChkAutoSave.IsChecked == true;
            _settings.ShowPreviewAfterCapture = ChkShowPreview.IsChecked == true;
            _settings.ShowMagnifier = ChkShowMagnifier.IsChecked == true;
            
            _settings.UsePrintScreenKey = ChkUsePrintScreen.IsChecked == true;
            if (CboPrintScreenAction.SelectedItem is ComboBoxItem actionItem)
            {
                _settings.PrintScreenAction = actionItem.Tag?.ToString() ?? "영역 캡처";
            }

            if (_settings.AutoSaveCapture)
            {
                try { if (!System.IO.Directory.Exists(_settings.DefaultSaveFolder)) System.IO.Directory.CreateDirectory(_settings.DefaultSaveFolder); }
                catch { }
            }

            // Hotkeys
            ReadHotkey(_settings.Hotkeys.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            ReadHotkey(_settings.Hotkeys.DelayCapture, HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey);
            ReadHotkey(_settings.Hotkeys.RealTimeCapture, HkRealTimeEnabled, HkRealTimeCtrl, HkRealTimeShift, HkRealTimeAlt, HkRealTimeWin, HkRealTimeKey); 
            ReadHotkey(_settings.Hotkeys.MultiCapture, HkMultiEnabled, HkMultiCtrl, HkMultiShift, HkMultiAlt, HkMultiWin, HkMultiKey); 
            ReadHotkey(_settings.Hotkeys.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            ReadHotkey(_settings.Hotkeys.DesignatedCapture, HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey);
            ReadHotkey(_settings.Hotkeys.WindowCapture, HkWindowCaptureEnabled, HkWindowCaptureCtrl, HkWindowCaptureShift, HkWindowCaptureAlt, HkWindowCaptureWin, HkWindowCaptureKey);
            ReadHotkey(_settings.Hotkeys.ElementCapture, HkElementCaptureEnabled, HkElementCaptureCtrl, HkElementCaptureShift, HkElementCaptureAlt, HkElementCaptureWin, HkElementCaptureKey);
            ReadHotkey(_settings.Hotkeys.ScrollCapture, HkScrollCaptureEnabled, HkScrollCaptureCtrl, HkScrollCaptureShift, HkScrollCaptureAlt, HkScrollCaptureWin, HkScrollCaptureKey);
            ReadHotkey(_settings.Hotkeys.OcrCapture, HkOcrCaptureEnabled, HkOcrCaptureCtrl, HkOcrCaptureShift, HkOcrCaptureAlt, HkOcrCaptureWin, HkOcrCaptureKey);
            ReadHotkey(_settings.Hotkeys.ScreenRecord, HkScreenRecordEnabled, HkScreenRecordCtrl, HkScreenRecordShift, HkScreenRecordAlt, HkScreenRecordWin, HkScreenRecordKey);
            ReadHotkey(_settings.Hotkeys.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            ReadHotkey(_settings.Hotkeys.TrayMode, HkTrayModeEnabled, HkTrayModeCtrl, HkTrayModeShift, HkTrayModeAlt, HkTrayModeWin, HkTrayModeKey);
            ReadHotkey(_settings.Hotkeys.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            ReadHotkey(_settings.Hotkeys.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            ReadHotkey(_settings.Hotkeys.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
            ReadHotkey(_settings.Hotkeys.OpenEditor, HkOpenEditorEnabled, HkOpenEditorCtrl, HkOpenEditorShift, HkOpenEditorAlt, HkOpenEditorWin, HkOpenEditorKey);

            EnsureDefaultKey(_settings.Hotkeys.RegionCapture, "A");
            EnsureDefaultKey(_settings.Hotkeys.DelayCapture, "D");
            EnsureDefaultKey(_settings.Hotkeys.RealTimeCapture, "R");
            EnsureDefaultKey(_settings.Hotkeys.MultiCapture, "M");   
            EnsureDefaultKey(_settings.Hotkeys.FullScreen, "F");
            EnsureDefaultKey(_settings.Hotkeys.DesignatedCapture, "G");
            EnsureDefaultKey(_settings.Hotkeys.WindowCapture, "W");
            EnsureDefaultKey(_settings.Hotkeys.ElementCapture, "E");
            EnsureDefaultKey(_settings.Hotkeys.ScrollCapture, "R");
            EnsureDefaultKey(_settings.Hotkeys.OcrCapture, "O");
            EnsureDefaultKey(_settings.Hotkeys.ScreenRecord, "V");
            EnsureDefaultKey(_settings.Hotkeys.SimpleMode, "Q");
            EnsureDefaultKey(_settings.Hotkeys.TrayMode, "T");
            EnsureDefaultKey(_settings.Hotkeys.SaveAll, "S");
            EnsureDefaultKey(_settings.Hotkeys.DeleteAll, "X");
            EnsureDefaultKey(_settings.Hotkeys.OpenSettings, "O");
            EnsureDefaultKey(_settings.Hotkeys.OpenEditor, "E");

            // System settings
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            if (StartupModeTrayRadio.IsChecked == true) { _settings.StartupMode = "Tray"; _settings.LastActiveMode = "Tray"; }
            else if (StartupModeNormalRadio.IsChecked == true) { _settings.StartupMode = "Normal"; _settings.LastActiveMode = "Normal"; }
            else if (StartupModeSimpleRadio.IsChecked == true) { _settings.StartupMode = "Simple"; _settings.LastActiveMode = "Simple"; }
            
            // Language
            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem)
            {
                var lang = langItem.Tag?.ToString() ?? "ko";
                _settings.Language = lang;
                CatchCapture.Models.LocalizationManager.SetLanguage(lang);
            }
            
            SetStartup(_settings.StartWithWindows);

            // Menu order
            _settings.MainMenuItems = _menuItems.Select(m => m.Key).ToList();

            // Theme Setting
            if (ThemeDark != null && ThemeDark.IsChecked == true) _settings.ThemeMode = "Dark";
            else if (ThemeLight != null && ThemeLight.IsChecked == true) _settings.ThemeMode = "Light";
            else if (ThemeBlue != null && ThemeBlue.IsChecked == true) _settings.ThemeMode = "Blue";
            else if (ThemeCustom != null && ThemeCustom.IsChecked == true) _settings.ThemeMode = "Custom";
            else _settings.ThemeMode = "General";
            
            // Background color and text color already updated in real-time in _settings
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert theme to original if changed
            var revertSettings = Settings.Load();
            revertSettings.ThemeMode = _originalThemeMode;
            revertSettings.ThemeBackgroundColor = _originalThemeBg;
            revertSettings.ThemeTextColor = _originalThemeFg;
            App.ApplyTheme(revertSettings);
            
            DialogResult = false;
            CatchCapture.Models.LocalizationManager.LanguageChanged -= OnLanguageChanged;
            Close();
        }

        // Added: Sidebar bottom links
        private void RestoreDefaults_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 현재 언어 설정 보존
            string currentLanguage = _settings.Language;

            _settings = new Settings();
            
            // 언어 설정 복원
            _settings.Language = currentLanguage;

            Settings.Save(_settings);
            LoadCapturePage();
            LoadMenuEditPage();  // 메뉴 편집도 기본값으로 복원
            LoadSystemPage();
            LoadHotkeysPage();
            CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("SettingsSaved"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Website_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ezupsoft.com",
                    UseShellExecute = true
                });
            }
            catch
            {
                CatchCapture.CustomMessageBox.Show("웹 브라우저를 열 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                string appName = "CatchCapture";
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable)
                    {
                        key?.SetValue(appName, $"\"{exePath}\" /autostart");
                    }
                    else
                    {
                        key?.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"시작 프로그램 설정 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Contact_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ezupsoft.com",
                    UseShellExecute = true
                });
            }
            catch
            {
                CatchCapture.CustomMessageBox.Show("기본 메일 클라이언트를 열 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PrivacyPolicy_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = new PrivacyPolicyWindow
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void AddSection(StackPanel parent, string title, string content)
        {
            var sectionTitle = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 20, 0, 10),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51))
            };
            parent.Children.Add(sectionTitle);

            var sectionContent = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85))
            };
            parent.Children.Add(sectionContent);
        }

        private void CboFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 모든 포맷에서 품질 조정 가능
            // 별도 처리 없음
        }



        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedLang = selectedItem.Tag?.ToString() ?? "ko";
                // 즉시 적용: 미리보기 텍스트 갱신을 위해 런타임 변경
                LocalizationManager.SetLanguage(selectedLang);
                CatchCapture.Models.LocalizationManager.SetLanguage(selectedLang);
                if (LanguageRestartNotice != null) 
                    LanguageRestartNotice.Visibility = Visibility.Collapsed;
            }
        }

        // Menu Edit Page
        private System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel> _menuItems = 
            new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>();

        private static void FillKeyCombo(ComboBox? combo, List<string> keys)
        {
            if (combo == null) return;
            combo.Items.Clear();
            foreach (var k in keys) combo.Items.Add(k);
        }
        private void LoadMenuEditPage()
        {
            _menuItems.Clear();
            
            // Load menu items from settings
            var menuKeys = _settings.MainMenuItems ?? new System.Collections.Generic.List<string>
            {
                "AreaCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "SimpleMode"
            };

            foreach (var key in menuKeys)
            {
                _menuItems.Add(new MenuItemViewModel
                {
                    Key = key,
                    DisplayName = GetMenuItemDisplayName(key)
                });
            }

            MenuItemsListBox.ItemsSource = _menuItems;
            UpdateAvailableMenus();
        }

        private void UpdateAvailableMenus()
        {
            // All possible menu items
            var allMenuKeys = new[]
            {
                "AreaCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord", "SimpleMode", "TrayMode"
            };

            // Get currently used keys
            var usedKeys = _menuItems.Select(m => m.Key).ToHashSet();

            // Find available (not currently used) items
            var availableItems = allMenuKeys
                .Where(key => !usedKeys.Contains(key))
                .Select(key => new MenuItemViewModel
                {
                    Key = key,
                    DisplayName = GetMenuItemDisplayName(key)
                })
                .ToList();

            AvailableMenuComboBox.ItemsSource = availableItems;
            AvailableMenuComboBox.DisplayMemberPath = "DisplayName";
            
            if (availableItems.Any())
            {
                AvailableMenuComboBox.SelectedIndex = 0;
            }
        }

        private string GetMenuItemDisplayName(string key)
        {
            return key switch
            {
                "AreaCapture"       => LocalizationManager.GetString("AreaCapture"),
                "DelayCapture"      => LocalizationManager.GetString("DelayCapture"),
                "RealTimeCapture"   => LocalizationManager.GetString("RealTimeCapture"),
                "MultiCapture"      => LocalizationManager.GetString("MultiCapture"),
                "FullScreen"        => LocalizationManager.GetString("FullScreen"),
                "DesignatedCapture" => LocalizationManager.GetString("DesignatedCapture"),
                "WindowCapture"     => LocalizationManager.GetString("WindowCapture"),
                "ElementCapture"    => LocalizationManager.GetString("ElementCapture"),
                "ScrollCapture"     => LocalizationManager.GetString("ScrollCapture"),
                "OcrCapture"        => LocalizationManager.GetString("OcrCapture"),
                "ScreenRecord"      => LocalizationManager.GetString("ScreenRecording"),
                "SimpleMode"        => LocalizationManager.GetString("SimpleMode"),
                "TrayMode"          => LocalizationManager.GetString("TrayMode"),
                _ => key
            };
        }

        // Drag and Drop support
        private Point _dragStartPoint;
        private MenuItemViewModel? _draggedItem;

        private void MenuItemsListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void MenuItemsListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Get the dragged ListBoxItem
                    var listBox = sender as System.Windows.Controls.ListBox;
                    var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                    if (listBoxItem != null && listBox != null)
                    {
                        _draggedItem = listBoxItem.Content as MenuItemViewModel;
                        if (_draggedItem != null)
                        {
                            DragDrop.DoDragDrop(listBoxItem, _draggedItem, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private ListBoxItem? _lastHighlightedItem = null;

        private void MenuItemsListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // Find the item under the mouse
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            
            // Clear previous highlight
            if (_lastHighlightedItem != null && _lastHighlightedItem != targetItem)
            {
                ClearDropHighlight(_lastHighlightedItem);
            }

            // Highlight current target
            if (targetItem != null && targetItem.Content != _draggedItem)
            {
                SetDropHighlight(targetItem);
                _lastHighlightedItem = targetItem;
            }
        }

        private void SetDropHighlight(ListBoxItem item)
        {
            // Find the Border inside the ListBoxItem and add a top border
            var border = FindVisualChild<Border>(item);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007AFF"));
                border.BorderThickness = new Thickness(2, 3, 2, 1);
            }
        }

        private void ClearDropHighlight(ListBoxItem item)
        {
            var border = FindVisualChild<Border>(item);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
                border.BorderThickness = new Thickness(1);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    return t;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void MenuItemsListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // Clear highlight
            if (_lastHighlightedItem != null)
            {
                ClearDropHighlight(_lastHighlightedItem);
                _lastHighlightedItem = null;
            }

            if (_draggedItem == null) return;

            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox == null) return;

            // Find drop target
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null) return;

            var target = targetItem.Content as MenuItemViewModel;
            if (target == null || target == _draggedItem) return;

            int oldIndex = _menuItems.IndexOf(_draggedItem);
            int newIndex = _menuItems.IndexOf(target);

            if (oldIndex != newIndex && oldIndex >= 0 && newIndex >= 0)
            {
                _menuItems.Move(oldIndex, newIndex);
            }

            _draggedItem = null;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                    return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MenuItemViewModel item)
            {
                if (_menuItems.Count <= 1)
                {
                    CatchCapture.CustomMessageBox.Show("최소 1개 이상의 메뉴 항목이 필요합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = CatchCapture.CustomMessageBox.Show(
                    $"'{item.DisplayName}' 메뉴를 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _menuItems.Remove(item);
                    UpdateAvailableMenus();
                }
            }
        }

        private void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableMenuComboBox.SelectedItem is MenuItemViewModel selectedItem)
            {
                _menuItems.Add(new MenuItemViewModel
                {
                    Key = selectedItem.Key,
                    DisplayName = selectedItem.DisplayName
                });
                UpdateAvailableMenus();
            }
            else
            {
                CatchCapture.CustomMessageBox.Show("추가할 메뉴를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { CatchCapture.Models.LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
            base.OnClosed(e);
        }
    }

    // Helper class for menu item editing
    public class MenuItemViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
