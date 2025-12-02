using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CatchCapture.Models;
using System.Diagnostics;

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
            LoadSystemPage();
            LoadHotkeysPage();
            HighlightNav(NavCapture, "Capture");
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // 모든 텍스트 갱신
            UpdateUIText();
        }

        private void UpdateUIText()
        {
            // 창 제목
            this.Title = LocalizationManager.Get("Settings");

            // 사이드바
            SidebarGeneralText.Text = LocalizationManager.Get("General");
            NavCapture.Content = LocalizationManager.Get("CaptureSettings");
            NavSystem.Content = LocalizationManager.Get("SystemSettings");
            NavHotkey.Content = LocalizationManager.Get("HotkeySettings");

            // 캡처 페이지
            CaptureSectionTitle.Text = LocalizationManager.Get("CaptureSettings");
            SaveSettingsGroup.Header = LocalizationManager.Get("SaveSettings");
            SavePathText.Text = LocalizationManager.Get("SavePath");
            BtnBrowse.Content = LocalizationManager.Get("Change");
            FileFormatText.Text = LocalizationManager.Get("FileFormat");
            QualityText.Text = LocalizationManager.Get("Quality");
            OptionsGroup.Header = LocalizationManager.Get("Options");
            ChkAutoSave.Content = LocalizationManager.Get("AutoSaveCapture");
            ChkShowPreview.Content = LocalizationManager.Get("ShowPreviewAfterCapture");

            // 단축키 페이지
            HotkeySectionTitle.Text = LocalizationManager.Get("HotkeySettings");
            PrintScreenGroup.Header = LocalizationManager.Get("UsePrintScreen");
            ChkUsePrintScreen.Content = LocalizationManager.Get("UsePrintScreen");
            // Print Screen 액션 항목 (표시는 로컬라이즈, 저장 값은 한국어로 유지)
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
                CboPrintScreenAction.Items.Add(new ComboBoxItem
                {
                    Content = LocalizationManager.Get(key),
                    Tag = tag
                });
            }

            // 단축키 체크박스 라벨
            HkRegionEnabled.Content = LocalizationManager.Get("AreaCapture");
            HkDelayEnabled.Content = LocalizationManager.Get("DelayCapture");
            HkRealTimeEnabled.Content = LocalizationManager.Get("RealTimeCapture");
            HkMultiEnabled.Content = LocalizationManager.Get("MultiCapture");
            HkFullEnabled.Content = LocalizationManager.Get("FullScreen");
            HkDesignatedEnabled.Content = LocalizationManager.Get("DesignatedCapture");
            HkWindowCaptureEnabled.Content = LocalizationManager.Get("WindowCapture");
            HkElementCaptureEnabled.Content = LocalizationManager.Get("ElementCapture");
            HkScrollCaptureEnabled.Content = LocalizationManager.Get("ScrollCapture");
            HkSaveAllEnabled.Content = LocalizationManager.Get("SaveAll");
            HkDeleteAllEnabled.Content = LocalizationManager.Get("DeleteAll");
            HkSimpleModeEnabled.Content = LocalizationManager.Get("Simple");
            HkOpenSettingsEnabled.Content = LocalizationManager.Get("OpenSettings");

            // 시스템 페이지
            SystemSectionTitle.Text = LocalizationManager.Get("SystemSettings");
            StartupGroup.Header = LocalizationManager.Get("StartupMode");
            StartWithWindowsCheckBox.Content = LocalizationManager.Get("StartWithWindows");
            StartupModeText.Text = LocalizationManager.Get("StartupMode");
            StartupModeTrayRadio.Content = LocalizationManager.Get("StartInTray");
            StartupModeNormalRadio.Content = LocalizationManager.Get("StartInNormal");
            StartupModeSimpleRadio.Content = LocalizationManager.Get("StartInSimple");

            // 언어 페이지
            LanguageGroup.Header = LocalizationManager.Get("LanguageSettings");
            LanguageLabelText.Text = LocalizationManager.Get("LanguageLabel");

            // 하단 버튼
            CancelButton.Content = LocalizationManager.Get("Cancel");
            SaveButton.Content = LocalizationManager.Get("Save");
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
                HkSaveAllKey, HkDeleteAllKey, HkSimpleModeKey, HkOpenSettingsKey 
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
                (Code: "ja", Name: "日本語")
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
            NavSystem.FontWeight = tag == "System" ? FontWeights.Bold : FontWeights.Normal;
            NavHotkey.FontWeight = tag == "Hotkey" ? FontWeights.Bold : FontWeights.Normal;
            
            PageCapture.Visibility = tag == "Capture" ? Visibility.Visible : Visibility.Collapsed;
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
                CboQuality.SelectedIndex = 2; // 기본값: 80%
            // Folder
            var defaultInstallFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? defaultInstallFolder
                : _settings.DefaultSaveFolder;
            // Auto-save
            ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
            // Show preview
            ChkShowPreview.IsChecked = _settings.ShowPreviewAfterCapture;
            
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
            EnsureDefaultKey(hk.SaveAll, "Z");              // S → Z 변경
            EnsureDefaultKey(hk.DeleteAll, "X");
            EnsureDefaultKey(hk.SimpleMode, "Q");           // M → Q 변경
            EnsureDefaultKey(hk.OpenSettings, "O");

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
            BindHotkey(hk.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            BindHotkey(hk.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            BindHotkey(hk.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            BindHotkey(hk.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
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
                    _settings.ImageQuality = 80;
                }
            }
            else
            {
                _settings.ImageQuality = 80;
            }
            var desiredFolder = (TxtFolder.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(desiredFolder))
            {
                desiredFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            }
            _settings.DefaultSaveFolder = desiredFolder;
            _settings.AutoSaveCapture = ChkAutoSave.IsChecked == true;
            _settings.ShowPreviewAfterCapture = ChkShowPreview.IsChecked == true;
            
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
            ReadHotkey(_settings.Hotkeys.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            ReadHotkey(_settings.Hotkeys.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            ReadHotkey(_settings.Hotkeys.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            ReadHotkey(_settings.Hotkeys.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);

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
            EnsureDefaultKey(_settings.Hotkeys.SaveAll, "S");
            EnsureDefaultKey(_settings.Hotkeys.DeleteAll, "X");
            EnsureDefaultKey(_settings.Hotkeys.SimpleMode, "M");
            EnsureDefaultKey(_settings.Hotkeys.OpenSettings, "O");

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
            _settings = new Settings();
            Settings.Save(_settings);
            LoadCapturePage();
            LoadSystemPage();
            LoadHotkeysPage();
            MessageBox.Show("기본 설정으로 복원되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"시작 프로그램 설정 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Contact_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mailto:support@catchcapture.app",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("기본 메일 클라이언트를 열 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CboFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboFormat.SelectedItem is ComboBoxItem item && CboQuality != null)
            {
                string? format = item.Content?.ToString();
                if (format == "PNG")
                {
                    // PNG는 항상 100% 품질로 고정
                    foreach (ComboBoxItem qualityItem in CboQuality.Items)
                    {
                        if (qualityItem.Tag?.ToString() == "100")
                        {
                            CboQuality.SelectedItem = qualityItem;
                            break;
                        }
                    }
                    CboQuality.IsEnabled = false;
                }
                else
                {
                    CboQuality.IsEnabled = true;
                }
            }
        }



        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedLang = selectedItem.Tag?.ToString() ?? "ko";
                // 즉시 적용: 미리보기 텍스트 갱신을 위해 런타임 변경
                CatchCapture.Models.LocalizationManager.SetLanguage(selectedLang);
                LanguageRestartNotice.Visibility = Visibility.Collapsed;
            }
        }
    }
}
