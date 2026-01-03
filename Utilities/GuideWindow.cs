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
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private const int VK_ESCAPE = 0x1B;

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private TextBlock _messageBlock;
        private TextBlock? _escBlock;
        private System.Windows.Threading.DispatcherTimer? _countdownTimer;
        private System.Windows.Threading.DispatcherTimer? _keyCheckTimer;
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
            panel.Margin = new Thickness(20, 12, 20, 12); // 여백 줄이기

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
                // 창이 뜨자마자 포커스를 가져오도록 시도
                try { this.Activate(); this.Focus(); } catch { }
            };

            this.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    CancelCountdown();
                }
            };
            // 창 크기 변경 시에도 둥근 모서리 다시 적용
            SizeChanged += (s, e) =>
            {
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
            
            // ESC 안내 문구 강제 추가/업데이트
            if (_escBlock == null)
            {
                _escBlock = new TextBlock
                {
                    Text = "ESC 키를 눌러 취소",
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 9,
                    Margin = new Thickness(0, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ((StackPanel)Content).Children.Add(_escBlock);
            }
            _escBlock.Visibility = Visibility.Visible;

            // 숫자 스타일 설정
            _messageBlock.FontSize = 36;
            _messageBlock.FontWeight = FontWeights.Bold;
            _messageBlock.Foreground = Brushes.White;
            _messageBlock.Text = _remainingSeconds.ToString();
            
            _countdownTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += (s, e) =>
            {
                _remainingSeconds--;
                if (_remainingSeconds <= 0)
                {
                    StopTimers();
                    this.Hide();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { onCompleted?.Invoke(); } catch { }
                        Close();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    _messageBlock.Text = _remainingSeconds.ToString();
                }
            };
            _countdownTimer.Start();

            // 전역 키 체크 타이머 (포커스 잃어도 ESC 감지)
            _keyCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _keyCheckTimer.Tick += (s, e) =>
            {
                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                {
                    CancelCountdown();
                }
            };
            _keyCheckTimer.Start();
        }

        private void CancelCountdown()
        {
            StopTimers();
            this.Close();
        }

        private void StopTimers()
        {
            _countdownTimer?.Stop();
            _keyCheckTimer?.Stop();
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
            if (hwnd == IntPtr.Zero) return; // 핸들이 없으면 리턴
            
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION);

            try
            {
                // 현재 창 크기 가져오기
                int width = (int)Math.Ceiling(this.ActualWidth);
                int height = (int)Math.Ceiling(this.ActualHeight);
                
                // 이전 Region 해제 (메모리 누수 방지)
                Win32.SetWindowRgn(hwnd, IntPtr.Zero, false);
                
                // 새 Region 생성 (오른쪽/하단 경계 포함을 위해 +1)
                IntPtr region = CreateRoundRectRgn(0, 0, width + 1, height + 1, 10, 10);
                
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