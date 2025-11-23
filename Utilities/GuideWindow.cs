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

        private TextBlock _messageBlock;
        private System.Windows.Threading.DispatcherTimer? _countdownTimer;
        private int _remainingSeconds;

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
            panel.Margin = new Thickness(24, 22, 24, 62); // 하단 여백 증가로 숫자 클리핑 방지

            _messageBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextOptions.SetTextRenderingMode(_messageBlock, TextRenderingMode.ClearType);
            TextOptions.SetTextFormattingMode(_messageBlock, TextFormattingMode.Ideal);
            panel.Children.Add(_messageBlock);

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

        public void UpdateMessage(string message, double? fontSize = null, bool? bold = null)
        {
            _messageBlock.Text = message;
            if (fontSize.HasValue) _messageBlock.FontSize = fontSize.Value;
            if (bold.HasValue) _messageBlock.FontWeight = bold.Value ? FontWeights.Bold : FontWeights.Normal;
        }

        public void StartCountdown(int seconds, Action onCompleted)
        {
            if (seconds <= 0)
            {
                onCompleted?.Invoke();
                return;
            }

            _remainingSeconds = seconds;
            // Make it visually prominent
            _messageBlock.FontSize = 44;
            _messageBlock.FontWeight = FontWeights.Bold;
            _messageBlock.TextWrapping = TextWrapping.NoWrap;
            _messageBlock.TextAlignment = TextAlignment.Center;
            _messageBlock.Foreground = Brushes.White; // 가시성 보장
            // 숫자가 아래로 잘리는 현상 방지: 줄 박스 높이와 마진 조정
            _messageBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            _messageBlock.LineHeight = _messageBlock.FontSize * 1.2; // 약간 여유
            _messageBlock.Margin = new Thickness(0, 6, 0, 12);
            // 미세한 그림자 효과로 대비 향상
            _messageBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 0,
                BlurRadius = 6,
                Opacity = 0.6
            };
            _messageBlock.Text = _remainingSeconds.ToString();
            _messageBlock.SnapsToDevicePixels = true;
            _messageBlock.UseLayoutRounding = true;
            _messageBlock.InvalidateMeasure();
            _messageBlock.InvalidateArrange();
            this.UpdateLayout();

            _countdownTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += (s, e) =>
            {
                _remainingSeconds--;
                if (_remainingSeconds <= 0)
                {
                    _countdownTimer!.Stop();
                    // 먼저 창을 즉시 숨김 (캡처 전에 화면에서 제거)
                    this.Hide();
                    // 약간의 딜레이 후 콜백 실행 (창이 완전히 숨겨질 시간 확보)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { onCompleted?.Invoke(); } catch { }
                        // 콜백 완료 후 창 닫기
                        Close();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    _messageBlock.Foreground = Brushes.White; // 혹시 스타일 간섭 방지
                    _messageBlock.Text = _remainingSeconds.ToString();
                    _messageBlock.InvalidateMeasure();
                    _messageBlock.InvalidateArrange();
                    _messageBlock.InvalidateVisual();
                    this.UpdateLayout();
                }
            };
            _countdownTimer.Start();
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