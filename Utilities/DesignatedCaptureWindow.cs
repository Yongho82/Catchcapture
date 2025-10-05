using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture.Utilities
{
    public class DesignatedCaptureWindow : Window
    {
        // Public result
        public Int32Rect SelectedArea { get; private set; }

        // Visuals
        private Canvas _canvas = null!;
        private Image _screenImage = null!;
        private BitmapSource _screenCapture = null!;
        private Rectangle _rect = null!;
        private RectangleGeometry _fullGeometry = null!;
        private RectangleGeometry _selectionGeometry = null!;
        private System.Windows.Shapes.Path _overlayPath = null!;
        private TextBox _tbWidth = null!;
        private TextBox _tbHeight = null!;
        private Button _btnCapture = null!;
        private Button _btnClose = null!;
        private TextBlock _sizeText = null!;
        private Border _headerBar = null!;

        // Drag/resize
        private bool _isDragging;
        private Point _dragStart;
        private Rect _rectStart;

        private Thumb _thumbTopLeft = null!;
        private Thumb _thumbTopRight = null!;
        private Thumb _thumbBottomLeft = null!;
        private Thumb _thumbBottomRight = null!;

        // Header dragging
        private bool _isHeaderDragging;
        private Point _headerDragStart;
        private bool _hasCentered = false;

        // Virtual screen bounds
        private double vLeft;
        private double vTop;
        private double vWidth;
        private double vHeight;

        // Fired whenever a capture is performed (overlay stays open)
        public event Action<System.Windows.Media.Imaging.BitmapSource>? CaptureCompleted;

        public DesignatedCaptureWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = false; // better performance
            Background = Brushes.Black;
            Cursor = Cursors.Arrow;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            UseLayoutRounding = true;
            KeyDown += OnKeyDown;

            vLeft = SystemParameters.VirtualScreenLeft;
            vTop = SystemParameters.VirtualScreenTop;
            vWidth = SystemParameters.VirtualScreenWidth;
            vHeight = SystemParameters.VirtualScreenHeight;

            Left = vLeft;
            Top = vTop;
            Width = vWidth;
            Height = vHeight;
            WindowState = WindowState.Normal;

            // Canvas
            _canvas = new Canvas { Width = vWidth, Height = vHeight, SnapsToDevicePixels = true };
            Content = _canvas;

            // Freeze background frame for smooth overlay
            _screenCapture = ScreenCaptureUtility.CaptureScreen();
            _screenImage = new Image { Source = _screenCapture };
            Panel.SetZIndex(_screenImage, -2);
            _canvas.Children.Add(_screenImage);

            // Dim overlay with hole for selection
            _fullGeometry = new RectangleGeometry(new Rect(0, 0, vWidth, vHeight));
            _selectionGeometry = new RectangleGeometry(new Rect(0, 0, 0, 0));
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(_fullGeometry);
            group.Children.Add(_selectionGeometry);

            _overlayPath = new System.Windows.Shapes.Path
            {
                Data = group,
                Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0))
            };
            _overlayPath.IsHitTestVisible = false;
            _canvas.Children.Add(_overlayPath);

            // Selection rectangle
            _rect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(72, 152, 255)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            _rect.MouseLeftButtonDown += Rect_MouseLeftButtonDown;
            _rect.MouseMove += Rect_MouseMove;
            _rect.MouseLeftButtonUp += Rect_MouseLeftButtonUp;
            _canvas.Children.Add(_rect);

            // Resize thumbs
            _thumbTopLeft = CreateThumb();
            _thumbTopRight = CreateThumb();
            _thumbBottomLeft = CreateThumb();
            _thumbBottomRight = CreateThumb();
            _canvas.Children.Add(_thumbTopLeft);
            _canvas.Children.Add(_thumbTopRight);
            _canvas.Children.Add(_thumbBottomLeft);
            _canvas.Children.Add(_thumbBottomRight);

            _thumbTopLeft.DragDelta += (s, e) => ResizeFromCorner(isLeft: true, isTop: true, e);
            _thumbTopRight.DragDelta += (s, e) => ResizeFromCorner(isLeft: false, isTop: true, e);
            _thumbBottomLeft.DragDelta += (s, e) => ResizeFromCorner(isLeft: true, isTop: false, e);
            _thumbBottomRight.DragDelta += (s, e) => ResizeFromCorner(isLeft: false, isTop: false, e);

            // Header bar with size (top-left of the selection)
            _sizeText = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Text = "0 x 0",
                VerticalAlignment = VerticalAlignment.Center
            };

            // Build header content: size text + inputs + capture button
            var headerWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _btnCapture = new Button
            {
                Content = new TextBlock { Text = "캡처", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
                Padding = new Thickness(10, 4, 10, 4),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _tbWidth = new TextBox { Width = 70, Height = 24, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var times = new TextBlock { Text = "x", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            _tbHeight = new TextBox { Width = 70, Height = 24, TextAlignment = TextAlignment.Center };

            headerWrap.Children.Add(_btnCapture);
            headerWrap.Children.Add(_tbWidth);
            headerWrap.Children.Add(times);
            headerWrap.Children.Add(_tbHeight);

            // Put size text at the leftmost with some margin
            var leftWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var sizeLabel = new TextBlock { Text = "크기:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 6, 0) };
            leftWrap.Children.Add(sizeLabel);
            leftWrap.Children.Add(_sizeText);

            _btnClose = new Button
            {
                Content = new TextBlock { Text = "✕", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
                Padding = new Thickness(8, 0, 8, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "닫기"
            };
            _btnClose.Click += (s, e) => { DialogResult = false; Close(); };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // left: size text
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); // gap
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // middle: capture + inputs
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // spacer
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // right: close

            Grid.SetColumn(leftWrap, 0);
            Grid.SetColumn(headerWrap, 2);
            Grid.SetColumn(_btnClose, 4);
            headerGrid.Children.Add(leftWrap);
            headerGrid.Children.Add(headerWrap);
            headerGrid.Children.Add(_btnClose);

            _headerBar = new Border
            {
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.35,
                    Direction = 270
                },
                Padding = new Thickness(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(67, 97, 238),
                    Color.FromRgb(58, 86, 212),
                    new Point(0, 0), new Point(1, 1))
            };
            _headerBar.Child = headerGrid;

            // Interactions for dragging by header
            _headerBar.MouseLeftButtonDown += (s, e) => { _isHeaderDragging = true; _headerDragStart = e.GetPosition(_canvas); _rectStart = GetRect(); Mouse.Capture(_headerBar); };
            _headerBar.MouseMove += (s, e) =>
            {
                if (!_isHeaderDragging) return;
                var p = e.GetPosition(_canvas);
                var dx = p.X - _headerDragStart.X;
                var dy = p.Y - _headerDragStart.Y;
                Rect nr = new Rect(_rectStart.X + dx, _rectStart.Y + dy, _rectStart.Width, _rectStart.Height);
                nr = ClampToBounds(nr);
                SetRect(nr);
            };
            _headerBar.MouseLeftButtonUp += (s, e) => { if (Mouse.Captured == _headerBar) Mouse.Capture(null); _isHeaderDragging = false; };

            _btnCapture.Click += (s, e) => CaptureAndNotify();
            _tbWidth.LostKeyboardFocus += (s, e) => ApplyTextboxSize();
            _tbHeight.LostKeyboardFocus += (s, e) => ApplyTextboxSize();
            _tbWidth.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyTextboxSize(); };
            _tbHeight.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyTextboxSize(); };

            _canvas.Children.Add(_headerBar);

            // Initial rectangle centered at 400x400 px (clamped to screen)
            double initW = Math.Min(400, vWidth - 20);
            double initH = Math.Min(400, vHeight - 20);
            double initL = Math.Max(0, (vWidth - initW) / 2);
            double initT = Math.Max(0, (vHeight - initH) / 2);
            SetRect(new Rect(initL, initT, initW, initH));

            // Re-center after layout/activation to handle DPI and multi-monitor quirks
            Loaded += (s, e) => Dispatcher.BeginInvoke(new Action(EnsureFirstCenter));
            ContentRendered += (s, e) => Dispatcher.BeginInvoke(new Action(EnsureFirstCenter));
            Activated += (s, e) => Dispatcher.BeginInvoke(new Action(CenterSelection));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private Thumb CreateThumb()
        {
            return new Thumb
            {
                Width = 10,
                Height = 10,
                Background = Brushes.DeepSkyBlue,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.SizeAll
            };
        }

        private void Rect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(_canvas);
            _rectStart = GetRect();
            Mouse.Capture(_rect);
        }

        private void Rect_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point p = e.GetPosition(_canvas);
            double dx = p.X - _dragStart.X;
            double dy = p.Y - _dragStart.Y;

            Rect newRect = new Rect(_rectStart.X + dx, _rectStart.Y + dy, _rectStart.Width, _rectStart.Height);
            newRect = ClampToBounds(newRect);
            SetRect(newRect);
        }

        private void Rect_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Captured == _rect) Mouse.Capture(null);
            _isDragging = false;
        }

        private void ResizeFromCorner(bool isLeft, bool isTop, DragDeltaEventArgs e)
        {
            Rect r = GetRect();
            double minW = 20, minH = 20;

            double newLeft = r.Left;
            double newTop = r.Top;
            double newRight = r.Right;
            double newBottom = r.Bottom;

            if (isLeft)
                newLeft += e.HorizontalChange;
            else
                newRight += e.HorizontalChange;

            if (isTop)
                newTop += e.VerticalChange;
            else
                newBottom += e.VerticalChange;

            // Normalize
            if (newRight - newLeft < minW)
            {
                if (isLeft) newLeft = newRight - minW; else newRight = newLeft + minW;
            }
            if (newBottom - newTop < minH)
            {
                if (isTop) newTop = newBottom - minH; else newBottom = newTop + minH;
            }

            Rect nr = new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
            nr = ClampToBounds(nr);
            SetRect(nr);
        }

        private Rect ClampToBounds(Rect rect)
        {
            double l = Math.Max(0, rect.Left);
            double t = Math.Max(0, rect.Top);
            double r = Math.Min(vWidth, rect.Right);
            double b = Math.Min(vHeight, rect.Bottom);
            double w = Math.Max(1, r - l);
            double h = Math.Max(1, b - t);
            return new Rect(l, t, w, h);
        }

        private Rect GetRect()
        {
            double l = Canvas.GetLeft(_rect);
            double t = Canvas.GetTop(_rect);
            double w = _rect.Width;
            double h = _rect.Height;
            return new Rect(l, t, w, h);
        }

        private void SetRect(Rect rect)
        {
            Canvas.SetLeft(_rect, rect.Left);
            Canvas.SetTop(_rect, rect.Top);
            _rect.Width = rect.Width;
            _rect.Height = rect.Height;

            // Update overlay hole
            _selectionGeometry.Rect = rect;

            // Move thumbs
            PositionThumbs(rect);

            // Update header text and position at top-left inside the rectangle
            _sizeText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
            _headerBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double hbh = _headerBar.DesiredSize.Height;

            // Ensure there is space ABOVE the rectangle for the header; if not, push the rectangle down.
            if (rect.Top - hbh < 0)
            {
                double shiftDown = (hbh - rect.Top) + 8; // 8px padding
                rect = new Rect(rect.Left, Math.Min(rect.Top + shiftDown, vHeight - rect.Height - 8), rect.Width, rect.Height);
            }

            // Update rectangle (in case we shifted it)
            Canvas.SetLeft(_rect, rect.Left);
            Canvas.SetTop(_rect, rect.Top);
            _rect.Width = rect.Width;
            _rect.Height = rect.Height;
            _selectionGeometry.Rect = rect;
            PositionThumbs(rect);

            // Titlebar style: same width as selection and attached ABOVE the selection
            _headerBar.Width = Math.Max(120, rect.Width);
            double headerLeft = rect.Left;
            double headerTop = rect.Top - hbh;
            // Clamp horizontally, and ensure stay within vertical bounds
            headerLeft = Math.Max(0, Math.Min(headerLeft, vWidth - _headerBar.Width));
            headerTop = Math.Max(0, Math.Min(headerTop, vHeight - hbh));
            Canvas.SetLeft(_headerBar, headerLeft);
            Canvas.SetTop(_headerBar, headerTop);

            // Update textbox values without triggering parse loops
            _tbWidth.Text = ((int)Math.Round(rect.Width)).ToString();
            _tbHeight.Text = ((int)Math.Round(rect.Height)).ToString();
        }

        private void PositionThumbs(Rect rect)
        {
            double half = _thumbTopLeft.Width / 2.0;
            Canvas.SetLeft(_thumbTopLeft, rect.Left - half);
            Canvas.SetTop(_thumbTopLeft, rect.Top - half);

            Canvas.SetLeft(_thumbTopRight, rect.Right - half);
            Canvas.SetTop(_thumbTopRight, rect.Top - half);

            Canvas.SetLeft(_thumbBottomLeft, rect.Left - half);
            Canvas.SetTop(_thumbBottomLeft, rect.Bottom - half);

            Canvas.SetLeft(_thumbBottomRight, rect.Right - half);
            Canvas.SetTop(_thumbBottomRight, rect.Bottom - half);
        }

        private void ApplyTextboxSize()
        {
            if (!int.TryParse(_tbWidth.Text, out int w) || w <= 0) return;
            if (!int.TryParse(_tbHeight.Text, out int h) || h <= 0) return;

            Rect r = GetRect();
            double newW = Math.Min(vWidth - 8, w);
            double newH = Math.Min(vHeight - 8, h);

            // If the new size would overflow to the right/bottom, shift left/up to keep visible
            double left = r.Left;
            double top = r.Top;
            if (left + newW > vWidth - 8) left = Math.Max(8, vWidth - newW - 8);
            if (top + newH > vHeight - 8) top = Math.Max(8, vHeight - newH - 8);

            SetRect(new Rect(left, top, Math.Max(1, newW), Math.Max(1, newH)));
        }

        private void CenterSelection()
        {
            // Safety margin to keep header and rectangle away from edges
            const double margin = 12;
            double w = Math.Min(400, Math.Max(100, vWidth - margin * 2));
            double h = Math.Min(400, Math.Max(100, vHeight - margin * 2));
            double l = Math.Max(margin, (vWidth - w) / 2);
            double t = Math.Max(margin, (vHeight - h) / 2);

            // If current rect is largely offscreen (e.g., due to virtual origin), force center
            Rect r = GetRect();
            bool offLeft = r.Right < margin;
            bool offTop = r.Bottom < margin;
            bool offRight = r.Left > vWidth - margin;
            bool offBottom = r.Top > vHeight - margin;
            if (double.IsNaN(r.Left) || offLeft || offTop || offRight || offBottom)
            {
                SetRect(new Rect(l, t, w, h));
            }
            else
            {
                // Otherwise, just ensure fully visible
                EnsureRectFullyVisible();
            }
        }

        private void EnsureFirstCenter()
        {
            if (_hasCentered) { CenterSelection(); return; }
            _hasCentered = true;

            const double margin = 12;
            double primW = SystemParameters.PrimaryScreenWidth;
            double primH = SystemParameters.PrimaryScreenHeight;
            double primOriginX = -vLeft;
            double primOriginY = -vTop;
            double w = Math.Min(400, Math.Max(100, primW - margin * 2));
            double h = Math.Min(400, Math.Max(100, primH - margin * 2));
            double l = primOriginX + Math.Max(margin, (primW - w) / 2);
            double t = primOriginY + Math.Max(margin, (primH - h) / 2);
            SetRect(new Rect(l, t, w, h));
        }

        private void EnsureRectFullyVisible()
        {
            const double margin = 8;
            Rect r = GetRect();
            double l = Math.Max(margin, Math.Min(r.Left, vWidth - r.Width - margin));
            double t = Math.Max(margin, Math.Min(r.Top, vHeight - r.Height - margin));
            SetRect(new Rect(l, t, r.Width, r.Height));
        }

        private void CaptureAndNotify()
        {
            // Convert DIPs to device pixels and offset with virtual origin
            var dpi = VisualTreeHelper.GetDpi(this);
            Rect r = GetRect();
            int pxLeft = (int)Math.Round((r.Left + vLeft) * dpi.DpiScaleX);
            int pxTop = (int)Math.Round((r.Top + vTop) * dpi.DpiScaleY);
            int pxWidth = (int)Math.Round(r.Width * dpi.DpiScaleX);
            int pxHeight = (int)Math.Round(r.Height * dpi.DpiScaleY);

            if (pxWidth < 5 || pxHeight < 5)
            {
                MessageBox.Show("선택 영역이 너무 작습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Capture the selected area and raise event (keep overlay open)
            var area = new Int32Rect(pxLeft, pxTop, pxWidth, pxHeight);
            var image = ScreenCaptureUtility.CaptureArea(area);
            try { CaptureCompleted?.Invoke(image); } catch { }

            // Show 1-second toast
            try
            {
                var toast = new GuideWindow("캡처되었습니다.", TimeSpan.FromSeconds(1));
                toast.Owner = this.Owner ?? this;
                toast.Show();
            }
            catch { }
        }
    }
}
