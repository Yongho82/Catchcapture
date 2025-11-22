using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture
{
    public class ScrollCaptureWindow : Window
    {
        // --- Win32 API ---
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

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

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private const byte VK_NEXT = 0x22; // Page Down
        private const uint KEYEVENTF_KEYUP = 0x0002;

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

        // Fields
        private System.Windows.Shapes.Rectangle highlightRect;
        private IntPtr currentTargetElement = IntPtr.Zero;
        public BitmapSource? CapturedImage { get; private set; }

        public ScrollCaptureWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            var canvas = new Canvas();
            highlightRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
                StrokeThickness = 4,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 191, 255)),
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(highlightRect);
            Content = canvas;

            _proc = HookCallback;
            Loaded += ScrollCaptureWindow_Loaded;
            Closed += ScrollCaptureWindow_Closed;
        }

        private void ScrollCaptureWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 창을 클릭 투과(Transparent) 상태로 만듦 -> WindowFromPoint가 통과함
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // 2. 마우스 후크 설치
            try
            {
                _hookID = SetHook(_proc);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"후킹 설정 실패: {ex.Message}");
            }
            
            this.Activate();
        }

        private void ScrollCaptureWindow_Closed(object? sender, EventArgs e)
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
                    UpdateHighlight(hookStruct.pt);
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    if (currentTargetElement != IntPtr.Zero)
                    {
                        Dispatcher.BeginInvoke(new Action(async () => 
                        {
                            // 후킹 해제
                            UnhookWindowsHookEx(_hookID);
                            _hookID = IntPtr.Zero;

                            highlightRect.Visibility = Visibility.Collapsed;
                            this.Opacity = 0;
                            
                            await Task.Delay(200);
                            
                            await PerformScrollCapture(currentTargetElement);
                            
                            DialogResult = CapturedImage != null;
                            Close();
                        }));

                        return (IntPtr)1;
                    }
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    Dispatcher.Invoke(() => {
                        DialogResult = false;
                        Close();
                    });
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void UpdateHighlight(POINT pt)
        {
            IntPtr hWnd = WindowFromPoint(pt);
            if (hWnd == IntPtr.Zero) return;

            if (hWnd == currentTargetElement) return;

            RECT rect;
            if (!GetWindowRect(hWnd, out rect)) return;

            currentTargetElement = hWnd;
            
            Dispatcher.Invoke(() =>
            {
                var topLeft = PointFromScreen(new System.Windows.Point(rect.Left, rect.Top));
                var bottomRight = PointFromScreen(new System.Windows.Point(rect.Right, rect.Bottom));

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

        private async Task PerformScrollCapture(IntPtr hWnd)
        {
            var screenshots = new List<Bitmap>();
            
            try
            {
                // Win32 핸들로 AutomationElement 생성
                var element = AutomationElement.FromHandle(hWnd);
                if (element == null) return;

                // 스크롤 패턴 찾기 (현재 요소 -> 부모 -> 자식 순으로 탐색)
                ScrollPattern? scrollPattern = null;
                object patternObj;
                
                // 1. 현재 요소에서 확인
                if (element.TryGetCurrentPattern(ScrollPattern.Pattern, out patternObj))
                {
                    scrollPattern = (ScrollPattern)patternObj;
                }
                
                // 2. 부모에서 확인 (컨테이너가 스크롤을 담당하는 경우)
                if (scrollPattern == null)
                {
                    var walker = TreeWalker.ControlViewWalker;
                    var parent = walker.GetParent(element);
                    while (parent != null && parent != AutomationElement.RootElement)
                    {
                        if (parent.TryGetCurrentPattern(ScrollPattern.Pattern, out patternObj))
                        {
                            scrollPattern = (ScrollPattern)patternObj;
                            break;
                        }
                        parent = walker.GetParent(parent);
                    }
                }

                // 3. 자식에서 확인 (가장 중요! 브라우저 콘텐츠 영역은 자식임)
                if (scrollPattern == null)
                {
                    // 첫 번째 자식부터 깊이 우선 탐색 (너무 깊지 않게)
                    scrollPattern = FindScrollPatternInChildren(element);
                }

                RECT rect;
                GetWindowRect(hWnd, out rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                // 1. 첫 화면 캡처
                screenshots.Add(CaptureRegion(rect.Left, rect.Top, width, height));

                if (scrollPattern != null && scrollPattern.Current.VerticallyScrollable)
                {
                    int maxScrolls = 20;

                    // 1. 마우스 커서를 창 중앙으로 이동 및 클릭하여 포커스 확보
                    int centerX = rect.Left + width / 2;
                    int centerY = rect.Top + height / 2;
                    SetCursorPos(centerX, centerY);
                    
                    // 클릭 (포커스 잡기)
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(200);

                    for (int i = 0; i < maxScrolls; i++)
                    {
                        // 스크롤 다운 (마우스 휠 사용)
                        try
                        {
                            // 휠을 적당히 굴림 (120 * 3.3 = 400)
                            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)-400), UIntPtr.Zero);
                        }
                        catch
                        {
                            break; 
                        }
                        
                        await Task.Delay(800); // 렌더링 대기

                        // 캡처
                        var newShot = CaptureRegion(rect.Left, rect.Top, width, height);

                        // 이전 화면과 비교 (스크롤이 안 되었으면 중단)
                        if (screenshots.Count > 0)
                        {
                            var lastShot = screenshots[screenshots.Count - 1];
                            if (AreBitmapsIdentical(lastShot, newShot))
                            {
                                newShot.Dispose();
                                break; // 스크롤이 안 됨 -> 종료
                            }
                        }

                        screenshots.Add(newShot);
                    }
                }

                CapturedImage = MergeScreenshots(screenshots);
                foreach (var s in screenshots) s.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"캡처 오류: {ex.Message}");
            }
        }

        private ScrollPattern? FindScrollPatternInChildren(AutomationElement element)
        {
            try
            {
                // Condition: ScrollPattern이 지원되는 요소 찾기
                var condition = new PropertyCondition(AutomationElement.IsScrollPatternAvailableProperty, true);
                var child = element.FindFirst(TreeScope.Descendants, condition);

                if (child != null)
                {
                    object patternObj;
                    if (child.TryGetCurrentPattern(ScrollPattern.Pattern, out patternObj))
                    {
                        return (ScrollPattern)patternObj;
                    }
                }
            }
            catch { }
            return null;
        }

        private Bitmap CaptureRegion(int x, int y, int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            }
            return bmp;
        }

        private bool AreBitmapsIdentical(Bitmap bmp1, Bitmap bmp2)
        {
            if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height) return false;

            try
            {
                // 1. 중앙 픽셀 비교
                int centerX = bmp1.Width / 2;
                int centerY = bmp1.Height / 2;
                if (bmp1.GetPixel(centerX, centerY) != bmp2.GetPixel(centerX, centerY)) return false;

                // 2. 5군데 샘플링 비교
                if (bmp1.GetPixel(10, 10) != bmp2.GetPixel(10, 10)) return false;
                if (bmp1.GetPixel(bmp1.Width - 10, bmp1.Height - 10) != bmp2.GetPixel(bmp1.Width - 10, bmp1.Height - 10)) return false;

                // 3. 전체 스캔 (100픽셀 간격)
                for (int y = 0; y < bmp1.Height; y += 100)
                {
                    for (int x = 0; x < bmp1.Width; x += 100)
                    {
                        if (bmp1.GetPixel(x, y) != bmp2.GetPixel(x, y)) return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private BitmapSource MergeScreenshots(List<Bitmap> screenshots)
        {
            if (screenshots.Count == 0) return null!;
            if (screenshots.Count == 1) return ConvertBitmapToBitmapSource(screenshots[0]);

            // 첫 번째 이미지부터 시작
            Bitmap finalImage = screenshots[0];

            for (int i = 1; i < screenshots.Count; i++)
            {
                // 다음 이미지와 합치기 (중복 제거 포함)
                finalImage = StitchImages(finalImage, screenshots[i]);
            }

            var result = ConvertBitmapToBitmapSource(finalImage);
            finalImage.Dispose();
            return result;
        }

        private Bitmap StitchImages(Bitmap top, Bitmap bottom)
        {
            // "Top-Matching 스티칭" (사용자 제안 방식)
            // 새 이미지(Bottom)의 상단 특정 라인(Probe)을 이전 이미지(Top)의 하단에서 찾습니다.
            // 장점: 이전 이미지의 하단(푸터 등)을 자연스럽게 잘라내고, 새 이미지의 깨끗한 내용으로 이을 수 있습니다.

            // 1. Probe 라인 설정 (새 이미지의 상단에서 헤더를 피한 위치)
            int headerAvoidance = 150; // 상단 고정 헤더 회피용
            if (bottom.Height <= headerAvoidance) headerAvoidance = 0;
            
            int probeY_in_Bottom = headerAvoidance; 
            int matchY_in_Top = -1;

            // 2. 이전 이미지(Top)의 하단에서부터 위로 탐색
            // Top의 바닥부터 위로 훑으면서, Bottom의 probeY 라인과 일치하는 곳을 찾음
            int searchLimit = top.Height / 2; // Top의 하단 50%만 검색
            
            for (int y = top.Height - 1; y >= top.Height - searchLimit; y--)
            {
                if (IsRowMatch(top, y, bottom, probeY_in_Bottom))
                {
                    // 일치 후보 발견! 주변 라인 검증
                    bool fullMatch = true;
                    int checkRange = 10;
                    for (int k = 1; k < checkRange; k++)
                    {
                        if (!IsRowMatch(top, y + k, bottom, probeY_in_Bottom + k) ||
                            !IsRowMatch(top, y - k, bottom, probeY_in_Bottom - k))
                        {
                            fullMatch = false;
                            break;
                        }
                    }

                    if (fullMatch)
                    {
                        matchY_in_Top = y;
                        break; // 가장 아래쪽(최신) 매칭을 찾으면 중단
                    }
                }
            }

            if (matchY_in_Top != -1)
            {
                // 매칭 성공!
                // Top의 matchY 지점이 Bottom의 probeY 지점과 같음.
                // 즉, Top은 matchY까지만 유효하고, 그 이후는 Bottom의 probeY부터 시작하는 내용으로 대체해야 함.
                
                // Top을 matchY까지 자름 (푸터 제거 효과)
                // Bottom을 probeY부터 붙임 (헤더 제거 효과)
                
                int topCutHeight = matchY_in_Top;
                int bottomStartY = probeY_in_Bottom;
                int bottomContentHeight = bottom.Height - bottomStartY;

                int newTotalHeight = topCutHeight + bottomContentHeight;
                var result = new Bitmap(top.Width, newTotalHeight);

                using (var g = Graphics.FromImage(result))
                {
                    // Top 그리기 (0 ~ matchY)
                    var topSrcRect = new System.Drawing.Rectangle(0, 0, top.Width, topCutHeight);
                    var topDestRect = new System.Drawing.Rectangle(0, 0, top.Width, topCutHeight);
                    g.DrawImage(top, topDestRect, topSrcRect, GraphicsUnit.Pixel);

                    // Bottom 그리기 (probeY ~ 끝)
                    var bottomSrcRect = new System.Drawing.Rectangle(0, bottomStartY, bottom.Width, bottomContentHeight);
                    var bottomDestRect = new System.Drawing.Rectangle(0, topCutHeight, bottom.Width, bottomContentHeight);
                    g.DrawImage(bottom, bottomDestRect, bottomSrcRect, GraphicsUnit.Pixel);
                }

                top.Dispose();
                return result;
            }
            else
            {
                // 매칭 실패: 그냥 이어붙임 (중복 발생 가능성 있음)
                int newHeight = top.Height + bottom.Height;
                var result = new Bitmap(Math.Max(top.Width, bottom.Width), newHeight);
                
                using (var g = Graphics.FromImage(result))
                {
                    g.DrawImage(top, 0, 0);
                    g.DrawImage(bottom, 0, top.Height);
                }
                
                top.Dispose();
                return result;
            }
        }

        private bool IsRowMatch(Bitmap bmp1, int y1, Bitmap bmp2, int y2)
        {
            if (y1 < 0 || y1 >= bmp1.Height || y2 < 0 || y2 >= bmp2.Height) return false;

            // 스크롤바 및 테두리 무시: 중앙 80%만 비교
            int width = Math.Min(bmp1.Width, bmp2.Width);
            int startX = (int)(width * 0.1); // 왼쪽 10% 무시
            int endX = (int)(width * 0.9);   // 오른쪽 10% (스크롤바) 무시

            for (int x = startX; x < endX; x += 10) // 10픽셀 간격 샘플링
            {
                var c1 = bmp1.GetPixel(x, y1);
                var c2 = bmp2.GetPixel(x, y2);
                
                if (Math.Abs(c1.R - c2.R) > 15 || Math.Abs(c1.G - c2.G) > 15 || Math.Abs(c1.B - c2.B) > 15)
                {
                    return false;
                }
            }
            return true;
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
