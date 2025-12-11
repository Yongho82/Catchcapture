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
using NAudio.Wave;

// Windows Timer Resolution API
using System.ComponentModel;

namespace CatchCapture.Recording
{

    /// <summary>
    /// 화면 녹화기 - GDI+ 기반 (Windows 10+ 호환)
    /// </summary>
    public class ScreenRecorder : IDisposable
    {
        // === Windows Timer Resolution API (고정밀 타이머) ===
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);
        
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);
        
        // 녹화 설정
        private RecordingSettings _settings;
        
        // 녹화 상태
        private bool _isRecording = false;
        private bool _isPaused = false;
        private bool _isDisposed = false;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        
        // 오디오 녹화 (NAudio)
        private IWaveIn? _audioCapture;
        private WaveFileWriter? _audioWriter;
        private string? _tempAudioPath;
        private TaskCompletionSource<bool>? _audioStopTcs; // 오디오 종료 대기용
        
        // === 실시간 인코딩 (FFmpeg Pipe) ===
        private Process? _ffmpegProcess;
        private Stream? _ffmpegStdin;
        private string? _tempVideoPath;    // 임시 비디오 (오디오 없는 버전)
        private bool _useRealTimeEncoding = true;  // 실시간 인코딩 사용 여부
        
        // FFmpeg 쓰기용 큐 (블로킹 방지)
        private System.Collections.Concurrent.ConcurrentQueue<byte[]>? _frameQueue;
        private Task? _ffmpegWriteTask;
        private volatile bool _stopWriting = false;
        
        // 프레임 저장 (실시간 인코딩 실패 시 폴백용)
        private List<byte[]> _frames;
        private List<int> _frameDelays;
        private byte[]? _firstFrame;  // 썸네일용 첫 프레임
        private DateTime _lastFrameTime;
        
        // 캡처 영역 크기 (Raw 데이터 디코딩용)
        private int _captureWidth;
        private int _captureHeight;
        
        // 일시정지 관련
        private DateTime _pauseStartTime;
        
        // 오디오 싱크 보정용 변수
        private long _totalAudioBytes = 0;
        private DateTime _audioStartTime;
        
        // 고정밀 타이밍
        private Stopwatch _frameStopwatch = new Stopwatch();
        private bool _timerResolutionSet = false;
        
        // 이벤트

        public event EventHandler? RecordingStarted;
        public event EventHandler<string>? RecordingStopped;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler<RecordingStats>? StatsUpdated;
        
        // 녹화 영역
        private Rect _captureArea;
        
        // 통계
        private int _frameCount;
        private DateTime _recordingStartTime;
        private TimeSpan _actualDuration;
        
        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public int FrameCount => _frameCount;
        
        public ScreenRecorder(RecordingSettings settings)
        {
            _settings = settings;
            _frames = new List<byte[]>();
            _frameDelays = new List<int>();
            
            // Windows Timer Resolution을 1ms로 설정 (고정밀 타이밍)
            try
            {
                TimeBeginPeriod(1);
                _timerResolutionSet = true;
            }
            catch { }
        }
        
        /// <summary>
        /// 녹화 시작
        /// </summary>
        public void StartRecording(Rect captureArea)
        {
            if (_isRecording) return;
            
            // 테두리 두께(3px)만큼 안쪽으로 조정하여 테두리가 녹화에 포함되지 않도록 함
            const int borderThickness = 3;
            _captureArea = new Rect(
                captureArea.X + borderThickness,
                captureArea.Y + borderThickness,
                Math.Max(captureArea.Width - (borderThickness * 2), 1),
                Math.Max(captureArea.Height - (borderThickness * 2), 1)
            );
            
            // 캡처 크기 저장 (FFmpeg 입력용 - 짝수로 보정)
            _captureWidth = (int)_captureArea.Width;
            _captureHeight = (int)_captureArea.Height;
            if (_captureWidth % 2 != 0) _captureWidth--;
            if (_captureHeight % 2 != 0) _captureHeight--;
            
            _isRecording = true;
            _isPaused = false;
            _frameCount = 0;
            _frames.Clear();
            _frameDelays.Clear();
            _firstFrame = null;  // 첫 프레임 초기화
            _recordingStartTime = DateTime.Now;
            _lastFrameTime = DateTime.Now;
            _cts = new CancellationTokenSource();
            
            // 오디오 녹화 시작 (시스템 사운드)
            if (_settings.RecordAudio)
            {
                try
                {
                    StartAudioRecording();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Audio setup failed: {ex.Message}");
                    // 오디오 실패해도 비디오는 계속 진행
                }
            }

            // === 실시간 인코딩: FFmpeg 프로세스 시작 ===
            _useRealTimeEncoding = FFmpegDownloader.IsFFmpegInstalled();
            if (_useRealTimeEncoding)
            {
                try
                {
                    StartFFmpegProcess();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FFmpeg process start failed: {ex.Message}");
                    _useRealTimeEncoding = false;  // 폴백: 메모리 저장 방식
                }
            }

            // 캡처 루프 시작
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
            
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// FFmpeg 실시간 인코딩 프로세스 시작
        /// </summary>
        private void StartFFmpegProcess()
        {
            string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath))
                throw new Exception("FFmpeg not found");
            
            // 임시 비디오 파일 경로 (오디오 없는 버전)
            string tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "temp");
            if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);
            
            _tempVideoPath = Path.Combine(tempFolder, $"rec_video_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.mp4");
            
            // 화질 설정
            string crfValue = _settings.Quality switch
            {
                RecordingQuality.High => "18",
                RecordingQuality.Medium => "26",
                RecordingQuality.Low => "32",
                _ => "23"
            };
            
            // FFmpeg 명령어 준비 (stdin에서 rawvideo 입력 받음)
            string fpsStr = _settings.FrameRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string arguments = $"-y -f rawvideo -pixel_format bgr24 -video_size {_captureWidth}x{_captureHeight} " +
                               $"-framerate {fpsStr} -i pipe:0 " +
                               $"-c:v libx264 -preset ultrafast -tune zerolatency -crf {crfValue} " +
                               $"-pix_fmt yuvj420p -x264-params fullrange=on " +
                               $"-movflags +faststart \"{_tempVideoPath}\"";
            
            Debug.WriteLine($"[FFmpeg RealTime] Starting: {arguments}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            _ffmpegProcess = Process.Start(startInfo);
            if (_ffmpegProcess == null)
                throw new Exception("Failed to start FFmpeg process");
            
            _ffmpegStdin = _ffmpegProcess.StandardInput.BaseStream;
            
            // FFmpeg 로그 출력 (비동기)
            _ffmpegProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Debug.WriteLine($"[FFmpeg] {e.Data}");
            };
            _ffmpegProcess.BeginErrorReadLine();
            
            // 프레임 큐 초기화 및 쓰기 태스크 시작
            _frameQueue = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
            _stopWriting = false;
            _ffmpegWriteTask = Task.Run(FFmpegWriteLoop);
        }
        
        /// <summary>
        /// FFmpeg stdin 쓰기 루프 (별도 스레드에서 실행)
        /// </summary>
        private void FFmpegWriteLoop()
        {
            try
            {
                while (!_stopWriting || (_frameQueue != null && !_frameQueue.IsEmpty))
                {
                    if (_frameQueue != null && _frameQueue.TryDequeue(out byte[]? frameData))
                    {
                        if (frameData != null && _ffmpegStdin != null)
                        {
                            try
                            {
                                _ffmpegStdin.Write(frameData, 0, frameData.Length);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[FFmpeg Write] Error: {ex.Message}");
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 큐가 비어있으면 잠시 대기
                        Thread.Sleep(1);
                    }
                }
                
                // 스트림 플러시
                try
                {
                    _ffmpegStdin?.Flush();
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FFmpeg WriteLoop] Fatal: {ex.Message}");
            }
        }

        private void StartAudioRecording()
        {
            string tempFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            if (!Directory.Exists(tempFolder)) tempFolder = Path.GetTempPath();
            
            _tempAudioPath = Path.Combine(tempFolder, $"rec_audio_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.wav");
            
            // 오디오 종료 신호 초기화
            _audioStopTcs = new TaskCompletionSource<bool>();
            
            _totalAudioBytes = 0;
            // _audioStartTime은 녹화 시작 시점(_recordingStartTime)과 동일하게 맞춤
            // 그러나 여기서는 StartAudioRecording이 StartRecording 내부에서 불리므로
            // _recordingStartTime이 이미 설정되어 있음.
            _audioStartTime = _recordingStartTime;

            // 시스템 사운드 (Loopback)
            // 주의: 소리가 나지 않으면 DataAvailable이 발생하지 않아 파일이 생성되지 않을 수 있음
            _audioCapture = new WasapiLoopbackCapture();
            _audioWriter = new WaveFileWriter(_tempAudioPath, _audioCapture.WaveFormat);
            
            _audioCapture.DataAvailable += (s, e) =>
            {
                if (_audioWriter != null && !_isPaused)
                {
                    // 볼륨 증폭 (소리가 너무 작다는 피드백 반영)
                    // WASAPI Loopback은 기본적으로 32-bit IEEE Float 형식을 사용함
                    if (_audioCapture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        // 7.0배 증폭 (추가 증폭)
                        float gain = 7.0f; 
                        var buffer = e.Buffer;
                        int bytesRecorded = e.BytesRecorded;
                        
                        // 4바이트(float) 단위로 처리
                        for (int i = 0; i < bytesRecorded; i += 4)
                        {
                            // 바이트 -> float 변환
                            float sample = BitConverter.ToSingle(buffer, i);
                            
                            // 증폭
                            sample *= gain;
                            
                            // 클리핑 방지 (최대 볼륨 초과 시 잡음 방지)
                            if (sample > 1.0f) sample = 1.0f;
                            else if (sample < -1.0f) sample = -1.0f;
                            
                            // float -> 바이트 다시 채우기
                            byte[] bytes = BitConverter.GetBytes(sample);
                            buffer[i] = bytes[0];
                            buffer[i + 1] = bytes[1];
                            buffer[i + 2] = bytes[2];
                            buffer[i + 3] = bytes[3];
                        }
                    }
                    

                    
                    // === 싱크 보정 (묵음 구간 채우기) ===
                    // 녹화 시작 후 지금까지 흘러간 시간만큼 오디오 데이터가 쌓여야 싱크가 맞음
                    // WasapiLoopback은 소리가 날 때만 이벤트를 발생시키므로, 소리가 없는 구간(시작 전이나 중간)은 건너뛰어짐
                    // -> 부족한 바이트만큼 0(묵음)으로 채워넣음
                    
                    if (_totalAudioBytes == 0)
                    {
                         // 첫 패킷일 경우, 시작 시간(_recordingStartTime) 기준 보정
                         _audioStartTime = _recordingStartTime; // 녹화 시작 버튼 누른 시점
                    }

                    var elapsed = DateTime.Now - _audioStartTime;
                    long expectedBytes = (long)(elapsed.TotalSeconds * _audioCapture.WaveFormat.AverageBytesPerSecond);
                    long missingBytes = expectedBytes - _totalAudioBytes - e.BytesRecorded; // 이번에 쓸 것까지 고려해서 차이 계산? 
                                                                                            // 아니, _totalAudioBytes는 '이미 쓴 것'.
                                                                                            // expectedBytes에는 '지금 이 시점까지 있어야 할 전체 양'.
                                                                                            // 이번 패킷(e.BytesRecorded)은 곧 쓸 거니까 빼고 빈틈 계산해야 함?
                                                                                            // -> (현재시각 - 시작시각)에 해당하는 양이 Total이어야 함.
                                                                                            //    현재 Total + 이번패킷 < Expected 이면 그 차이가 묵음.
                    
                    // 좀 더 간단히:
                    // 현재 시점까지 기록되어야 할 총 바이트 수 = expectedBytes
                    // 현재까지 기록된 바이트 수 = _totalAudioBytes
                    // 이번에 들어온 바이트 수 = e.BytesRecorded
                    // 채워야 할 묵음 = expectedBytes - (_totalAudioBytes + e.BytesRecorded)
                    
                    long gapBytes = expectedBytes - (_totalAudioBytes + e.BytesRecorded);
                    
                    // 100ms 이상 차이나면 보정 (너무 작은 갭은 무시)
                    long thresholdBytes = (_audioCapture.WaveFormat.AverageBytesPerSecond / 10); 
                    
                    if (gapBytes > thresholdBytes)
                    {
                        // 갭이 너무 크면(예: 절전모드 등) 적당히 자르거나, 최대 10초까지만 채운다거나 하는 안전장가 있으면 좋지만
                        // 일단은 그대로 채움 (정확한 싱크를 위해)
                        
                        // 바이트 정렬 (BlockAlign 단위)
                        gapBytes -= (gapBytes % _audioCapture.WaveFormat.BlockAlign);
                        
                        if (gapBytes > 0)
                        {
                            // 묵음 버퍼 생성 및 쓰기
                            // 메모리 절약을 위해 나누어 쓰기
                            byte[] silenceBuffer = new byte[Math.Min(gapBytes, 1024 * 1024)]; // 최대 1MB 청크
                            Array.Clear(silenceBuffer, 0, silenceBuffer.Length);
                            
                            while (gapBytes > 0)
                            {
                                int toMinWrite = (int)Math.Min(gapBytes, silenceBuffer.Length);
                                _audioWriter.Write(silenceBuffer, 0, toMinWrite);
                                _totalAudioBytes += toMinWrite;
                                gapBytes -= toMinWrite;
                            }
                        }
                    }
                    // === 싱크 보정 끝 ===

                    _audioWriter.Write(e.Buffer, 0, e.BytesRecorded);
                    _totalAudioBytes += e.BytesRecorded;
                }
            };
            
            _audioCapture.RecordingStopped += (s, e) =>
            {
                try
                {
                    _audioWriter?.Flush();
                    _audioWriter?.Dispose();
                    _audioWriter = null;
                    _audioCapture?.Dispose();
                    _audioCapture = null;
                }
                catch { }
                finally
                {
                    // 종료 완료 신호
                    _audioStopTcs?.TrySetResult(true);
                }
            };
            
            _audioCapture.StartRecording();
        }
        
        /// <summary>
        /// 녹화 일시정지/재개
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            
            if (_isPaused)
            {
                // 일시정지 시작 시간 기록
                _pauseStartTime = DateTime.Now;
            }
            else
            {
                // 재개 시: 멈춰있던 시간만큼 StartTime을 뒤로 미룸 (마치 멈춘 적 없는 것처럼 시간 연속성 보장)
                TimeSpan pauseDuration = DateTime.Now - _pauseStartTime;
                _recordingStartTime += pauseDuration;
                _audioStartTime += pauseDuration; // 오디오 기준점도 이동
            }
        }
        
        /// <summary>
        /// 녹화 중지 (저장하지 않음)
        /// </summary>
        public async Task StopRecording()
        {
            if (!_isRecording) return;
            
            // 일시정지 상태에서 정지하면, 멈춘 시간만큼 보정 후 종료
            if (_isPaused)
            {
                TimeSpan pauseDuration = DateTime.Now - _pauseStartTime;
                _recordingStartTime += pauseDuration;
                _audioStartTime += pauseDuration;
                _isPaused = false;
            }
            
            _isRecording = false;
            _actualDuration = DateTime.Now - _recordingStartTime;
            _cts?.Cancel();
            
            // 캡처 루프 종료 대기
            if (_captureTask != null)
            {
                try
                {
                    await _captureTask;
                }
                catch { }
            }

            // === 실시간 인코딩: FFmpeg 프로세스 종료 ===
            if (_useRealTimeEncoding && _ffmpegProcess != null)
            {
                try
                {
                    // 쓰기 태스크 종료 신호
                    _stopWriting = true;
                    
                    // 쓰기 태스크 완료 대기 (모든 큐 소진)
                    if (_ffmpegWriteTask != null)
                    {
                        await Task.WhenAny(_ffmpegWriteTask, Task.Delay(10000));
                    }
                    
                    // stdin을 닫아서 FFmpeg에게 입력 종료 알림
                    _ffmpegStdin?.Close();
                    _ffmpegStdin = null;
                    
                    // FFmpeg가 인코딩 완료할 때까지 대기 (최대 30초)
                    bool exited = _ffmpegProcess.WaitForExit(30000);
                    if (!exited)
                    {
                        Debug.WriteLine("[FFmpeg] Process did not exit in time, killing...");
                        _ffmpegProcess.Kill();
                    }
                    
                    Debug.WriteLine($"[FFmpeg] Exit code: {_ffmpegProcess.ExitCode}");
                    
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                    
                    // 큐 정리
                    _frameQueue = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FFmpeg] Error stopping process: {ex.Message}");
                }
            }

            // 오디오 중지 및 파일 닫기 대기
            if (_audioCapture != null)
            {
                _audioCapture.StopRecording();
                
                // 오디오 파일이 완전히 닫힐 때까지 대기 (최대 5초)
                if (_audioStopTcs != null)
                {
                    await Task.WhenAny(_audioStopTcs.Task, Task.Delay(5000));
                }
                
                // 추가 대기 (파일 시스템 플러시)
                await Task.Delay(500);
            }
            
            // 오디오 파일 존재 여부 확인
            Debug.WriteLine($"[Audio] Temp file exists: {File.Exists(_tempAudioPath)}, Size: {(File.Exists(_tempAudioPath ?? "") ? new FileInfo(_tempAudioPath!).Length : 0)} bytes");
            
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
                
                // === 실시간 인코딩 사용 여부 확인 ===
                bool hasRealTimeVideo = !string.IsNullOrEmpty(_tempVideoPath) && File.Exists(_tempVideoPath);
                
                if (_settings.Format == RecordingFormat.GIF)
                {
                    if (_frames.Count > 0)
                    {
                        savedPath = await SaveAsGifAsync(outputPath);
                    }
                    else if (hasRealTimeVideo)
                    {
                        // 실시간 인코딩된 MP4를 GIF로 변환
                        savedPath = await ConvertMP4ToGifAsync(_tempVideoPath!, outputPath);
                    }
                }
                else if (_settings.Format == RecordingFormat.MP4)
                {
                    if (hasRealTimeVideo)
                    {
                        // 실시간 인코딩 완료된 비디오 사용
                        savedPath = await MergeVideoAudioAsync(_tempVideoPath!, outputPath);
                    }
                    else if (_frames.Count > 0)
                    {
                        // 폴백: 메모리에 저장된 프레임으로 인코딩
                        savedPath = await SaveAsMP4Async(outputPath);
                    }
                }
                
                if (string.IsNullOrEmpty(savedPath))
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                // 임시 비디오 파일 정리
                CleanupTempFiles();
            }
            
            return savedPath;
        }
        
        /// <summary>
        /// 실시간 인코딩된 비디오와 오디오 합성
        /// </summary>
        private async Task<string> MergeVideoAudioAsync(string videoPath, string outputPath)
        {
            string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
            
            // 오디오 파일 확인
            bool hasAudio = !string.IsNullOrEmpty(_tempAudioPath) && 
                           File.Exists(_tempAudioPath) && 
                           new FileInfo(_tempAudioPath).Length > 1024;
            
            if (!hasAudio)
            {
                // 오디오 없으면 비디오만 복사
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(videoPath, outputPath);
                Debug.WriteLine($"[RealTime] No audio, moved video directly: {outputPath}");
                return outputPath;
            }
            
            // 오디오와 비디오 합성
            Debug.WriteLine($"[RealTime] Merging video and audio...");
            
            string arguments = $"-y -i \"{videoPath}\" -i \"{_tempAudioPath}\" " +
                              "-c:v copy -c:a aac -b:a 192k -shortest " +
                              $"-movflags +faststart \"{outputPath}\"";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) throw new Exception("Failed to start FFmpeg for merging");
            
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit(60000));
            
            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                Debug.WriteLine($"[RealTime] Merge complete: {outputPath}");
                return outputPath;
            }
            else
            {
                // 합성 실패 시 비디오만 사용
                Debug.WriteLine($"[RealTime] Merge failed, using video only");
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(videoPath, outputPath);
                return outputPath;
            }
        }
        
        /// <summary>
        /// MP4를 GIF로 변환
        /// </summary>
        private async Task<string> ConvertMP4ToGifAsync(string videoPath, string outputPath)
        {
            string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
            string gifPath = Path.ChangeExtension(outputPath, ".gif");
            
            // 팔레트 생성 후 GIF 변환 (고품질)
            string paletteTemp = Path.Combine(Path.GetTempPath(), $"palette_{Guid.NewGuid()}.png");
            
            try
            {
                // 팔레트 생성
                var paletteProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -i \"{videoPath}\" -vf \"fps=15,palettegen\" \"{paletteTemp}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                paletteProcess?.WaitForExit(30000);
                
                // GIF 생성
                var gifProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -i \"{videoPath}\" -i \"{paletteTemp}\" -lavfi \"fps=15 [x]; [x][1:v] paletteuse\" \"{gifPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                await Task.Run(() => gifProcess?.WaitForExit(60000));
                
                return File.Exists(gifPath) ? gifPath : string.Empty;
            }
            finally
            {
                if (File.Exists(paletteTemp)) File.Delete(paletteTemp);
            }
        }
        
        /// <summary>
        /// 임시 파일 정리
        /// </summary>
        private void CleanupTempFiles()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempVideoPath) && File.Exists(_tempVideoPath))
                {
                    File.Delete(_tempVideoPath);
                    _tempVideoPath = null;
                }
                
                if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
                {
                    File.Delete(_tempAudioPath);
                    _tempAudioPath = null;
                }
            }
            catch { }
        }
        
        /// <summary>
        /// 첫 프레임을 썸네일로 반환 (Raw 데이터에서 생성)
        /// </summary>
        public BitmapSource? GetThumbnail()
        {
            // _firstFrame이 있으면 사용, 없으면 _frames[0] 사용
            byte[]? frameData = _firstFrame ?? (_frames.Count > 0 ? _frames[0] : null);
            if (frameData == null || _captureWidth <= 0 || _captureHeight <= 0) return null;
            
            try
            {
                // Raw BGR24 데이터에서 Bitmap 생성
                using var bitmap = new Bitmap(_captureWidth, _captureHeight, PixelFormat.Format24bppRgb);
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, _captureWidth, _captureHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
                
                try
                {
                    int rowBytes = _captureWidth * 3;
                    for (int y = 0; y < _captureHeight; y++)
                    {
                        IntPtr destPtr = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                        Marshal.Copy(frameData, y * rowBytes, destPtr, rowBytes);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
                
                // PNG로 변환 후 WPF BitmapImage로 로드
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = ms;
                bitmapImage.DecodePixelWidth = 200;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 캡처 루프 (고정밀 타이밍 최적화)
        /// </summary>
        private async Task CaptureLoop(CancellationToken token)
        {
            double frameIntervalMs = 1000.0 / _settings.FrameRate;
            long frameIntervalTicks = (long)(frameIntervalMs * Stopwatch.Frequency / 1000.0);
            
            _frameStopwatch.Restart();
            long nextFrameTicks = _frameStopwatch.ElapsedTicks;
            
            while (!token.IsCancellationRequested && _isRecording)
            {
                if (_isPaused)
                {
                    await Task.Delay(100, token);
                    // 일시정지 해제 후 타이밍 리셋
                    nextFrameTicks = _frameStopwatch.ElapsedTicks;
                    continue;
                }
                
                try
                {
                    // 화면 캡처
                    var frameData = CaptureScreen();
                    
                    if (frameData != null)
                    {
                        // 첫 프레임 저장 (썸네일용)
                        if (_firstFrame == null)
                        {
                            _firstFrame = frameData;
                        }
                        
                        // === 실시간 인코딩: 프레임 큐에 추가 (비동기 쓰기) ===
                        if (_useRealTimeEncoding && _frameQueue != null)
                        {
                            _frameQueue.Enqueue(frameData);
                        }
                        else
                        {
                            // 폴백: 메모리에 프레임 저장
                            _frames.Add(frameData);
                        }
                        
                        _frameDelays.Add((int)frameIntervalMs);
                        _frameCount++;
                        
                        // 통계 업데이트 (5프레임마다 - UI 오버헤드 감소)
                        if (_frameCount % 5 == 0)
                        {
                            var stats = new RecordingStats
                            {
                                FrameCount = _frameCount,
                                Duration = DateTime.Now - _recordingStartTime,
                                CurrentFps = CalculateCurrentFps(),
                                FileSizeEstimate = EstimateFileSize()
                            };
                            
                            // UI 스레드에서 이벤트 발생 (비동기로 변경하여 블로킹 방지)
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                StatsUpdated?.Invoke(this, stats);
                            });
                        }
                    }
                    
                    // 다음 프레임 시간 계산
                    nextFrameTicks += frameIntervalTicks;
                    
                    // 고정밀 대기 (SpinWait + Sleep 조합)
                    long remainingTicks = nextFrameTicks - _frameStopwatch.ElapsedTicks;
                    if (remainingTicks > 0)
                    {
                        double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
                        
                        // 2ms 이상 남으면 Thread.Sleep으로 CPU 절약
                        if (remainingMs > 2)
                        {
                            Thread.Sleep((int)(remainingMs - 1));
                        }
                        
                        // 나머지는 SpinWait으로 정밀 대기
                        while (_frameStopwatch.ElapsedTicks < nextFrameTicks)
                        {
                            Thread.SpinWait(10);
                        }
                    }
                    else
                    {
                        // 프레임 드롭 - 다음 타이밍으로 스킵
                        nextFrameTicks = _frameStopwatch.ElapsedTicks;
                    }
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
            
            _frameStopwatch.Stop();
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public Int32 x; public Int32 y; }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO 
        { 
            public Int32 cbSize; 
            public Int32 flags; 
            public IntPtr hCursor; 
            public POINT ptScreenPos; 
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        const Int32 CURSOR_SHOWING = 0x00000001;

        /// <summary>
        /// 화면 캡처 (GDI+ & 커서 포함)
        /// </summary>
        private byte[]? CaptureScreen()
        {
            try
            {
                // FFmpeg에 전달한 것과 동일한 크기 사용 (짝수 보정된 값)
                int width = _captureWidth;
                int height = _captureHeight;
                
                if (width <= 0 || height <= 0) return null;
                
                using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(
                    (int)_captureArea.X,
                    (int)_captureArea.Y,
                    0, 0,
                    new System.Drawing.Size(width, height),
                    CopyPixelOperation.SourceCopy);
                
                // 마우스 커서 그리기
                if (_settings.ShowMouseEffects)
                {
                    try
                    {
                    CURSORINFO pci;
                    pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                    if (GetCursorInfo(out pci))
                    {
                        if (pci.flags == CURSOR_SHOWING)
                        {
                            // 캡처 영역 기준 상대 좌표 계산
                            int cursorX = pci.ptScreenPos.x - (int)_captureArea.X;
                            int cursorY = pci.ptScreenPos.y - (int)_captureArea.Y;

                            // 커서가 캡처 영역 내에 있을 때만 그리기 (옵션)
                            // DrawIcon API 사용 (가장 간단하고 확실한 방법)
                            var hdc = graphics.GetHdc();
                            try 
                            {
                                DrawIcon(hdc, cursorX, cursorY, pci.hCursor);
                            }
                            finally 
                            { 
                                graphics.ReleaseHdc(hdc); 
                            }
                        }
                    }
                }
                catch { /* 커서 캡처 실패 시 무시 */ }
                }
                
                // Raw 픽셀 데이터로 저장 (PNG 압축 제거로 CPU 부하 감소)
                // 저장 시 FFmpeg에서 직접 변환하므로 Raw 형태로 저장
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);
                
                try
                {
                    int stride = bitmapData.Stride;
                    int rowBytes = width * 3;
                    byte[] rawData = new byte[rowBytes * height];
                    
                    // Stride 제거하여 연속 메모리로 복사
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr srcPtr = IntPtr.Add(bitmapData.Scan0, y * stride);
                        Marshal.Copy(srcPtr, rawData, y * rowBytes, rowBytes);
                    }
                    
                    return rawData;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
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

            // 1. FFmpeg가 설치되어 있다면 고성능/고화질 변환 사용
            if (FFmpegDownloader.IsFFmpegInstalled())
            {
                string tempMp4Path = Path.ChangeExtension(outputPath, ".temp.mp4");
                try
                {
                    // 임시 MP4 생성 (기존 로직 활용하여 고속 생성)
                    // SaveAsMP4Async는 내부적으로 raw_data 등을 사용하여 빠르게 비디오를 만듦
                    await SaveAsMP4Async(tempMp4Path);

                    if (File.Exists(tempMp4Path))
                    {
                        string ffmpegPath = FFmpegDownloader.GetFFmpegPath();
                        
                        // 화질별 FFmpeg GIF 변환 필터 설정
                        // - palettegen/paletteuse: 256색 팔레트를 동적으로 최적화하여 '떨림' 잡음 제거 및 화질 향상
                        // - fps: 프레임 수 조절로 용량 관리
                        // - scale: 해상도 조절
                        
                        string scaleStr = "iw";
                        string fpsStr = "15"; // 기본 15fps
                        string paletteGenOpt = ""; 
                        string paletteUseOpt = "";

                        switch (_settings.Quality)
                        {
                            case RecordingQuality.High:
                                scaleStr = "iw"; // 100%
                                fpsStr = Math.Min(_settings.FrameRate, 30).ToString();
                                paletteGenOpt = "stats_mode=diff"; // 움직임 최적화
                                paletteUseOpt = "dither=sierra2_4a"; // 고품질 디더링
                                break;
                                
                            case RecordingQuality.Medium:
                                scaleStr = "iw*0.8"; // 80%
                                fpsStr = Math.Min(_settings.FrameRate, 15).ToString();
                                paletteGenOpt = "max_colors=128:stats_mode=diff"; // 128색
                                paletteUseOpt = "dither=bayer:bayer_scale=3"; // 패턴 디더링 (용량 절약)
                                break;
                                
                            case RecordingQuality.Low:
                                scaleStr = "iw*0.6"; // 60%
                                fpsStr = "10"; // 10fps 고정
                                paletteGenOpt = "max_colors=64:stats_mode=diff"; // 64색
                                paletteUseOpt = "dither=none"; // 디더링 없음 (가장 깔끔, 등고선 발생 가능하지만 용량/속도 최강)
                                break;
                        }

                        // 복합 필터 구성
                        string vf = $"fps={fpsStr},scale={scaleStr}:-1:flags=lanczos,split[s0][s1];[s0]palettegen={paletteGenOpt}[p];[s1][p]paletteuse={paletteUseOpt}";

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = ffmpegPath,
                            Arguments = $"-y -i \"{tempMp4Path}\" -vf \"{vf}\" \"{outputPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null) await process.WaitForExitAsync();
                        }
                        
                        return outputPath;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FFmpeg GIF Failed: {ex.Message}");
                    // 실패 시 아래 C# 코드로 폴백
                }
                finally
                {
                    if (File.Exists(tempMp4Path)) File.Delete(tempMp4Path);
                }
            }
            
            // 2. Fallback: FFmpeg 없을 때 C# 내장 인코더 사용 (Raw 데이터에서 변환)
            await Task.Run(() =>
            {
                using var stream = new FileStream(outputPath, FileMode.Create);
                
                // GIF 헤더 작성
                var encoder = new AnimatedGifEncoder();
                encoder.Start(stream);
                encoder.SetDelay(1000 / _settings.FrameRate);
                encoder.SetRepeat(0); // 무한 반복
                
                // 품질 설정
                int qualityParam = _settings.Quality == RecordingQuality.Low ? 20 : 10;
                encoder.SetQuality(qualityParam);
                
                int rowBytes = _captureWidth * 3;
                
                foreach (var frameData in _frames)
                {
                    // Raw BGR24 데이터에서 Bitmap 복원
                    using var bitmap = new Bitmap(_captureWidth, _captureHeight, PixelFormat.Format24bppRgb);
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, _captureWidth, _captureHeight),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format24bppRgb);
                    
                    try
                    {
                        for (int y = 0; y < _captureHeight; y++)
                        {
                            IntPtr destPtr = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                            Marshal.Copy(frameData, y * rowBytes, destPtr, rowBytes);
                        }
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }
                    
                    // 화질 설정에 따른 리사이징 (용량 감소 핵심)
                    double scale = _settings.Quality switch
                    {
                        RecordingQuality.High => 1.0,      // 원본
                        RecordingQuality.Medium => 0.8,    // 80%
                        RecordingQuality.Low => 0.6,       // 60%
                        _ => 0.8
                    };
                    
                    if (scale < 0.99)
                    {
                        int newW = (int)(bitmap.Width * scale);
                        int newH = (int)(bitmap.Height * scale);
                        using var resized = new System.Drawing.Bitmap(newW, newH);
                        using (var g = System.Drawing.Graphics.FromImage(resized))
                        {
                            // 품질은 유지하되 크기를 줄임
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(bitmap, 0, 0, newW, newH);
                        }
                        encoder.AddFrame(resized);
                    }
                    else
                    {
                        encoder.AddFrame(bitmap);
                    }
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
                
                // 실제 FPS 계산 (15초 녹화 -> 9초 재생 문제 수정)
                double actualFps = _settings.FrameRate;
                if (_actualDuration.TotalSeconds > 0 && _frames.Count > 0)
                {
                    actualFps = (double)_frames.Count / _actualDuration.TotalSeconds;
                    // 최소/최대 FPS 보정 (너무 느리거나 빠르지 않게)
                    if (actualFps < 1) actualFps = 1;
                    if (actualFps > 60) actualFps = 60;
                    
                    Log($"Actual Duration: {_actualDuration.TotalSeconds:F2}s, Frames: {_frames.Count}, Actual FPS: {actualFps:F2}");
                }

                // 1. Raw 데이터 파일 생성 (프레임은 이미 Raw BGR24 형식으로 저장되어 있음)
                await Task.Run(() =>
                {
                    using (var fs = new FileStream(rawDataPath, FileMode.Create, FileAccess.Write))
                    {
                        int rowBytes = width * 3;
                        int expectedFrameSize = rowBytes * height;
                        
                        for (int i = 0; i < _frames.Count; i++)
                        {
                            try
                            {
                                byte[] frameData = _frames[i];
                                
                                // 프레임 크기가 맞는지 확인
                                if (frameData.Length == expectedFrameSize)
                                {
                                    // Raw 데이터를 직접 파일에 쓰기
                                    fs.Write(frameData, 0, frameData.Length);
                                }
                                else
                                {
                                    // 크기가 다르면 원본 캡처 크기로 복원 후 조정
                                    int origRowBytes = _captureWidth * 3;
                                    
                                    if (frameData.Length == origRowBytes * _captureHeight)
                                    {
                                        // 크기 조정 필요 (짝수 보정으로 인한 차이)
                                        for (int y = 0; y < height; y++)
                                        {
                                            fs.Write(frameData, y * origRowBytes, rowBytes);
                                        }
                                    }
                                    else
                                    {
                                        Log($"Frame {i} size mismatch: expected {expectedFrameSize}, got {frameData.Length}");
                                    }
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
                    // 비디오 인자
                    // -framerate를 실제 계산된 값으로 넣어 재생 속도를 맞춤
                    string actualFpsStr = actualFps.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    string videoInput = $"-f rawvideo -pixel_format bgr24 -video_size {width}x{height} -framerate {actualFpsStr} -i \"{rawDataPath}\"";
                    
                    // 화질 설정 (CRF 및 해상도 조절)
                    string crfValue = "23";
                    string vfOption = ""; // 비디오 필터 (해상도 조절 + 색상 범위 보존)
                    
                    // 색상 범위 보존 필터 (full range 유지)
                    // in_range=full:out_range=full - BGR24(full) -> YUV420P(full) 변환 시 색상 손실 방지
                    string colorRangeFilter = "in_range=full:out_range=full";
                    
                    switch (_settings.Quality)
                    {
                        case RecordingQuality.High:
                            // HD: 원본 해상도, 고화질 (CRF 18)
                            crfValue = "18";
                            vfOption = $"-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2:{colorRangeFilter}\""; 
                            break;
                            
                        case RecordingQuality.Medium:
                            // SD: 75% 해상도, 중화질 (CRF 26)
                            crfValue = "26";
                            string scale75 = (0.75).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            vfOption = $"-vf \"scale=trunc(iw*{scale75}/2)*2:trunc(ih*{scale75}/2)*2:{colorRangeFilter}\"";
                            break;
                            
                        case RecordingQuality.Low:
                            // LD: 50% 해상도, 저화질 (CRF 32)
                            crfValue = "32";
                            string scale50 = (0.5).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            vfOption = $"-vf \"scale=trunc(iw*{scale50}/2)*2:trunc(ih*{scale50}/2)*2:{colorRangeFilter}\"";
                            break;
                    }

                    // 오디오 인자
                    string audioInput = "";
                    string audioMap = "";
                    
                    Log($"Checking audio file: {_tempAudioPath}");
                    Log($"Audio enabled in settings: {_settings.RecordAudio}");
                    
                    if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
                    {
                        var audioInfo = new FileInfo(_tempAudioPath);
                        Log($"Audio File Found. Size: {audioInfo.Length} bytes");
                        
                        // WAV 헤더는 약 44 bytes. 최소 1KB 이상이면 유효한 오디오로 간주
                        if (audioInfo.Length > 1024) 
                        {
                            audioInput = $"-i \"{_tempAudioPath}\"";
                            // 비디오(0)와 오디오(1)를 모두 포함, -shortest로 더 짧은 쪽에 맞춤
                            audioMap = "-map 0:v -map 1:a -c:a aac -b:a 192k -shortest"; 
                            Log("Audio will be included in output.");
                        }
                        else
                        {
                             Log($"Audio file is too small ({audioInfo.Length} bytes). Only WAV header? Skipping audio.");
                        }
                    }
                    else
                    {
                        Log($"Audio file not found at: {_tempAudioPath}");
                        Log("No audio recording found or audio feature disabled.");
                    }
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        // 멀티스레드 + 빠른 인코딩 최적화
                        // -threads 0: 자동으로 최대 스레드 사용
                        // -tune zerolatency: 빠른 인코딩 최적화
                        // -movflags +faststart: 웹 재생 최적화
                        // yuvj420p: Full Range YUV420P (JPEG 색상 범위)
                        // -x264-params fullrange=on: x264 인코더에 full range 명시
                        Arguments = $"-y {videoInput} {audioInput} -threads 0 {vfOption} -c:v libx264 -preset ultrafast -tune zerolatency -crf {crfValue} -pix_fmt yuvj420p -x264-params fullrange=on -movflags +faststart {audioMap} \"{tempMp4Path}\"",
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
                    CatchCapture.CustomMessageBox.Show($"녹화 실패: {ex.Message}\n로그: {logPath}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return string.Empty;
            }
            finally
            {
                // 정리
                try { if (File.Exists(rawDataPath)) File.Delete(rawDataPath); } catch { }
                try { if (File.Exists(tempMp4Path)) File.Delete(tempMp4Path); } catch { }
                
                // 오디오 임시 파일도 여기서 정리 (FFmpeg 사용 완료 후)
                if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
                {
                    try { File.Delete(_tempAudioPath); Log("Audio temp file deleted."); } catch { }
                }
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
            
            // 오디오 정리
            _audioWriter?.Dispose();
            _audioCapture?.Dispose();
            if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
            {
                try { File.Delete(_tempAudioPath); } catch { }
            }
            
            // Windows Timer Resolution 복원
            if (_timerResolutionSet)
            {
                try { TimeEndPeriod(1); } catch { }
            }
            
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

