using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace CatchCapture.Recording
{
    public partial class RecordingInfoOverlay : Window
    {
        [DllImport("user32.dll")]
        private static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        public RecordingInfoOverlay()
        {
            InitializeComponent();
            this.Loaded += RecordingInfoOverlay_Loaded;
        }

        private void RecordingInfoOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            var interop = new WindowInteropHelper(this);
            bool result = SetWindowDisplayAffinity(interop.Handle, WDA_EXCLUDEFROMCAPTURE) != 0;
            if (!result)
            {
                // Fallback to WDA_MONITOR (1) if modern exclude fails
                SetWindowDisplayAffinity(interop.Handle, 0x00000001);
            }
            
            System.Diagnostics.Debug.WriteLine($"RecordingInfoOverlay Affinity Result: {result}");

            // 전체 화면 크기로 설정 (RecordingOverlay와 동일하게)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        public void UpdateFileSizeText(string text, Rect selectionArea)
        {
            if (FileSizeText == null) return;
            
            FileSizeText.Text = text;
            FileSizeText.Visibility = Visibility.Visible;

            // 위치 조정: 선택 영역 우측 상단 10px 안쪽
            if (selectionArea.Width > 0 && selectionArea.Height > 0)
            {
                FileSizeText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(FileSizeText, selectionArea.Right - FileSizeText.DesiredSize.Width);
                Canvas.SetTop(FileSizeText, Math.Max(5, selectionArea.Top - 25));
            }
        }
        
        public void HideFileSize()
        {
            if (FileSizeText != null)
                FileSizeText.Visibility = Visibility.Collapsed;
        }
    }
}
