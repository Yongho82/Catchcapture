using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CatchCapture.Models;

namespace CatchCapture.Recording
{
    /// <summary>
    /// 녹화 도구 창 - 화면 녹화 제어 UI
    /// </summary>
    public partial class RecordingWindow : Window
    {
        // 녹화 설정
        private RecordingSettings _settings;
        
        // 녹화기
        private ScreenRecorder? _recorder;
        
        // 녹화 시간 타이머
        private DispatcherTimer _recordingTimer;
        private TimeSpan _recordingDuration;
        
        // 녹화 영역 오버레이 창
        private RecordingOverlay? _overlay;
        
        // 도킹 관련
        private bool _isDocked = false;
        private const double DOCK_THRESHOLD = 20;
        
        // 상태
        public bool IsRecording => _recorder?.IsRecording ?? false;
        
        public RecordingWindow()
        {
            InitializeComponent();
            
            _settings = new RecordingSettings();
            
            // 타이머 초기화
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += RecordingTimer_Tick;
            
            // UI 초기화
            UpdateUI();
            
            // 창 닫힐 때 정리
            Closed += RecordingWindow_Closed;
            
            // 키보드 단축키
            KeyDown += RecordingWindow_KeyDown;
            
            // 로드 시 오버레이 표시
            Loaded += RecordingWindow_Loaded;
        }
        
        private void RecordingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ShowOverlay();
            
            // 오버레이 상단 중앙에 도구 창 배치
            if (_overlay != null && _overlay.IsVisible)
            {
                var selectionArea = _overlay.SelectionArea;
                this.Left = selectionArea.Left + (selectionArea.Width - this.Width) / 2;
                this.Top = Math.Max(0, selectionArea.Top - this.Height - 10);
            }
            
            // 오버레이보다 위에 표시되도록 활성화
            this.Activate();
            this.Topmost = true;
        }
        
        /// <summary>
        /// 오버레이 표시
        /// </summary>
        private void ShowOverlay()
        {
            if (_overlay == null)
            {
                _overlay = new RecordingOverlay();
                _overlay.AreaChanged += Overlay_AreaChanged;
                
                // 마지막 위치 복원
                if (_settings.LastAreaWidth > 0 && _settings.LastAreaHeight > 0)
                {
                    _overlay.SelectionArea = new Rect(
                        _settings.LastAreaLeft,
                        _settings.LastAreaTop,
                        _settings.LastAreaWidth,
                        _settings.LastAreaHeight);
                }
                
                // ★ 핵심: 툴바가 오버레이 위에 항상 뜨도록 Owner 설정
                // 주의: WPF에서 Owner를 설정하려면 Owner가 먼저 Show() 되어야 할 수도 있음
                // 하지만 여기서는 순서상 오버레이를 먼저 띄우고 툴바를 그 위에 얹는 개념
                
                _overlay.Show();
                
                // 툴바를 오버레이의 'Owned Window'로 설정하면 툴바가 항상 오버레이 위에 뜸
                this.Owner = _overlay;
            }
            else
            {
                if (!_overlay.IsVisible)
                {
                    _overlay.Show();
                }
            }
        }
        
        #region 이벤트 핸들러
        
