using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using CatchCapture.Models;
using System.Windows.Controls; // Orientation

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        // Win32 API 상수 및 메서드
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private Point lastPosition;
        private int _delaySeconds = 3; // default delay
        private bool _verticalMode;
        private readonly DispatcherTimer _hideTitleTimer = new DispatcherTimer();
        private readonly DispatcherTimer _topmostCheckTimer = new DispatcherTimer();
        private bool _isMouseInside;
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        public event EventHandler? DesignatedCaptureRequested;
        
        public SimpleModeWindow()
        {
            InitializeComponent();
            
            // 최상위 창 설정을 더 강력하게 적용
            Topmost = true;
            ShowInTaskbar = true; // 작업표시줄에 표시하여 독립적인 창으로 인식
            
            // 위치는 호출자가 지정 (MainWindow에서 설정)
            // PositionWindow();

            // Load last orientation
            var s = Settings.Load();
            _verticalMode = s.SimpleModeVertical;
            ApplyOrientation(_verticalMode, suppressPersist:true);

            // Ensure we can receive keyboard input
            this.Focusable = true;
            this.Loaded += (_, __) =>
            {
                try { 
                    this.Focus(); 
                    Keyboard.Focus(this);
                    // 로드 후 Win32 API로 최상위 강제 설정
                    ForceTopmost();
                } catch { }
            };

            // Keep always-on-top even after losing focus (e.g., clicking taskbar)
            this.Deactivated += SimpleModeWindow_Deactivated;
            this.Activated += SimpleModeWindow_Activated;

            // Hover hide timer setup (slightly longer for stickier hover)
            _hideTitleTimer.Interval = TimeSpan.FromMilliseconds(800);
            _hideTitleTimer.Tick += (s2, e2) =>
            {
                _hideTitleTimer.Stop();
                // Keep showing if context menu is open
                if (DelayContextMenu != null && DelayContextMenu.IsOpen)
                {
                    ShowTitleBar();
                    return;
                }
                if (!_isMouseInside)
                {
                    HideTitleBar();
                }
            };

            // Ensure title bar stays while menu open; hide after it closes (if mouse is out)
            if (DelayContextMenu != null)
            {
                DelayContextMenu.Opened += (o, e) =>
                {
                    ShowTitleBar();
                };
                DelayContextMenu.Closed += (o, e) =>
                {
                    if (!_isMouseInside)
                    {
                        _hideTitleTimer.Stop();
                        _hideTitleTimer.Start();
                    }
                };
            }

            // 주기적으로 최상위 상태 확인하는 타이머 설정 (1초마다 더 자주)
            _topmostCheckTimer.Interval = TimeSpan.FromSeconds(1);
            _topmostCheckTimer.Tick += (s, e) =>
            {
                // 창이 보이고 최소화되지 않은 상태에서만 확인
                if (IsVisible && WindowState != WindowState.Minimized)
                {
                    // 현재 포그라운드 창이 다른 창인지 확인
                    var foregroundWindow = GetForegroundWindow();
                    var thisWindow = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    
                    if (foregroundWindow != thisWindow && thisWindow != IntPtr.Zero)
                    {
                        // 다른 창이 포그라운드에 있으면 강제로 최상위 재설정
                        AggressiveForceTopmost();
                    }
                }
            };
            _topmostCheckTimer.Start();
        }
        
        private void PositionWindow()
        {
            // 좌측 상단 기본값 (호출자가 지정하지 않은 경우에만 사용할 수 있도록 남겨둠)
            Left = 10;
            Top = 10;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastPosition = e.GetPosition(this);
            DragMove();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInside = true;
            _hideTitleTimer.Stop();
            ShowTitleBar();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInside = false;
            _hideTitleTimer.Stop();
            _hideTitleTimer.Start();
        }

        private void TitleBarBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInside = true;
            _hideTitleTimer.Stop();
            ShowTitleBar();
        }

        private void TitleBarBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInside = false;
            _hideTitleTimer.Stop();
            _hideTitleTimer.Start();
        }

        private void ButtonsPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseInside = true;
            _hideTitleTimer.Stop();
            ShowTitleBar();
        }

        private void ButtonsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseInside = false;
            _hideTitleTimer.Stop();
            _hideTitleTimer.Start();
        }

        private void ShowTitleBar()
        {
            if (TitleRow != null && TitleBarBorder != null)
            {
                // Overlay title bar: only toggle visibility to avoid layout shifts
                TitleBarBorder.Visibility = Visibility.Visible;
            }
        }

        private void HideTitleBar()
        {
            if (TitleRow != null && TitleBarBorder != null)
            {
                // Overlay title bar: only toggle visibility to avoid layout shifts
                TitleBarBorder.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ToggleOrientationButton_Click(object sender, RoutedEventArgs e)
        {
            _verticalMode = !_verticalMode;
            ApplyOrientation(_verticalMode);
        }

        private void ShowTitleBar_Dup()
        {
            if (TitleRow != null && TitleBarBorder != null)
            {
                TitleBarBorder.Visibility = Visibility.Visible;
            }
        }

        private void HideTitleBar_Dup()
        {
            if (TitleRow != null && TitleBarBorder != null)
            {
                TitleBarBorder.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ToggleOrientationButton_Click_Dup(object sender, RoutedEventArgs e)
        {
            _verticalMode = !_verticalMode;
            ApplyOrientation(_verticalMode);
        }
        
        private void ApplyOrientation(bool vertical, bool suppressPersist = false)
        {
            if (ButtonsPanel == null) return;

            ButtonsPanel.Orientation = vertical ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal;

            // 타이틀바 표시 규칙
            TitleLeftIcon.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
            TitleText.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible; // 가로모드에서만 "간편모드" 표시
            ToggleOrientationIcon.Text = vertical ? "↔" : "↕";

            // 창 크기: 세로 60x200, 가로 200x85
            if (vertical)
            {
                this.Width = 61;
                this.Height = 218; // +5px
            }
            else
            {
                this.Width = 200;
                this.Height = 85;
            }

            if (!suppressPersist)
            {
                try
                {
                    var s = Settings.Load();
                    s.SimpleModeVertical = vertical;
                    Settings.Save(s);
                }
                catch { }
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // X로 종료 시 마지막 간편모드 위치/모드 저장
            try
            {
                var s = Settings.Load();
                s.LastSimpleLeft = this.Left;
                s.LastSimpleTop = this.Top;
                s.LastModeIsSimple = true; // 간편모드로 종료
                Settings.Save(s);
            }
            catch { /* ignore persistence errors */ }

            // 타이머 정리
            CleanupTimers();

            // 이제 X는 앱 종료
            Application.Current.Shutdown();
        }

        private void CleanupTimers()
        {
            try
            {
                _hideTitleTimer?.Stop();
                _topmostCheckTimer?.Stop();
            }
            catch { /* 정리 중 오류 무시 */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 창이 닫힐 때 타이머 정리
            CleanupTimers();
            base.OnClosed(e);
        }
        
        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            AreaCaptureRequested?.Invoke(this, EventArgs.Empty);
            // 캡처 후 자동으로 클립보드에 복사
            System.Threading.Thread.Sleep(100); // 캡처가 완료될 때까지 잠시 대기
            ShowCopiedNotification();
        }
        
        private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            FullScreenCaptureRequested?.Invoke(this, EventArgs.Empty);
            // 캡처 후 자동으로 클립보드에 복사
            System.Threading.Thread.Sleep(100); // 캡처가 완료될 때까지 잠시 대기
            ShowCopiedNotification();
        }

        private void DesignatedButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            DesignatedCaptureRequested?.Invoke(this, EventArgs.Empty);
            System.Threading.Thread.Sleep(100);
            ShowCopiedNotification();
        }
        
        private void ExitSimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
            this.Close();
        }
        
        private void ShowCopiedNotification()
        {
            // 클립보드 복사 알림 표시 (더 짧게 표시: 0.4s)
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(0.4));
            notification.Owner = this;
            Show(); // 다시 표시
            notification.Show();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        // Delay capture icon: open menu and run delayed area capture
        private void DelayIconButton_Click(object sender, RoutedEventArgs e)
        {
            // 버튼 바로 아래에 컨텍스트 메뉴가 열리도록 강제 설정
            if (DelayContextMenu != null && DelayIconButton != null)
            {
                DelayContextMenu.PlacementTarget = DelayIconButton;
                DelayContextMenu.Placement = PlacementMode.Bottom;
                DelayContextMenu.HorizontalOffset = 0;
                DelayContextMenu.VerticalOffset = 4;
                DelayContextMenu.IsOpen = true;
            }
        }

        private void DelayOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out var secs))
            {
                _delaySeconds = secs;
                StartDelayedAreaCapture(_delaySeconds);
            }
        }

        private void StartDelayedAreaCapture(int seconds)
        {
            // 일반모드와 동일하게 카운트 스티커(GuideWindow) 표시 후 캡처 시작
            var countdown = new GuideWindow("", null)
            {
                Owner = this
            };
            countdown.Show();
            countdown.StartCountdown(seconds, () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Hide();
                    AreaCaptureRequested?.Invoke(this, EventArgs.Empty);
                    System.Threading.Thread.Sleep(100);
                    ShowCopiedNotification();
                });
            });
        }

        private void SimpleModeWindow_Deactivated(object? sender, EventArgs e)
        {
            // 더 강력한 방법으로 최상위 유지 - 즉시 실행
            try
            {
                if (WindowState != WindowState.Minimized && IsVisible)
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        // 먼저 TOPMOST 해제 후 다시 설정 (Z-order 새로고침)
                        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                        
                        // 추가로 BringWindowToTop 호출
                        BringWindowToTop(hwnd);
                    }
                }
            }
            catch { /* 오류 무시 */ }
            
            // 추가로 지연된 재확인도 수행
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ForceTopmost();
            }), DispatcherPriority.Background);
        }

        private void SimpleModeWindow_Activated(object? sender, EventArgs e)
        {
            // 활성화될 때도 최상위 상태 확인
            ForceTopmost();
        }

        private void ForceTopmost()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch { /* 오류 무시 */ }
        }

        private void AggressiveForceTopmost()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    // 더 적극적인 방법: 여러 단계로 최상위 설정
                    
                    // 1단계: TOPMOST 해제
                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    
                    // 2단계: 창을 맨 위로 가져오기
                    BringWindowToTop(hwnd);
                    
                    // 3단계: TOPMOST 재설정
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    
                    // 4단계: WPF 속성도 동기화
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (IsVisible && WindowState != WindowState.Minimized)
                        {
                            Topmost = false;
                            Topmost = true;
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch { /* 오류 무시 */ }
        }

        private void SimpleModeWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+M: exit Simple Mode (toggle back to normal mode)
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
                this.Close();
                e.Handled = true;
            }
        }
    }
}