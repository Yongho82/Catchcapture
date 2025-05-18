using System;
using System.Windows;
using System.Windows.Input;

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        private Point lastPosition;
        
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        
        public SimpleModeWindow()
        {
            InitializeComponent();
            
            // 화면 오른쪽 아래에 위치
            PositionWindow();
        }
        
        private void PositionWindow()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            
            Left = screenWidth - Width - 20;
            Top = screenHeight - Height - 50;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastPosition = e.GetPosition(this);
            DragMove();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            AreaCaptureRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            FullScreenCaptureRequested?.Invoke(this, EventArgs.Empty);
        }
    }
} 