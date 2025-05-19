using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        private Point lastPosition;
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        
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
            // 프로그램 전체 종료
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
        
        private void ExitSimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void ShowCopiedNotification()
        {
            // 클립보드 복사 알림 표시
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(1.5));
            notification.Owner = this;
            Show(); // 다시 표시
            notification.Show();
        }
    }
} 