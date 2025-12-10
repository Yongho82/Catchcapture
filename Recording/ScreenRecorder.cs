using System;
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
        /// MP4로 저장 (FFMediaToolkit 사용)
        /// </summary>
        private async Task<string> SaveAsMP4Async(string outputPath)
        {
            if (_frames.Count == 0)
                return string.Empty;
            
            // FFmpeg DLL 확인 (없으면 GIF로 폴백)
            if (!FFmpegDownloader.IsFFmpegInstalled())
            {
                // FFmpeg가 없으면 GIF로 저장
                string gifPath = Path.ChangeExtension(outputPath, ".gif");
                return await SaveAsGifAsync(gifPath);
            }
            
            // FFmpeg 경로 설정
            string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                FFMediaToolkit.FFmpegLoader.FFmpegPath = ffmpegPath;
            }
            
            await Task.Run(() =>
            {
                var settings = new FFMediaToolkit.Encoding.VideoEncoderSettings(
                    width: (int)_captureArea.Width,
                    height: (int)_captureArea.Height,
                    framerate: _settings.FrameRate,
                    codec: FFMediaToolkit.Encoding.VideoCodec.H264)
                {
                    EncoderPreset = FFMediaToolkit.Encoding.EncoderPreset.Fast,
                    CRF = 23
                };
                
                using var file = FFMediaToolkit.Encoding.MediaBuilder.CreateContainer(outputPath).WithVideo(settings).Create();
                
                foreach (var frameData in _frames)
                {
                    using var ms = new MemoryStream(frameData);
                    using var bitmap = new Bitmap(ms);
                    
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format24bppRgb);
                    
                    try
                    {
                        var imageData = FFMediaToolkit.Graphics.ImageData.FromPointer(
                            bitmapData.Scan0,
                            FFMediaToolkit.Graphics.ImagePixelFormat.Bgr24,
                            new System.Drawing.Size(bitmap.Width, bitmap.Height));
                        
                        file.Video.AddFrame(imageData);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }
                }
            });
            
            return outputPath;
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
