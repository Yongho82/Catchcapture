using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace CatchCapture.Recording
{
    /// <summary>
    /// FFmpeg DLL 런타임 다운로더
    /// </summary>
    public static class FFmpegDownloader
    {
        private static readonly string[] RequiredDlls = new[]
        {
            "avcodec-61.dll",
            "avdevice-61.dll",
            "avfilter-10.dll",
            "avformat-61.dll",
            "avutil-59.dll",
            "swresample-5.dll",
            "swscale-8.dll"
        };
        
        // 고정 버전 URL (더 안정적)
        private const string DownloadUrl = "https://github.com/GyanD/codexffmpeg/releases/download/7.1/ffmpeg-7.1-essentials_build.zip";
        
        /// <summary>
        /// FFmpeg 설치 경로 가져오기 (설치된 경우)
        /// </summary>
        public static string GetFFmpegPath()
        {
            // 1. 프로그램 실행 폴더 확인
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (CheckDllsInPath(baseDir)) return baseDir;
            
            // 2. AppData 폴더 확인
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "ffmpeg");
            if (CheckDllsInPath(appDataDir)) return appDataDir;
            
            return string.Empty;
        }
        
        private static bool CheckDllsInPath(string path)
        {
            if (!Directory.Exists(path)) return false;
            
            // 하나라도 있으면 설치된 것으로 간주
            foreach (var dll in RequiredDlls)
            {
                if (File.Exists(Path.Combine(path, dll))) return true;
            }
            
            // avcodec*.dll 패턴 확인
            return Directory.GetFiles(path, "avcodec*.dll").Length > 0;
        }
        
        /// <summary>
        /// FFmpeg DLL이 모두 존재하는지 확인
        /// </summary>
        public static bool IsFFmpegInstalled()
        {
            return !string.IsNullOrEmpty(GetFFmpegPath());
        }
        
        /// <summary>
        /// FFmpeg DLL 다운로드 및 설치
        /// </summary>
        public static async Task<bool> DownloadFFmpegAsync(IProgress<int>? progress = null)
        {
            try
            {
                string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg_download.zip");
                string tempExtract = Path.Combine(Path.GetTempPath(), "ffmpeg_extract");
                
                // 설치 대상 경로 결정 (AppData 우선 시도)
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "ffmpeg");
                
                // 실행 폴더에 쓰기 권한이 있는지 확인 (간단한 테스트)
                try 
                {
                    string testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".test_write");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    // 쓰기 성공하면 실행 폴더 사용
                    targetDir = AppDomain.CurrentDomain.BaseDirectory;
                }
                catch 
                {
                    // 쓰기 실패하면 AppData 사용
                }
                
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                
                progress?.Report(0);
                
                // 기존 임시 파일/폴더 삭제
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                
                // 다운로드
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.Add("User-Agent", "CatchCapture");
                    
                    using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                    
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            int percent = (int)((totalRead * 50) / totalBytes);
                            progress?.Report(percent);
                        }
                    }
                }
                
                progress?.Report(50);
                
                // 압축 해제
                ZipFile.ExtractToDirectory(tempZip, tempExtract, true);
                
                progress?.Report(60);
                
                // bin 폴더에서 DLL 찾기
                var binFolders = Directory.GetDirectories(tempExtract, "bin", SearchOption.AllDirectories);
                
                if (binFolders.Length > 0)
                {
                    string binFolder = binFolders[0];
                    var dllFiles = Directory.GetFiles(binFolder, "*.dll");
                    
                    int count = 0;
                    foreach (var dllFile in dllFiles)
                    {
                        string fileName = Path.GetFileName(dllFile);
                        string destPath = Path.Combine(targetDir, fileName);
                        File.Copy(dllFile, destPath, true);
                        
                        count++;
                        int percent = 60 + (count * 40 / Math.Max(1, dllFiles.Length));
                        progress?.Report(percent);
                    }
                }
                
                // 임시 파일 정리
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                
                progress?.Report(100);
                
                // 저장 경로 표시
                MessageBox.Show($"FFmpeg DLL이 다음 위치에 저장되었습니다:\n{targetDir}", "설치 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"FFmpeg 다운로드 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
