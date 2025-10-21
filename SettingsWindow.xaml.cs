using System;
using System.IO;
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
            TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : _settings.DefaultSaveFolder;
            // Auto-save
            ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
        }

        private void LoadHotkeysPage()
        {
            var hk = _settings.Hotkeys;
            BindHotkey(hk.SimpleCapture, HkSimpleEnabled, HkSimpleCtrl, HkSimpleShift, HkSimpleAlt, HkSimpleWin, HkSimpleKey);
            BindHotkey(hk.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            BindHotkey(hk.WindowCapture, HkWindowEnabled, HkWindowCtrl, HkWindowShift, HkWindowAlt, HkWindowWin, HkWindowKey);
            BindHotkey(hk.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            BindHotkey(hk.SizeCapture, HkSizeEnabled, HkSizeCtrl, HkSizeShift, HkSizeAlt, HkSizeWin, HkSizeKey);
        }

        private static void BindHotkey(ToggleHotkey src, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox key)
        {
            en.IsChecked = src.Enabled;
            ctrl.IsChecked = src.Ctrl;
            shift.IsChecked = src.Shift;
            alt.IsChecked = src.Alt;
            win.IsChecked = src.Win;
            key.Text = src.Key;
        }

        private static void ReadHotkey(ToggleHotkey dst, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, TextBox key)
        {
            dst.Enabled = en.IsChecked == true;
            dst.Ctrl = ctrl.IsChecked == true;
            dst.Shift = shift.IsChecked == true;
            dst.Alt = alt.IsChecked == true;
            dst.Win = win.IsChecked == true;
            dst.Key = key.Text?.Trim() ?? string.Empty;
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
            _settings.DefaultSaveFolder = TxtFolder.Text?.Trim() ?? string.Empty;
            _settings.AutoSaveCapture = ChkAutoSave.IsChecked == true;

            // Hotkeys
            ReadHotkey(_settings.Hotkeys.SimpleCapture, HkSimpleEnabled, HkSimpleCtrl, HkSimpleShift, HkSimpleAlt, HkSimpleWin, HkSimpleKey);
            ReadHotkey(_settings.Hotkeys.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            ReadHotkey(_settings.Hotkeys.WindowCapture, HkWindowEnabled, HkWindowCtrl, HkWindowShift, HkWindowAlt, HkWindowWin, HkWindowKey);
            ReadHotkey(_settings.Hotkeys.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            ReadHotkey(_settings.Hotkeys.SizeCapture, HkSizeEnabled, HkSizeCtrl, HkSizeShift, HkSizeAlt, HkSizeWin, HkSizeKey);

            Settings.Save(_settings);
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
