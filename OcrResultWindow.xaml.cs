using System.Windows;
using CatchCapture.Models;
using CatchCapture.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

namespace CatchCapture
{
    public partial class OcrResultWindow : Window
    {
        public OcrResultWindow(string text, bool showWarning = false)
        {
            InitializeComponent();
            
            ResultTextBox.Text = text;
            
            if (showWarning)
            {
                WarningText.Visibility = Visibility.Visible;
            }
            
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
            GoogleTranslateButton.Content = LocalizationManager.Get("GoogleTranslate");
            CopyButton.Content = LocalizationManager.Get("CopySelected");
            CloseButton.Content = LocalizationManager.Get("Close");
            WarningText.Text = LocalizationManager.Get("OcrLanguagePackWarning");
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultTextBox.Text))
            {
                Clipboard.SetText(ResultTextBox.Text);
                MessageBox.Show(LocalizationManager.Get("CopyToClipboard"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        public bool RequestGoogleTranslate { get; private set; } = false;

        private void GoogleTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ResultTextBox.Text)) return;

            try
            {
                // 1. OCR 텍스트의 언어 감지 및 타겟 언어 결정
                string detectedLang = DetectLanguage(ResultTextBox.Text);
                string targetLang = DetermineTargetLanguage(detectedLang, LocalizationManager.CurrentLanguage);
                
                // 2. 공용 유틸리티 사용하여 번역 창 열기 및 자동 붙여넣기 실행
                CatchCapture.Utilities.GoogleSearchUtility.SearchTranslation(ResultTextBox.Text, targetLang);
                
                // 3. 안내 메시지 및 창 닫기
                RequestGoogleTranslate = true;
                this.DialogResult = true; 
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationManager.Get("GoogleTranslateFailed"), ex.Message));
            }
        }

        // OCR 텍스트의 언어 간단히 감지 (생략 가능하면 유틸리티로 옮겨도 됨)
        private string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "en";
            int kor = 0, jpn = 0, chn = 0, eng = 0;
            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) kor++;
                else if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF)) jpn++;
                else if (c >= 0x4E00 && c <= 0x9FFF) chn++;
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) eng++;
            }
            int max = Math.Max(Math.Max(kor, jpn), Math.Max(chn, eng));
            if (max == 0) return "en";
            if (kor == max) return "ko";
            if (jpn == max) return "ja";
            if (chn == max) return "zh-CN";
            return "en";
        }
        
        private string DetermineTargetLanguage(string detectedLang, string uiLang)
        {
            string uiTargetLang = uiLang switch { "ko" => "ko", "zh" => "zh-CN", "ja" => "ja", _ => "en" };
            if (detectedLang == uiTargetLang || (detectedLang == "zh-CN" && uiLang == "zh")) return "en";
            if (detectedLang == "en") return uiTargetLang;
            return uiTargetLang;
        }
        
        private void ShowGuideMessage(string message, TimeSpan duration)
        {
            try { new GuideWindow(message, duration) { Owner = this }.Show(); } catch { }
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
