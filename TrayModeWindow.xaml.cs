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
            // 창 밖 클릭 시 숨기기
            this.Hide();
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
