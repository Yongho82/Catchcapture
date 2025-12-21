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
            if (string.IsNullOrEmpty(ResultTextBox.Text))
            {
                // 간단한 메시지박스로 대체하거나 생략 (이미 닫힐 것이므로)
                return;
            }

            try
            {
                // 1. 텍스트를 클립보드에 복사
                Clipboard.SetText(ResultTextBox.Text);
                
                // 2. OCR 텍스트의 언어 감지
                string detectedLang = DetectLanguage(ResultTextBox.Text);
                
                // 3. 현재 UI 언어 가져오기
                string uiLang = LocalizationManager.CurrentLanguage; 
                
                // 4. 타겟 언어 결정
                string targetLang = DetermineTargetLanguage(detectedLang, uiLang);
                
                // 5. 구글 번역 페이지 URL 생성
                string translateUrl = $"https://translate.google.com/?sl=auto&tl={targetLang}&op=translate";
                
                // 6. 임시 HTML 파일 생성
                string tempPath = Path.Combine(Path.GetTempPath(), $"google_translate_{DateTime.Now.Ticks}.html");
                
                string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Google Translate</title>
</head>
<body>
    <p>{LocalizationManager.Get("RedirectingToTranslate")}</p>
    <script>
        window.location.href = ""{translateUrl}"";
    </script>
</body>
</html>";

                File.WriteAllText(tempPath, htmlContent);

                // 7. 브라우저로 HTML 파일 실행
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
                
                // 8. 2초 후 자동으로 Ctrl+V 입력 (비동기, 메인 UI 스레드 의존성 최소화)
                Task.Run(async () => 
                {
                    await Task.Delay(2000);
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        SendCtrlV();
                    });
                });
                
                // 9. 요청 플래그 설정 및 창 닫기
                RequestGoogleTranslate = true;
                this.DialogResult = true; 
                this.Close();
            }
            catch (Exception ex)
            {
                // 오류 발생 시 메시지 박스 (창이 닫히지 않았을 경우)
                MessageBox.Show(string.Format(LocalizationManager.Get("GoogleTranslateFailed"), ex.Message));
            }
        }

        // OCR 텍스트의 언어 간단히 감지
        private string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "en";
            
            int koreanCount = 0;
            int japaneseCount = 0;
            int chineseCount = 0;
            int englishCount = 0;
            
            foreach (char c in text)
            {
                // 한글 (가-힣)
                if (c >= 0xAC00 && c <= 0xD7A3)
                    koreanCount++;
                // 히라가나 (ぁ-ん) 또는 가타카나 (ァ-ヶ)
                else if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF))
                    japaneseCount++;
                // CJK 한자 (중국어/일본어 한자)
                else if (c >= 0x4E00 && c <= 0x9FFF)
                    chineseCount++;
                // 영어 알파벳
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    englishCount++;
            }
            
            // 가장 많이 나타나는 문자 유형으로 언어 판단
            int max = Math.Max(Math.Max(koreanCount, japaneseCount), Math.Max(chineseCount, englishCount));
            
            if (max == 0) return "en"; // 기본값
            
            if (koreanCount == max) return "ko";
            if (japaneseCount == max) return "ja";
            if (chineseCount == max) return "zh-CN";
            return "en";
        }
        
        // 타겟 언어 결정 로직
        private string DetermineTargetLanguage(string detectedLang, string uiLang)
        {
            // UI 언어를 구글 번역 코드로 매핑
            string uiTargetLang = uiLang switch
            {
                "ko" => "ko",
                "zh" => "zh-CN",
                "ja" => "ja",
                _ => "en"
            };
            
            // 감지된 언어와 UI 언어가 같으면 → 영어로 번역
            if (detectedLang == uiTargetLang || 
                (detectedLang == "zh-CN" && uiLang == "zh"))
            {
                return "en";
            }
            
            // 감지된 언어가 영어면 → UI 언어로 번역
            if (detectedLang == "en")
            {
                return uiTargetLang;
            }
            
            // 그 외의 경우 → UI 언어로 번역
            return uiTargetLang;
        }
        
        // GuideWindow 표시
        private void ShowGuideMessage(string message, TimeSpan duration)
        {
            try
            {
                var guide = new GuideWindow(message, duration);
                guide.Owner = this;
                guide.Show();
            }
            catch
            {
                // GuideWindow 실패 시 무시
            }
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int VK_CONTROL = 0x11;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void SendCtrlV()
        {
            try
            {
                // Ctrl 누름
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                // V 누름
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                // V 뗌
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(50);
                // Ctrl 뗌
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch
            {
                // 실패해도 무시
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
