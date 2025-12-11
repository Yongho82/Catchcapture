using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatchCapture.Models;

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

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _kbdProc;
        private IntPtr _kbdHookID = IntPtr.Zero;

        // Use distinct externs to avoid delegate overload ambiguity
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "SetWindowsHookExW")]
        private static extern IntPtr SetWindowsHookExMouse(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "SetWindowsHookExW")]
        private static extern IntPtr SetWindowsHookExKeyboard(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

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

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private const byte VK_NEXT = 0x22; // Page Down
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_ESCAPE = 0x1B;

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
        private Border? mouseTooltip;
        private TextBlock? mouseTooltipText;
        private TextBlock? mouseEmoji;
        public bool EscCancelled { get; private set; } = false;
        private bool isCapturing = false; // 캡처 진행 중 여부 추적
        private System.Windows.Controls.Image? customCursor; // 커스텀 커서 이미지

        private static void Log(string message)
        {
            // Debug logging disabled
        }

        public ScrollCaptureWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Cursor = System.Windows.Input.Cursors.None; // 커서 숨김 (커스텀 커서 사용)

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            var canvas = new Canvas();
            highlightRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
                StrokeThickness = 4,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 191, 255)),
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(highlightRect);
            
            // 커스텀 커서 이미지 로드
            try
            {
                var cursorImagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "sc_cursor.png");
                if (System.IO.File.Exists(cursorImagePath))
                {
                    customCursor = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri(cursorImagePath)),
                        Width = 32,
                        Height = 32,
                        Visibility = Visibility.Visible
                    };
                    canvas.Children.Add(customCursor);
                }
            }
            catch { /* 커서 이미지 로드 실패 시 무시 */ }
            
            Content = canvas;

            _proc = HookCallback;
            _kbdProc = KeyboardHookCallback;
            Loaded += ScrollCaptureWindow_Loaded;
            Closed += ScrollCaptureWindow_Closed;
            CatchCapture.Models.LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void ScrollCaptureWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 창을 클릭 투과(Transparent) 상태로 만듦 -> WindowFromPoint가 통과함
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // 2. 마우스/키보드 후크 설치
            try
            {
                _hookID = SetHook(_proc);
                _kbdHookID = SetKeyboardHook(_kbdProc);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"후킹 설정 실패: {ex.Message}");
            }

            this.Activate();
            
            // 마우스 커서 숨김 (Win32 API 사용)
            ShowCursor(false);

            // 마우스 따라다니는 HUD 생성 (이모지 + 텍스트)
            if (Content is Canvas canvas)
            {
                mouseEmoji = new TextBlock
                {
                    Text = "🔄",  // 스크롤 회전 화살표 이모지
                    FontSize = 20,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                stack.Children.Add(mouseEmoji);
                mouseTooltipText = new TextBlock
                {
                    Text = $"{LocalizationManager.Get("ScrollClickToStart")}\nESC : {LocalizationManager.Get("EscToCancel")}",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left,
                    LineHeight = 16
                };
                stack.Children.Add(mouseTooltipText);

                mouseTooltip = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 0)),
                    CornerRadius = new CornerRadius(8),
                    Child = stack,
                    Padding = new Thickness(10, 6, 10, 6),
                    Visibility = Visibility.Visible
                };
                canvas.Children.Add(mouseTooltip);
            }
        }

        private void ScrollCaptureWindow_Closed(object? sender, EventArgs e)
        {
            try { CatchCapture.Models.LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
            // 마우스 커서 복원
            ShowCursor(true);
            
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            if (_kbdHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_kbdHookID);
                _kbdHookID = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookExMouse(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr SetKeyboardHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookExKeyboard(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
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
                    
                    if (Content is Canvas c)
                    {
                        var p = PointFromScreen(new System.Windows.Point(hookStruct.pt.X, hookStruct.pt.Y));
                        
                        // 커스텀 커서 위치 업데이트
                        if (customCursor != null)
                        {
                            Canvas.SetLeft(customCursor, p.X);
                            Canvas.SetTop(customCursor, p.Y);
                        }
                        
                        // HUD 위치를 마우스 오른쪽 아래로 이동
                        if (mouseTooltip != null)
                        {
                            Canvas.SetLeft(mouseTooltip, p.X + 40);
                            Canvas.SetTop(mouseTooltip, p.Y + 20);
                        }
                    }
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    if (currentTargetElement != IntPtr.Zero)
                    {
                        var clickPoint = hookStruct.pt; // 클릭한 좌표 저장
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            // 후킹 해제
                            UnhookWindowsHookEx(_hookID);
                            _hookID = IntPtr.Zero;

                            highlightRect.Visibility = Visibility.Collapsed;
                            this.Opacity = 0;

                            await Task.Delay(200);

                            await PerformScrollCapture(currentTargetElement, clickPoint);

                            DialogResult = CapturedImage != null;
                            Close();
                        }));

                        return (IntPtr)1; // 클릭 이벤트 막음 (우리가 다시 클릭할 것이므로)
                    }
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    Dispatcher.Invoke(() =>
                    {
                        DialogResult = false;
                        Close();
                    });
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_ESCAPE)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        EscCancelled = true;
                        
                        // 캡처 시작 전: 완전히 취소하고 창 닫기
                        if (!isCapturing)
                        {
                            DialogResult = false;
                            Close();
                        }
                        // 캡처 시작 후: EscCancelled 플래그만 설정 (루프가 중단되고 지금까지 캡처한 이미지 병합)
                    }));
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_kbdHookID, nCode, wParam, lParam);
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

        private async Task PerformScrollCapture(IntPtr hWnd, POINT clickPt)
        {
            isCapturing = true; // 캡처 시작 표시
            var screenshots = new List<Bitmap>();

            try
            {
                // 1. 포커스 잡기 (사용자가 클릭한 위치를 다시 클릭)
                SetCursorPos(clickPt.X, clickPt.Y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                // 창 활성화 보장
                SetForegroundWindow(hWnd);
                await Task.Delay(500); // 충분한 대기 시간

                // Win32 핸들로 AutomationElement 생성
                var element = AutomationElement.FromHandle(hWnd);

                // 스크롤 패턴 찾기 (옵션 - 없어도 진행)
                ScrollPattern? scrollPattern = null;
                if (element != null)
                {
                    object patternObj;
                    if (element.TryGetCurrentPattern(ScrollPattern.Pattern, out patternObj))
                        scrollPattern = (ScrollPattern)patternObj;
                    else
                        scrollPattern = FindScrollPatternInChildren(element);
                }

                // 캡처 영역 결정: 항상 전체 창 (첫 헤더 포함을 위해)
                RECT captureRect;
                GetWindowRect(hWnd, out var winRect);
                captureRect = winRect;

                // 헤더 높이 계산 (스티칭 시 Sticky Header 제거용)
                int headerHeight = 0;
                if (scrollPattern != null && element != null)
                {
                    try
                    {
                        var scrollableElement = element;
                        if (!element.TryGetCurrentPattern(ScrollPattern.Pattern, out _))
                        {
                            var condition = new PropertyCondition(AutomationElement.IsScrollPatternAvailableProperty, true);
                            var child = element.FindFirst(TreeScope.Descendants, condition);
                            if (child != null) scrollableElement = child;
                        }

                        if (scrollableElement != null)
                        {
                            var elemRect = scrollableElement.Current.BoundingRectangle;

                            // 헤더 높이 = 스크롤 영역 시작점 - 창 시작점
                            headerHeight = (int)(elemRect.Top - winRect.Top);
                            if (headerHeight < 0) headerHeight = 0;

                            // 너무 크면(창의 50% 이상) 오류일 수 있으므로 제한
                            if (headerHeight > (winRect.Bottom - winRect.Top) / 2) headerHeight = 0;
                        }
                    }
                    catch { }
                }

                int width = captureRect.Right - captureRect.Left;
                int height = captureRect.Bottom - captureRect.Top;

                // 스크롤바 영역 제외 (우측 25px)
                if (width > 50) width -= 15;

                // 윈도우 경계 보정 (좌측, 하단 테두리 살짝 제외)
                if (width > 20)
                {
                    captureRect.Left += 8;
                    width -= 8;
                }
                //if (height > 20) height -= 8;

                // 1. 첫 화면 캡처 전 안정화
                await Task.Delay(300);
                screenshots.Add(CaptureRegion(captureRect.Left, captureRect.Top, width, height));

                // 강제 스크롤 시도
                int maxScrolls = 50; // 스크롤 양이 줄어드니 횟수를 늘림
                // 스크롤 양을 아주 작게 고정 (천천히 내려가도록)
                int scrollAmount = -120; // 휠 1칸 (표준)

                for (int i = 0; i < maxScrolls; i++)
                {
                    // ESC로 중간 중단 지원 (지금까지 캡처한 이미지는 유지하고 병합)
                    if (EscCancelled)
                    {
                        Log($"ESC 키로 스크롤 캡처 중단. 지금까지 {screenshots.Count}개 스크린샷 병합");
                        break; // 루프만 중단, screenshots는 병합 처리
                    }

                    // 스크롤 다운
                    try
                    {
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)scrollAmount), UIntPtr.Zero);
                    }
                    catch { }

                    await Task.Delay(400); // 스크롤 후 대기

                    // 캡처
                    var newShot = CaptureRegion(captureRect.Left, captureRect.Top, width, height);

                    // 중복 검사
                    if (screenshots.Count > 0)
                    {
                        var lastShot = screenshots[screenshots.Count - 1];
                        if (AreBitmapsIdentical(lastShot, newShot))
                        {
                            newShot.Dispose();
                            break;
                        }
                    }

                    screenshots.Add(newShot);
                }

                Log($"스크롤 캡처 총 {screenshots.Count}개 스크린샷 캡처됨");
                // 하단 여백 계산 (스티칭 정확도 향상)
                int bottomMargin = 50; // 하단 5px 여백 고려
                CapturedImage = MergeScreenshots(screenshots, headerHeight, bottomMargin);
                foreach (var s in screenshots) s.Dispose();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"캡처 오류: {ex.Message}");
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
                int differentPixels = 0;
                int totalChecked = 0;

                // 50픽셀 간격으로 더 정밀하게 스캔
                for (int y = 0; y < bmp1.Height; y += 50)
                {
                    for (int x = 0; x < bmp1.Width; x += 50)
                    {
                        var c1 = bmp1.GetPixel(x, y);
                        var c2 = bmp2.GetPixel(x, y);

                        totalChecked++;

                        // 10 이상 차이나면 다른 픽셀로 간주
                        if (Math.Abs(c1.R - c2.R) > 10 ||
                            Math.Abs(c1.G - c2.G) > 10 ||
                            Math.Abs(c1.B - c2.B) > 10)
                        {
                            differentPixels++;
                        }
                    }
                }

                // 98% 이상 같으면 동일한 이미지로 판단 (더 엄격하게)
                double similarity = 1.0 - ((double)differentPixels / totalChecked);
                return similarity > 0.98;
            }
            catch
            {
                return false;
            }
        }

        private BitmapSource MergeScreenshots(List<Bitmap> screenshots, int headerHeight, int bottomMargin = 0)
        {
            if (screenshots.Count == 0) return null!;
            if (screenshots.Count == 1) return ConvertBitmapToBitmapSource(screenshots[0]);

            // 첫 번째 이미지부터 시작
            Bitmap finalImage = screenshots[0];

            for (int i = 1; i < screenshots.Count; i++)
            {
                // 다음 이미지와 합치기 (헤더 높이와 하단 여백 전달)
                finalImage = StitchImages(finalImage, screenshots[i], headerHeight, bottomMargin);
            }

            var result = ConvertBitmapToBitmapSource(finalImage);
            finalImage.Dispose();
            return result;
        }

        private Bitmap StitchImages(Bitmap top, Bitmap bottom, int headerHeight, int bottomMargin = 0)
        {
            // 다중 프로브 전략: 상단 위주로 검색 (스크롤이 작으므로 Bottom의 상단이 Top의 하단과 매칭됨)
            double[] probeRatios = { 0.1, 0.2, 0.3, 0.5 }; 
            
            foreach (var ratio in probeRatios)
            {
                int probeY_in_Bottom = Math.Max(headerHeight, (int)(bottom.Height * ratio));
                
                // 안전 장치
                if (probeY_in_Bottom >= bottom.Height - 20) continue;

                // ★ 핵심 수정 1: 검색 범위를 창 높이만큼 충분히 확보 ★
                // 스크롤이 작아서 겹치는 부분이 매우 큼. 500px로는 부족함.
                int searchRange = bottom.Height; 
                int startSearchY = Math.Max(headerHeight, top.Height - searchRange);
                int endSearchY = top.Height - Math.Max(10, bottomMargin); 

                // ★ 핵심 수정 2: Top-Down 검색으로 변경 ★
                // 범위가 충분하므로 위에서부터 찾아야 중복을 확실히 제거함
                for (int y = startSearchY; y <= endSearchY; y++)
                {
                    if (IsRowMatch(top, y, bottom, probeY_in_Bottom))
                    {
                        // 매칭 성공! 검증 강화
                        if (VerifyMatch(top, y, bottom, probeY_in_Bottom))
                        {
                            Log($"매칭 성공 (Ratio {ratio:P0}): matchY={y}, probeY={probeY_in_Bottom}");
                            return CreateStitchedImage(top, bottom, y, probeY_in_Bottom);
                        }
                    }
                }
            }

            // 모든 시도 실패 시: Fallback
            // 스크롤 양이 작으므로(120px), 안전하게 100px 정도만 겹쳤다고 가정하고 붙임
            // 이렇게 하면 최소한 내용은 이어짐 (약간의 중복이나 공백이 있을 순 있어도 날려먹진 않음)
            Log("매칭 완전 실패. Fallback: 안전하게 100px 겹침 처리");
            int assumedOverlap = 100; 
            int fallbackMatchY = Math.Max(0, top.Height - assumedOverlap);
            int fallbackProbeY = headerHeight; 
            
            return CreateStitchedImage(top, bottom, fallbackMatchY, fallbackProbeY);
        }

        private bool VerifyMatch(Bitmap top, int y, Bitmap bottom, int probeY)
        {
            // 추가 검증: 5줄 더 확인
            int checkRange = Math.Min(20, top.Height - y - 1);
            checkRange = Math.Min(checkRange, bottom.Height - probeY - 1);
            
            if (checkRange < 5) return true; // 검증 공간 부족하면 그냥 통과

            for (int k = 5; k < checkRange; k += 5)
            {
                if (!IsRowMatch(top, y + k, bottom, probeY + k)) return false;
            }
            return true;
        }

        private Bitmap CreateStitchedImage(Bitmap top, Bitmap bottom, int topCutY, int bottomStartY)
        {
             int topHeight = topCutY;
             int bottomHeight = bottom.Height - bottomStartY;
             
             var result = new Bitmap(top.Width, topHeight + bottomHeight);
             using (var g = Graphics.FromImage(result))
             {
                 g.DrawImage(top, new System.Drawing.Rectangle(0, 0, top.Width, topHeight), 
                     new System.Drawing.Rectangle(0, 0, top.Width, topHeight), GraphicsUnit.Pixel);
                     
                 g.DrawImage(bottom, new System.Drawing.Rectangle(0, topHeight, bottom.Width, bottomHeight), 
                     new System.Drawing.Rectangle(0, bottomStartY, bottom.Width, bottomHeight), GraphicsUnit.Pixel);
             }
             top.Dispose();
             return result;
        }

        private bool IsRowMatch(Bitmap bmp1, int y1, Bitmap bmp2, int y2)
        {
            if (y1 < 0 || y1 >= bmp1.Height || y2 < 0 || y2 >= bmp2.Height) return false;

            int width = Math.Min(bmp1.Width, bmp2.Width);
            int startX = (int)(width * 0.1); 
            int endX = (int)(width * 0.9);   

            int totalPoints = 0;
            int mismatchPoints = 0;

            // 5픽셀 간격으로 더 촘촘하게 검사하되, 오차를 허용
            for (int x = startX; x < endX; x += 5) 
            {
                totalPoints++;
                var c1 = bmp1.GetPixel(x, y1);
                var c2 = bmp2.GetPixel(x, y2);

                // 픽셀 오차 허용 범위 (15)
                if (Math.Abs(c1.R - c2.R) > 15 || Math.Abs(c1.G - c2.G) > 15 || Math.Abs(c1.B - c2.B) > 15)
                {
                    mismatchPoints++;
                }
            }

            // 전체의 20% 이하만 틀리면 매칭 성공으로 간주 (유연한 매칭)
            // 예: 움직이는 배너나 미세한 변화 무시
            return mismatchPoints <= (totalPoints * 0.1);
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

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try
            {
                if (mouseTooltipText != null)
                {
                    mouseTooltipText.Text = $"{LocalizationManager.Get("ScrollClickToStart")}\nESC : {LocalizationManager.Get("EscToCancel")}";
                }
            }
            catch { }
        }
    }
}

