using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture.Utilities
{
    public class WindowCaptureOverlay : Window
    {
        // --- Win32 API Definitions ---
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        // Hooking
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // --- Fields ---
        private Rectangle highlightRect;
        private IntPtr currentTargetWindow = IntPtr.Zero;
        public BitmapSource? CapturedImage { get; private set; }

        public WindowCaptureOverlay()
        {
            // 창 설정
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true;
            Background = Brushes.Transparent; // 완전히 투명
            ShowInTaskbar = false;
            
            // 전체 화면 크기
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // UI 구성
            var canvas = new Canvas();
            highlightRect = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(5, 255, 0, 0)), // 아주 연한 붉은 틴트
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(highlightRect);
            Content = canvas;

            // Hook Delegate 유지 (GC 방지)
            _proc = HookCallback;

            Loaded += WindowCaptureOverlay_Loaded;
            Closed += WindowCaptureOverlay_Closed;
        }

        private void WindowCaptureOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 창을 클릭 투과(Transparent) 상태로 만듦
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // 2. 마우스 후크 설치
            _hookID = SetHook(_proc);
            
            // 포커스 확보
            this.Activate();
        }

        private void WindowCaptureOverlay_Closed(object? sender, EventArgs e)
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    // 마우스 이동 시 하이라이트 업데이트
                    UpdateHighlight(hookStruct.pt);
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    // 클릭 시 캡처 수행 및 클릭 이벤트 전파 차단(1 반환)
                    if (currentTargetWindow != IntPtr.Zero)
                    {
                        // 비동기로 캡처 시퀀스 시작
                        Dispatcher.BeginInvoke(new Action(async () => 
                        {
                            // 1. 오버레이 숨기기
                            highlightRect.Visibility = Visibility.Collapsed;
                            this.Opacity = 0; // 창 전체 투명화
                            
                            // 2. 렌더링 갱신을 위해 잠시 대기
                            await System.Threading.Tasks.Task.Delay(100);
                            
                            // 3. 캡처 수행
                            CaptureTargetWindow();
                            
                            // 4. 창 닫기
                            DialogResult = true;
                            Close();
                        }));

                        return (IntPtr)1; // 클릭 무시 (Swallow)
                    }
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    // 우클릭 시 취소
                    Dispatcher.Invoke(() => {
                        DialogResult = false;
                        Close();
                    });
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        private void UpdateHighlight(POINT pt)
        {
            IntPtr hWnd = WindowFromPoint(pt);
            
            if (hWnd == IntPtr.Zero) return;

            // 루트 윈도우 찾기
            IntPtr rootWnd = GetAncestor(hWnd, GA_ROOT);
            if (rootWnd != IntPtr.Zero) hWnd = rootWnd;

            if (hWnd == currentTargetWindow) return;

            RECT rect;
            // 그림자 제외한 실제 영역 가져오기 시도
            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) != 0)
            {
                // 실패하면 일반 영역 가져오기
                if (!GetWindowRect(hWnd, out rect)) return;
            }

            currentTargetWindow = hWnd;
            
            // UI 업데이트는 Dispatcher를 통해
            Dispatcher.Invoke(() =>
            {
                // 좌표 변환
                var topLeft = PointFromScreen(new Point(rect.Left, rect.Top));
                var bottomRight = PointFromScreen(new Point(rect.Right, rect.Bottom));

                double w = bottomRight.X - topLeft.X;
                double h = bottomRight.Y - topLeft.Y;

                if (w > 0 && h > 0)
                {
                    Canvas.SetLeft(highlightRect, topLeft.X);
                    Canvas.SetTop(highlightRect, topLeft.Y);
                    highlightRect.Width = w;
                    highlightRect.Height = h;
                    highlightRect.Visibility = Visibility.Visible;
                }
            });
        }

        private void CaptureTargetWindow()
        {
            if (currentTargetWindow == IntPtr.Zero) return;

            try
            {
                RECT rect;
                // 그림자 제외한 실제 영역 가져오기 시도
                if (DwmGetWindowAttribute(currentTargetWindow, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) != 0)
                {
                    GetWindowRect(currentTargetWindow, out rect);
                }

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0) return;

                var bitmap = new System.Drawing.Bitmap(width, height);
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    // 화면에서 직접 복사
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
                }

                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    CapturedImage = source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch { }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
