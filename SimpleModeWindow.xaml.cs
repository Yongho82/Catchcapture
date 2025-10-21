using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;
using System.Windows.Threading;

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        private Point lastPosition;
        private int _delaySeconds = 3; // default delay
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        public event EventHandler? DesignatedCaptureRequested;
        
        public SimpleModeWindow()
        {
            InitializeComponent();
            
            // 화면 좌측 상단에 위치
            PositionWindow();
        }
        
        private void PositionWindow()
        {
            // 좌측 상단으로 위치 설정
            Left = 10;
            Top = 10;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastPosition = e.GetPosition(this);
            DragMove();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 프로그램 종료 대신 메인 프로그램으로 복귀
            ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
            this.Close();
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
            // 클립보드 복사 알림 표시
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(1.5));
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
            // Toggle context menu
            if (DelayContextMenu != null)
            {
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
            Hide();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, e) =>
            {
                (s as DispatcherTimer)?.Stop();
                AreaCaptureRequested?.Invoke(this, EventArgs.Empty);
                System.Threading.Thread.Sleep(100);
                ShowCopiedNotification();
            };
            timer.Start();
        }
    }
}