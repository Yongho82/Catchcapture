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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

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
        private const int VK_ESCAPE = 0x1B;
        
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
        
        // This will hold the frozen screen image
        private BitmapSource? _fullScreenImage;

        public BitmapSource? CapturedImage { get; private set; }

        // --- Magnifier Fields ---
        private const double MagnifierSize = 150;
        private const double MagnificationFactor = 3.0;
        private bool showMagnifier = true;
        private Border? magnifierBorder;
        private Image? magnifierImage;
        private Canvas? magnifierCanvas;
        private Line? magnifierCrosshairH;
        private Line? magnifierCrosshairV;
        private TextBlock? coordsTextBlock;
        private Rectangle? colorPreviewRect;
        private TextBlock? colorValueTextBlock;
        private bool isColorHexFormat = true;
        private Color lastHoverColor = Colors.Transparent;

        // Key State Tracking
        private bool _wasShiftDown = false;
        private bool _wasCDown = false;

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

            // Start invisible to take screenshot first
            Opacity = 0;

            // Fullscreen coverage
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            InitializeUI();
            
            // Install Mouse Hook
            _proc = HookCallback;
            _hookID = SetHook(_proc);

            // Timer for polling detection
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(20);
            _timer.Tick += OnTimerTick;
            
            Loaded += async (s, e) => {
                // 1. Capture Full Screen immediately (Freeze effect)
                await CaptureFullScreenAsync();

                // 2. Show Window (with frozen background)
                Opacity = 1;
                
                // 3. Start logic
                _timer.Start();
                Activate();
            };

            Closed += (s, e) => {
                _timer.Stop();
                UnhookWindowsHookEx(_hookID);
            };
        }
        
        private async Task CaptureFullScreenAsync()
        {
            try
            {
                await Task.Delay(50); // Small delay to ensure clean state
                
                // Calculate total screen area in pixels (heuristic)
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0, dpiY = 1.0;
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                int pxX = (int)(Left * dpiX); // Likely 0 or negative depending on monitor setup
                int pxY = (int)(Top * dpiY);
                int pxW = (int)(Width * dpiX);
                int pxH = (int)(Height * dpiY);
                
                // Use screen capture utility to get the full desktop image
                // Assuming CaptureArea takes screen coordinates (pixels)
                var area = new Int32Rect(pxX, pxY, pxW, pxH);
                _fullScreenImage = Utilities.ScreenCaptureUtility.CaptureArea(area);
                
                if (_fullScreenImage != null)
                {
                    Background = new ImageBrush(_fullScreenImage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fullscreen capture failed: {ex.Message}");
            }
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
                    POINT pt;
                    GetCursorPos(out pt);
                    _dragStartPoint = new Point(pt.X - Left, pt.Y - Top);
                    _isDragging = true;

                    Dispatcher.BeginInvoke(() => {
                        // Don't hide highlight immediately
                        _selectionRect.Width = 0;
                        _selectionRect.Height = 0;
                        Canvas.SetLeft(_selectionRect, _dragStartPoint.X);
                        Canvas.SetTop(_selectionRect, _dragStartPoint.Y);
                    });

                    return (IntPtr)1; // Eat event
                }
                else if (msg == WM_LBUTTONUP)
                {
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
                               // Single Click: Capture Highlighted
                               if (_highlightRect.Visibility == Visibility.Visible && !_currentElementRect.IsEmpty)
                               {
                                   CaptureArea(_currentElementRect);
                               }
                           }
                           else
                           {
                               // Drag: Capture Selection
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
                    Dispatcher.BeginInvoke(() => {
                        DialogResult = false;
                        Close();
                    });
                    return (IntPtr)1; // Eat event
                }
                else if (msg == WM_MOUSEMOVE)
                {
                   POINT pt;
                   GetCursorPos(out pt);
                   Point currentPos = new Point(pt.X - Left, pt.Y - Top);

                   if (_isDragging)
                   {
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
                            
                            if (_selectionRect.Visibility != Visibility.Visible)
                            {
                                _highlightRect.Visibility = Visibility.Collapsed;
                                _selectionRect.Visibility = Visibility.Visible;
                                _infoText.Visibility = Visibility.Visible;
                            }
                        });
                   }

                   // Update Magnifier
                   Dispatcher.BeginInvoke(() => {
                       UpdateMagnifier(currentPos);
                   });
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            // WS_EX_TRANSPARENT allows UIA to see through.
            // Even with ImageBrush background, this style makes mouse events pass through (caught by hook)
            // and hit tests pass through (good for UIA).
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

            // 1. Highlight Rectangle - Blue
            _highlightRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(20, 30, 144, 255)),
                Visibility = Visibility.Collapsed
            };

            // 2. Selection Rectangle (Red Dotted)
            _selectionRect = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 2, 2 }, // Dotted
                Fill = Brushes.Transparent, // No fill for selection, or slight red tint? Standard was transparent bounds.
                // Keeping it transparent to match "Our basic area capture" (SnippingWindow) which is usually just border.
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
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 2 }
            };
            Canvas.SetLeft(_guideText, (Width - 400) / 2);
            Canvas.SetTop(_guideText, Height / 2 - 50);

            canvas.Children.Add(_guideText);
            canvas.Children.Add(_highlightRect);
            canvas.Children.Add(_selectionRect);
            canvas.Children.Add(_infoText);
            
            Content = canvas;

            // Initialize Magnifier UI
            CreateMagnifier(canvas);

            // Settings load
            var settings = Models.Settings.Load();
            showMagnifier = settings.ShowMagnifier;
            if (!showMagnifier && magnifierBorder != null) magnifierBorder.Visibility = Visibility.Collapsed;
            
             Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() => { 
                if(_guideText != null) _guideText.Visibility = Visibility.Collapsed; 
             }));
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            // Escape Check
            if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                DialogResult = false;
                Close();
                return;
            }

            // Shift Check (Hex/RGB Toggle)
            // VK_SHIFT = 0x10
            bool isShiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            if (isShiftDown && !_wasShiftDown)
            {
                isColorHexFormat = !isColorHexFormat;
                UpdateColorInfoText();
            }
            _wasShiftDown = isShiftDown;

            // C Key Check (Copy Color)
            // 'C' key code is 0x43
            bool isCDown = (GetAsyncKeyState(0x43) & 0x8000) != 0;
            if (isCDown && !_wasCDown)
            {
               CopyColorToClipboard(true);
            }
            _wasCDown = isCDown;

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
                    if (element.Current.ProcessId == Process.GetCurrentProcess().Id)
                        return;

                    Rect r = element.Current.BoundingRectangle;
                    if (r.Width == 0 || r.Height == 0) return;
                    
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

        private void CaptureArea(Rect rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            if (_fullScreenImage == null) return;

            try 
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0, dpiY = 1.0;
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }

                int x = (int)(rect.X * dpiX);
                int y = (int)(rect.Y * dpiY);
                int w = (int)(rect.Width * dpiX);
                int h = (int)(rect.Height * dpiY);

                // Check bounds within full screen image
                if (x < 0) x = 0;
                if (y < 0) y = 0;
                if (x + w > _fullScreenImage.PixelWidth) w = _fullScreenImage.PixelWidth - x;
                if (y + h > _fullScreenImage.PixelHeight) h = _fullScreenImage.PixelHeight - y;

                if (w > 0 && h > 0)
                {
                    var cropped = new CroppedBitmap(_fullScreenImage, new Int32Rect(x, y, w, h));
                    CapturedImage = cropped;
                    DialogResult = true;
                    Close();
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Capture failed: {ex.Message}");
                // Fallback attempt?
                DialogResult = false; 
                Close();
            }
        }
        private void CreateMagnifier(Canvas parentCanvas)
        {
            double borderThick = 1.0;
            double cornerRad = 15.0; // Rounded corners
            // Calculate inner size so it fits inside the border without being clipped
            // Border Width is fixed to MagnifierSize (150), so content must be smaller by 2*Thickness
            double innerSize = MagnifierSize - (borderThick * 2); 

            // Magnifier Image
            magnifierImage = new Image
            {
                Width = innerSize,
                Height = innerSize,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(magnifierImage, BitmapScalingMode.NearestNeighbor);

            // Crosshairs (Red Dotted)
            double centerPos = innerSize / 2.0;
            
            magnifierCrosshairH = new Line
            {
                X1 = 0, X2 = innerSize,
                Y1 = centerPos, Y2 = centerPos,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                IsHitTestVisible = false
            };

            magnifierCrosshairV = new Line
            {
                X1 = centerPos, X2 = centerPos,
                Y1 = 0, Y2 = innerSize,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                IsHitTestVisible = false
            };

            // Center pixel box
            var centerFrame = new Rectangle
            {
                Width = 7, Height = 7,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerFrame, centerPos - 3.5);
            Canvas.SetTop(centerFrame, centerPos - 3.5);

            // Canvas
            magnifierCanvas = new Canvas
            {
                Width = innerSize,
                Height = innerSize,
                Background = Brushes.White
            };
            magnifierCanvas.Children.Add(magnifierImage);
            magnifierCanvas.Children.Add(magnifierCrosshairH);
            magnifierCanvas.Children.Add(magnifierCrosshairV);
            magnifierCanvas.Children.Add(centerFrame);

            // Clip
            magnifierCanvas.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, innerSize, innerSize),
                RadiusX = Math.Max(0, cornerRad - borderThick),
                RadiusY = Math.Max(0, cornerRad - borderThick)
            };

            // Info Panel
            var infoStack = new StackPanel { Orientation = Orientation.Vertical };
            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Padding = new Thickness(8),
                Child = infoStack,
                CornerRadius = new CornerRadius(0, 0, cornerRad, cornerRad)
            };

            coordsTextBlock = new TextBlock
            {
                Text = "(0, 0)",
                Foreground = Brushes.White,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var colorRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            colorPreviewRect = new Rectangle
            {
                Width = 14, Height = 14,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.White,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            colorValueTextBlock = new TextBlock
            {
                Text = "#FFFFFF",
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            colorRow.Children.Add(colorPreviewRect);
            colorRow.Children.Add(colorValueTextBlock);

            var helpText1 = new TextBlock
            {
                Text = "[C] 색상복사",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
             var helpText2 = new TextBlock
            {
                Text = "[Shift] RGB/HEX 값 바꾸기",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 5)
            };

            infoStack.Children.Add(coordsTextBlock);
            infoStack.Children.Add(colorRow);
            infoStack.Children.Add(helpText1);
            infoStack.Children.Add(helpText2);

            var containerStack = new StackPanel();
            containerStack.Children.Add(magnifierCanvas);
            containerStack.Children.Add(infoBorder);

            magnifierBorder = new Border
            {
                Width = MagnifierSize,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(borderThick),
                Background = Brushes.Black,
                Child = containerStack,
                Visibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(cornerRad)
            };
            
             magnifierBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 10,
                ShadowDepth = 3,
                Opacity = 0.7
            };

            parentCanvas.Children.Add(magnifierBorder);
            Panel.SetZIndex(magnifierBorder, 2000); // High Z-Index
        }

        private void UpdateMagnifier(Point mousePos)
        {
            if (!showMagnifier) return;
            if (magnifierBorder == null || magnifierImage == null || _fullScreenImage == null)
                return;

            try
            {
                magnifierBorder.Visibility = Visibility.Visible;

                var dpi = VisualTreeHelper.GetDpi(this);
                int centerX = (int)(mousePos.X * dpi.DpiScaleX);
                int centerY = (int)(mousePos.Y * dpi.DpiScaleY);

                // Zoomed Image
                int cropSize = (int)(MagnifierSize / MagnificationFactor);
                int halfCrop = cropSize / 2;

                int cropX = Math.Max(0, Math.Min(centerX - halfCrop, _fullScreenImage.PixelWidth - cropSize));
                int cropY = Math.Max(0, Math.Min(centerY - halfCrop, _fullScreenImage.PixelHeight - cropSize));
                int cropW = Math.Min(cropSize, _fullScreenImage.PixelWidth - cropX);
                int cropH = Math.Min(cropSize, _fullScreenImage.PixelHeight - cropY);

                if (cropW > 0 && cropH > 0)
                {
                    var cropped = new CroppedBitmap(_fullScreenImage, new Int32Rect(cropX, cropY, cropW, cropH));
                    magnifierImage.Source = cropped;
                }

                // Color extraction
                if (centerX >= 0 && centerY >= 0 && centerX < _fullScreenImage.PixelWidth && centerY < _fullScreenImage.PixelHeight)
                {
                    byte[] pixels = new byte[4];
                    var rect = new Int32Rect(centerX, centerY, 1, 1);
                    try
                    {
                        _fullScreenImage.CopyPixels(rect, pixels, 4, 0);
                        lastHoverColor = Color.FromRgb(pixels[2], pixels[1], pixels[0]);
                        
                        if (colorPreviewRect != null) colorPreviewRect.Fill = new SolidColorBrush(lastHoverColor);
                        UpdateColorInfoText();
                    }
                    catch {}
                }

                if (coordsTextBlock != null) coordsTextBlock.Text = $"({centerX}, {centerY})";

                // Position
                double offsetX = 20; 
                double offsetY = 20; 
                double totalHeight = magnifierBorder.ActualHeight;
                if (double.IsNaN(totalHeight) || totalHeight == 0) totalHeight = MagnifierSize + 100;

                double magX = mousePos.X + offsetX;
                double magY = mousePos.Y + offsetY;

                if (magX + MagnifierSize > Width) magX = mousePos.X - MagnifierSize - offsetX;
                if (magY + totalHeight > Height) magY = mousePos.Y - totalHeight - offsetY;

                Canvas.SetLeft(magnifierBorder, magX);
                Canvas.SetTop(magnifierBorder, magY);
            }
            catch
            {
                magnifierBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateColorInfoText()
        {
            if (colorValueTextBlock == null) return;
            if (isColorHexFormat)
                colorValueTextBlock.Text = $"#{lastHoverColor.R:X2}{lastHoverColor.G:X2}{lastHoverColor.B:X2}";
            else
                colorValueTextBlock.Text = $"({lastHoverColor.R}, {lastHoverColor.G}, {lastHoverColor.B})";
        }

        private void CopyColorToClipboard(bool closeWindow = false)
        {
            try
            {
                string textToCopy = isColorHexFormat 
                    ? $"#{lastHoverColor.R:X2}{lastHoverColor.G:X2}{lastHoverColor.B:X2}"
                    : $"{lastHoverColor.R}, {lastHoverColor.G}, {lastHoverColor.B}";
                
                Clipboard.SetText(textToCopy);
                
                string msg = "클립보드에 복사되었습니다.";
                Utilities.StickerWindow.Show(msg);

                if (closeWindow)
                {
                    DialogResult = false;
                    Close();
                }
            }
            catch {}
        }
    }
}
