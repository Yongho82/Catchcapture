using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using CatchCapture.Models;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace CatchCapture.Utilities
{
    public class DropboxUploadProvider
    {
        private const string AppKey = "fdc25lx94w5nypl"; 
        private const string RedirectUri = "http://localhost:52475/";

        private static DropboxUploadProvider? _instance;
        public static DropboxUploadProvider Instance => _instance ??= new DropboxUploadProvider();

        private static readonly HttpClient client = new HttpClient();
        private Settings _settings => Settings.Load(); // 항상 최신 설정 인스턴스 사용
        private string? _codeVerifier;
        private CancellationTokenSource? _loginCts;
        private HttpListener? _listener; // 리스너를 필드로 관리하여 확실히 종료

        private DropboxUploadProvider()
        {
        }

        public bool IsConnected => !string.IsNullOrEmpty(_settings.DropboxRefreshToken);

        /// <summary>
        /// 드롭박스 사용자 정보 가져오기
        /// </summary>
        public async Task<string?> GetAccountInfoAsync()
        {
            if (string.IsNullOrEmpty(_settings.DropboxAccessToken))
            {
                if (!await RefreshTokenAsync()) return null;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/users/get_current_account");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DropboxAccessToken);
                
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    return json["name"]?["display_name"]?.ToString();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 완전한 로그아웃 (서버 토큰 폐기 및 로컬 삭제)
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_settings.DropboxAccessToken))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/auth/token/revoke");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DropboxAccessToken);
                    await client.SendAsync(request);
                }
            }
            catch { }
            finally
            {
                _settings.DropboxAccessToken = "";
                _settings.DropboxRefreshToken = "";
                _settings.Save();
            }
        }

        public async Task<bool> LoginAsync()
        {
            // 1. 이전 로그인 시도가 있다면 취소
            _loginCts?.Cancel();
            _loginCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30초로 단축
            var token = _loginCts.Token;

            try
            {
                // PKCE 준비 (보안 코드 생성)
                _codeVerifier = GenerateRandomString(64);
                string codeChallenge = GenerateCodeChallenge(_codeVerifier);

                // 이전 리스너가 있다면 정리
                if (_listener != null && _listener.IsListening)
                {
                    try { _listener.Stop(); } catch { }
                    try { _listener.Close(); } catch { }
                }

                _listener = new HttpListener();
                _listener.Prefixes.Add(RedirectUri);
                
                try
                {
                    _listener.Start();
                }
                catch
                {
                    // 포트 점유 해제를 위해 잠시 대기 후 재시도
                    await Task.Delay(300);
                    try { _listener.Start(); } catch { return false; }
                }

                // PKCE 파라미터(code_challenge) 추가
                string authUrl = $"https://www.dropbox.com/oauth2/authorize?client_id={AppKey}&response_type=code&redirect_uri={UrlEncode(RedirectUri)}&token_access_type=offline&code_challenge={codeChallenge}&code_challenge_method=S256";
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

                // 브라우저 응답 대기 (취소 토큰 적용)
                HttpListenerContext context;
                try
                 {
                    var contextTask = _listener.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, token));
                    
                    if (completedTask != contextTask)
                    {
                        _listener.Stop();
                        return false; 
                    }
                    
                    context = await contextTask;
                }
                catch
                {
                    _listener?.Stop();
                    return false;
                }

                var code = context.Request.QueryString["code"];

                // 완료 페이지 HTML 응답 로직
                string iconBase64 = GetAppIconBase64();
                string html = GetSuccessHtml(iconBase64);

                byte[] responseBytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();
                
                await Task.Delay(500);
                _listener.Stop();
                _listener.Close();
                _listener = null;

                if (string.IsNullOrEmpty(code)) return false;
                return await ExchangeCodeForTokenAsync(code);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dropbox Login Error: {ex.Message}");
                return false;
            }
            finally
            {
                _loginCts = null;
            }
        }

        private string GetSuccessHtml(string iconBase64)
        {
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
        <h2>연동에 성공했습니다!</h2>
        <p>이제 CatchCapture에서 Dropbox를 사용할 수 있습니다.<br>이 창은 잠시 후 자동으로 닫힙니다.</p>
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

        public void CancelLogin()
        {
            _loginCts?.Cancel();
            try { _listener?.Stop(); } catch { }
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            var values = new Dictionary<string, string>
            {
                {"code", code},
                {"grant_type", "authorization_code"},
                {"client_id", AppKey},
                {"redirect_uri", RedirectUri},
                {"code_verifier", _codeVerifier ?? ""} // PKCE 검증기 포함
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://api.dropbox.com/oauth2/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var json = JObject.Parse(responseString);
                var accessToken = json["access_token"]?.ToString();
                var refreshToken = json["refresh_token"]?.ToString();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    _settings.DropboxAccessToken = accessToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                        _settings.DropboxRefreshToken = refreshToken;
                    
                    _settings.Save();
                    return true;
                }
            }
            else
            {
                Debug.WriteLine($"Dropbox Token Exchange Failed: {responseString}");
            }
            return false;
        }

        private async Task<bool> RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_settings.DropboxRefreshToken)) return false;

            var values = new Dictionary<string, string>
            {
                {"grant_type", "refresh_token"},
                {"refresh_token", _settings.DropboxRefreshToken},
                {"client_id", AppKey}
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://api.dropbox.com/oauth2/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var json = JObject.Parse(responseString);
                var accessToken = json["access_token"]?.ToString();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    _settings.DropboxAccessToken = accessToken;
                    _settings.Save();
                    return true;
                }
            }
            return false;
        }

        public async Task<string> UploadImageAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(_settings.DropboxAccessToken))
            {
                if (!await RefreshTokenAsync())
                {
                    if (!await LoginAsync()) throw new Exception("Dropbox 로그인이 필요합니다.");
                }
            }

            try
            {
                return await ExecuteUploadAsync(imagePath);
            }
            catch (Exception ex) when (ex.Message.Contains("401"))
            {
                if (await RefreshTokenAsync())
                {
                    return await ExecuteUploadAsync(imagePath);
                }
                throw;
            }
        }

        private async Task<string> ExecuteUploadAsync(string imagePath)
        {
            string fileName = Path.GetFileName(imagePath);
            string dropboxPath = $"/CatchCapture/{fileName}";

            using (var fileStream = File.OpenRead(imagePath))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/upload");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DropboxAccessToken);
                
                var apiArg = JsonConvert.SerializeObject(new
                {
                    path = dropboxPath,
                    mode = "overwrite",
                    autorename = true,
                    mute = false
                });
                request.Headers.Add("Dropbox-API-Arg", apiArg);
                request.Content = new StreamContent(fileStream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Dropbox Upload Error: {response.StatusCode} - {error}");
                }
            }

            var linkRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings");
            linkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DropboxAccessToken);
            linkRequest.Content = new StringContent(JsonConvert.SerializeObject(new
            {
                path = dropboxPath,
                settings = new { requested_visibility = "public" }
            }), Encoding.UTF8, "application/json");

            var linkResponse = await client.SendAsync(linkRequest);
            var linkResponseString = await linkResponse.Content.ReadAsStringAsync();

            if (linkResponse.IsSuccessStatusCode)
            {
                var json = JObject.Parse(linkResponseString);
                string? url = json["url"]?.ToString();
                if (!string.IsNullOrEmpty(url)) return url.Replace("?dl=0", "?raw=1");
            }
            else if (linkResponseString.Contains("shared_link_already_exists"))
            {
                return await GetExistingLinkAsync(dropboxPath);
            }

            throw new Exception("공유 링크를 생성하지 못했습니다.");
        }

        private async Task<string> GetExistingLinkAsync(string dropboxPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/sharing/list_shared_links");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.DropboxAccessToken);
            request.Content = new StringContent(JsonConvert.SerializeObject(new
            {
                path = dropboxPath,
                direct_only = true
            }), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var json = JObject.Parse(responseString);
                var links = json["links"] as JArray;
                if (links != null && links.Count > 0)
                {
                    string? url = links[0]?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url)) return url.Replace("?dl=0", "?raw=1");
                }
            }
            throw new Exception("기존 링크를 가져오지 못했습니다.");
        }

        // PKCE 헬퍼 함수들
        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            var randomBytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var result = new StringBuilder(length);
            foreach (byte b in randomBytes) result.Append(chars[b % chars.Length]);
            return result.ToString();
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(challengeBytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
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
            return ""; // 아이콘 없으면 빈값 (이미지 태그는 깨지겠지만 텍스트는 나옴)
        }

        private string UrlEncode(string value) => WebUtility.UrlEncode(value);
    }
}
