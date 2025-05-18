using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace CatchCapture.Utilities
{
    public class GuideWindow : Window
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public GuideWindow(string message, TimeSpan? duration = null)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            BorderThickness = new Thickness(0);

            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(20, 15, 20, 15);

            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(messageBlock);

            if (message.Contains("ESC"))
            {
                TextBlock escBlock = new TextBlock
                {
                    Text = "ESC 키를 눌러 취소",
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 9,
                    Margin = new Thickness(0, 5, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panel.Children.Add(escBlock);
            }

            Content = panel;

            Loaded += (s, e) =>
            {
                CenterWindowOnScreen();
                MakeWindowRounded();
            };

            if (duration.HasValue)
            {
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = duration.Value
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    Close();
                };
                timer.Start();
            }
        }

        private void CenterWindowOnScreen()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = (screenWidth - ActualWidth) / 2;
            Top = (screenHeight - ActualHeight) / 2;
        }

        private void MakeWindowRounded()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION);

            try
            {
                IntPtr region = CreateRoundRectRgn(0, 0, (int)ActualWidth, (int)ActualHeight, 10, 10);
                var hwndSource = HwndSource.FromHwnd(hwnd);
                if (hwndSource?.CompositionTarget != null)
                {
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }
                Win32.SetWindowRgn(hwnd, region, true);
            }
            catch (Exception)
            {
                // 라운드 처리 실패 시 무시
            }
        }
    }

    internal class Win32
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    }
} 