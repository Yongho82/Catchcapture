using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace CatchCapture.Utilities
{
    public class StickerWindow : Window
    {
        public StickerWindow(string message)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)), // Semi-transparent dark
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 10, 20, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect 
                { 
                    BlurRadius = 10, 
                    ShadowDepth = 3, 
                    Opacity = 0.5 
                }
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = textBlock;
            Content = border;

            // Ensure Window is created
            Width = 300; // Min width? No, Auto is better but maybe safer to have content constraints
            
            // Positioning Logic:
            // CenterScreen works on Primary Screen mostly. 
            // Better to center on the Mouse or the Active Window screen.
            
            // Try to center on mouse screen
            try 
            {
                // Get mouse position
                var mousePt = GetMousePosition();
                // Simple centering around mouse or offset
                Left = mousePt.X - 100; // Approximate centering if width is ~200
                Top = mousePt.Y + 20;   // Below mouse
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
            catch 
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Auto close timer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
            timer.Tick += (s, e) => 
            {
                timer.Stop();
                var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
                anim.Completed += (s2, e2) => Close();
                BeginAnimation(OpacityProperty, anim);
            };
            timer.Start();

            // Fade In
            BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2)));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static Point GetMousePosition()
        {
             GetCursorPos(out POINT pt);
             return new Point(pt.X, pt.Y);
        }

        public static void Show(string message)
        {
            // Run on UI thread
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    var win = new StickerWindow(message);
                    win.Show();
                });
            }
        }
    }
}
