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
            LoadCapturePage();
            LoadHotkeysPage();
            HighlightNav(NavCapture, true);
        }

        private void HighlightNav(Button btn, bool capture)
        {
            NavCapture.FontWeight = capture ? FontWeights.Bold : FontWeights.Normal;
            NavHotkey.FontWeight = capture ? FontWeights.Normal : FontWeights.Bold;
            PageCapture.Visibility = capture ? Visibility.Visible : Visibility.Collapsed;
            PageHotkey.Visibility = capture ? Visibility.Collapsed : Visibility.Visible;
        }

        private void NavCapture_Click(object sender, RoutedEventArgs e) => HighlightNav(NavCapture, true);
        private void NavHotkey_Click(object sender, RoutedEventArgs e) => HighlightNav(NavHotkey, false);

        private void LoadCapturePage()
        {
            // Format
            CmbFormat.SelectedIndex = _settings.FileSaveFormat.Equals("JPG", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            // Folder
            var defaultInstallFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? defaultInstallFolder
                : _settings.DefaultSaveFolder;
            // Auto-save
            ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
        }

        private static void EnsureDefaultKey(ToggleHotkey hk, string defaultKey)
        {
            if (string.IsNullOrWhiteSpace(hk.Key))
                hk.Key = defaultKey;
        }

        private static void NormalizeKey(TextBox keyBox)
        {
            if (keyBox == null) return;
            var t = (keyBox.Text ?? string.Empty).Trim();
            // keep first token, uppercase
            if (t.Length > 0)
            {
                // if user typed more than one char, keep first non-space char
                var first = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? t;
                keyBox.Text = first.ToUpperInvariant();
            }
        }

        private void LoadHotkeysPage()
        {
            var hk = _settings.Hotkeys;

            // Fill defaults if empty
            EnsureDefaultKey(hk.RegionCapture, "A");
            EnsureDefaultKey(hk.DelayCapture, "D");
            EnsureDefaultKey(hk.FullScreen, "F");
            EnsureDefaultKey(hk.DesignatedCapture, "W");
            EnsureDefaultKey(hk.SaveAll, "Z");
            EnsureDefaultKey(hk.DeleteAll, "X");
            EnsureDefaultKey(hk.SimpleMode, "M");
            EnsureDefaultKey(hk.OpenSettings, "O");

            // Bind to UI
            BindHotkey(hk.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            BindHotkey(hk.DelayCapture, HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey);
            BindHotkey(hk.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            BindHotkey(hk.DesignatedCapture, HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey);
            BindHotkey(hk.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            BindHotkey(hk.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            BindHotkey(hk.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            BindHotkey(hk.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
        }

        private static void BindHotkey(ToggleHotkey src, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox key)
        {
            en.IsChecked = src.Enabled;
            ctrl.IsChecked = src.Ctrl;
            shift.IsChecked = src.Shift;
            alt.IsChecked = src.Alt;
            win.IsChecked = src.Win;
            key.Text = (src.Key ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void ReadHotkey(ToggleHotkey dst, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox key)
        {
            // Normalize UI input first
            NormalizeKey(key);

            dst.Enabled = en.IsChecked == true;
            dst.Ctrl = ctrl.IsChecked == true;
            dst.Shift = shift.IsChecked == true;
            dst.Alt = alt.IsChecked == true;
            dst.Win = win.IsChecked == true;
            dst.Key = (key.Text ?? string.Empty).Trim().ToUpperInvariant();
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
            _settings.FileSaveFormat = (CmbFormat.SelectedIndex == 1) ? "JPG" : "PNG";
            var desiredFolder = (TxtFolder.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(desiredFolder))
            {
                desiredFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            }
            _settings.DefaultSaveFolder = desiredFolder;
            _settings.AutoSaveCapture = ChkAutoSave.IsChecked == true;

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
            ReadHotkey(_settings.Hotkeys.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            ReadHotkey(_settings.Hotkeys.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            ReadHotkey(_settings.Hotkeys.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            ReadHotkey(_settings.Hotkeys.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);

            // Ensure defaults if user left any key empty
            EnsureDefaultKey(_settings.Hotkeys.RegionCapture, "A");
            EnsureDefaultKey(_settings.Hotkeys.DelayCapture, "D");
            EnsureDefaultKey(_settings.Hotkeys.FullScreen, "F");
            EnsureDefaultKey(_settings.Hotkeys.DesignatedCapture, "W");
            EnsureDefaultKey(_settings.Hotkeys.SaveAll, "Z");
            EnsureDefaultKey(_settings.Hotkeys.DeleteAll, "X");
            EnsureDefaultKey(_settings.Hotkeys.SimpleMode, "M");
            EnsureDefaultKey(_settings.Hotkeys.OpenSettings, "O");

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
            LoadHotkeysPage();
            MessageBox.Show("기본 설정으로 복원되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
