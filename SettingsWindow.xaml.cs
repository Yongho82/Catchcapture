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
        private Settings _settings;

        public SettingsWindow()
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
            HighlightNav(NavCapture, "Capture");
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
            // 창 제목
            this.Title = LocalizationManager.GetString("Settings");
            // 사이드바
            SidebarGeneralText.Text = LocalizationManager.GetString("General");
            NavCapture.Content = LocalizationManager.GetString("CaptureSettings"); // 키 확인 필요 (CaptureSettings가 더 적절해 보임)
            if (NavMenuEdit != null) NavMenuEdit.Content = LocalizationManager.GetString("MenuEdit");
            NavSystem.Content = LocalizationManager.GetString("SystemSettings");
            NavHotkey.Content = LocalizationManager.GetString("HotkeySettings");
            // 메뉴 편집 페이지 (추가)
            if (MenuEditSectionTitle != null) MenuEditSectionTitle.Text = LocalizationManager.GetString("MenuEdit");
            if (MenuEditGuideText != null) MenuEditGuideText.Text = LocalizationManager.GetString("MenuEditGuide");
            // Add 버튼의 리소스 키가 없다면 임시로 "Add" 사용하거나 "+ 추가" 유지
            // LocalizationManager.GetString("Add")가 있다면 사용
            if (AddMenuButton != null) AddMenuButton.Content = "+ " + (LocalizationManager.GetString("Add") ?? "추가"); 
            if (RestoreMenuButton != null) RestoreMenuButton.Content = LocalizationManager.GetString("RestoreDefaultMenus");
            // 앱 이름(사이드바 상단)과 하단 정보
            if (SidebarAppNameText != null)
                SidebarAppNameText.Text = LocalizationManager.GetString("AppTitle"); // AppName 대신 AppTitle 사용
            if (VersionText != null)
                VersionText.Text = $"{LocalizationManager.GetString("Version")} 1.0.0";
            if (WebsiteIcon != null)
                WebsiteIcon.ToolTip = LocalizationManager.GetString("VisitHomepage");
            if (RestoreDefaultsText != null)
                RestoreDefaultsText.Text = LocalizationManager.GetString("RestoreDefaults");
            if (PrivacyPolicyText != null)
                PrivacyPolicyText.Text = LocalizationManager.GetString("PrivacyPolicy");
            // 캡처 페이지
            CaptureSectionTitle.Text = LocalizationManager.GetString("CaptureSettings");
            SaveSettingsGroup.Header = LocalizationManager.GetString("SaveSettings");
            SavePathText.Text = LocalizationManager.GetString("SavePath");
            BtnBrowse.Content = LocalizationManager.GetString("Change");
            FileFormatText.Text = LocalizationManager.GetString("FileFormat");
            QualityText.Text = LocalizationManager.GetString("Quality");
            OptionsGroup.Header = LocalizationManager.GetString("Options");
            ChkAutoSave.Content = LocalizationManager.GetString("AutoSaveCapture");
            ChkShowPreview.Content = LocalizationManager.GetString("ShowPreviewAfterCapture");
            ChkShowMagnifier.Content = LocalizationManager.GetString("ShowMagnifier") ?? "영역 캡처 시 돋보기 표시";
            // 단축키 페이지
            HotkeySectionTitle.Text = LocalizationManager.GetString("HotkeySettings");
            PrintScreenGroup.Header = LocalizationManager.GetString("UsePrintScreen");
            ChkUsePrintScreen.Content = LocalizationManager.GetString("UsePrintScreen");
            
            // Print Screen 액션 항목 (표시는 로컬라이즈, 저장 값은 한국어로 유지)
            // 기존 아이템을 지우고 다시 채워야 함
            string? currentActionTag = null;
            if (CboPrintScreenAction.SelectedItem is ComboBoxItem selectedAction)
            {
                currentActionTag = selectedAction.Tag as string ?? null;
            }
            var items = new[]
            {
                (key: "AreaCapture", tag: "영역 캡처"),
                (key: "FullScreen", tag: "전체화면"),
                (key: "DesignatedCapture", tag: "지정 캡처"),
                (key: "WindowCapture", tag: "창 캡처"),
                (key: "ElementCapture", tag: "단위 캡처"),
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
                
                // 선택 상태 복원
                if (tag == currentActionTag)
                {
                    CboPrintScreenAction.SelectedItem = item;
                }
            }
            if (CboPrintScreenAction.SelectedItem == null && CboPrintScreenAction.Items.Count > 0)
            {
                 CboPrintScreenAction.SelectedIndex = 0;
            }
            // 단축키 체크박스 라벨
            HkRegionEnabled.Content = LocalizationManager.GetString("AreaCapture");
            HkDelayEnabled.Content = LocalizationManager.GetString("DelayCapture");
            HkRealTimeEnabled.Content = LocalizationManager.GetString("RealTimeCapture");
            HkMultiEnabled.Content = LocalizationManager.GetString("MultiCapture");
            HkFullEnabled.Content = LocalizationManager.GetString("FullScreen");
            HkDesignatedEnabled.Content = LocalizationManager.GetString("DesignatedCapture");
            HkWindowCaptureEnabled.Content = LocalizationManager.GetString("WindowCapture");
            HkElementCaptureEnabled.Content = LocalizationManager.GetString("ElementCapture");
            HkScrollCaptureEnabled.Content = LocalizationManager.GetString("ScrollCapture");
            HkOcrCaptureEnabled.Content = LocalizationManager.GetString("OcrCapture");
            HkScreenRecordEnabled.Content = LocalizationManager.GetString("ScreenRecording");
            HkSimpleModeEnabled.Content = LocalizationManager.GetString("SimpleMode"); // ScreenRecord 아래로 이동 (Step 3에서 XAML 변경됨)
            HkTrayModeEnabled.Content = LocalizationManager.GetString("TrayMode");
            HkSaveAllEnabled.Content = LocalizationManager.GetString("SaveAll");
            HkDeleteAllEnabled.Content = LocalizationManager.GetString("DeleteAll");
            HkOpenSettingsEnabled.Content = LocalizationManager.GetString("OpenSettings");
            HkOpenEditorEnabled.Content = LocalizationManager.GetString("OpenEditor");
            // 시스템 페이지
            SystemSectionTitle.Text = LocalizationManager.GetString("SystemSettings");
            StartupGroup.Header = LocalizationManager.GetString("StartupMode");
            StartWithWindowsCheckBox.Content = LocalizationManager.GetString("StartWithWindows");
            StartupModeText.Text = LocalizationManager.GetString("StartupMode");
            StartupModeTrayRadio.Content = LocalizationManager.GetString("StartInTray");
            StartupModeNormalRadio.Content = LocalizationManager.GetString("StartInNormal");
            StartupModeSimpleRadio.Content = LocalizationManager.GetString("StartInSimple");
            if (StartupModeNotice != null)
                StartupModeNotice.Text = LocalizationManager.GetString("StartupModeNotice");
            // 언어 페이지
            LanguageGroup.Header = LocalizationManager.GetString("LanguageSettings");
            LanguageLabelText.Text = LocalizationManager.GetString("LanguageLabel");
            if (LanguageRestartNotice != null)
                LanguageRestartNotice.Text = LocalizationManager.GetString("RestartRequired");
            // 하단 버튼
            CancelButton.Content = LocalizationManager.GetString("Cancel");
            SaveButton.Content = LocalizationManager.GetString("Save");
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
            NavMenuEdit.FontWeight = tag == "MenuEdit" ? FontWeights.Bold : FontWeights.Normal;
            NavSystem.FontWeight = tag == "System" ? FontWeights.Bold : FontWeights.Normal;
            NavHotkey.FontWeight = tag == "Hotkey" ? FontWeights.Bold : FontWeights.Normal;
            
            PageCapture.Visibility = tag == "Capture" ? Visibility.Visible : Visibility.Collapsed;
            PageMenuEdit.Visibility = tag == "MenuEdit" ? Visibility.Visible : Visibility.Collapsed;
            PageSystem.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
            PageHotkey.Visibility = tag == "Hotkey" ? Visibility.Visible : Visibility.Collapsed;
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
            // Format
            // Format
            string fmt = _settings.FileSaveFormat ?? "PNG";
            foreach (ComboBoxItem item in CboFormat.Items)
            {
                if (item.Content?.ToString()?.Equals(fmt, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CboFormat.SelectedItem = item;
                    break;
                }
            }
            // 품질 콤보박스 설정
            foreach (ComboBoxItem item in CboQuality.Items)
            {
                if (item.Tag?.ToString() == _settings.ImageQuality.ToString())
                {
                    CboQuality.SelectedItem = item;
                    break;
                }
            }
            if (CboQuality.SelectedItem == null)
                CboQuality.SelectedIndex = 0; // 기본값: 100%
            // Folder
            var defaultInstallFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? defaultInstallFolder
                : _settings.DefaultSaveFolder;
            // Auto-save
            ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
            // Show preview
            ChkShowPreview.IsChecked = _settings.ShowPreviewAfterCapture;
            // Show magnifier
            ChkShowMagnifier.IsChecked = _settings.ShowMagnifier;
            
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
            // 윈도우 시작 시 자동 실행
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            
            // 시작 모드
            if (_settings.StartupMode == "Tray")
                StartupModeTrayRadio.IsChecked = true;
            else if (_settings.StartupMode == "Normal")
                StartupModeNormalRadio.IsChecked = true;
            else if (_settings.StartupMode == "Simple")
                StartupModeSimpleRadio.IsChecked = true;
            else
                StartupModeTrayRadio.IsChecked = true; // 기본값
            
            // 언어 설정
            string currentLang = _settings.Language ?? "ko";
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

        private static void BindHotkey(ToggleHotkey src, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, ComboBox key)
        {
            en.IsChecked = src.Enabled;
            ctrl.IsChecked = src.Ctrl;
            shift.IsChecked = src.Shift;
            alt.IsChecked = src.Alt;
            win.IsChecked = src.Win;
            key.SelectedItem = (src.Key ?? string.Empty).Trim().ToUpperInvariant();
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
            // Capture options
            // Capture options
            if (CboFormat.SelectedItem is ComboBoxItem item)
            {
                _settings.FileSaveFormat = item.Content?.ToString() ?? "PNG";
            }
            
            // 품질 설정 (콤보박스에서)
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
            
            // Print Screen key
            _settings.UsePrintScreenKey = ChkUsePrintScreen.IsChecked == true;
            if (CboPrintScreenAction.SelectedItem is ComboBoxItem actionItem)
            {
                // 저장은 한국어 기본 문자열(Tag)로 유지 (기존 스위치 로직 호환)
                _settings.PrintScreenAction = actionItem.Tag?.ToString() ?? "영역 캡처";
            }

            // Ensure folder exists if autosave is enabled
            if (_settings.AutoSaveCapture)
            {
                try { if (!System.IO.Directory.Exists(_settings.DefaultSaveFolder)) System.IO.Directory.CreateDirectory(_settings.DefaultSaveFolder); }
                catch { /* ignore create errors; user may fix path later */ }
            }

            // Hotkeys - read and normalize
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

            // Ensure defaults if user left any key empty
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

            // 시스템 설정
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            if (StartupModeTrayRadio.IsChecked == true)
            {
                _settings.StartupMode = "Tray";
                _settings.LastActiveMode = "Tray";  // 시작 모드와 마지막 모드 동기화
            }
            else if (StartupModeNormalRadio.IsChecked == true)
            {
                _settings.StartupMode = "Normal";
                _settings.LastActiveMode = "Normal";  // 시작 모드와 마지막 모드 동기화
            }
            else if (StartupModeSimpleRadio.IsChecked == true)
            {
                _settings.StartupMode = "Simple";
                _settings.LastActiveMode = "Simple";  // 시작 모드와 마지막 모드 동기화
            }
            
            // 언어 설정
            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem)
            {
                var lang = langItem.Tag?.ToString() ?? "ko";
                _settings.Language = lang;
                // 런타임 언어 적용 (재시작 없이)
                CatchCapture.Models.LocalizationManager.SetLanguage(lang);
            }
            
            // 윈도우 시작 프로그램 등록/해제
            SetStartup(_settings.StartWithWindows);

            // Menu items order
            _settings.MainMenuItems = _menuItems.Select(m => m.Key).ToList();

            Settings.Save(_settings);
            // 디버그용 메시지박스 제거 - 설정이 자동으로 저장됨
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
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

        private void RestoreMenu_Click(object sender, RoutedEventArgs e)
        {
            var result = CatchCapture.CustomMessageBox.Show(
                "기본 메뉴로 복원하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Restore to default menu items
                _menuItems.Clear();
                var defaultKeys = new[]
                {
                    "AreaCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                    "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture"
                };

                foreach (var key in defaultKeys)
                {
                    _menuItems.Add(new MenuItemViewModel
                    {
                        Key = key,
                        DisplayName = GetMenuItemDisplayName(key)
                    });
                }

                UpdateAvailableMenus();
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
