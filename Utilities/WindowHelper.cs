using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CatchCapture.Utilities
{
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi); // 'ref' added

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// WindowStyle="None"인 창이 최대화될 때 작업 표시줄을 가리는 문제를 해결합니다.
        /// SourceInitialized 이벤트나 생성자에서 호출하십시오.
        /// </summary>
        public static void FixMaximizedWindow(Window window)
        {
            window.SourceInitialized += (s, e) =>
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                HwndSource source = HwndSource.FromHwnd(handle);
                if (source != null)
                {
                    source.AddHook(WindowProc);
                }
            };
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true; // Mark as handled to prevent WPF from overriding our logic
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            object? structure = Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            if (structure == null) return;
            
            MINMAXINFO mmi = (MINMAXINFO)structure;

            // Get monitor info where the window is currently located
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;

                    // For WPF windows with AllowsTransparency=True and WindowStyle.None,
                    // setting ptMaxPosition relative to the monitor origin is the most reliable way 
                    // to respect the taskbar without the window shifting or disappearing.
                    mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.x = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                    
                    // Track sizes must be explicitly set to allow a large enough window
                    mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
                    mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }
    }
}
