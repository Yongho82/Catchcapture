using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using WinForms = System.Windows.Forms;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using System.Text;
using System.ComponentModel;
using CatchCapture.Models;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Webp;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace CatchCapture.Utilities
{
    public static class ScreenCaptureUtility
    {
        // Win32 API 선언
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_MONITOR = 0x00000001;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private const uint GA_ROOT = 2;
        private const uint GW_HWNDNEXT = 2;

        public static (string AppName, string Title) GetActiveWindowMetadata()
        {
            try
            {
                IntPtr hWnd = IntPtr.Zero;
                
                // 1. 현재 활성 창 가져오기
                hWnd = GetForegroundWindow();
                (string AppName, string Title) result = TryGetWindowData(hWnd);

                // 2. 만약 활성 창이 우리 앱(CatchCapture)이거나 유효하지 않다면, Z-Order 순서대로 그 아래 창을 찾음
                if (IsSelfProcess(result.AppName) || IsInvalidMetadata(result))
                {
                    IntPtr currentHwnd = hWnd;
                    // 최대 10개 창을 탐색
                    for (int i = 0; i < 20; i++)
                    {
                        currentHwnd = GetWindow(currentHwnd, GW_HWNDNEXT);
                        if (currentHwnd == IntPtr.Zero) break;

                        if (!IsWindowVisible(currentHwnd)) continue;

                        var candidateData = TryGetWindowData(currentHwnd);
                        
                        // 필터링: 우리 앱, 껍데기 앱, 제목 없는 앱 제외
                        if (IsSelfProcess(candidateData.AppName)) continue;
                        if (IsInvalidMetadata(candidateData)) continue;
                        
                        // 시스템 더미 창 필터링
                        if (candidateData.Title == "Program Manager" || 
                            candidateData.Title == "Taskbar" ||
                            candidateData.Title == "Windows 입력 환경") continue;

                        // 적절한 창을 찾았으면 채택
                        return candidateData;
                    }
                }
                else
                {
                    // 활성 창이 우리 앱이 아니면 그대로 반환
                    return result;
                }

                return ("Unknown", "Unknown");
            }
            catch
            {
                return ("Unknown", "Unknown");
            }
        }

        /// <summary>
        /// 특정 화면 좌표에 있는 창의 메타데이터 가져오기
        /// </summary>
        public static (string AppName, string Title) GetWindowAtPoint(int screenX, int screenY)
        {
            try
            {
                POINT p = new POINT { X = screenX, Y = screenY };
                IntPtr hWnd = WindowFromPoint(p);
                IntPtr shellWindow = GetShellWindow();

                // 바탕화면이나 시스템 유령 레이어(IME 등)를 건너뛰고 실제 앱 창 찾기
                while (hWnd != IntPtr.Zero)
                {
                    // 해당 윈도우가 실제로 마우스 포인트를 포함하고 있는지 확인
                    RECT rect;
                    if (GetWindowRect(hWnd, out rect))
                    {
                        if (p.X >= rect.Left && p.X <= rect.Right && p.Y >= rect.Top && p.Y <= rect.Bottom)
                        {
                            IntPtr rootHwnd = GetAncestor(hWnd, GA_ROOT);
                            var data = TryGetWindowData(rootHwnd);

                            // 제외할 시스템 창 제목 리스트
                            bool isSystemGhost = data.Title == "Program Manager" || 
                                               data.Title == "MSCTFIME UI" || 
                                               data.Title == "Default IME" ||
                                               data.Title == "DummyWindow" ||
                                               data.Title.Contains("시스템 트레이 오버플로") ||
                                               data.Title.Contains("Notification Area") ||
                                               string.IsNullOrEmpty(data.Title);

                            // explorer 프로세스인데 실제 폴더 창이나 바탕화면이 아닌 각종 시스템 UI 레이어 필터링
                            bool isExplorerGhost = data.AppName.Equals("explorer", StringComparison.OrdinalIgnoreCase) && 
                                                 (isSystemGhost || 
                                                  data.Title == "Execute" || 
                                                  data.Title == "작업 표시줄" ||
                                                  data.Title == "Taskbar");

                            // 1. 자기 자신이 아니고 2. 시스템 유령창이 아니며 3. 쉘 윈도우가 아닌 경우 인정
                            if (!IsSelfProcess(data.AppName) && 
                                !isSystemGhost && 
                                !isExplorerGhost &&
                                rootHwnd != shellWindow)
                            {
                                return data;
                            }
                        }
                    }

                    // 현재 좌표와 상관없는 윈도우도 Z-Order상에는 있을 수 있으므로 계속 다음을 찾음
                    hWnd = GetWindow(hWnd, GW_HWNDNEXT);
                }

                return ("Unknown", "Unknown");
            }
            catch
            {
                return ("Unknown", "Unknown");
            }
        }

        private static bool IsSelfProcess(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return false;
            return appName.Equals("CatchCapture", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInvalidMetadata((string AppName, string Title) data)
        {
            return string.IsNullOrEmpty(data.AppName) || data.AppName == "Unknown" || 
                   string.IsNullOrEmpty(data.Title) || data.Title == "Unknown";
        }

        private static (string AppName, string Title) TryGetWindowData(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return ("Unknown", "Unknown");

            string title = "Unknown";
            string appName = "Unknown";

            try
            {
                // Get Title
                StringBuilder titleBuilder = new StringBuilder(256);
                if (GetWindowText(hWnd, titleBuilder, 256) > 0)
                {
                    title = titleBuilder.ToString();
                }

                // Get Process Name
                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId != 0)
                {
                    try 
                    {
                        var process = System.Diagnostics.Process.GetProcessById((int)processId);
                        appName = process.ProcessName;
                    }
                    catch (Win32Exception) 
                    {
                        // 권한 문제 등으로 접근 불가 시 무시 (예: 시스템 프로세스)
                    }
                    catch (ArgumentException)
                    {
                        // 프로세스가 이미 종료됨
                    }
                }
            }
            catch
            {
                // 개별 단계 실패 시 무시
            }
            
            return (appName, title);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;
            [FieldOffset(0)]
            public HARDWAREINPUT Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        // Windows 메시지 상수
        private const uint WM_VSCROLL = 0x0115;
        private const uint SB_PAGEDOWN = 3;
        
        // 가상 키 코드
        private const int VK_RETURN = 0x0D; // 엔터 키
        private const int VK_NEXT = 0x22;   // Page Down 키
        private const int VK_DOWN = 0x28;   // 아래 방향키
        
        // 가상 키 및 입력 상수
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        
        public static BitmapSource CaptureScreen()
        {
            // Capture the entire virtual desktop across all monitors
            int vx = (int)SystemParameters.VirtualScreenLeft;
            int vy = (int)SystemParameters.VirtualScreenTop;
            int vw = (int)SystemParameters.VirtualScreenWidth;
            int vh = (int)SystemParameters.VirtualScreenHeight;

            using (Bitmap bitmap = new Bitmap(vw, vh, PixelFormat.Format32bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(vx, vy, 0, 0, new System.Drawing.Size(vw, vh));
                }

                return ConvertBitmapToBitmapSource(bitmap);
            }
        }

        // Helper to get the virtual screen rectangle (multi-monitor bounds)
        public static System.Drawing.Rectangle GetVirtualScreenRectangle()
        {
            return new System.Drawing.Rectangle(
                (int)SystemParameters.VirtualScreenLeft,
                (int)SystemParameters.VirtualScreenTop,
                (int)SystemParameters.VirtualScreenWidth,
                (int)SystemParameters.VirtualScreenHeight
            );
        }

        public static BitmapSource CaptureArea(Int32Rect area)
        {
            using (Bitmap bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(area.X, area.Y, 0, 0, new System.Drawing.Size(area.Width, area.Height));
                }

                return ConvertBitmapToBitmapSource(bitmap);
            }
        }
        
        public static BitmapSource? CaptureScrollableWindow()
        {
            try
            {
                LogToFile("스크롤 캡처 시작");
                // MessageBox 대신 Dispatcher를 통해 GuideWindow만 사용
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("ClickWindowThenEnter"), TimeSpan.FromSeconds(2));
                    guideWindow.Show();
                });
                
                // 사용자가 엔터 키를 누를 때까지 대기
                IntPtr hWnd = IntPtr.Zero;
                bool enterPressed = false;
                IntPtr lastActiveWindow = GetActiveWindow();
                
                while (!enterPressed)
                {
                    // 현재 활성화된 창의 핸들 가져오기
                    IntPtr currentHWnd = GetForegroundWindow();
                    
                    // 창이 변경되었다면 클릭된 것으로 판단
                    if (currentHWnd != IntPtr.Zero && currentHWnd != lastActiveWindow)
                    {
                        hWnd = currentHWnd;
                        
                        // 캡처 시작 안내
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("PressEnterToStartScroll"), TimeSpan.FromSeconds(2));
                            guideWindow.Show();
                        });
                        
                        // 엔터키를 확실히 기다리는 단계 추가
                        bool waitingForEnter = true;
                        while (waitingForEnter)
                        {
                            if ((GetAsyncKeyState(VK_RETURN) & 0x8000) != 0)
                            {
                                waitingForEnter = false;
                                enterPressed = true;
                                
                                // 메시지 창 모두 닫기
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    foreach (Window window in System.Windows.Application.Current.Windows)
                                    {
                                        if (window is CatchCapture.Utilities.GuideWindow)
                                        {
                                            window.Close();
                                        }
                                    }
                                });
                            }
                            Thread.Sleep(50);
                        }
                    }
                    
                    Thread.Sleep(50); // CPU 부하 감소
                }
                
                if (hWnd == IntPtr.Zero)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("WindowNotFound"), TimeSpan.FromSeconds(1.5));
                        guideWindow.Show();
                    });
                    return null;
                }
                
                // 캡처 시작 안내
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("StartingScrollCapture"), TimeSpan.FromSeconds(1.5));
                    guideWindow.Show();
                });
                
                // 창 활성화
                SetForegroundWindow(hWnd);
                Thread.Sleep(500); // 창이 활성화될 시간을 주기
                
                // 창의 크기 정보 가져오기
                RECT windowRect;
                GetWindowRect(hWnd, out windowRect);
                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;
                
                // 클라이언트 영역 크기 가져오기
                RECT clientRect;
                GetClientRect(hWnd, out clientRect);
                int clientWidth = clientRect.Right - clientRect.Left;
                int clientHeight = clientRect.Bottom - clientRect.Top;
                
                // 클라이언트 영역 좌표 가져오기
                POINT clientOrigin = new POINT { X = 0, Y = 0 };
                ClientToScreen(hWnd, ref clientOrigin);
                
                // 첫 번째 스크린샷 캡처
                List<Bitmap> capturedImages = new List<Bitmap>();
                Bitmap firstCapture = new Bitmap(clientWidth, clientHeight, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(firstCapture))
                {
                    g.CopyFromScreen(clientOrigin.X, clientOrigin.Y, 0, 0, new System.Drawing.Size(clientWidth, clientHeight));
                }
                capturedImages.Add(firstCapture);
                
                // 중복을 감지하기 위한 픽셀 비교용 이미지
                Bitmap lastVisibleArea = new Bitmap(clientWidth, clientHeight / 4, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(lastVisibleArea))
                {
                    g.DrawImage(firstCapture, 
                        new Rectangle(0, 0, clientWidth, clientHeight / 4), 
                        new Rectangle(0, clientHeight - clientHeight / 4, clientWidth, clientHeight / 4), 
                        GraphicsUnit.Pixel);
                }
                
                int maxScrolls = 20; // 무한 스크롤 방지용 최대 스크롤 횟수
                int scrollCount = 0;
                bool isScrollEnded = false;
                
                // 최초 스크롤 전 확인을 위한 저장
                Bitmap beforeFirstScroll = new Bitmap(clientWidth, clientHeight, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(beforeFirstScroll))
                {
                    g.CopyFromScreen(clientOrigin.X, clientOrigin.Y, 0, 0, new System.Drawing.Size(clientWidth, clientHeight));
                }
                
                // 작은 단위로 스크롤 (방향키 여러 번 사용)
                SendSmallScroll(5); // 중간 정도의 스크롤 양
                Thread.Sleep(500); // 스크롤 후 페이지 로드 대기
                
                // 첫 스크롤 후 캡처
                Bitmap afterFirstScroll = new Bitmap(clientWidth, clientHeight, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(afterFirstScroll))
                {
                    g.CopyFromScreen(clientOrigin.X, clientOrigin.Y, 0, 0, new System.Drawing.Size(clientWidth, clientHeight));
                }
                
                // 첫 번째 스크롤에서 변화가 없다면 스크롤이 없는 창으로 판단하고 종료
                bool hasScroll = !AreImagesEqual(beforeFirstScroll, afterFirstScroll);
                
                if (!hasScroll)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("NoScrollInWindow"), TimeSpan.FromSeconds(1.5));
                        guideWindow.Show();
                    });
                    
                    // 스크롤 없는 경우 현재 이미지만 반환
                    BitmapSource singleResult = ConvertBitmapToBitmapSource(beforeFirstScroll);
                    beforeFirstScroll.Dispose();
                    afterFirstScroll.Dispose();
                    return singleResult;
                }
                
                // 두 번째 캡처를 저장 (첫 번째 스크롤 이후 화면)
                capturedImages.Add(afterFirstScroll);
                
                // 다음 비교를 위해 현재 이미지의 하단 부분 저장
                using (Graphics g = Graphics.FromImage(lastVisibleArea))
                {
                    g.DrawImage(afterFirstScroll, 
                        new Rectangle(0, 0, clientWidth, clientHeight / 4), 
                        new Rectangle(0, clientHeight - clientHeight / 4, clientWidth, clientHeight / 4), 
                        GraphicsUnit.Pixel);
                }
                
                // 첫 번째 스크롤 이미지와 두 번째 스크롤 이미지가 거의 동일한지 한 번 더 확인
                bool hasRealScroll = !AreImagesAlmostIdentical(beforeFirstScroll, afterFirstScroll, 95);
                
                LogToFile($"스크롤 감지됨: {hasRealScroll}");
                
                if (!hasRealScroll)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("NoScrollableContent"), TimeSpan.FromSeconds(1.5));
                        guideWindow.Show();
                    });
                    
                    // 스크롤 없는 경우 현재 이미지만 반환
                    BitmapSource singleResult = ConvertBitmapToBitmapSource(beforeFirstScroll);
                    beforeFirstScroll.Dispose();
                    afterFirstScroll.Dispose();
                    return singleResult;
                }
                
                // 스크롤 가능한 영역 감지 (상단 및 하단 고정 영역 제외)
                Rectangle scrollableRegion = DetectScrollableRegion(beforeFirstScroll, afterFirstScroll);
                LogToFile($"감지된 스크롤 영역: X={scrollableRegion.X}, Y={scrollableRegion.Y}, 너비={scrollableRegion.Width}, 높이={scrollableRegion.Height}");
                
                // 스크롤바 제외 (일반적으로 오른쪽 20px 정도가 스크롤바)
                int scrollbarWidth = 20; // 스크롤바 예상 너비
                scrollableRegion.Width = Math.Max(1, scrollableRegion.Width - scrollbarWidth);
                LogToFile($"스크롤바 제외 후 영역: 너비={scrollableRegion.Width}");
                
                // 스크롤 영역이 감지되지 않았다면 전체 영역 사용
                if (scrollableRegion.Width <= 10 || scrollableRegion.Height <= 10)
                {
                    LogToFile("유효한 스크롤 영역을 감지하지 못함");
                    scrollableRegion = new Rectangle(0, 0, clientWidth, clientHeight);
                }
                
                // 스크롤 별 모든 캡처된 이미지 전체 픽셀 비교를 위한 리스트
                List<Bitmap> uniqueImages = new List<Bitmap>();
                
                // 첫 번째 이미지에서 스크롤 영역만 추출
                Bitmap firstScrollRegion = ExtractRegion(beforeFirstScroll, scrollableRegion);
                capturedImages.Clear(); // 기존 이미지 제거
                capturedImages.Add(firstScrollRegion); // 스크롤 영역만 추가
                uniqueImages.Add(firstScrollRegion);
                
                // 두 번째 이미지에서 스크롤 영역만 추출
                Bitmap secondScrollRegion = ExtractRegion(afterFirstScroll, scrollableRegion);
                capturedImages.Add(secondScrollRegion);
                uniqueImages.Add(secondScrollRegion);
                
                // 합쳐질 전체 높이 계산
                int totalScrollHeight = 0;
                foreach (var img in capturedImages)
                {
                    totalScrollHeight += img.Height;
                }
                LogToFile($"초기 스크롤 높이: {totalScrollHeight}px");
                
                // 스크롤 캡처 시작
                while (!isScrollEnded && scrollCount < maxScrolls)
                {
                    // 마지막에 가까울 때 더 작은 스크롤, 그 외에는 Page Down 사용
                    bool isNearEnd = false;
                    
                    // 임계값 도달: 연속해서 비슷한 이미지가 나오면 마지막에 가까운 것으로 판단
                    if (scrollCount > 0 && capturedImages.Count >= 2)
                    {
                        // 마지막 두 이미지가 70% 이상 비슷하면 마지막에 가까운 것으로 간주
                        Bitmap lastImage = capturedImages[capturedImages.Count - 1];
                        Bitmap beforeLastImage = capturedImages[capturedImages.Count - 2];
                        
                        double similarity = CalculateImageSimilarity(lastImage, beforeLastImage);
                        LogToFile($"마지막 두 이미지 유사도: {similarity:F4}");
                        
                        if (similarity > 0.7) // 70% 이상 유사할 경우
                        {
                            isNearEnd = true;
                            LogToFile("마지막 페이지에 가까워짐 감지");
                        }
                    }
                    
                    if (isNearEnd)
                    {
                        // 마지막에 가까울 때 더 작은 스크롤 (방향키 사용)
                        SendSmallScroll(8); // 스크롤 양을 증가
                        LogToFile("페이지 끝에 가까움: 작은 스크롤 사용");
                    }
                    else
                    {
                        // 일반적인 경우 페이지 다운 키 사용
                        SendPageDownKey();
                        LogToFile("일반 스크롤 (Page Down 사용)");
                    }
                    
                    Thread.Sleep(600); // 스크롤 후 페이지 로드 대기 시간 증가
                    
                    // 스크린샷 캡처
                    Bitmap currentCapture = new Bitmap(clientWidth, clientHeight, PixelFormat.Format32bppRgb);
                    using (Graphics g = Graphics.FromImage(currentCapture))
                    {
                        g.CopyFromScreen(clientOrigin.X, clientOrigin.Y, 0, 0, new System.Drawing.Size(clientWidth, clientHeight));
                    }
                    
                    // 스크롤 영역만 추출
                    Bitmap currentScrollRegion = ExtractRegion(currentCapture, scrollableRegion);
                    
                    // 이전에 캡처한 모든 이미지와 비교하여 중복 여부 확인
                    bool isDuplicate = false;
                    foreach (var prevImage in uniqueImages)
                    {
                        if (AreImagesAlmostIdentical(prevImage, currentScrollRegion, 97))
                        {
                            isDuplicate = true;
                            LogToFile($"중복 이미지 감지됨: 스크롤 {scrollCount}");
                            break;
                        }
                    }
                    
                    // 중복된 이미지가 발견되면 스크롤 종료
                    if (isDuplicate)
                    {
                        isScrollEnded = true;
                        
                        // 메시지 표시
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("ScrollCompletedDuplicate"), TimeSpan.FromSeconds(1));
                            guideWindow.Show();
                        });
                        
                        // 중복 이미지는 추가하지 않음
                        currentCapture.Dispose();
                        currentScrollRegion.Dispose();
                        break;
                    }
                    
                    // 이미지에 변화가 있으면 저장
                    capturedImages.Add(currentScrollRegion);
                    uniqueImages.Add(currentScrollRegion);
                    LogToFile($"새 스크롤 이미지 추가됨: 스크롤 {scrollCount}, 크기: {currentScrollRegion.Width}x{currentScrollRegion.Height}");
                    
                    // 다음 비교를 위해 현재 이미지의 하단 부분 저장
                    using (Graphics g = Graphics.FromImage(lastVisibleArea))
                    {
                        g.DrawImage(currentScrollRegion, 
                            new Rectangle(0, 0, currentScrollRegion.Width, Math.Min(currentScrollRegion.Height / 4, lastVisibleArea.Height)), 
                            new Rectangle(0, Math.Max(0, currentScrollRegion.Height - currentScrollRegion.Height / 4), 
                                        currentScrollRegion.Width, Math.Min(currentScrollRegion.Height / 4, currentScrollRegion.Height)), 
                            GraphicsUnit.Pixel);
                    }
                    
                    currentCapture.Dispose(); // 원본 이미지 해제
                    scrollCount++;
                    
                    // 캡처 이미지가 너무 많아지면 중단
                    if (capturedImages.Count > 15) // 더 작은 값으로 설정
                    {
                        isScrollEnded = true;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("ReachedMaxCaptures"), TimeSpan.FromSeconds(1.5));
                            guideWindow.Show();
                        });
                        break;
                    }
                }
                
                // 스크롤 횟수 초과했지만 스크롤이 끝나지 않았을 경우
                if (scrollCount >= maxScrolls && !isScrollEnded)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var guideWindow = new CatchCapture.Utilities.GuideWindow(LocalizationManager.Get("ReachedMaxScrolls"), TimeSpan.FromSeconds(1.5));
                        guideWindow.Show();
                    });
                }
                
                // 이미지 합치기
                int totalHeight = 0;
                // 이미지 간 겹치는 영역 크기 계산
                List<int> overlapHeights = new List<int>();
                
                // 처음엔 겹치는 부분 없음
                overlapHeights.Add(0);
                
                // 두 번째 이미지부터 이전 이미지와 겹치는 부분 계산
                for (int i = 1; i < capturedImages.Count; i++)
                {
                    Bitmap currentImg = capturedImages[i];
                    Bitmap prevImg = capturedImages[i - 1];
                    
                    int overlapHeight = CalculateExactOverlapHeight(prevImg, currentImg);
                    LogToFile($"이미지 {i-1}과 {i} 사이 겹치는 높이: {overlapHeight}px");
                    overlapHeights.Add(overlapHeight);
                }
                
                // 실제 필요한 전체 높이 계산 (겹치는 부분 제외)
                totalHeight = capturedImages[0].Height; // 첫 번째 이미지 높이
                for (int i = 1; i < capturedImages.Count; i++)
                {
                    totalHeight += capturedImages[i].Height - overlapHeights[i];
                }
                
                LogToFile($"계산된 총 높이: {totalHeight}px");
                
                // 이미지가 너무 크면 조정
                int maxHeight = 20000; // 최대 이미지 높이 (줄임)
                if (totalHeight > maxHeight)
                {
                    totalHeight = maxHeight;
                }
                
                // 첫 번째 이미지의 너비 사용
                int imageWidth = capturedImages[0].Width;
                
                // 결과 이미지 생성
                Bitmap combinedImage = new Bitmap(imageWidth, totalHeight, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(combinedImage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality; // 추가: 픽셀 처리 품질 향상
                    
                    int currentY = 0;
                    for (int i = 0; i < capturedImages.Count; i++)
                    {
                        // 현재 이미지
                        var img = capturedImages[i];
                        
                        // 이미지 간 자연스러운 연결을 위한 블렌딩 처리
                        if (i > 0 && overlapHeights[i] > 0)
                        {
                            // 이미지 그리기 (겹치는 부분은 BlendImage 메서드로 처리)
                            int nonOverlapHeight = img.Height - overlapHeights[i];
                            
                            // 겹치는 부분은 블렌딩 처리
                            BlendImagesAtSeam(combinedImage, img, currentY - overlapHeights[i], overlapHeights[i]);
                            
                            // 겹치지 않는 부분 그리기
                            g.DrawImage(img, 
                                new Rectangle(0, currentY, img.Width, nonOverlapHeight), 
                                new Rectangle(0, overlapHeights[i], img.Width, nonOverlapHeight), 
                                GraphicsUnit.Pixel);
                            
                            // Y 위치 업데이트 (겹치는 부분은 제외)
                            currentY += nonOverlapHeight;
                        }
                        else
                        {
                            // 첫 번째 이미지는 그대로 그림
                            g.DrawImage(img, new System.Drawing.Point(0, currentY));
                            currentY += img.Height;
                        }
                        
                        // 최대 높이 초과 시 중단
                        if (currentY >= maxHeight)
                            break;
                        
                        // 각 이미지 리소스 해제
                        img.Dispose();
                    }
                }
                
                BitmapSource result = ConvertBitmapToBitmapSource(combinedImage);
                combinedImage.Dispose();
                
                return result;
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show("스크롤 캡처 중 오류가 발생했습니다: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        
        // 두 이미지를 비교하는 메서드
        private static bool AreImagesEqual(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                return false;
                
            int matchThreshold = 98; // 일치 기준 백분율 (%) - 더 엄격하게 변경
            int differentPixels = 0;
            int totalPixels = img1.Width * img1.Height;
            int maxDifferentPixels = totalPixels * (100 - matchThreshold) / 100;
            
            // 샘플링 - 모든 픽셀을 비교하지 않고 일부만 비교 (속도 향상)
            int samplingRate = 5; // 5픽셀마다 한 번씩 체크
            
            for (int y = 0; y < img1.Height; y += samplingRate)
            {
                for (int x = 0; x < img1.Width; x += samplingRate)
                {
                    if (differentPixels > maxDifferentPixels)
                        return false;
                        
                    Color pixel1 = img1.GetPixel(x, y);
                    Color pixel2 = img2.GetPixel(x, y);
                    
                    if (!AreColorsEqual(pixel1, pixel2))
                        differentPixels++;
                }
            }
            
            // 실제 다른 픽셀 비율 계산 (샘플링 비율 고려)
            int estimatedDifferentPixels = differentPixels * samplingRate * samplingRate;
            return estimatedDifferentPixels <= maxDifferentPixels;
        }
        
        // 두 색상을 비교하는 메서드
        private static bool AreColorsEqual(Color c1, Color c2)
        {
            int threshold = 10; // 색상 차이 임계값
            return Math.Abs(c1.R - c2.R) <= threshold &&
                   Math.Abs(c1.G - c2.G) <= threshold &&
                   Math.Abs(c1.B - c2.B) <= threshold;
        }

        public static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // UI 스레드가 아닌 다른 스레드에서 사용할 수 있도록 Freeze

                return bitmapImage;
            }
        }

        public static Bitmap ConvertBitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(outStream);
                outStream.Flush();

                return new Bitmap(outStream);
            }
        }

        public static void SaveImageToFile(BitmapSource bitmapSource, string filePath, int quality = 100)
        {
            // 파일 확장자에 따라 인코더 선택
            string extension = System.IO.Path.GetExtension(filePath).ToLower();

            if (extension == ".webp")
            {
                // WebP는 별도 처리 (파일 락 문제 방지)
                SaveAsWebpNative(bitmapSource, filePath, quality);
                return;
            }

            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder;
                
                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        var jpegEncoder = new JpegBitmapEncoder();
                        // [Smart Optimization] 사용자가 100%를 선택해도 내부적으로 95%로 저장하여 용량 최적화 (시각적 차이 없음)
                        jpegEncoder.QualityLevel = (quality >= 100) ? 95 : quality;
                        encoder = jpegEncoder;
                        break;
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    case ".png":
                    default:
                        encoder = new PngBitmapEncoder();
                        break;
                }
                
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(fileStream);
            }
        }

        public static void SaveAsWebpNative(BitmapSource bitmapSource, string filePath, int quality)
        {
            try
            {
                // 1. BitmapSource를 BGRA8 바이트 배열로 변환
                BitmapSource bgra32Source = bitmapSource;
                if (bitmapSource.Format != System.Windows.Media.PixelFormats.Bgra32 && 
                    bitmapSource.Format != System.Windows.Media.PixelFormats.Pbgra32)
                {
                    bgra32Source = new FormatConvertedBitmap(bitmapSource, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                }

                int width = bgra32Source.PixelWidth;
                int height = bgra32Source.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                bgra32Source.CopyPixels(pixels, stride, 0);

                // 2. ImageSharp Image 생성 (BGRA -> RGBA 변환 필요)
                using (var image = new SixLabors.ImageSharp.Image<Bgra32>(width, height))
                {
                    // 픽셀 데이터 복사
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            int rowOffset = y * stride;
                            for (int x = 0; x < width; x++)
                            {
                                int pixelOffset = rowOffset + x * 4;
                                row[x] = new Bgra32(
                                    pixels[pixelOffset + 2], // R (WPF BGRA에서 3번째 바이트)
                                    pixels[pixelOffset + 1], // G
                                    pixels[pixelOffset],     // B (WPF BGRA에서 1번째 바이트)
                                    pixels[pixelOffset + 3]  // A
                                );
                            }
                        }
                    });

                    // 3. WebP로 저장
                    var encoder = new WebpEncoder
                    {
                        Quality = quality,
                        FileFormat = quality >= 100 ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
                    };

                    image.SaveAsWebp(filePath, encoder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebP 저장 실패: {ex.Message}");
                
                // Fallback to PNG
                try
                {
                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { }
                    }

                    using (var fallbackStream = new FileStream(filePath, FileMode.Create))
                    {
                        var pngEncoder = new PngBitmapEncoder();
                        pngEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        pngEncoder.Save(fallbackStream);
                    }
                }
                catch (Exception fbEx)
                {
                    MessageBox.Show($"이미지 저장에 실패했습니다.\n\nWebP 오류: {ex.Message}\nPNG 백업 오류: {fbEx.Message}", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static void SaveImageToStream(BitmapSource bitmapSource, Stream outputStream, string? format, int quality)
        {
            string fmt = format?.ToUpper() ?? "PNG";

            if (fmt == "WEBP")
            {
                try
                {
                    // 1. BitmapSource를 BGRA8 바이트 배열로 변환
                    BitmapSource bgra32Source = bitmapSource;
                    if (bitmapSource.Format != System.Windows.Media.PixelFormats.Bgra32 && 
                        bitmapSource.Format != System.Windows.Media.PixelFormats.Pbgra32)
                    {
                        bgra32Source = new FormatConvertedBitmap(bitmapSource, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    }

                    int width = bgra32Source.PixelWidth;
                    int height = bgra32Source.PixelHeight;
                    int stride = width * 4;
                    byte[] pixels = new byte[height * stride];
                    bgra32Source.CopyPixels(pixels, stride, 0);

                    // 2. ImageSharp Image 생성
                    using (var image = new SixLabors.ImageSharp.Image<Bgra32>(width, height))
                    {
                        image.ProcessPixelRows(accessor =>
                        {
                            for (int y = 0; y < height; y++)
                            {
                                var row = accessor.GetRowSpan(y);
                                int rowOffset = y * stride;
                                for (int x = 0; x < width; x++)
                                {
                                    int pixelOffset = rowOffset + x * 4;
                                    row[x] = new Bgra32(
                                        pixels[pixelOffset + 2], // R
                                        pixels[pixelOffset + 1], // G
                                        pixels[pixelOffset],     // B
                                        pixels[pixelOffset + 3]  // A
                                    );
                                }
                            }
                        });

                        // 3. WebP로 저장
                        var encoder = new WebpEncoder
                        {
                            Quality = quality,
                            FileFormat = quality >= 100 ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
                        };

                        image.SaveAsWebp(outputStream, encoder);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebP Stream Saving Failed: {ex.Message} - Falling back to PNG");
                    var pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    pngEncoder.Save(outputStream);
                }
            }
            else
            {
                BitmapEncoder encoder;
                switch (fmt)
                {
                    case "JPG": case "JPEG":
                        var jpgEncoder = new JpegBitmapEncoder(); jpgEncoder.QualityLevel = quality; encoder = jpgEncoder; break;
                    case "BMP": encoder = new BmpBitmapEncoder(); break;
                    case "GIF": encoder = new GifBitmapEncoder(); break;
                    case "PNG": default: encoder = new PngBitmapEncoder(); break;
                }
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(outputStream);
            }
        }

        public static void CopyImageToClipboard(BitmapSource bitmapSource)
        {
            System.Windows.Clipboard.SetImage(bitmapSource);
        }

        public static void CopyTextToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            System.Windows.Clipboard.SetText(text);
        }

        // 두 이미지가 거의 완전히 동일한지 비교 (스크롤 감지용)
        private static bool AreImagesAlmostIdentical(Bitmap img1, Bitmap img2)
        {
            return AreImagesAlmostIdentical(img1, img2, 99);
        }
        
        // 두 이미지가 지정된 임계값으로 동일한지 비교
        private static bool AreImagesAlmostIdentical(Bitmap img1, Bitmap img2, int threshold)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                return false;
                
            int matchThreshold = threshold; // 일치 기준 백분율 (%)
            int differentPixels = 0;
            int totalPixels = img1.Width * img1.Height;
            int maxDifferentPixels = totalPixels * (100 - matchThreshold) / 100;
            
            // 작은 스크롤일 경우 더 민감하게 검사
            // 이미지 하단 부분에서 작은 차이도 감지할 수 있도록
            int samplingRate = 5; // 기본 샘플링 비율 (1/5 픽셀 체크)
            
            // 하단 25% 영역 집중 검사 (스크롤 시 주로 변경되는 영역)
            int bottomRegionStart = (int)(img1.Height * 0.75);
            
            for (int y = bottomRegionStart; y < img1.Height; y += samplingRate / 2)
            {
                for (int x = 0; x < img1.Width; x += samplingRate)
                {
                    if (y >= 0 && y < img1.Height && x >= 0 && x < img1.Width)
                    {
                        try
                        {
                            Color pixel1 = img1.GetPixel(x, y);
                            Color pixel2 = img2.GetPixel(x, y);
                            
                            // 색상 차이 확인 (더 엄격하게)
                            if (Math.Abs(pixel1.R - pixel2.R) > 2 ||
                                Math.Abs(pixel1.G - pixel2.G) > 2 ||
                                Math.Abs(pixel1.B - pixel2.B) > 2)
                            {
                                differentPixels += 3; // 하단 영역 차이에 더 가중치
                            }
                        }
                        catch
                        {
                            differentPixels++;
                        }
                    }
                }
            }
            
            // 이미지 상단 부분도 샘플링 검사
            for (int y = 0; y < bottomRegionStart; y += samplingRate * 2)
            {
                for (int x = 0; x < img1.Width; x += samplingRate * 2)
                {
                    if (y >= 0 && y < img1.Height && x >= 0 && x < img1.Width)
                    {
                        try
                        {
                            Color pixel1 = img1.GetPixel(x, y);
                            Color pixel2 = img2.GetPixel(x, y);
                            
                            if (Math.Abs(pixel1.R - pixel2.R) > 3 ||
                                Math.Abs(pixel1.G - pixel2.G) > 3 ||
                                Math.Abs(pixel1.B - pixel2.B) > 3)
                            {
                                differentPixels++;
                            }
                        }
                        catch
                        {
                            differentPixels++;
                        }
                    }
                }
            }
            
            // 실제 다른 픽셀 비율 계산 (샘플링 비율 고려)
            int estimatedDifferentPixels = differentPixels * 5; // 대략적인 추정치
            
            LogToFile($"이미지 비교: 다른 픽셀 수: {differentPixels}, 예상 다른 픽셀 수: {estimatedDifferentPixels}, 최대 허용 다른 픽셀 수: {maxDifferentPixels}");
            
            return estimatedDifferentPixels <= maxDifferentPixels;
        }

        // Page Down 키를 시뮬레이션하여 스크롤
        private static void SendPageDownKey()
        {
            // Page Down 키 이벤트 생성
            INPUT[] inputs = new INPUT[2];
            
            // Key Down
            inputs[0].Type = INPUT_KEYBOARD;
            inputs[0].Data.Keyboard.Vk = VK_NEXT;
            inputs[0].Data.Keyboard.Scan = 0;
            inputs[0].Data.Keyboard.Flags = KEYEVENTF_KEYDOWN;
            inputs[0].Data.Keyboard.Time = 0;
            inputs[0].Data.Keyboard.ExtraInfo = IntPtr.Zero;
            
            // Key Up
            inputs[1].Type = INPUT_KEYBOARD;
            inputs[1].Data.Keyboard.Vk = VK_NEXT;
            inputs[1].Data.Keyboard.Scan = 0;
            inputs[1].Data.Keyboard.Flags = KEYEVENTF_KEYUP;
            inputs[1].Data.Keyboard.Time = 0;
            inputs[1].Data.Keyboard.ExtraInfo = IntPtr.Zero;
            
            // 키 이벤트 전송
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        
        // 아래 방향키를 여러 번 눌러 작은 양만큼 스크롤
        private static void SendSmallScroll(int times)
        {
            for (int i = 0; i < times; i++)
            {
                // 아래 방향키 이벤트 생성
                INPUT[] inputs = new INPUT[2];
                
                // Key Down
                inputs[0].Type = INPUT_KEYBOARD;
                inputs[0].Data.Keyboard.Vk = VK_DOWN; // 방향키 아래
                inputs[0].Data.Keyboard.Scan = 0;
                inputs[0].Data.Keyboard.Flags = KEYEVENTF_KEYDOWN;
                inputs[0].Data.Keyboard.Time = 0;
                inputs[0].Data.Keyboard.ExtraInfo = IntPtr.Zero;
                
                // Key Up
                inputs[1].Type = INPUT_KEYBOARD;
                inputs[1].Data.Keyboard.Vk = VK_DOWN; // 방향키 아래
                inputs[1].Data.Keyboard.Scan = 0;
                inputs[1].Data.Keyboard.Flags = KEYEVENTF_KEYUP;
                inputs[1].Data.Keyboard.Time = 0;
                inputs[1].Data.Keyboard.ExtraInfo = IntPtr.Zero;
                
                // 키 이벤트 전송
                SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
                
                // 키 입력 사이 약간의 지연
                Thread.Sleep(50);
            }
        }

        // 두 이미지를 비교하여 스크롤 가능한 영역 감지
        private static Rectangle DetectScrollableRegion(Bitmap before, Bitmap after)
        {
            int width = Math.Min(before.Width, after.Width);
            int height = Math.Min(before.Height, after.Height);
            
            // 결과 저장 변수
            int topBorder = 0;
            int bottomBorder = height;
            int leftBorder = 0;
            int rightBorder = width;
            
            // 상단 경계선 찾기 (위에서 아래로)
            for (int y = 0; y < height / 2; y += 5) // 화면 중간까지만 검사
            {
                int diffCount = 0;
                for (int x = 0; x < width; x += 10)
                {
                    Color pixelBefore = before.GetPixel(x, y);
                    Color pixelAfter = after.GetPixel(x, y);
                    
                    if (!AreColorsEqual(pixelBefore, pixelAfter))
                    {
                        diffCount++;
                    }
                }
                
                // 충분한 차이가 발견되면 스크롤 영역 시작점으로 간주
                if (diffCount > width / 100) // 1% 이상 차이나면
                {
                    topBorder = Math.Max(0, y - 5); // 약간 위로 여유 공간
                    break;
                }
            }
            
            // 하단 경계선 찾기 (아래에서 위로)
            for (int y = height - 1; y > height / 2; y -= 5)
            {
                int diffCount = 0;
                for (int x = 0; x < width; x += 10)
                {
                    Color pixelBefore = before.GetPixel(x, y);
                    Color pixelAfter = after.GetPixel(x, y);
                    
                    if (!AreColorsEqual(pixelBefore, pixelAfter))
                    {
                        diffCount++;
                    }
                }
                
                if (diffCount > width / 100)
                {
                    bottomBorder = Math.Min(height, y + 5); // 약간 아래로 여유 공간
                    break;
                }
            }
            
            // 대부분의 웹 브라우저는 좌우 전체 영역을 사용하므로 좌우 경계는 크게 신경쓰지 않음
            
            // 스크롤 영역이 너무 작으면 전체 영역 사용
            if (bottomBorder - topBorder < height / 4)
            {
                LogToFile("스크롤 영역이 너무 작아 전체 영역을 사용함");
                return new Rectangle(0, 0, width, height);
            }
            
            LogToFile($"감지된 스크롤 영역 경계: 상단={topBorder}, 하단={bottomBorder}, 좌측={leftBorder}, 우측={rightBorder}");
            return new Rectangle(leftBorder, topBorder, rightBorder - leftBorder, bottomBorder - topBorder);
        }
        
        // 이미지에서 특정 영역만 추출
        private static Bitmap ExtractRegion(Bitmap source, Rectangle region)
        {
            // 영역이 이미지 크기를 벗어나지 않도록 조정
            region = new Rectangle(
                Math.Max(0, region.X),
                Math.Max(0, region.Y),
                Math.Min(source.Width - region.X, region.Width),
                Math.Min(source.Height - region.Y, region.Height)
            );
            
            if (region.Width <= 0 || region.Height <= 0)
            {
                LogToFile("추출 영역이 유효하지 않음, 원본 이미지 반환");
                return new Bitmap(source);
            }
            
            // 지정된 영역만 새 비트맵으로 복사
            Bitmap result = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height), region, GraphicsUnit.Pixel);
            }
            return result;
        }
        
        // 로그 파일에 기록
        private static void LogToFile(string message)
        {
            try
            {
                string logPath = "shape_debug.log";
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // 로그 작성 실패 시 무시
            }
        }

        // 두 이미지의 겹치는 높이를 정확히 계산
        private static int CalculateExactOverlapHeight(Bitmap topImage, Bitmap bottomImage)
        {
            int minHeight = Math.Min(topImage.Height, bottomImage.Height);
            // 최대 겹침 높이 (이미지 높이의 1/3까지만 검사)
            int maxOverlap = minHeight / 3;
            
            // 최소 매칭 점수 임계값
            double bestMatchScore = 0;
            int bestMatchHeight = 0;
            
            // 다양한 겹침 높이에 대해 검사 (아래에서 위로)
            for (int overlapHeight = 10; overlapHeight < maxOverlap; overlapHeight += 5)
            {
                // 상단 이미지의 하단 부분
                Rectangle topRect = new Rectangle(0, topImage.Height - overlapHeight, topImage.Width, overlapHeight);
                // 하단 이미지의 상단 부분
                Rectangle bottomRect = new Rectangle(0, 0, bottomImage.Width, overlapHeight);
                
                double matchScore = CompareImageRegions(topImage, bottomImage, topRect, bottomRect);
                
                if (matchScore > bestMatchScore)
                {
                    bestMatchScore = matchScore;
                    bestMatchHeight = overlapHeight;
                    
                    // 매칭 점수가 충분히 높으면 바로 반환
                    if (matchScore > 0.95)
                        break;
                }
            }
            
            LogToFile($"최적의 겹침 높이: {bestMatchHeight}px (매칭 점수: {bestMatchScore:F4})");
            return bestMatchHeight;
        }
        
        // 두 이미지 영역을 비교하여 유사도 점수 반환 (0~1, 1이 완전 일치)
        private static double CompareImageRegions(Bitmap img1, Bitmap img2, Rectangle rect1, Rectangle rect2)
        {
            int sampleWidth = Math.Min(rect1.Width, rect2.Width);
            int sampleHeight = Math.Min(rect1.Height, rect2.Height);
            
            if (sampleWidth <= 0 || sampleHeight <= 0)
                return 0;
            
            int matchedPixels = 0;
            int totalPixels = 0;
            
            // 샘플링 간격 (성능 향상)
            int sampleStep = 3;
            
            for (int y = 0; y < sampleHeight; y += sampleStep)
            {
                for (int x = 0; x < sampleWidth; x += sampleStep)
                {
                    try
                    {
                        Color c1 = img1.GetPixel(rect1.X + x, rect1.Y + y);
                        Color c2 = img2.GetPixel(rect2.X + x, rect2.Y + y);
                        
                        if (AreColorsVeryClose(c1, c2))
                            matchedPixels++;
                        
                        totalPixels++;
                    }
                    catch
                    {
                        // 인덱스 오류 무시
                    }
                }
            }
            
            return totalPixels > 0 ? (double)matchedPixels / totalPixels : 0;
        }
        
        // 두 색상이 매우 유사한지 확인 (이미지 연결 부분 매칭용)
        private static bool AreColorsVeryClose(Color c1, Color c2)
        {
            // 더 엄격한 색상 비교 (연결 부분에 사용)
            return Math.Abs(c1.R - c2.R) <= 5 &&
                   Math.Abs(c1.G - c2.G) <= 5 &&
                   Math.Abs(c1.B - c2.B) <= 5;
        }
        
        // 이미지 연결 부분을 블렌딩하여 매끄럽게 연결
        private static void BlendImagesAtSeam(Bitmap destImage, Bitmap sourceImage, int yPosition, int overlapHeight)
        {
            // 블렌딩 영역이 없으면 처리 안함
            if (overlapHeight <= 0)
                return;
            
            int blendStartY = Math.Max(0, yPosition);
            int blendEndY = Math.Min(destImage.Height - 1, yPosition + overlapHeight);
            
            for (int y = blendStartY; y < blendEndY; y++)
            {
                // 블렌딩 계수 (0~1): 위쪽은 기존 이미지 비중이 높고, 아래쪽은 새 이미지 비중이 높게
                float blendFactor = (float)(y - blendStartY) / (blendEndY - blendStartY);
                
                for (int x = 0; x < Math.Min(destImage.Width, sourceImage.Width); x++)
                {
                    try
                    {
                        // 기존 이미지의 픽셀
                        Color destColor = destImage.GetPixel(x, y);
                        
                        // 새 이미지의 픽셀 (y 위치 조정)
                        int sourceY = (y - yPosition);
                        if (sourceY >= 0 && sourceY < sourceImage.Height)
                        {
                            Color sourceColor = sourceImage.GetPixel(x, sourceY);
                            
                            // 두 색상을 블렌딩
                            int r = (int)(destColor.R * (1 - blendFactor) + sourceColor.R * blendFactor);
                            int g = (int)(destColor.G * (1 - blendFactor) + sourceColor.G * blendFactor);
                            int b = (int)(destColor.B * (1 - blendFactor) + sourceColor.B * blendFactor);
                            
                            // 블렌딩된 색상 적용
                            destImage.SetPixel(x, y, Color.FromArgb(255, r, g, b));
                        }
                    }
                    catch
                    {
                        // 인덱스 오류 무시
                    }
                }
            }
        }

        // 두 이미지의 유사도 계산 (0~1 사이 값, 1이 완전 동일)
        private static double CalculateImageSimilarity(Bitmap img1, Bitmap img2)
        {
            int sampleWidth = Math.Min(img1.Width, img2.Width);
            int sampleHeight = Math.Min(img1.Height, img2.Height);
            
            if (sampleWidth <= 0 || sampleHeight <= 0)
                return 0;
            
            int matchedPixels = 0;
            int totalPixels = 0;
            int samplingRate = 10; // 성능을 위한 샘플링
            
            // 이미지 전체 유사도 검사
            for (int y = 0; y < sampleHeight; y += samplingRate)
            {
                for (int x = 0; x < sampleWidth; x += samplingRate)
                {
                    try
                    {
                        Color c1 = img1.GetPixel(x, y);
                        Color c2 = img2.GetPixel(x, y);
                        
                        // 색상 유사도 계산
                        if (AreColorsSimilar(c1, c2, 10)) // 10은 색상 차이 허용 임계값
                            matchedPixels++;
                        
                        totalPixels++;
                    }
                    catch
                    {
                        // 인덱스 오류 무시
                    }
                }
            }
            
            return totalPixels > 0 ? (double)matchedPixels / totalPixels : 0;
        }
        
        // 두 색상의 유사도 검사 (임계값 내의 차이만 허용)
        private static bool AreColorsSimilar(Color c1, Color c2, int threshold)
        {
            return Math.Abs(c1.R - c2.R) <= threshold &&
                   Math.Abs(c1.G - c2.G) <= threshold &&
                   Math.Abs(c1.B - c2.B) <= threshold;
        }
    }
} 