        /// <summary>
        /// 키보드 단축키 처리
        /// </summary>
        private void RecordingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F9:
                    // F9: 녹화 시작/정지
                    RecordButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Escape:
                    // ESC: 녹화 중지 및 창 닫기
                    if (IsRecording)
                    {
                        StopRecordingAsync();
                    }
                    else
                    {
                        Close();
                    }
                    e.Handled = true;
                    break;
            }
        }
        
        /// <summary>
        /// 타이틀바 드래그 이동
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 더블클릭: 상단 도킹
                ToggleDock();
            }
            else
            {
                DragMove();
                
                // 드래그 완료 후 도킹 체크
                CheckDocking();
            }
        }
        
        /// <summary>
        /// 닫기 버튼
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording)
            {
                var result = MessageBox.Show(
                    "녹화가 진행 중입니다. 저장하시겠습니까?",
                    "녹화 중",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    StopRecordingAsync();
                }
                else if (result == MessageBoxResult.No)
                {
                    _recorder?.Dispose();
                    _overlay?.Close();
                    Close();
                }
                // Cancel: 아무것도 안 함
            }
            else
            {
                _overlay?.Close();
                Close();
            }
        }
        
        /// <summary>
        /// 녹화 시작/정지 버튼
        /// </summary>
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording)
            {
                StopRecordingAsync();
            }
            else
            {
                StartRecording();
            }
        }
        
        /// <summary>
        /// 오디오 버튼 클릭 (메뉴 열기)
        /// </summary>
        private void AudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            AudioButton.ContextMenu.IsOpen = true;
        }
        
        /// <summary>
        /// 오디오 메뉴 아이템 클릭
        /// </summary>
        private void AudioMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 메뉴 아이템 상태에 따라 설정 업데이트
            // 현재는 단순 UI 토글만 구현 (실제 오디오 녹음은 미구현)
            bool mic = MicMenuItem.IsChecked;
            bool sys = SystemSoundMenuItem.IsChecked;
            
            _settings.RecordAudio = mic || sys;
            
            UpdateUI();
        }
        
        /// <summary>
        /// 파일 형식 변경 (GIF <-> MP4)
        /// </summary>
        private void FormatButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            _settings.Format = _settings.Format == RecordingFormat.MP4 
                ? RecordingFormat.GIF 
                : RecordingFormat.MP4;
            
            UpdateUI();
        }
        
        /// <summary>
        /// 화질 변경 (고/중/저 순환)
        /// </summary>
        private void QualityButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            _settings.Quality = _settings.Quality switch
            {
                RecordingQuality.High => RecordingQuality.Medium,
                RecordingQuality.Medium => RecordingQuality.Low,
                RecordingQuality.Low => RecordingQuality.High,
                _ => RecordingQuality.Medium
            };
            
            UpdateUI();
        }
        
        /// <summary>
        /// 프레임 레이트 변경 (30 -> 60 -> 15 순환)
        /// </summary>
        private void FpsButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            _settings.FrameRate = _settings.FrameRate switch
            {
                30 => 60,
                60 => 15,
                15 => 30,
                _ => 30
            };
            
            UpdateUI();
        }
        
        /// <summary>
        /// 마우스 효과 토글
        /// </summary>
        private void MouseEffectToggle_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            _settings.ShowMouseEffects = MouseEffectToggle.IsChecked == true;
        }
        
        /// <summary>
        /// 타이머 변경 (0 -> 3 -> 5 -> 10 순환)
        /// </summary>
        private void TimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            _settings.CountdownSeconds = _settings.CountdownSeconds switch
            {
                0 => 3,
                3 => 5,
                5 => 10,
                10 => 0,
                _ => 0
            };
            
            UpdateUI();
        }
        
        /// <summary>
        /// 녹화 타이머 틱
        /// </summary>
        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            _recordingDuration = _recordingDuration.Add(TimeSpan.FromSeconds(1));
            RecordingTimeText.Text = _recordingDuration.ToString(@"mm\:ss");
        }
        
        /// <summary>
        /// 창 닫힐 때 정리
        /// </summary>
        private void RecordingWindow_Closed(object? sender, EventArgs e)
        {
            _recordingTimer.Stop();
            _recorder?.Dispose();
            
            if (_overlay != null)
            {
                _overlay.Close();
                _overlay = null;
            }
            
            Owner = null;
        }
        
        #endregion
        
        #region 녹화 제어
        
        /// <summary>
        /// 녹화 시작
        /// </summary>
        private async void StartRecording()
        {
            if (IsRecording) return;

            // === 여기부터 FFmpeg 자동 다운로드 (당신 기존 코드에 딱 맞춤) ===
            if (_settings.Format == RecordingFormat.MP4 && !FFmpegDownloader.IsFFmpegInstalled())
            {
                var result = MessageBox.Show(
                    "MP4 고화질 녹화를 위해 FFmpeg가 필요합니다.\n" +
                    "지금 자동으로 다운로드할까요? (약 80MB, 인터넷 필요, 1회만)\n\n" +
                    "취소하면 GIF로 녹화됩니다.",
                    "FFmpeg 다운로드 필요",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 당신이 이미 만든 그 아름다운 프로그레스 창 재사용
                    var progressWindow = new Window
                    {
                        Title = "FFmpeg 다운로드 중... (80MB)",
                        Width = 450, Height = 160,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize,
                        WindowStyle = WindowStyle.ToolWindow,
                        Topmost = true,
                        Owner = this
                    };

                    var pb = new System.Windows.Controls.ProgressBar { Height = 35, Margin = new Thickness(20, 20, 20, 10) };
                    var tb = new TextBlock { Text = "연결 중...", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 14 };
                    var sp = new StackPanel();
                    sp.Children.Add(tb);
                    sp.Children.Add(pb);
                    progressWindow.Content = sp;
                    progressWindow.Show();

                    var progress = new Progress<int>(p =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            pb.Value = p;
                            tb.Text = p < 100 ? $"다운로드 중... {p}%" : "설치 완료!";
                        });
                    });

                    bool success = await FFmpegDownloader.DownloadFFmpegAsync(progress);

                    progressWindow.Close();

                    if (!success)
                    {
                        MessageBox.Show("FFmpeg 다운로드 실패.\nGIF로 녹화됩니다.", "실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _settings.Format = RecordingFormat.GIF;
                        UpdateUI();
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    _settings.Format = RecordingFormat.GIF;
                    UpdateUI();
                }
                else return; // Cancel
            }
            // === FFmpeg 처리 끝 ===

            // 나머지는 당신 원래 코드 그대로
            if (_settings.CountdownSeconds > 0)
                await Task.Delay(_settings.CountdownSeconds * 1000);

            _overlay?.SetRecordingMode(true);

            _recorder = new ScreenRecorder(_settings);
            _recorder.StatsUpdated += Recorder_StatsUpdated;
            _recorder.RecordingStopped += Recorder_RecordingStopped;
            _recorder.ErrorOccurred += Recorder_ErrorOccurred;

            var captureArea = new Rect(
                _settings.LastAreaLeft, _settings.LastAreaTop,
                _settings.LastAreaWidth, _settings.LastAreaHeight);

            _recorder.StartRecording(captureArea);

            _recordingDuration = TimeSpan.Zero;
            _recordingTimer.Start();

            RecordButtonIcon.Visibility = Visibility.Collapsed;
            StopButtonIcon.Visibility = Visibility.Visible;

            EnableSettings(false);
            UpdateUI();
        }
        
        /// <summary>
        /// 녹화 정지 (비동기)
        /// </summary>
        private async void StopRecordingAsync()
        {
            if (!IsRecording) return;
            
            _recordingTimer.Stop();
            
            // 버튼 아이콘을 녹화(●)로 변경
            RecordButtonIcon.Visibility = Visibility.Visible;
            StopButtonIcon.Visibility = Visibility.Collapsed;
            
            // 녹화만 중지 (저장하지 않음)
            try
            {
                await _recorder!.StopRecording();
                
                if (_recorder.FrameCount > 0)
                {
                    // MainWindow의 캡처 리스트에 추가 (recorder 객체와 설정 전달)
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // 녹화 데이터를 MainWindow에 전달 (자동 저장 시작)
                        // 중요: 여기서 _recorder의 소유권을 MainWindow로 넘깁니다. 
                        // 따라서 이 창이 닫힐 때 Dispose() 하지 않도록 null로 설정해야 합니다.
                        mainWindow.AddVideoToList(_recorder, _settings);
                        _recorder = null; 
                    }
                    
                    // 녹화 도구 창 닫기
                    Close();
                }
                else
                {
                    MessageBox.Show("녹화된 프레임이 없어 저장할 수 없습니다.", "알림", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"녹화 중지 중 오류가 발생했습니다:\n{ex.Message}", "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            // 버튼 다시 활성화
            EnableSettings(true);
            
            UpdateUI();
        }
        
        /// <summary>
        /// 설정 버튼 활성화/비활성화
        /// </summary>
        private void EnableSettings(bool enable)
        {
            AudioButton.IsEnabled = enable;
            FormatButton.IsEnabled = enable;
            QualityButton.IsEnabled = enable;
            FpsButton.IsEnabled = enable;
            MouseEffectToggle.IsEnabled = enable;
            TimerButton.IsEnabled = enable;
        }
        
        /// <summary>
        /// 녹화 통계 업데이트
        /// </summary>
        private void Recorder_StatsUpdated(object? sender, RecordingStats stats)
        {
            // FPS 표시 (선택사항)
            // RecordingTimeText.Text = $"{stats.FormattedDuration} ({stats.CurrentFps:F1}fps)";
        }
        
        /// <summary>
        /// 녹화 완료
        /// </summary>
        private void Recorder_RecordingStopped(object? sender, string path)
        {
            // 이미 StopRecordingAsync에서 처리됨
        }
        
        /// <summary>
        /// 녹화 오류
        /// </summary>
        private void Recorder_ErrorOccurred(object? sender, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"녹화 중 오류가 발생했습니다:\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        
        #endregion
        
        #region UI 업데이트
        
        /// <summary>
        /// UI 상태 업데이트
        /// </summary>
        private void UpdateUI()
        {
            // 오디오 상태 (초록 점)
            AudioStatusDot.Visibility = _settings.RecordAudio ? Visibility.Visible : Visibility.Collapsed;
            MicMenuItem.IsChecked = _settings.RecordAudio; // 임시: 둘 중 하나라도 켜지면 켜진 걸로 간주
            
            // 파일 형식
            FormatText.Text = _settings.Format.ToString();
            
            // 화질
            QualityText.Text = _settings.Quality switch
            {
                RecordingQuality.High => "HD",
                RecordingQuality.Medium => "SD",
                RecordingQuality.Low => "LD",
                _ => "SD"
            };
            
            // 프레임
            FpsText.Text = $"{_settings.FrameRate}F";
            
            // 마우스 효과
            MouseEffectToggle.IsChecked = _settings.ShowMouseEffects;
            
            // 타이머
            TimerText.Text = _settings.CountdownSeconds > 0 ? _settings.CountdownSeconds.ToString() : "";
            TimerText.Visibility = _settings.CountdownSeconds > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // 영역 크기
            AreaSizeText.Text = $"{(int)_settings.LastAreaWidth}x{(int)_settings.LastAreaHeight}";
        }
        
        /// <summary>
        /// 영역 변경 시 호출
        /// </summary>
        private void Overlay_AreaChanged(object? sender, Rect area)
        {
            _settings.LastAreaLeft = area.Left;
            _settings.LastAreaTop = area.Top;
            _settings.LastAreaWidth = area.Width;
            _settings.LastAreaHeight = area.Height;
            
            UpdateUI();
        }
        
        #endregion
        
        #region 도킹
        
        /// <summary>
        /// 상단 가장자리 도킹 체크
        /// </summary>
        private void CheckDocking()
        {
            if (Top <= DOCK_THRESHOLD)
            {
                DockToTop();
            }
        }
        
        /// <summary>
        /// 상단 도킹
        /// </summary>
        private void DockToTop()
        {
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - Width) / 2;
            Top = 0;
            _isDocked = true;
        }
        
        /// <summary>
        /// 도킹 토글
        /// </summary>
        private void ToggleDock()
        {
            if (_isDocked)
            {
                // 도킹 해제
                Top = 100;
                _isDocked = false;
            }
            else
            {
                DockToTop();
            }
        }
        
        #endregion
    }
}
