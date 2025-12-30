using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace CatchCapture.Utilities
{
    public static class GoogleSearchUtility
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int VK_CONTROL = 0x11;
        private const int VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// 이미지를 구글 렌즈로 검색 (3.5초 대기 + 더블 탭)
        /// </summary>
        public static void SearchImage(BitmapSource image)
        {
            try
            {
                // 1. 이미지 클립보드 복사
                ScreenCaptureUtility.CopyImageToClipboard(image);

                // 2. 구글 렌즈 URL 생성 (Base64 편법 포함 - 보험용)
                double scale = 1.0;
                double maxSide = 400.0;
                if (image.PixelWidth > maxSide || image.PixelHeight > maxSide)
                {
                    scale = Math.Min(maxSide / image.PixelWidth, maxSide / image.PixelHeight);
                }
                var transformedBitmap = new TransformedBitmap(image, new ScaleTransform(scale, scale));
                var encoder = new JpegBitmapEncoder { QualityLevel = 70 };
                encoder.Frames.Add(BitmapFrame.Create(transformedBitmap));
                
                using var ms = new MemoryStream();
                encoder.Save(ms);
                string base64 = Convert.ToBase64String(ms.ToArray());
                string lensUrl = $"https://lens.google.com/upload?ep=gsbubb&hl=ko&re=df&st={DateTimeOffset.Now.ToUnixTimeMilliseconds()}#base64:{base64}";

                // 3. 임시 HTML로 브라우저 실행
                OpenUrlWithHtmlRedirect(lensUrl, "Google Lens");

                // 4. 매크로 실행 (3.5초 후 더블 탭)
                RunPasteMacro();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 실행 실패: {ex.Message}", "오류");
            }
        }

        /// <summary>
        /// 텍스트를 구글 번역으로 이동 (3.5초 대기 + 더블 탭)
        /// </summary>
        public static void SearchTranslation(string text, string targetLang)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return;

                // 1. 텍스트 클립보드 복사
                ScreenCaptureUtility.CopyTextToClipboard(text);

                // 2. 번역 URL 생성
                string translateUrl = $"https://translate.google.com/?sl=auto&tl={targetLang}&op=translate";

                // 3. 임시 HTML로 브라우저 실행
                OpenUrlWithHtmlRedirect(translateUrl, "Google Translate");

                // 4. 매크로 실행 (3.5초 후 더블 탭)
                RunPasteMacro();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"번역 실행 실패: {ex.Message}", "오류");
            }
        }

        private static void OpenUrlWithHtmlRedirect(string url, string title)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"google_search_{DateTime.Now.Ticks}.html");
                string htmlContent = $@"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{title}</title></head>
                <body><p>페이지로 이동 중...</p><script>window.location.href = ""{url}"";</script></body></html>";
                File.WriteAllText(tempPath, htmlContent);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static void RunPasteMacro()
        {
            // 3.5초 대기 후 자동으로 Ctrl+V 입력 (더블 탭 전략)
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                // UI 스레드 상관없이 전역 키보드 이벤트 송출
                SendCtrlV();
                await Task.Delay(1000);
                SendCtrlV();
            });
        }

        private static void SendCtrlV()
        {
            try
            {
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                Thread.Sleep(50);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }
    }
}
