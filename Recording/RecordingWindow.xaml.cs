using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using CatchCapture.Models;

namespace CatchCapture.Recording
{
    /// <summary>
    /// 녹화 도구 창 - 화면 녹화 제어 UI
    /// </summary>
    public partial class RecordingWindow : Window
    {

        
        // 설정
        private RecordingSettings _settings;
        private CatchCapture.Models.Settings _globalSettings;
        
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
        private DispatcherTimer _autoHideTimer;
        private bool _isHidden = false;
        private Rect? _savedArea = null; // 전체 화면 전 크기 저장용

        private bool _isAttachedToOverlay = true; // 도구상자가 영역을 따라다니는지 여부
        
        // 상태
        public bool IsRecording => _recorder?.IsRecording ?? false;
        
        public RecordingWindow()
        {
            InitializeComponent();
            
            _globalSettings = CatchCapture.Models.Settings.Load();
            _settings = _globalSettings.Recording;
            
            // 타이머 초기화
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += RecordingTimer_Tick;
            
            // 도킹 자동숨김 타이머
            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromSeconds(0.5);
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            

            
            // 이벤트 구독
            MouseEnter += RecordingWindow_MouseEnter;
            MouseLeave += RecordingWindow_MouseLeave;
            
            // UI 초기화
            UpdateUI();
            
            // 언어 변경 핸들러 등록
            CatchCapture.Resources.LocalizationManager.LanguageChanged += OnLanguageChanged;
            
            // 초기 UI 텍스트 설정
            UpdateUIText();
            
            // 창 닫힐 때 정리
            Closed += RecordingWindow_Closed;
            
            // 키보드 단축키
            KeyDown += RecordingWindow_KeyDown;
            
            // 로드 시 오버레이 표시
            Loaded += RecordingWindow_Loaded;
        }

        /// <summary>
        /// 녹화 중 툴팁 비활성화/활성화
        /// </summary>
        private void SetToolTipsEnabled(bool enabled)
        {
            // 윈도우 자체의 툴팁 서비스 비활성화
            ToolTipService.SetIsEnabled(this, enabled);
            
            // 모든 자식 요소를 재귀적으로 순회하며 툴팁 비활성화
            SetToolTipsEnabledRecursive(this, enabled);
        }

        private void SetToolTipsEnabledRecursive(DependencyObject parent, bool enabled)
        {
            if (parent == null) return;

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                // 각 자식 요소의 툴팁 서비스 설정
                if (child is FrameworkElement element)
                {
                    ToolTipService.SetIsEnabled(element, enabled);
                }
                
                // 재귀적으로 하위 요소 처리
                SetToolTipsEnabledRecursive(child, enabled);
            }
        }
        
        private void RecordingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 캡처에서 창 제외 (Windows 10 2004+)
            var interop = new WindowInteropHelper(this);
            SetWindowDisplayAffinity(interop.Handle, WDA_EXCLUDEFROMCAPTURE);

