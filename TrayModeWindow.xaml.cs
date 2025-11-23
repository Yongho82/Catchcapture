using System;
using System.Windows;
using System.Windows.Input;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class TrayModeWindow : Window
    {
        private MainWindow mainWindow;

        public TrayModeWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            
            // 창 위치 설정
            PositionWindow();
        }

        private void PositionWindow()
        {
            // 주 모니터의 작업 영역 가져오기
            var workArea = SystemParameters.WorkArea;
            
            // 우측 하단에 위치 (트레이 근처)
            this.Left = workArea.Right - this.Width - 10;
            this.Top = workArea.Bottom - this.Height - 10;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 항상 위가 비활성화되어 있을 때만 창 밖 클릭 시 숨기기
            if (!this.Topmost)
            {
                this.Hide();
            }
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            // 항상 위 토글
            this.Topmost = !this.Topmost;
            UpdateTopmostIcon();
        }

        private void UpdateTopmostIcon()
        {
            // 아이콘 투명도 업데이트 (활성화 시 진하게, 비활성화 시 연하게)
            if (this.Topmost)
            {
                TopmostIcon.Opacity = 1.0; // 진한 회색 (활성)
            }
            else
            {
                TopmostIcon.Opacity = 0.3; // 연한 회색 (비활성)
            }
        }

        public void UpdateCaptureCount(int count)
        {
            CaptureCountText.Text = count.ToString();
            
            // 간단한 스케일 애니메이션
            if (count > 0)
            {
                var scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                CaptureCounterButton.RenderTransform = scaleTransform;
                CaptureCounterButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.3,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
            }
        }

        private void CaptureCounterButton_Click(object sender, RoutedEventArgs e)
        {
            // 일반 모드로 전환하여 캡처 목록 확인
            this.Hide();
            mainWindow.SwitchToNormalMode();
        }

        // 캡처 버튼 이벤트 핸들러들
        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerAreaCapture();
        }

        private void DelayCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerDelayCapture();
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerFullScreenCapture();
        }

        private void DesignatedCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerDesignatedCapture();
        }

        private void WindowCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerWindowCapture();
        }

        private void UnitCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerUnitCapture();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerCopySelected();
        }

        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerCopyAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerSaveSelected();
        }

        private void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerSaveAll();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerDeleteSelected();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerDeleteAll();
        }

        private void SimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerSimpleMode();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerSettings();
        }
    }
}
