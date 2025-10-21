using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

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
            
            // 위치는 호출자가 지정 (MainWindow에서 설정)
            // PositionWindow();
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
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
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
            // 클립보드 복사 알림 표시 (짧게 표시)
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(0.7));
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
    }
}