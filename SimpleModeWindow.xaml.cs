using System;
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
        private Point lastPosition;
        private int _delaySeconds = 3; // default delay
        private bool _verticalMode;
        private readonly DispatcherTimer _hideTitleTimer = new DispatcherTimer();
        private bool _isMouseInside;
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        public event EventHandler? DesignatedCaptureRequested;
        
        public SimpleModeWindow()
        {
            InitializeComponent();
            
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
                try { this.Focus(); Keyboard.Focus(this); } catch { }
            };

            // Keep always-on-top even after losing focus (e.g., clicking taskbar)
            this.Deactivated += SimpleModeWindow_Deactivated;

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

            // 이제 X는 앱 종료
            Application.Current.Shutdown();
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
            // Reassert Topmost to keep window above normal windows (taskbar still stays above)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Topmost = false; // toggle to refresh z-order
                Topmost = true;
            }), DispatcherPriority.ApplicationIdle);
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