            // 먼저 주 모니터에 도구상자 배치
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                var bounds = primaryScreen.WorkingArea;
                this.Left = bounds.X + (bounds.Width - this.Width) / 2;
                this.Top = bounds.Y + 50;
            }
            
            // 도구상자가 배치된 후 오버레이 표시 (같은 모니터에)
            ShowOverlay();
            
            // MP3 모드가 아닐 때만 오버레이 기준으로 위치 재조정
            if (_settings.Format != RecordingFormat.MP3 && _overlay != null && _overlay.IsVisible)
            {
                UpdateToolboxPosition(_overlay.SelectionArea);
            }
            
            // 오버레이보다 위에 표시되도록 활성화
            this.Activate();
            this.Topmost = true;
            
            // 위치 잡은 후 보이게 하기
            this.Opacity = 1;

            // 글로벌 서비스 단축키 등록
            RegisterGlobalHotKey();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RecordingWindow_Loaded error: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    /// <summary>
    /// 오버레이 표시
    /// </summary>
    private void ShowOverlay()
    {
        // MP3 모드일 경우 오버레이 숨기기
        if (_settings.Format == RecordingFormat.MP3)
        {
            if (_overlay != null && _overlay.IsVisible)
            {
                _overlay.Hide();
            }
            return;
        }
        
        if (_overlay == null)
        {
            _overlay = new RecordingOverlay();
            _overlay.AreaChanged += Overlay_AreaChanged;
            _overlay.EscapePressed += Overlay_EscapePressed; // ESC 키 처리
            _overlay.AreaSelectionEnded += Overlay_AreaSelectionEnded; // 선택 모드 종료 처리
            
            _overlay.Show();
            
            // 주 모니터 중앙에 오버레이 배치
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                var workArea = primaryScreen.WorkingArea;
                
                // 물리적 좌표(pixels) -> 논리적 좌표(units) 변환
                Point logicalTopLeft = _overlay.PointFromScreen(new Point(workArea.X, workArea.Y));
                Point logicalBottomRight = _overlay.PointFromScreen(new Point(workArea.Right, workArea.Bottom));
                
                double w = logicalBottomRight.X - logicalTopLeft.X;
                double h = logicalBottomRight.Y - logicalTopLeft.Y;
                
                double overlayWidth = 800;
                double overlayHeight = 600;
                
                var selectionArea = new Rect(
                    logicalTopLeft.X + (w - overlayWidth) / 2,
                    logicalTopLeft.Y + (h - overlayHeight) / 2,
                    overlayWidth,
                    overlayHeight
                );
                _overlay.SelectionArea = selectionArea;
            }
            
            // Windows 11 멀티 모니터 호환성: Show() 직후 명시적으로 활성화
            _overlay.Topmost = true;
            _overlay.Activate();
            
            // 툴바를 오버레이의 'Owned Window'로 설정하면 툴바가 항상 오버레이 위에 뜸
            this.Owner = _overlay;
        }
        else
        {
            if (!_overlay.IsVisible)
            {
                _overlay.Show();
                _overlay.Topmost = true;
                _overlay.Activate();
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

        private const int HOTKEY_ID_REC_START_STOP = 9500;
        private IntPtr _hwnd;
        private HwndSource? _hwndSource;

        private void RegisterGlobalHotKey()
        {
            var hk = _globalSettings.Hotkeys.RecordingStartStop;
            if (hk == null || !hk.Enabled || string.IsNullOrEmpty(hk.Key)) return;

            try
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                _hwndSource = HwndSource.FromHwnd(_hwnd);
                if (_hwndSource != null)
                {
                    _hwndSource.AddHook(HwndHook);
                }

                uint modifiers = 0;
                if (hk.Ctrl) modifiers |= 0x0002;
                if (hk.Alt) modifiers |= 0x0001;
                if (hk.Shift) modifiers |= 0x0004;
                if (hk.Win) modifiers |= 0x0008;

                uint vk = 0;
                string keyStr = hk.Key.ToUpperInvariant();
                
                // Manual conversion to VK for F-keys and letters/digits
                if (keyStr.Length == 1)
                {
                    vk = (uint)keyStr[0];
                }
                else if (keyStr.StartsWith("F") && int.TryParse(keyStr.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
                {
                    vk = (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
                }
                else
                {
                    // Fallback to Enum.TryParse if manual fails or for other special keys
                    if (Enum.TryParse<System.Windows.Forms.Keys>(hk.Key, out var keys))
                    {
                        vk = (uint)keys;
                    }
                }

                if (vk != 0)
                {
                    UnregisterHotKey(_hwnd, HOTKEY_ID_REC_START_STOP); // Clean up if existing
                    bool success = RegisterHotKey(_hwnd, HOTKEY_ID_REC_START_STOP, modifiers, vk);
                    if (!success)
                    {
                        string msg = CatchCapture.Resources.LocalizationManager.GetString("HotkeyRegisterError") ?? "기존 사용하는 단축키가 있어 변경이 필요합니다.\n설정에서 단축키를 재 지정해주세요.";
                        CatchCapture.CustomMessageBox.Show(msg, CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering hotkey: {ex.Message}");
            }
        }

        private void UnregisterGlobalHotKey()
        {
            try
            {
                if (_hwnd != IntPtr.Zero)
                {
                    UnregisterHotKey(_hwnd, HOTKEY_ID_REC_START_STOP);
                    _hwndSource?.RemoveHook(HwndHook);
                }
            }
            catch { }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_REC_START_STOP)
            {
                RecordButton_Click(this, new RoutedEventArgs());
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        /// <summary>
        /// 오버레이에서 ESC 키 눌렸을 때 처리
        /// </summary>
        private void Overlay_EscapePressed(object? sender, EventArgs e)
        {
            // 녹화 중이면 녹화 중지 후 저장
            if (IsRecording)
            {
                StopRecordingAsync();
            }
            else
            {
                // 녹화 중이 아니면 전체 닫기
                Close();
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
                // 도킹 상태에서 드래그 시 도킹 해제 및 애니메이션 제거
                if (_isDocked)
                {
                    _isDocked = false;
                    _isHidden = false;
                    _autoHideTimer.Stop();
                    BeginAnimation(TopProperty, null); // 애니메이션 제거해야 DragMove 가능
                }

                // 사용자가 직접 도구상자를 움직이면 자동 추적 중단
                _isAttachedToOverlay = false;

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
                var result = CatchCapture.CustomMessageBox.Show(
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
                    // 리소스 정리는 Closed 이벤트에서 처리됨
                    Close();
                }
                // Cancel: 아무것도 안 함
            }
            else
            {
                // 녹화 중이 아니면 닫기 (리소스 정리는 Closed 이벤트에서)
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
        /// 일시정지/재개 버튼 클릭
        /// </summary>
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recorder == null || !IsRecording) return;
            
            _recorder.TogglePause();
            
            if (_recorder.IsPaused)
            {
                // 일시정지 상태: 타이머 멈춤, 아이콘을 '재개(▶)'로 변경
                _recordingTimer.Stop();
                PauseIcon.Visibility = Visibility.Collapsed;
                ResumeIcon.Visibility = Visibility.Visible;
                PauseButton.ToolTip = "녹화 재개";
            }
            else
            {
                // 녹화 재개: 타이머 시작, 아이콘을 '일시정지(||)'로 변경
                _recordingTimer.Start();
                PauseIcon.Visibility = Visibility.Visible;
                ResumeIcon.Visibility = Visibility.Collapsed;
                PauseButton.ToolTip = "일시정지";
            }
        }
        
        /// <summary>
        /// 시스템 오디오 토글 버튼 클릭
        /// </summary>
        private void SystemAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            // 토글
            _settings.RecordAudio = !_settings.RecordAudio;
            
            // UI 업데이트
            if (_settings.RecordAudio)
            {
                SystemAudioOffIcon.Visibility = Visibility.Collapsed;
                SystemAudioOnIcon.Visibility = Visibility.Visible;
                SystemAudioButton.ToolTip = "시스템 소리 녹음 (ON)";
            }
            else
            {
                SystemAudioOffIcon.Visibility = Visibility.Visible;
                SystemAudioOnIcon.Visibility = Visibility.Collapsed;
                SystemAudioButton.ToolTip = "시스템 소리 녹음 (OFF)";
            }
        }
        
        /// <summary>
        /// 마이크 토글 버튼 클릭
        /// </summary>
        private void MicButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording) return;
            
            // 마이크 장치 확인
            try
            {
                int micCount = NAudio.Wave.WaveIn.DeviceCount;
                if (micCount == 0)
                {
                    CatchCapture.CustomMessageBox.Show("마이크 장치가 감지되지 않았습니다.\n오디오 입력 장치를 연결해주세요.", 
                        "마이크 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 첫 번째 마이크 장치 정보 표시
                var capabilities = NAudio.Wave.WaveIn.GetCapabilities(0);
                System.Diagnostics.Debug.WriteLine($"[Mic] Found device: {capabilities.ProductName}");
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"마이크 장치 확인 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // 토글
            _settings.RecordMic = !_settings.RecordMic;
            
            // UI 업데이트
            if (_settings.RecordMic)
            {
                MicOffIcon.Visibility = Visibility.Collapsed;
                MicSlash.Visibility = Visibility.Collapsed;
                MicOnIcon.Visibility = Visibility.Visible;
                MicButton.ToolTip = "마이크 녹음 (ON)";
            }
            else
            {
                MicOffIcon.Visibility = Visibility.Visible;
                MicSlash.Visibility = Visibility.Visible;
                MicOnIcon.Visibility = Visibility.Collapsed;
                MicButton.ToolTip = "마이크 녹음 (OFF)";
            }
        }
        
        /// <summary>
        /// 영역 새로 선택 버튼 클릭
        /// </summary>
        private void AreaSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay == null) return;
            
            // 토글 버튼이므로 IsChecked 상태를 확인
            // 이미 IsChecked가 바뀐 상태로 들어옴 (클릭 시 자동 토글됨)
            bool isChecked = AreaSelectButton.IsChecked == true;

            if (isChecked)
            {
                if (!_overlay.IsSelectingNewArea)
                {
                    this.Hide(); // 툴박스 숨기기
                    _overlay.StartNewSelectionMode();
                }
            }
            else
            {
                if (_overlay.IsSelectingNewArea)
                {
                    _overlay.CancelNewSelectionMode();
                }
            }
        }
        
        private void DrawingPopup_Opened(object sender, EventArgs e)
        {
            try 
            {
                if (DrawingPopup.Child is UIElement child)
                {
                    var source = PresentationSource.FromVisual(child) as System.Windows.Interop.HwndSource;
                    if (source != null)
                    {
                        SetWindowDisplayAffinity(source.Handle, WDA_EXCLUDEFROMCAPTURE);
                    }
                }
            }
            catch { }
        }

        private void Overlay_AreaSelectionEnded(object? sender, EventArgs e)
        {
            this.Show(); // 툴박스 다시 보이기
            this.Activate(); // 활성화
            
            if (AreaSelectButton != null)
            {
                AreaSelectButton.IsChecked = false;
            }
        }
        /// 전체 화면 버튼 클릭 (토글)
        /// </summary>
        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay == null) return;
            
            // 이미 전체 화면 상태인지 확인 (저장된 영역이 있는지로 판단)
            if (_savedArea.HasValue)
            {
                // 원래 크기로 복원
                _overlay.SelectionArea = _savedArea.Value;
                _savedArea = null; // 저장된 값 초기화
                
                // 버튼 스타일 원래대로 (선택적)
            }
            else
            {
                // 현재 영역 저장
                _savedArea = _overlay.SelectionArea;
                
                // 현재 마우스가 있는 화면의 전체 크기로 설정
                var mousePt = System.Windows.Forms.Control.MousePosition;
                var screen = System.Windows.Forms.Screen.FromPoint(mousePt);
                var bounds = screen.Bounds;
                
                // 물리적 좌표(pixels) -> 논리적 좌표(units) 변환
                // _overlay를 기준으로 좌표를 가져와야 하므로 PointFromScreen 사용
                Point logicalTopLeft = _overlay.PointFromScreen(new Point(bounds.X, bounds.Y));
                Point logicalBottomRight = _overlay.PointFromScreen(new Point(bounds.Right, bounds.Bottom));
                
                _overlay.SelectionArea = new Rect(logicalTopLeft, logicalBottomRight);
            }
        }
        
        /// <summary>
    /// 파일 형식 라디오 버튼 클릭 처리
    /// </summary>
    private void FormatRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsRecording || sender is not RadioButton rb) return;

        if (rb == RbFormatMp4)
        {
            _settings.Format = RecordingFormat.MP4;
        }
        else if (rb == RbFormatGif)
        {
            _settings.Format = RecordingFormat.GIF;
        }
        else if (rb == RbFormatMp3)
        {
            _settings.Format = RecordingFormat.MP3;
            _settings.RecordAudio = true; // MP3 모드는 오디오가 필수
        }

        UpdateUI();
        ShowOverlay(); // 형식에 따라 오버레이 표시/숨기기
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
            UpdateUI();
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
        /// 도구 모음 접기/펴기
        /// </summary>
        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                // 접기
                SettingsPanel.Visibility = Visibility.Collapsed;
                CollapseArrow.Text = "▶"; // 펴기 버튼
                CollapseButton.ToolTip = "도구 모음 펼치기";
            }
            else
            {
                // 펴기
                SettingsPanel.Visibility = Visibility.Visible;
                CollapseArrow.Text = "◀"; // 접기 버튼
                CollapseButton.ToolTip = "도구 모음 숨기기";
            }
        }
        
        /// <summary>
        /// 녹화 타이머 틱
        /// </summary>
        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            _recordingDuration = _recordingDuration.Add(TimeSpan.FromSeconds(1));
            RecordingTimeText.Text = _recordingDuration.ToString(@"mm\:ss");

            // 파일 용량 업데이트
            if (_recorder != null && _overlay != null)
            {
                long bytes = _recorder.CurrentFileSize;
                string sizeStr;
                if (bytes < 1024) sizeStr = $"{bytes} B";
                else if (bytes < 1024 * 1024) sizeStr = $"{bytes / 1024.0:F1} KB";
                else if (bytes < 1024 * 1024 * 1024) sizeStr = $"{bytes / 1024.0 / 1024.0:F1} MB";
                else sizeStr = $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
                
                _overlay.UpdateFileSizeText(sizeStr);
            }
        }
        
        /// <summary>
        /// 창 닫힐 때 정리
        /// </summary>
        private void RecordingWindow_Closed(object? sender, EventArgs e)
        {
            UnregisterGlobalHotKey();

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
            // === FFmpeg 필수 확인 ===
            // MP4 및 GIF 고품질 저장을 위해 FFmpeg가 반드시 필요함
            if (!FFmpegDownloader.IsFFmpegInstalled())
            {
                var result = CatchCapture.CustomMessageBox.Show(
                    "동영상 녹화(MP4/GIF) 기능을 사용하려면 추가 구성 요소(FFmpeg)가 필요합니다.\n\n" +
                    "지금 다운로드하여 설치하시겠습니까?",
                    "추가 구성 요소 필요",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 모던한 다운로드 UI 표시
                    var progressWindow = new Window
                    {
                        Title = "FFmpeg 다운로드",
                        Width = 420,
                        Height = 180,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize,
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Background = System.Windows.Media.Brushes.Transparent,
                        Topmost = true
                    };

                    // 메인 컨테이너
                    var mainBorder = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 55)),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(30, 25, 30, 25),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 100)),
                        BorderThickness = new Thickness(1)
                    };

                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    // 제목
                    var titleText = new TextBlock
                    {
                        Text = "📦 FFmpeg 다운로드 중...",
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = System.Windows.Media.Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    // 상태 텍스트
                    var statusText = new TextBlock
                    {
                        Text = "서버 연결 중...",
                        FontSize = 12,
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 190)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 15)
                    };

                    // 프로그레스 바 배경
                    var progressBg = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 75)),
                        Height = 6,
                        CornerRadius = new CornerRadius(3)
                    };

                    // 프로그레스 바 (Grid로 구현)
                    var progressFill = new Border
                    {
                        Background = new System.Windows.Media.LinearGradientBrush
                        {
                            StartPoint = new System.Windows.Point(0, 0),
                            EndPoint = new System.Windows.Point(1, 0),
                            GradientStops = new System.Windows.Media.GradientStopCollection
                            {
                                new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(99, 102, 241), 0),
                                new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(168, 85, 247), 1)
                            }
                        },
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = 0
                    };

                    var progressGrid = new Grid { Height = 6 };
                    progressGrid.Children.Add(progressBg);
                    progressGrid.Children.Add(progressFill);

                    // 퍼센트 텍스트
                    var percentText = new TextBlock
                    {
                        Text = "0%",
                        FontSize = 13,
                        Foreground = System.Windows.Media.Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 12, 0, 0)
                    };

                    sp.Children.Add(titleText);
                    sp.Children.Add(statusText);
                    sp.Children.Add(progressGrid);
                    sp.Children.Add(percentText);

                    mainBorder.Child = sp;
                    progressWindow.Content = mainBorder;
                    progressWindow.Show();

                    double maxWidth = 360; // progressGrid의 실제 너비
                    
                    var progress = new Progress<int>(p =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            progressFill.Width = (p / 100.0) * maxWidth;
                            percentText.Text = $"{p}%";
                            
                            if (p < 50)
                                statusText.Text = "다운로드 중...";
                            else if (p < 70)
                                statusText.Text = "압축 해제 중...";
                            else if (p < 100)
                                statusText.Text = "설치 중...";
                            else
                                statusText.Text = "✓ 완료!";
                        });
                    });

                    bool success = await FFmpegDownloader.DownloadFFmpegAsync(progress);

                    if (success)
                    {
                        // 성공 시 완료 메시지 표시 후 1초 대기
                        Dispatcher.Invoke(() =>
                        {
                            titleText.Text = "✅ FFmpeg 설치 완료!";
                            statusText.Text = "녹화 기능을 사용할 수 있습니다.";
                        });
                        await Task.Delay(1200);
                    }

                    progressWindow.Close();

                    if (!success)
                    {
                        // 자동 다운로드 실패 -> 수동 설치 제안
                        var manualResult = CatchCapture.CustomMessageBox.Show(
                            "자동 다운로드에 실패했습니다.\n" +
                            "직접 ffmpeg.exe 파일을 선택하여 설치하시겠습니까?\n\n" +
                            "(ffmpeg.exe 파일을 선택하면 올바른 위치로 복사됩니다)", 
                            "설치 실패", 
                            MessageBoxButton.YesNo, 
                            MessageBoxImage.Warning);
                            
                        if (manualResult == MessageBoxResult.Yes)
                        {
                            var openFileDialog = new Microsoft.Win32.OpenFileDialog
                            {
                                Filter = "FFmpeg 실행 파일 (ffmpeg.exe)|ffmpeg.exe|모든 파일 (*.*)|*.*",
                                Title = "ffmpeg.exe 파일 선택"
                            };
                            
                            if (openFileDialog.ShowDialog() == true)
                            {
                                bool manualSuccess = FFmpegDownloader.ManualInstall(openFileDialog.FileName);
                                if (!manualSuccess)
                                {
                                    return; // 수동 설치 실패
                                }
                                // 성공 시 계속 진행
                                CatchCapture.CustomMessageBox.Show("FFmpeg 수동 설치가 완료되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                return; // 파일 선택 취소
                            }
                        }
                        else
                        {
                            return; // 수동 설치 거부 (녹화 취소)
                        }
                    }
                }
                else
                {
                    return; // 사용자가 설치 거부 시 녹화 시작 취소
                }
            }
            // === FFmpeg 확인 끝 ===
            // === FFmpeg 처리 끝 ===

            // 나머지는 당신 원래 코드 그대로
            // === 중앙 카운트다운 표시 ===
            if (_settings.CountdownSeconds > 0)
            {
                var tcs = new TaskCompletionSource<bool>();
                
                // 카운트다운 표시용 가이드 윈도우 생성
                // GuideWindow는 CatchCapture.Utilities 네임스페이스에 있음
                var countdown = new CatchCapture.Utilities.GuideWindow("", null)
                {
                    Topmost = true
                };
                
                countdown.Show();
                countdown.StartCountdown(_settings.CountdownSeconds, () => 
                {
                    tcs.SetResult(true);
                });
                
                // 카운트다운 완료 대기
                await tcs.Task;
            }

            _overlay?.SetRecordingMode(true);

            _recorder = new ScreenRecorder(_settings);
            _recorder.StatsUpdated += Recorder_StatsUpdated;
            _recorder.RecordingStopped += Recorder_RecordingStopped;
            _recorder.ErrorOccurred += Recorder_ErrorOccurred;

            // 현재 오버레이의 선택 영역 가져오기 (설정값보다 화면에 보이는 실제 위치 우선)
            Rect captureArea = _overlay?.SelectionArea ?? new Rect(
                _settings.LastAreaLeft, _settings.LastAreaTop,
                _settings.LastAreaWidth, _settings.LastAreaHeight);

            // [중요] WPF 논리 좌표를 스크린의 물리적 픽셀 좌표로 변환
            // 다중 모니터 및 High DPI 환경에서 GDI+ CopyFromScreen은 물리적 좌표가 필요합니다.
            // PointToScreen은 윈도우의 위치(VirtualScreenLeft 등)와 DPI 배율을 모두 계산에 포함합니다.
            Int32Rect captureRect;
            if (_overlay != null)
            {
                // UI 스레드에서 안전하게 좌표 변환
                Point screenTopLeft = _overlay.PointToScreen(new Point(captureArea.X, captureArea.Y));
                Point screenBottomRight = _overlay.PointToScreen(new Point(captureArea.Right, captureArea.Bottom));

                captureRect = new Int32Rect(
                    (int)Math.Round(screenTopLeft.X),
                    (int)Math.Round(screenTopLeft.Y),
                    (int)Math.Round(screenBottomRight.X - screenTopLeft.X),
                    (int)Math.Round(screenBottomRight.Y - screenTopLeft.Y)
                );
            }
            else
            {
                // 폴백 (오버레이가 없을 경우)
                captureRect = new Int32Rect(
                    (int)captureArea.X,
                    (int)captureArea.Y,
                    (int)captureArea.Width,
                    (int)captureArea.Height
                );
            }

            _recorder.StartRecording(captureRect);

            _recordingDuration = TimeSpan.Zero;
            _recordingTimer.Start();


            RecordButtonIcon.Visibility = Visibility.Collapsed;
            StopButtonIcon.Visibility = Visibility.Visible;
            
            // 일시정지 버튼 표시 (녹화 시작 상태)
            PauseButton.Visibility = Visibility.Visible;
            PauseIcon.Visibility = Visibility.Visible;
            ResumeIcon.Visibility = Visibility.Collapsed;
            PauseButton.ToolTip = "일시정지";

            EnableSettings(false);
            UpdateUI();

            // 녹화 중 툴팁 비활성화 (캡처 방지)
            SetToolTipsEnabled(false);
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
            
            // 일시정지 버튼 숨김
            PauseButton.Visibility = Visibility.Collapsed;

            // 툴팁 재활성화
            SetToolTipsEnabled(true);
            
            // 녹화만 중지 (저장하지 않음)
            try
            {
                await _recorder!.StopRecording();
                
                // MP3 모드: 프레임이 없어도 저장 (오디오만 있으면 됨)
                // 비디오 모드: 프레임이 있어야 저장
                bool canSave = _settings.Format == RecordingFormat.MP3 || _recorder.FrameCount > 0;
                
                if (canSave)
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
                    
                    // 녹화 도구 창 닫기 로직 제거 (사용자 요청: 도구 상자 유지)
                    // Close(); 
                }
                else
                {
                    CatchCapture.CustomMessageBox.Show("녹화된 프레임이 없어 저장할 수 없습니다.", "알림", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"녹화 중지 중 오류가 발생했습니다:\n{ex.Message}", "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            // 버튼 다시 활성화
            EnableSettings(true);
            
            // 오버레이를 다시 선택 모드(핸들 표시 등)로 전환
            _overlay?.SetRecordingMode(false);
            
            UpdateUI();
        }
        
        /// <summary>
        /// 설정 버튼 활성화/비활성화
        /// </summary>
        private void EnableSettings(bool enable)
        {
            SystemAudioButton.IsEnabled = enable;
            MicButton.IsEnabled = enable;
            if (RbFormatMp4 != null) RbFormatMp4.IsEnabled = enable;
            if (RbFormatGif != null) RbFormatGif.IsEnabled = enable;
            if (RbFormatMp3 != null) RbFormatMp3.IsEnabled = enable;
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
                CatchCapture.CustomMessageBox.Show($"녹화 중 오류가 발생했습니다:\n{ex.Message}", "오류",
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
        // 파일 형식 버튼 상태 업데이트
        if (RbFormatMp4 != null) RbFormatMp4.IsChecked = _settings.Format == RecordingFormat.MP4;
        if (RbFormatGif != null) RbFormatGif.IsChecked = _settings.Format == RecordingFormat.GIF;
        if (RbFormatMp3 != null) RbFormatMp3.IsChecked = _settings.Format == RecordingFormat.MP3;
        
        bool isGifMode = _settings.Format == RecordingFormat.GIF;
        bool isMp3Mode = _settings.Format == RecordingFormat.MP3;
        bool isVideoMode = !isMp3Mode; // MP4, GIF는 비디오 모드 (화면 필요)

        // 1. 비디오 관련 버튼 제어
        if (QualityButton != null) QualityButton.IsEnabled = isVideoMode;
        if (FpsButton != null) FpsButton.IsEnabled = isVideoMode && !isGifMode; // GIF는 내부 정책 사용
        if (MouseEffectToggle != null) MouseEffectToggle.IsEnabled = isVideoMode;
        if (AreaSelectButton != null) AreaSelectButton.IsEnabled = isVideoMode;

        if (FullScreenButton != null) FullScreenButton.IsEnabled = isVideoMode;
        if (DrawingButton != null) DrawingButton.IsEnabled = isVideoMode;
        if (AreaSizeText != null) AreaSizeText.Visibility = isVideoMode ? Visibility.Visible : Visibility.Collapsed;

        // 2. 오디오 관련 버튼 제어 (GIF는 오디오 불가, MP3는 오디오 전용)
        bool canRecordAudio = !isGifMode;
        if (SystemAudioButton != null)
        {
            SystemAudioButton.IsEnabled = canRecordAudio && !IsRecording;
            SystemAudioButton.IsChecked = _settings.RecordAudio;
            SystemAudioOffIcon.Visibility = _settings.RecordAudio ? Visibility.Collapsed : Visibility.Visible;
            SystemAudioOnIcon.Visibility = _settings.RecordAudio ? Visibility.Visible : Visibility.Collapsed;
        }
        
        if (MicButton != null)
        {
            MicButton.IsEnabled = canRecordAudio && !IsRecording;
            MicButton.IsChecked = _settings.RecordMic;
            MicOffIcon.Visibility = _settings.RecordMic ? Visibility.Collapsed : Visibility.Visible;
            MicSlash.Visibility = _settings.RecordMic ? Visibility.Collapsed : Visibility.Visible;
            MicOnIcon.Visibility = _settings.RecordMic ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // 녹화 중에는 형식 변경 불가
        if (RbFormatMp4 != null) RbFormatMp4.IsEnabled = !IsRecording;
        if (RbFormatGif != null) RbFormatGif.IsEnabled = !IsRecording;
        if (RbFormatMp3 != null) RbFormatMp3.IsEnabled = !IsRecording;
        
        // 파일 형식은 상단 RadioButton으로 처리됨
        
        // 화질
        if (QualityText != null)
        {
            QualityText.Text = _settings.Quality switch
            {
                RecordingQuality.High => "HD",
                RecordingQuality.Medium => "SD",
                RecordingQuality.Low => "LD",
                _ => "SD"
            };
        }
        
        // 프레임
        if (FpsText != null)
        {
            FpsText.Text = $"{_settings.FrameRate}F";
        }
        
        // 마우스 효과 (커서 표시)
        if (MouseEffectToggle != null) MouseEffectToggle.IsChecked = _settings.ShowMouseEffects;
        if (_settings.ShowMouseEffects)
        {
            MouseOnIcon.Visibility = Visibility.Visible;
            MouseOffIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            MouseOnIcon.Visibility = Visibility.Collapsed;
            MouseOffIcon.Visibility = Visibility.Visible;
        }
        
        // 타이머
        if (TimerText != null)
        {
            TimerText.Text = _settings.CountdownSeconds > 0 ? _settings.CountdownSeconds.ToString() : "";
        }
        if (TimerCountBadge != null)
        {
            TimerCountBadge.Visibility = _settings.CountdownSeconds > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // 영역 크기
        if (AreaSizeText != null && isVideoMode)
        {
            AreaSizeText.Text = $"{(int)_settings.LastAreaWidth}x{(int)_settings.LastAreaHeight}";
        }
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

            // 도구상자가 도킹 상태가 아니고, 사용자가 수동으로 옮기지 않았을 때만 영역을 따라다님
            if (_isAttachedToOverlay && !_isDocked && _overlay != null)
            {
                UpdateToolboxPosition(area);
            }
        }

        private void UpdateToolboxPosition(Rect area)
        {
            if (_overlay == null) return;

            // 오버레이가 위치한 모니터 상의 실제 영역 계산 (Virtual Screen 좌표계)
            Rect absoluteArea = new Rect(_overlay.Left + area.Left, _overlay.Top + area.Top, area.Width, area.Height);
            
            // 영역의 중심점 계산
            Point center = new Point(absoluteArea.Left + absoluteArea.Width / 2, absoluteArea.Top + absoluteArea.Height / 2);
            
            // 해당 중심점이 포함된 스크린 찾기
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)center.X, (int)center.Y));
            var workArea = screen.WorkingArea; // Taskbar 제외 영역

            // 1. 상단 배치 시도 (Top Outer) - 가로 모드
            // 조건: 영역 위쪽에 툴바 높이(약 60px) + 여유(10px)가 있는가?
            bool canPlaceTop = (absoluteArea.Top - workArea.Top) >= 70;

            if (canPlaceTop)
            {
                SetLayoutMode(false); // 가로 모드
                this.UpdateLayout();  // 크기 갱신

                double currentWidth = this.ActualWidth;
                double currentHeight = this.ActualHeight;

                double newLeft = absoluteArea.Left + (absoluteArea.Width - currentWidth) / 2;
                double newTop = absoluteArea.Top - currentHeight - 10;

                // 가로 위치 화면 보정
                if (newLeft < workArea.Left) newLeft = workArea.Left + 10;
                if (newLeft + currentWidth > workArea.Right) newLeft = workArea.Right - currentWidth - 10;

                this.Left = newLeft;
                this.Top = newTop;
            }
            else
            {
                // 상단 공간 부족 -> 세로 모드 전환
                SetLayoutMode(true);
                this.UpdateLayout(); // 크기 갱신

                double currentWidth = this.ActualWidth;
                double currentHeight = this.ActualHeight;

                // 2. 좌측 외부 배치 시도 (Left Outer)
                // 조건: 영역 왼쪽에 툴바 폭 + 여유(10px)가 있는가?
                bool canPlaceLeft = (absoluteArea.Left - workArea.Left) >= (currentWidth + 10);
                
                // 3. 우측 외부 배치 시도 (Right Outer)
                // 조건: 영역 오른쪽에 툴바 폭 + 여유(10px)가 있는가?
                bool canPlaceRight = (workArea.Right - absoluteArea.Right) >= (currentWidth + 10);

                if (canPlaceLeft)
                {
                    // 좌측 바깥 배치
                    double newLeft = absoluteArea.Left - currentWidth - 10;
                    double newTop = absoluteArea.Top; // 상단 정렬
                    
                    // 세로 위치 화면 보정
                    if (newTop < workArea.Top) newTop = workArea.Top + 10;
                    if (newTop + currentHeight > workArea.Bottom) newTop = workArea.Bottom - currentHeight - 10;

                    this.Left = newLeft;
                    this.Top = newTop;
                }
                else if (canPlaceRight)
                {
                    // 우측 바깥 배치
                    double newLeft = absoluteArea.Right + 10;
                    double newTop = absoluteArea.Top; // 상단 정렬

                    // 세로 위치 화면 보정
                    if (newTop < workArea.Top) newTop = workArea.Top + 10;
                    if (newTop + currentHeight > workArea.Bottom) newTop = workArea.Bottom - currentHeight - 10;

                    this.Left = newLeft;
                    this.Top = newTop;
                }
                else
                {
                    // 4. 공간 모두 부족 -> 내부 좌측 배치 (Inner Left) - 최후의 수단
                    double newLeft = absoluteArea.Left + 10;
                    double newTop = absoluteArea.Top + 10;

                    // 화면 보정
                    if (newLeft < workArea.Left) newLeft = workArea.Left + 10;
                    if (newTop < workArea.Top) newTop = workArea.Top + 10;
                    if (newTop + currentHeight > workArea.Bottom) newTop = workArea.Bottom - currentHeight - 10;

                    this.Left = newLeft;
                    this.Top = newTop;
                }
            }
        }

        private bool _isVerticalLayout = false;

        private void SetLayoutMode(bool vertical)
        {
            if (_isVerticalLayout == vertical) return;
            _isVerticalLayout = vertical;

            if (vertical)
            {
                // 세로 모드
                
                // 마진 초기화 (세로 모드에 맞게)
                if (MainToolbarPanel != null)
                {
                    MainToolbarPanel.Orientation = Orientation.Vertical;
                    MainToolbarPanel.Margin = new Thickness(4, 4, 4, 4);
                }
                
                if (SettingsPanel != null)
                {
                    SettingsPanel.Orientation = Orientation.Vertical;
                    SettingsPanel.Margin = new Thickness(0, 4, 0, 4);
                }

                // 타이틀바 텍스트 숨김 (폭 좁게 유지)
                if (TitleText != null) TitleText.Visibility = Visibility.Collapsed;
                
                // 아이콘도 숨겨서 더 깔끔하게 (선택적)
                // TitleBar 내부 Image 찾기 필요하지만 x:Name이 없으므로 생략 혹은 TitleText 옆의 stackpanel 접근 필요
                // 여기선 TitleText만 숨김

                // 녹화 시간 텍스트 정렬
                if (RecordingTimeText != null)
                {
                    RecordingTimeText.Margin = new Thickness(0, 0, 0, 8);
                    RecordingTimeText.HorizontalAlignment = HorizontalAlignment.Center;
                }

                // 구분선 숨기기
                if (Separator1 != null) Separator1.Visibility = Visibility.Collapsed;
                if (Separator2 != null) Separator2.Visibility = Visibility.Collapsed;
                
                // 영역 크기 텍스트 숨기기 (세로 모드에선 공간 차지 큼) 또는 작게 표시
                if (AreaSizeText != null) AreaSizeText.Visibility = Visibility.Collapsed;

                // 접기 버튼 회전 (아래쪽을 가리키거나 위쪽을 가리키도록?) 
                // 일단 숨기거나 유지. 세로 모드에서 접기는 복잡할 수 있음. 유지.
                
                this.Width = Double.NaN;
                this.Height = Double.NaN;
            }
            else
            {
                // 가로 모드 (복원)
                if (MainToolbarPanel != null)
                {
                    MainToolbarPanel.Orientation = Orientation.Horizontal;
                    MainToolbarPanel.Margin = new Thickness(8, 4, 8, 4);
                }
                
                if (SettingsPanel != null)
                {
                    SettingsPanel.Orientation = Orientation.Horizontal;
                    SettingsPanel.Margin = new Thickness(0, 0, 0, 0);
                }

                if (TitleText != null) TitleText.Visibility = Visibility.Visible;

                if (RecordingTimeText != null)
                {
                    RecordingTimeText.Margin = new Thickness(0, 0, 8, 0);
                    // RecordingTimeText.VerticalAlignment = VerticalAlignment.Center; // 기본값
                }

                if (Separator1 != null) Separator1.Visibility = Visibility.Visible;
                if (Separator2 != null) Separator2.Visibility = Visibility.Visible;
                
                if (AreaSizeText != null) AreaSizeText.Visibility = Visibility.Visible;

                this.Width = Double.NaN;
                this.Height = Double.NaN;
            }
        }
        
        #endregion
        
        #region 도킹
        
        /// <summary>
        /// 상단 가장자리 도킹 체크
        /// </summary>
        private void CheckDocking()
        {
            if (Math.Abs(Top) <= DOCK_THRESHOLD)
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
            
            // 자동 숨김 체크 시작
            if (!IsMouseOver) _autoHideTimer.Start();
        }
        
        /// <summary>
        /// 도킹 토글
        /// </summary>
        private void ToggleDock()
        {
            if (_isDocked)
            {
                // 도킹 해제
                _isDocked = false;
                _isHidden = false;
                _autoHideTimer.Stop();
                BeginAnimation(TopProperty, null); // 애니메이션 제거
                Top = 100;
            }
            else
            {
                DockToTop();
            }
        }
        
        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            if (_isDocked && !IsMouseOver && !_isHidden)
            {
                SlideUp();
            }
            _autoHideTimer.Stop();
        }

        private void RecordingWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            _autoHideTimer.Stop();
            if (_isDocked && _isHidden)
            {
                SlideDown();
            }
        }

        private void RecordingWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDocked && !_isHidden)
            {
                _autoHideTimer.Start();
            }
        }

        private void SlideUp()
        {
            // Height만큼 위로 올려서 아래 2px만 남김
            double targetTop = -(ActualHeight - 2); 
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                targetTop, TimeSpan.FromMilliseconds(200));
            anim.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            BeginAnimation(TopProperty, anim);
            _isHidden = true;
        }

        private void SlideDown()
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                0, TimeSpan.FromMilliseconds(200));
            anim.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            BeginAnimation(TopProperty, anim);
            _isHidden = false;
        }

        #endregion

        #region 언어 변경

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => UpdateUIText());
        }

        private void UpdateUIText()
        {
            var loc = CatchCapture.Resources.LocalizationManager.GetString;
            
            // 윈도우 제목과 타이틀바 텍스트
            this.Title = loc("RecordingTool");
            if (TitleText != null) TitleText.Text = loc("RecordingTool");
            
            // 버튼 툴팁
            if (RecordButton != null) RecordButton.ToolTip = loc("StartStopRecording");
            if (PauseButton != null)
            {
                // 일시정지/재개 상태에 따라 툴팁 변경 (UpdateUI에서도 처리)
                PauseButton.ToolTip = _recorder?.IsPaused == true ? loc("Resume") : loc("Pause");
            }
            if (SystemAudioButton != null)
            {
                SystemAudioButton.ToolTip = _settings.RecordAudio ? loc("SystemAudioOn") : loc("SystemAudioOff");
            }
            if (MicButton != null)
            {
                MicButton.ToolTip = _settings.RecordMic ? loc("MicrophoneOn") : loc("MicrophoneOff");
            }
            if (RbFormatMp4 != null) RbFormatMp4.ToolTip = loc("FileFormat") + " (MP4)";
            if (RbFormatGif != null) RbFormatGif.ToolTip = loc("FileFormat") + " (GIF)";
            if (RbFormatMp3 != null) RbFormatMp3.ToolTip = loc("FileFormat") + " (MP3)";
            if (QualityButton != null) QualityButton.ToolTip = loc("VideoQuality");
            if (FpsButton != null) FpsButton.ToolTip = loc("FrameRate");
            if (MouseEffectToggle != null) MouseEffectToggle.ToolTip = loc("ShowMouseCursor");
            if (TimerButton != null) TimerButton.ToolTip = loc("Countdown");
            if (CollapseButton != null)
            {
                CollapseButton.ToolTip = SettingsPanel.Visibility == Visibility.Visible ? loc("HideToolbar") : loc("ShowToolbar");
            }

            // 새로운 도구 툴팁 추가
            if (AreaSelectButton != null) AreaSelectButton.ToolTip = loc("AreaSelection");

            if (FullScreenButton != null) FullScreenButton.ToolTip = loc("FullScreen");
            if (DrawingButton != null) DrawingButton.ToolTip = loc("DrawingTools");
            
            // 그리기 도구 팝업 내부 버튼 툴팁
            if (PenRadio != null) PenRadio.ToolTip = loc("ToolPen");
            if (HighlightRadio != null) HighlightRadio.ToolTip = loc("ToolHighlighter");
            if (EraserRadio != null) EraserRadio.ToolTip = loc("ToolEraser");
            if (LineRadio != null) LineRadio.ToolTip = loc("ToolLine");
            if (ArrowRadio != null) ArrowRadio.ToolTip = loc("ToolArrow");
            if (RectangleRadio != null) RectangleRadio.ToolTip = loc("ToolRectangle");
            if (EllipseRadio != null) EllipseRadio.ToolTip = loc("ToolEllipse");
            if (NumberingRadio != null) NumberingRadio.ToolTip = loc("ToolNumbering");
            if (ClearAllButton != null) ClearAllButton.ToolTip = loc("ToolClearAll");
        }

        #endregion

        #region 그리기 도구 이벤트 핸들러

        private Color _currentDrawingColor = Colors.Red; // 기본값

        private void DrawingButton_Click(object sender, RoutedEventArgs e)
        {
            bool isDrawing = DrawingButton.IsChecked == true;
            DrawingPopup.IsOpen = isDrawing;
            
            if (_overlay != null)
            {
                _overlay.SetDrawingMode(isDrawing);
                
                // 그리기 모드가 켜지면 현재 선택된 도구 적용
                if (isDrawing)
                {
                    ApplyCurrentDrawingTool();
                }
            }
        }

        private void PenRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void HighlightRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void EraserRadio_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay != null)
            {
                _overlay.SetEraserMode();
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorCode)
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorCode);
                    _currentDrawingColor = color;
                    ApplyCurrentDrawingTool();
                }
                catch { }
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay != null)
            {
                _overlay.ClearDrawing();
            }
        }

        private void ApplyCurrentDrawingTool()
        {
            if (_overlay == null) return;

            if (PenRadio.IsChecked == true)
            {
                _overlay.SetPenMode(_currentDrawingColor, 3, false);
            }
            else if (HighlightRadio.IsChecked == true)
            {
                // 형광펜은 반투명하게 적용
                Color highlightColor = _currentDrawingColor;
                highlightColor.A = 100; // 약 40% 불투명도
                _overlay.SetPenMode(highlightColor, 12, true);
            }
            else if (EraserRadio.IsChecked == true)
            {
                _overlay.SetEraserMode();
            }
            else if (LineRadio.IsChecked == true)
            {
                _overlay.SetShapeTool("line", _currentDrawingColor);
            }
            else if (ArrowRadio.IsChecked == true)
            {
                _overlay.SetShapeTool("arrow", _currentDrawingColor);
            }
            else if (RectangleRadio.IsChecked == true)
            {
                _overlay.SetShapeTool("rectangle", _currentDrawingColor);
            }
            else if (EllipseRadio.IsChecked == true)
            {
                _overlay.SetShapeTool("ellipse", _currentDrawingColor);
            }
            else if (NumberingRadio.IsChecked == true)
            {
                _overlay.SetNumberingMode(_currentDrawingColor);
            }
        }

        private void LineRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void ArrowRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void RectangleRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void EllipseRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void NumberingRadio_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentDrawingTool();
        }

        private void DrawingPopup_Closed(object sender, EventArgs e)
        {
            DrawingButton.IsChecked = false;
            if (_overlay != null)
            {
                _overlay.SetDrawingMode(false);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Z: Undo
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _overlay?.PerformUndo();
                e.Handled = true;
            }
            // Ctrl+Y: Redo
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _overlay?.PerformRedo();
                e.Handled = true;
            }
        }

        #endregion
    }
}

