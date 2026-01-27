using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Diagnostics;

namespace CatchCapture.Utilities
{
    public class GoogleDriveUploadProvider
    {
        // 꼭 필요한 권한만 요청 (파일 생성 및 이메일 확인)
        private static readonly string[] Scopes = { 
            "https://www.googleapis.com/auth/drive.file", 
            "https://www.googleapis.com/auth/userinfo.email" 
        };
        private static readonly string ApplicationName = "CatchCapture";

        private static GoogleDriveUploadProvider? _instance;
        public static GoogleDriveUploadProvider Instance => _instance ??= new GoogleDriveUploadProvider();

        private DriveService? _service;
        private UserCredential? _credential;
        private CancellationTokenSource? _loginCts; // 추가
        public string UserEmail { get; private set; } = "";

        public bool IsConnected => _credential != null && !string.IsNullOrEmpty(UserEmail);

        private GoogleDriveUploadProvider() { }

        private static string GetStoredTokenPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CatchCapture",
                "GoogleDriveAuth");
        }

        private static bool CheckStoredToken()
        {
            string credPath = GetStoredTokenPath();
            return Directory.Exists(credPath) && Directory.GetFiles(credPath).Length > 0;
        }

        public async Task<bool> LoginAsync()
        {
            try
            {
                // 1. 기존에 꼬여있을 수 있는 토큰 폴더 삭제 (새로 로그인 시도 시)
                string credPath = GetStoredTokenPath();
                if (Directory.Exists(credPath))
                {
                    try { Directory.Delete(credPath, true); } catch { }
                }

                // 2. 프리미엄 디자인 HTML 준비
                string html = GetPremiumSuccessHtml();

                // 1. 기존 로그인 시도 취소
                _loginCts?.Cancel();
                _loginCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30초로 단축

                using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
                {
                    var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

                    // 구글 공식 리시버에 우리 HTML을 주입 (가장 안정적인 방식)
                    var receiver = new LocalServerCodeReceiver(html);
                    
                    _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        secrets,
                        Scopes,
                        "user",
                        _loginCts.Token,
                        new FileDataStore(credPath, true),
                        receiver);
                }

                if (_credential != null)
                {
                    await InitServiceAsync();
                    return true;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Google Login Error: {ex.Message}");
            }
            finally
            {
                _loginCts = null;
            }
            return false;
        }

        public void CancelLogin()
        {
            _loginCts?.Cancel();
        }

        public async Task<bool> TrySilentLoginAsync()
        {
            if (_credential != null) return true;
            if (!CheckStoredToken()) return false;
            
            try 
            {
                using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
                {
                    var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
                    string credPath = GetStoredTokenPath();
                    
                    _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true));
                }

                if (_credential != null)
                {
                    await InitServiceAsync();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private async Task InitServiceAsync()
        {
            if (_credential == null) return;

            _service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName,
            });

            var aboutRequest = _service.About.Get();
            aboutRequest.Fields = "user";
            var about = await aboutRequest.ExecuteAsync();
            UserEmail = about.User?.EmailAddress ?? "Unknown";
        }

        public async Task LogoutAsync()
        {
            try
            {
                if (_credential != null)
                {
                    await _credential.RevokeTokenAsync(CancellationToken.None);
                }

                string credPath = GetStoredTokenPath();
                if (Directory.Exists(credPath))
                {
                    Directory.Delete(credPath, true);
                }

                _credential = null;
                _service = null;
                UserEmail = "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Google Logout Error: {ex.Message}");
            }
        }

        public async Task<string> UploadImageAsync(string imagePath)
        {
            if (_service == null)
            {
                if (!await TrySilentLoginAsync())
                {
                    throw new Exception("Google Drive Login Required.");
                }
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(imagePath),
                Parents = new List<string> { "root" }
            };

            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(imagePath, FileMode.Open))
            {
                request = _service!.Files.Create(fileMetadata, stream, "image/png");
                request.Fields = "id, webViewLink, webContentLink";
                await request.UploadAsync();
            }

            var file = request.ResponseBody;
            
            await _service.Permissions.Create(new Google.Apis.Drive.v3.Data.Permission
            {
                Role = "reader",
                Type = "anyone"
            }, file.Id).ExecuteAsync();

            return file.WebContentLink;
        }

        private string GetAppIconBase64()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "catcha.png");
                if (File.Exists(path))
                {
                    return Convert.ToBase64String(File.ReadAllBytes(path));
                }
            }
            catch { }
            return "";
        }

        private string GetPremiumSuccessHtml()
        {
            string iconBase64 = GetAppIconBase64();
            return $@"
<html>
<head>
    <meta charset='utf-8'>
    <title>CatchCapture - 인증 완료</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; background-color: #f5f7fa; color: #333; }}
        .container {{ text-align: center; background: white; padding: 40px; border-radius: 20px; box-shadow: 0 10px 25px rgba(0,0,0,0.05); max-width: 400px; width: 90%; }}
        .icon {{ width: 80px; height: 80px; margin-bottom: 20px; }}
        h2 {{ margin: 10px 0; color: #2d3436; }}
        p {{ color: #636e72; font-size: 14px; line-height: 1.6; }}
        .success-mark {{ color: #00b894; font-size: 48px; margin-bottom: 10px; }}
        .loader {{ border: 3px solid #f3f3f3; border-top: 3px solid #3498db; border-radius: 50%; width: 20px; height: 20px; animation: spin 2s linear infinite; display: inline-block; vertical-align: middle; margin-right: 10px; }}
        @keyframes spin {{ 0% {{ transform: rotate(0deg); }} 100% {{ transform: rotate(360deg); }} }}
    </style>
</head>
<body>
    <div class='container'>
        <img src='data:image/png;base64,{iconBase64}' class='icon' alt='Logo'>
        <div class='success-mark'>✓</div>
        <h2>Google 드라이브 연동 성공!</h2>
        <p>이제 CatchCapture에서 Google 드라이브를 사용할 수 있습니다.<br>이 창은 잠시 후 자동으로 닫힙니다.</p>
        <div id='status' style='margin-top: 20px;'>
            <div class='loader'></div>
            <span style='font-size: 12px; color: #999;'>창을 닫는 중...</span>
        </div>
        <p id='fallback' style='display:none; font-size: 12px; color: #e74c3c; margin-top: 15px;'>
            창이 자동으로 닫히지 않으면 직접 닫아주세요.
        </p>
    </div>
    <script>
        setTimeout(function() {{
            try {{
                window.open('', '_self', '');
                window.close();
                setTimeout(function() {{
                    if (!window.closed) {{
                        document.getElementById('status').style.display = 'none';
                        document.getElementById('fallback').style.display = 'block';
                    }}
                }}, 500);
            }} catch (e) {{
                document.getElementById('status').style.display = 'none';
                document.getElementById('fallback').style.display = 'block';
            }}
        }}, 2500);
    </script>
</body>
</html>";
        }
    }
}
