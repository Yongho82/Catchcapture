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
            InitKeyComboBoxes(); // 콤보박스 초기화
            LoadCapturePage();
            LoadSystemPage();
            LoadHotkeysPage();
            HighlightNav(NavCapture, "Capture");
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

            var boxes = new[] { 
                HkRegionKey, HkDelayKey, HkFullKey, HkDesignatedKey,
                HkWindowCaptureKey, HkElementCaptureKey, HkScrollCaptureKey,
                HkSaveAllKey, HkDeleteAllKey, HkSimpleModeKey, HkOpenSettingsKey 
            };

            foreach (var box in boxes)
            {
                box.ItemsSource = keys;
            }
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
            TxtQuality.Text = _settings.ImageQuality.ToString();
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
                if (item.Content?.ToString() == _settings.PrintScreenAction)
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
            
            if (int.TryParse(TxtQuality.Text, out int q))
            {
                _settings.ImageQuality = Math.Max(1, Math.Min(100, q));
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
            
            // Print Screen key
            _settings.UsePrintScreenKey = ChkUsePrintScreen.IsChecked == true;
            if (CboPrintScreenAction.SelectedItem is ComboBoxItem actionItem)
            {
                _settings.PrintScreenAction = actionItem.Content?.ToString() ?? "영역 캡처";
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
                _settings.StartupMode = "Tray";
            else if (StartupModeNormalRadio.IsChecked == true)
                _settings.StartupMode = "Normal";
            else if (StartupModeSimpleRadio.IsChecked == true)
                _settings.StartupMode = "Simple";
            
            // 윈도우 시작 프로그램 등록/해제
            SetStartup(_settings.StartWithWindows);

            Settings.Save(_settings);
            try
            {
                // Verify persistence
                var reloaded = Settings.Load();
                // Very light check: ensure one of the hotkeys round-trips (e.g., RegionCapture)
                bool ok = reloaded?.Hotkeys?.RegionCapture?.Key == _settings.Hotkeys.RegionCapture.Key;
                var pathInfo = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "settings.json");
                if (ok)
                {
                    MessageBox.Show($"설정이 저장되었습니다.\n{pathInfo}", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"설정 저장 확인에 실패했습니다.\n경로: {pathInfo}", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch
            {
                MessageBox.Show("설정을 저장했지만 확인 중 오류가 발생했습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
                        key?.SetValue(appName, $"\"{exePath}\"");
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
            if (CboFormat.SelectedItem is ComboBoxItem item && TxtQuality != null)
            {
                string? format = item.Content?.ToString();
                if (format == "PNG")
                {
                    TxtQuality.Text = "100";
                    TxtQuality.IsEnabled = false;
                }
                else
                {
                    TxtQuality.IsEnabled = true;
                }
            }
        }

        private void TxtQuality_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9]+$");
        }
    }
}
