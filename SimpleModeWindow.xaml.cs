using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using CatchCapture.Models;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        // Win32 API
        private const int HWND_TOPMOST = -1;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private enum DockSide { None, Left, Right, Top }
        private DockSide _dockSide = DockSide.None;
        private bool _isCollapsed = false;
        private DispatcherTimer _collapseTimer;
        
        private int _delaySeconds = 3;
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        public event EventHandler? DesignatedCaptureRequested;
        
        public SimpleModeWindow()
        {
            InitializeComponent();
            
            Topmost = true;
            ShowInTaskbar = true;
            
            // 초기 상태: 가로 모드
            ApplyLayout(false);

            _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _collapseTimer.Tick += CollapseTimer_Tick;

            this.Loaded += (_, __) => ForceTopmost();
            this.Deactivated += (_, __) => 
            {
                if (_dockSide != DockSide.None && !_isCollapsed)
                    _collapseTimer.Start();
                ForceTopmost();
            };
            this.Activated += (_, __) => ForceTopmost();
            
            this.MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);

                _dockSide = DockSide.None;
                _isCollapsed = false;
                _collapseTimer.Stop();
                
                try 
                { 
                    DragMove(); 
                    CheckDocking();
                    Dispatcher.BeginInvoke(new Action(CheckDocking), DispatcherPriority.ApplicationIdle);
                } 
                catch { }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CheckDocking();
            Dispatcher.BeginInvoke(new Action(CheckDocking), DispatcherPriority.ApplicationIdle);
        }

        private void CheckDocking()
        {
            var workArea = SystemParameters.WorkArea;
            double snapDist = 80;

            double currentLeft = Left;
            double currentTop = Top;
            double currentRight = currentLeft + ActualWidth;

            // 좌측 도킹
            if (Math.Abs(currentLeft - workArea.Left) < snapDist)
            {
                ApplyLayout(true); // 세로 모드
                UpdateLayout();
                Left = workArea.Left;
                _dockSide = DockSide.Left;
            }
            // 우측 도킹
            else if (Math.Abs(currentRight - workArea.Right) < snapDist)
            {
                ApplyLayout(true); // 세로 모드
                UpdateLayout();
                // 너비가 변경되었으므로 Width 속성을 사용해 위치 재계산
                Left = workArea.Right - this.Width;
                _dockSide = DockSide.Right;
            }
            // 상단 도킹
            else if (Math.Abs(currentTop - workArea.Top) < snapDist)
            {
                ApplyLayout(false); // 가로 모드
                UpdateLayout();
                Top = workArea.Top;
                _dockSide = DockSide.Top;
            }
            else
            {
                _dockSide = DockSide.None;
                ApplyLayout(false); // 도킹 해제 시 가로 모드
            }

            if (_dockSide != DockSide.None)
            {
                // 도킹 시 화면 밖으로 나가지 않도록 보정
                if (_dockSide == DockSide.Left || _dockSide == DockSide.Right)
                {
                    if (Top < workArea.Top) Top = workArea.Top;
                    if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
                }
                else if (_dockSide == DockSide.Top)
                {
                    if (Left < workArea.Left) Left = workArea.Left;
                    if (Left + Width > workArea.Right) Left = workArea.Right - Width;
                }

                if (!IsMouseOver) _collapseTimer.Start();
            }
        }

        private void ApplyLayout(bool vertical)
        {
            if (vertical)
            {
                HorizontalRoot.Visibility = Visibility.Collapsed;
                VerticalRoot.Visibility = Visibility.Visible;
                Width = 50;
                Height = 230;
            }
            else
            {
                HorizontalRoot.Visibility = Visibility.Visible;
                VerticalRoot.Visibility = Visibility.Collapsed;
                Width = 200;
                Height = 85;
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _collapseTimer.Stop();
            if (_dockSide != DockSide.None && _isCollapsed)
            {
                Expand();
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if ((DelayMenuH != null && DelayMenuH.IsOpen) || (DelayMenuV != null && DelayMenuV.IsOpen)) return;

            if (_dockSide != DockSide.None && !_isCollapsed)
            {
                _collapseTimer.Start();
            }
        }

        private void CollapseTimer_Tick(object? sender, EventArgs e)
        {
            _collapseTimer.Stop();
            if (IsMouseOver) return;
            if ((DelayMenuH != null && DelayMenuH.IsOpen) || (DelayMenuV != null && DelayMenuV.IsOpen)) return;
            
            Collapse();
        }

        private void Collapse()
        {
            if (_dockSide == DockSide.None) return;
            _isCollapsed = true;
            
            double targetLeft = Left;
            double targetTop = Top;
            double peekAmount = 8;

            if (_dockSide == DockSide.Left) targetLeft = -ActualWidth + peekAmount;
            else if (_dockSide == DockSide.Right) targetLeft = SystemParameters.WorkArea.Right - peekAmount;
            else if (_dockSide == DockSide.Top) targetTop = -ActualHeight + peekAmount;

            AnimateWindow(targetLeft, targetTop);
        }

        private void Expand()
        {
            if (_dockSide == DockSide.None) return;
            _isCollapsed = false;

            var workArea = SystemParameters.WorkArea;
            double targetLeft = Left;
            double targetTop = Top;

            if (_dockSide == DockSide.Left) targetLeft = workArea.Left;
            else if (_dockSide == DockSide.Right) targetLeft = workArea.Right - ActualWidth;
            else if (_dockSide == DockSide.Top) targetTop = workArea.Top;

            AnimateWindow(targetLeft, targetTop);
        }

        private void AnimateWindow(double toLeft, double toTop)
        {
            var animLeft = new DoubleAnimation(toLeft, TimeSpan.FromMilliseconds(200)) 
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            var animTop = new DoubleAnimation(toTop, TimeSpan.FromMilliseconds(200)) 
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

            animLeft.Completed += (s, e) => { Left = toLeft; };
            animTop.Completed += (s, e) => { Top = toTop; };
            
            BeginAnimation(LeftProperty, animLeft);
            BeginAnimation(TopProperty, animTop);
        }

        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.SwitchToTrayMode();
            }
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw && mw.notifyIcon != null)
            {
                mw.notifyIcon.Visible = true;
            }
            Hide();
        }

        private void ExitSimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e) => PerformCapture(AreaCaptureRequested);
        private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e) => PerformCapture(FullScreenCaptureRequested);
        private void DesignatedButton_Click(object sender, RoutedEventArgs e) => PerformCapture(DesignatedCaptureRequested);
        
        private void PerformCapture(EventHandler? handler)
        {
            Hide();
            handler?.Invoke(this, EventArgs.Empty);
            System.Threading.Thread.Sleep(100);
            ShowCopiedNotification();
        }

        private void ShowCopiedNotification()
        {
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(0.4));
            notification.Owner = this;
            Show();
            notification.Show();
        }

        private void DelayIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn)
            {
                if (btn.Name == "DelayBtnH" && DelayMenuH != null)
                {
                    DelayMenuH.PlacementTarget = btn;
                    DelayMenuH.IsOpen = true;
                }
                else if (btn.Name == "DelayBtnV" && DelayMenuV != null)
                {
                    DelayMenuV.PlacementTarget = btn;
                    DelayMenuV.IsOpen = true;
                }
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
            var countdown = new GuideWindow("", null) { Owner = this };
            countdown.Show();
            countdown.StartCountdown(seconds, () => Dispatcher.Invoke(() => PerformCapture(AreaCaptureRequested)));
        }

        private void ForceTopmost()
        {
            try {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            } catch { }
        }

        private void SimpleModeWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ExitSimpleModeButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}