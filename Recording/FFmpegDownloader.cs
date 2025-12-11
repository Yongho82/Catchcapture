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
    /// FFmpeg 실행 파일(CLI) 다운로더
    /// </summary>
    public static class FFmpegDownloader
    {
        // 다운로드 URL 목록 (미러)
        private static readonly string[] DownloadUrls = new[] 
        {
            "https://github.com/GyanD/codexffmpeg/releases/download/7.1/ffmpeg-7.1-essentials_build.zip", // Primary
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip" // Backup
        };
        
        /// <summary>
        /// FFmpeg 실행 파일 경로 가져오기 (설치된 경우)
        /// </summary>
        public static string GetFFmpegPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (CheckExeInPath(baseDir)) return Path.Combine(baseDir, "ffmpeg.exe");
            
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "ffmpeg");
            if (CheckExeInPath(appDataDir)) return Path.Combine(appDataDir, "ffmpeg.exe");
            
            return string.Empty;
        }
        
        private static bool CheckExeInPath(string path)
        {
            if (!Directory.Exists(path)) return false;
            return File.Exists(Path.Combine(path, "ffmpeg.exe"));
        }
        
        public static bool IsFFmpegInstalled() => !string.IsNullOrEmpty(GetFFmpegPath());
        
        /// <summary>
        /// FFmpeg 다운로드 및 설치
        /// </summary>
        public static async Task<bool> DownloadFFmpegAsync(IProgress<int>? progress = null)
        {
            string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg_download.zip");
            string tempExtract = Path.Combine(Path.GetTempPath(), "ffmpeg_extract");
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "ffmpeg");
            
            try 
            {
                // 권한 체크 및 디렉토리 생성
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"설치 폴더 생성 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // URL 순차 시도
            foreach (var url in DownloadUrls)
            {
                try
                {
                    progress?.Report(0);
                    
                    // cleanup
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                    
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(20);
                        client.DefaultRequestHeaders.Add("User-Agent", "CatchCapture");
                        
                        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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
                    progress?.Report(70);
                    
                    // ffmpeg.exe 찾기
                    bool found = false;
                    var exeFiles = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories);
                    
                    if (exeFiles.Length > 0)
                    {
                        string destPath = Path.Combine(targetDir, "ffmpeg.exe");
                        File.Copy(exeFiles[0], destPath, true);
                        found = true;
                    }
                    
                    if (!found) throw new Exception("압축 파일 내에서 ffmpeg.exe를 찾을 수 없습니다.");
                    
                    progress?.Report(100);
                    
                    // 정리
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                    
                    CatchCapture.CustomMessageBox.Show($"FFmpeg 설치가 완료되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true; // 성공 시 즉시 리턴
                }
                catch (Exception)
                {
                    // 이번 URL 실패 - 다음 URL 시도
                    continue; 
                }
            }
            
            // 모든 URL 실패
            return false;
        }

        /// <summary>
        /// 수동 설치 (파일 직접 지정)
        /// </summary>
        public static bool ManualInstall(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return false;
                
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "ffmpeg");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                
                string destPath = Path.Combine(targetDir, "ffmpeg.exe");
                File.Copy(sourcePath, destPath, true);
                
                return true;
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"수동 설치 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
