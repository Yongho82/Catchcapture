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
            SizeToContent = SizeToContent.Height; // Width is fixed
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            Width = 320; // Slightly wider for ShareX style

            // ShareX Style: Dark background
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), // Dark gray
                CornerRadius = new CornerRadius(0), // ShareX is usually rectangular or slightly rounded. Let's go with slightly rounded.
                Padding = new Thickness(15),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect 
                { 
                    BlurRadius = 10, 
                    ShadowDepth = 5, 
                    Opacity = 0.4,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Accent bar width
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // content

            // Accent Bar
            var accentBar = new Rectangle
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                Fill = new SolidColorBrush(Color.FromRgb(0, 122, 204)), // VS Blue / ShareX Blue-ish
                Margin = new Thickness(0, -15, 0, -15) // Stretch vertically to cover padding
            };
            Grid.SetColumn(accentBar, 0);

            // Content Stack (Title + Message)
            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(contentStack, 1);

            // Title "Catch Capture"
            var titleBlock = new TextBlock
            {
                Text = "Catch Capture",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4) // Spacing below title
            };

            // Message Body
            var messageBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)), // Slightly off-white for readability
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            
            contentStack.Children.Add(titleBlock);
            contentStack.Children.Add(messageBlock);
            
            grid.Children.Add(accentBar);
            grid.Children.Add(contentStack);

            border.Child = grid;
            Content = border;

            // Positioning Logic: Bottom Right (Tray Area)
            this.Loaded += (s, e) =>
            {
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Bottom - this.ActualHeight - 10;
                
                // Slide Up / Fade In Animation
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25));
                var slideUp = new System.Windows.Media.Animation.DoubleAnimation(this.Top + 20, this.Top, TimeSpan.FromSeconds(0.25))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                
                this.BeginAnimation(OpacityProperty, fadeIn);
                this.BeginAnimation(TopProperty, slideUp);
            };

            // Auto close timer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.0) };
            timer.Tick += (s, e) => 
            {
                timer.Stop();
                // Fade Out / Drop Down
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                // Optional: Slide down
                var slideDown = new System.Windows.Media.Animation.DoubleAnimation(this.Top, this.Top + 20, TimeSpan.FromSeconds(0.3));
                
                fadeOut.Completed += (s2, e2) => Close();
                this.BeginAnimation(OpacityProperty, fadeOut);
                this.BeginAnimation(TopProperty, slideDown);
            };
            timer.Start();
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
