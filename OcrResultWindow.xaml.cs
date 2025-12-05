using System.Windows;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class OcrResultWindow : Window
    {
        public OcrResultWindow(string text)
        {
            InitializeComponent();
            ResultTextBox.Text = text;
            // Localize UI
            UpdateUIText();
            LocalizationManager.LanguageChanged += LocalizationManager_LanguageChanged;
        }

        private void LocalizationManager_LanguageChanged(object? sender, System.EventArgs e)
        {
            try { UpdateUIText(); } catch { }
        }

        private void UpdateUIText()
        {
            this.Title = LocalizationManager.Get("OcrResultTitle");
            HeaderText.Text = LocalizationManager.Get("ExtractedText");
            CopyButton.Content = LocalizationManager.Get("CopySelected");
            CloseButton.Content = LocalizationManager.Get("Close");
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultTextBox.Text))
            {
                Clipboard.SetText(ResultTextBox.Text);
                MessageBox.Show(LocalizationManager.Get("CopyToClipboard"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try { LocalizationManager.LanguageChanged -= LocalizationManager_LanguageChanged; } catch { }
            base.OnClosed(e);
        }
    }
}
