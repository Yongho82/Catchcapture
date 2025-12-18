using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture
{
    public class ElementCaptureWindow : Window
    {
        // --- P/Invoke Definitions ---
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
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // --- Constants ---
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        // --- Delegates & Structs ---
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // --- Fields ---
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        private System.Windows.Threading.DispatcherTimer _timer;

        private Rectangle _highlightRect = null!;
        private Rectangle _selectionRect = null!;
        private TextBlock _infoText = null!;
        private TextBlock _guideText = null!;

        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Rect _currentElementRect = Rect.Empty;
        
        public BitmapSource? CapturedImage { get; private set; }

        // --- Constructor ---
        public ElementCaptureWindow()
        {
            // Window Styles
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            
            // Allow UIA to look through this window, but clicks are handled by Hook
            IsHitTestVisible = false;

            // Fullscreen coverage
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            InitializeUI();
            
            // Install Mouse Hook
            _proc = HookCallback;
            _hookID = SetHook(_proc);

            // Timer for polling detection (UIA is expensive, running it on timer is safer than on every mouse move)
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(20);
            _timer.Tick += OnTimerTick;
            
            Loaded += (s, e) => {
                _timer.Start();
                Activate();
            };
            Closed += (s, e) => {
                _timer.Stop();
                UnhookWindowsHookEx(_hookID);
            };
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                
                if (msg == WM_LBUTTONDOWN)
                {
                    // SWALLOW Click -> Start our logic
                    POINT pt;
                    GetCursorPos(out pt);
                    _dragStartPoint = new Point(pt.X - Left, pt.Y - Top);
                    _isDragging = true;

                    Dispatcher.BeginInvoke(() => {
                        _highlightRect.Visibility = Visibility.Collapsed;
                        _selectionRect.Visibility = Visibility.Visible;
                        _selectionRect.Width = 0;
                        _selectionRect.Height = 0;
                        Canvas.SetLeft(_selectionRect, _dragStartPoint.X);
                        Canvas.SetTop(_selectionRect, _dragStartPoint.Y);
                    });

                    return (IntPtr)1; // Eat event (Stop propagation)
                }
                else if (msg == WM_LBUTTONUP)
                {
                    // SWALLOW Click -> End logic
                    if (_isDragging)
                    {
                        _isDragging = false;
                        
                        POINT pt;
                        GetCursorPos(out pt);
                        Point currentPos = new Point(pt.X - Left, pt.Y - Top);
                        Vector diff = currentPos - _dragStartPoint;

                        Dispatcher.BeginInvoke(() => {
                           if (diff.Length < 5)
                           {
                               // Single Click: Capture Highlighted Element
                               if (_currentElementRect != Rect.Empty && _highlightRect.Visibility == Visibility.Visible)
                               {
                                   CaptureArea(_currentElementRect);
                               }
                           }
                           else
                           {
                               // Drag: Capture Selected Region
                               CaptureArea(new Rect(
                                   Canvas.GetLeft(_selectionRect), 
                                   Canvas.GetTop(_selectionRect), 
                                   _selectionRect.Width, 
                                   _selectionRect.Height));
                           }
                        });
                    }
                    return (IntPtr)1; // Eat event
                }
                else if (msg == WM_RBUTTONDOWN)
                {
                    // SWALLOW Right Click -> Cancel
                    Dispatcher.BeginInvoke(() => {
                        DialogResult = false;
                        Close();
                    });
                    return (IntPtr)1; // Eat event
                }
                else if (msg == WM_MOUSEMOVE)
                {
                   // PASS-THROUGH Mouse Move (Let underlying apps see hover)
                   // But update our drag selection visual
                   if (_isDragging)
                   {
                        POINT pt;
                        GetCursorPos(out pt);
                        Point currentPos = new Point(pt.X - Left, pt.Y - Top);
                        
                        Dispatcher.BeginInvoke(() => {
                            double x = Math.Min(_dragStartPoint.X, currentPos.X);
                            double y = Math.Min(_dragStartPoint.Y, currentPos.Y);
                            double w = Math.Abs(currentPos.X - _dragStartPoint.X);
                            double h = Math.Abs(currentPos.Y - _dragStartPoint.Y);

                            Canvas.SetLeft(_selectionRect, x);
                            Canvas.SetTop(_selectionRect, y);
                            _selectionRect.Width = w;
                            _selectionRect.Height = h;

                            Canvas.SetLeft(_infoText, x);
                            Canvas.SetTop(_infoText, y - 25);
                            _infoText.Text = $"{(int)w} x {(int)h}";
                            _infoText.Visibility = Visibility.Visible;
                        });
                   }
                   // Return CallNextHookEx to allow others to see mouse move
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Apply WS_EX_TRANSPARENT to allow UIA Hit Testing to pass through the window
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        private void InitializeUI()
        {
            var canvas = new Canvas
            {
                Width = Width,
                Height = Height,
                Background = Brushes.Transparent
            };

            // 1. Highlight Rectangle (Hover) - Blue
            _highlightRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // DodgerBlue
                StrokeThickness = 2,
                RadiusX = 0, RadiusY = 0,
                Fill = new SolidColorBrush(Color.FromArgb(20, 30, 144, 255)),
                Visibility = Visibility.Collapsed
            };

            // 2. Selection Rectangle (Drag)
            _selectionRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 120, 215)),
                Visibility = Visibility.Collapsed
            };

            // 3. Info Text
            _infoText = new TextBlock
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                Foreground = Brushes.White,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11,
                Visibility = Visibility.Collapsed
            };
            
            // 4. Guide Text
            _guideText = new TextBlock
            {
                Text = "요소 캡처 모드 - 요소 위: 클릭 | 빈 공간: 드래그\nESC: 취소",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 2 }
            };
            Canvas.SetLeft(_guideText, (Width - 400) / 2);
            Canvas.SetTop(_guideText, Height / 2 - 50);

            canvas.Children.Add(_guideText);
            canvas.Children.Add(_highlightRect);
            canvas.Children.Add(_selectionRect);
            canvas.Children.Add(_infoText);
            Content = canvas;
            
             // Auto-hide guide
             Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() => { 
                if(_guideText != null) _guideText.Visibility = Visibility.Collapsed; 
             }));
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_isDragging) return;

            POINT pt;
            GetCursorPos(out pt);
            DetectElementUnderMouse(pt);
        }

        private void DetectElementUnderMouse(POINT screenPt)
        {
            try
            {
                System.Windows.Point sysPt = new System.Windows.Point(screenPt.X, screenPt.Y);
                var element = System.Windows.Automation.AutomationElement.FromPoint(sysPt);

                if (element != null)
                {
                    // Prevent catching self or huge parent window
                    if (element.Current.ProcessId == Process.GetCurrentProcess().Id)
                        return;

                    Rect r = element.Current.BoundingRectangle;
                    if (r.Width == 0 || r.Height == 0) return;
                    
                    // Convert Screen to Local
                    double localX = r.X - Left;
                    double localY = r.Y - Top;

                    _currentElementRect = new Rect(localX, localY, r.Width, r.Height);

                    Canvas.SetLeft(_highlightRect, localX);
                    Canvas.SetTop(_highlightRect, localY);
                    _highlightRect.Width = r.Width;
                    _highlightRect.Height = r.Height;
                    _highlightRect.Visibility = Visibility.Visible;

                    Canvas.SetLeft(_infoText, localX);
                    Canvas.SetTop(_infoText, localY - 25 > 0 ? localY - 25 : localY + r.Height + 5);
                    
                    _infoText.Text = $"{(int)r.Width} x {(int)r.Height}";
                    _infoText.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private async void CaptureArea(Rect rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;

            // 1. Hide UI
            Visibility = Visibility.Hidden;
            await Task.Delay(50); // Wait for render

            try
            {
                // 2. Capture Logic
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0, dpiY = 1.0;
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                double screenX = Left + rect.X;
                double screenY = Top + rect.Y;

                int pxX = (int)(screenX * dpiX);
                int pxY = (int)(screenY * dpiY);
                int pxW = (int)(rect.Width * dpiX);
                int pxH = (int)(rect.Height * dpiY);

                var area = new Int32Rect(pxX, pxY, pxW, pxH);
                
                var bitmap = Utilities.ScreenCaptureUtility.CaptureArea(area);
                CapturedImage = bitmap;
                
                DialogResult = true;
                Close();
            }
            catch
            {
                Show();
                Visibility = Visibility.Visible;
            }
        }
    }
}
