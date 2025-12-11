using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture.Recording
{
    /// <summary>
    /// 화면 녹화기 - GDI+ 기반 (Windows 10+ 호환)
    /// </summary>
    public class ScreenRecorder : IDisposable
    {
        // 녹화 설정
        private RecordingSettings _settings;
        
        // 녹화 상태
        private bool _isRecording = false;
        private bool _isPaused = false;
        private bool _isDisposed = false;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        
        // 프레임 저장
        private List<byte[]> _frames;
        private List<int> _frameDelays;
        private DateTime _lastFrameTime;
        
        // 이벤트
        public event EventHandler<BitmapSource>? FrameCaptured;
        public event EventHandler? RecordingStarted;
        public event EventHandler<string>? RecordingStopped;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler<RecordingStats>? StatsUpdated;
        
        // 녹화 영역
        private Rect _captureArea;
        
        // 통계
        private int _frameCount;
        private DateTime _recordingStartTime;
        
        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public int FrameCount => _frameCount;
        
        public ScreenRecorder(RecordingSettings settings)
        {
            _settings = settings;
            _frames = new List<byte[]>();
            _frameDelays = new List<int>();
        }
        
        /// <summary>
        /// 녹화 시작
        /// </summary>
        public void StartRecording(Rect captureArea)
        {
            if (_isRecording) return;
            
            _captureArea = captureArea;
            _isRecording = true;
            _isPaused = false;
            _frameCount = 0;
            _frames.Clear();
            _frameDelays.Clear();
            _recordingStartTime = DateTime.Now;
            _lastFrameTime = DateTime.Now;
            _cts = new CancellationTokenSource();
            
            // 캡처 루프 시작
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
            
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// 녹화 일시정지/재개
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
        }
        
        /// <summary>
        /// 녹화 중지 (저장하지 않음)
        /// </summary>
        public async Task StopRecording()
        {
            if (!_isRecording) return;
            
            _isRecording = false;
            _cts?.Cancel();
            
            // 캡처 루프 종료 대기
            if (_captureTask != null)
            {
                try
                {
                    await _captureTask;
                }
                catch (OperationCanceledException) { /* 무시 */ }
                catch (Exception) { /* 무시 */ }
            }
            
            RecordingStopped?.Invoke(this, string.Empty);
        }
        
        /// <summary>
        /// 녹화 파일 저장
        /// </summary>
        public async Task<string> SaveRecordingAsync(string outputPath)
        {
            string savedPath = string.Empty;
            
            try
            {
                // 폴더 생성
                string? outputFolder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputFolder) && !Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }
                
                if (_settings.Format == RecordingFormat.GIF && _frames.Count > 0)
                {
                    savedPath = await SaveAsGifAsync(outputPath);
                }
                else if (_settings.Format == RecordingFormat.MP4 && _frames.Count > 0)
                {
                    // MP4 저장 (Media Foundation 사용)
                    savedPath = await SaveAsMP4Async(outputPath);
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            
            return savedPath;
        }
        
        /// <summary>
        /// 첫 프레임을 썸네일로 반환
        /// </summary>
        public BitmapSource? GetThumbnail()
        {
            if (_frames.Count == 0) return null;
            
            try
            {
                using var ms = new MemoryStream(_frames[0]);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.DecodePixelWidth = 200;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 캡처 루프
        /// </summary>
        private async Task CaptureLoop(CancellationToken token)
        {
            int frameInterval = 1000 / _settings.FrameRate; // ms
            
            while (!token.IsCancellationRequested && _isRecording)
            {
                if (_isPaused)
                {
                    await Task.Delay(100, token);
                    continue;
                }
                
                try
                {
                    var startTime = DateTime.Now;
                    
                    // 화면 캡처
                    var frameData = CaptureScreen();
                    
                    if (frameData != null)
                    {
                        _frames.Add(frameData);
                        _frameDelays.Add(frameInterval);
                        _frameCount++;
                        
                        // 통계 업데이트
                        var stats = new RecordingStats
                        {
                            FrameCount = _frameCount,
                            Duration = DateTime.Now - _recordingStartTime,
                            CurrentFps = CalculateCurrentFps(),
                            FileSizeEstimate = EstimateFileSize()
                        };
                        
                        // UI 스레드에서 이벤트 발생
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            StatsUpdated?.Invoke(this, stats);
                        });
                    }
                    
                    // 프레임 간격 유지
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    var delay = Math.Max(1, frameInterval - (int)elapsed);
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }
        
        /// <summary>
        /// 화면 캡처 (GDI+)
        /// </summary>
        private byte[]? CaptureScreen()
        {
            try
            {
                int width = (int)_captureArea.Width;
                int height = (int)_captureArea.Height;
                
                if (width <= 0 || height <= 0) return null;
                
                using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(
                    (int)_captureArea.X,
                    (int)_captureArea.Y,
                    0, 0,
                    new System.Drawing.Size(width, height),
                    CopyPixelOperation.SourceCopy);
                
                // PNG로 압축하여 저장 (GIF 변환 시 필요)
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// GIF로 저장
        /// </summary>
        private async Task<string> SaveAsGifAsync(string outputPath)
        {
            if (_frames.Count == 0)
                return string.Empty;
            
            await Task.Run(() =>
            {
                using var stream = new FileStream(outputPath, FileMode.Create);
                
                // GIF 헤더 작성
                var encoder = new AnimatedGifEncoder();
                encoder.Start(stream);
                encoder.SetDelay(1000 / _settings.FrameRate);
                encoder.SetRepeat(0); // 무한 반복
                encoder.SetQuality(10); // 품질 (1-20, 낮을수록 좋음)
                
                foreach (var frameData in _frames)
                {
                    using var ms = new MemoryStream(frameData);
                    using var img = System.Drawing.Image.FromStream(ms);
                    encoder.AddFrame(img);
                }
                
                encoder.Finish();
            });
            
            return outputPath;
        }
        
        /// <summary>
        /// MP4로 저장 (ffmpeg.exe CLI 사용 - Intermediate File 방식 /w Debug)
        /// </summary>
        private async Task<string> SaveAsMP4Async(string outputPath)
        {
            // 디버그 로그 설정
            string logPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? "C:\\", "ffmpeg_debug.txt");
            void Log(string msg) 
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
                Debug.WriteLine($"[ScreenRecorder] {msg}");
            }

            Log("=== Recording Save Start (Intermediate File Mode) ===");
            Log($"Target Path: {outputPath}");

            if (_frames.Count == 0)
            {
                Log("Error: No frames recorded.");
                return string.Empty;
            }

            if (!FFmpegDownloader.IsFFmpegInstalled())
            {
                Log("FFmpeg fallback to GIF");
                string gifPath = Path.ChangeExtension(outputPath, ".gif");
                return await SaveAsGifAsync(gifPath);
            }

            string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
            Log($"FFmpeg Path: {ffmpegPath}");

            // 임시 폴더 (공용 문서)
            string tempFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            if (!Directory.Exists(tempFolder)) tempFolder = Path.GetTempPath();

            string rawDataFileName = $"raw_data_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.raw";
            string rawDataPath = Path.Combine(tempFolder, rawDataFileName);
            
            string tempMp4FileName = $"rec_temp_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.mp4";
            string tempMp4Path = Path.Combine(tempFolder, tempMp4FileName);

            Log($"Raw Data Path: {rawDataPath}");

            try
            {
                int width = (int)_captureArea.Width;
                int height = (int)_captureArea.Height;
                Log($"Dimensions: {width}x{height} (Original)");

                // libx264 짝수 보정
                if (width % 2 != 0) width--;
                if (height % 2 != 0) height--;
                Log($"Dimensions: {width}x{height} (Adjusted)");

                // 1. Raw 데이터 파일 생성
                await Task.Run(() =>
                {
                    using (var fs = new FileStream(rawDataPath, FileMode.Create, FileAccess.Write))
                    {
                        for (int i = 0; i < _frames.Count; i++)
                        {
                            try
                            {
                                using var ms = new MemoryStream(_frames[i]);
                                using var bitmap = new Bitmap(ms);

                                var bitmapData = bitmap.LockBits(
                                    new Rectangle(0, 0, width, height),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);

                                try
                                {
                                    int stride = bitmapData.Stride;
                                    int rowBytes = width * 3;
                                    byte[] frameBuffer = new byte[rowBytes * height];

                                    // Stride 제거 Copy
                                    for (int y = 0; y < height; y++)
                                    {
                                        IntPtr srcPtr = IntPtr.Add(bitmapData.Scan0, y * stride);
                                        Marshal.Copy(srcPtr, frameBuffer, y * rowBytes, rowBytes);
                                    }
                                    
                                    fs.Write(frameBuffer, 0, frameBuffer.Length);
                                }
                                finally
                                {
                                    bitmap.UnlockBits(bitmapData);
                                }
                            }
                            catch (Exception frameEx)
                            {
                                Log($"Frame {i} processing failed: {frameEx.Message}");
                            }
                        }
                    }
                });

                var rawFileInfo = new FileInfo(rawDataPath);
                Log($"Raw File Created. Size: {rawFileInfo.Length} bytes");
                if (rawFileInfo.Length == 0) throw new Exception("Raw data file is empty.");

                // 2. FFmpeg 실행
                await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -f rawvideo -pixel_format bgr24 -video_size {width}x{height} -framerate {_settings.FrameRate} -i \"{rawDataPath}\" -c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p \"{tempMp4Path}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    Log($"Executing FFmpeg: {startInfo.Arguments}");

                    using var process = Process.Start(startInfo);
                    if (process == null) throw new Exception("Failed to start FFmpeg.");

                    process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[STDERR] {e.Data}"); };
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($"[STDOUT] {e.Data}"); };

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    process.WaitForExit();
                    Log($"FFmpeg Exit Code: {process.ExitCode}");
                    
                    if (process.ExitCode != 0) throw new Exception($"FFmpeg failed with code {process.ExitCode}");
                });

                // 3. 파일 이동 확인
                if (File.Exists(tempMp4Path))
                {
                    var resultInfo = new FileInfo(tempMp4Path);
                    Log($"Temp MP4 Size: {resultInfo.Length}");
                    
                    if (resultInfo.Length < 100)
                    {
                        throw new Exception("Generated MP4 file is too small (likely invalid).");
                    }

                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(tempMp4Path, outputPath);
                    Log("Successfully moved to target path.");
                    
                    return outputPath;
                }
                else
                {
                    throw new Exception("Output MP4 file was not created by FFmpeg.");
                }
            }
            catch (Exception ex)
            {
                Log($"Fatal Error: {ex.Message}\n{ex.StackTrace}");
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"녹화 실패: {ex.Message}\n로그: {logPath}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return string.Empty;
            }
            finally
            {
                // 정리
                try { if (File.Exists(rawDataPath)) File.Delete(rawDataPath); } catch { }
                try { if (File.Exists(tempMp4Path)) File.Delete(tempMp4Path); } catch { }
            }
        }
        
        /// <summary>
        /// 현재 FPS 계산
        /// </summary>
        private double CalculateCurrentFps()
        {
            var elapsed = (DateTime.Now - _recordingStartTime).TotalSeconds;
            if (elapsed <= 0) return 0;
            return _frameCount / elapsed;
        }
        
        /// <summary>
        /// 파일 크기 추정
        /// </summary>
        private long EstimateFileSize()
        {
            long totalBytes = 0;
            foreach (var frame in _frames)
            {
                totalBytes += frame.Length;
            }
            // GIF 압축률 대략 추정
            return totalBytes / 3;
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _isRecording = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _frames.Clear();
            _frameDelays.Clear();
            
            GC.SuppressFinalize(this);
        }
    }
    
    /// <summary>
    /// 녹화 통계
    /// </summary>
    public class RecordingStats
    {
        public int FrameCount { get; set; }
        public TimeSpan Duration { get; set; }
        public double CurrentFps { get; set; }
        public long FileSizeEstimate { get; set; }
        
        public string FormattedDuration => Duration.ToString(@"mm\:ss");
        public string FormattedSize => FormatBytes(FileSizeEstimate);
        
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
