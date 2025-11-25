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
        private Rectangle _rect = null!;
        private RectangleGeometry _fullGeometry = null!;
        private RectangleGeometry _selectionGeometry = null!;
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
            // 투명한 창으로 시작 (사진처럼)
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true; // 투명도 허용
            Background = Brushes.Transparent; // 완전 투명 배경
            Cursor = Cursors.Arrow;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            UseLayoutRounding = true;
            KeyDown += OnKeyDown;

            vLeft = SystemParameters.VirtualScreenLeft;
            vTop = SystemParameters.VirtualScreenTop;
            vWidth = SystemParameters.VirtualScreenWidth;
            vHeight = SystemParameters.VirtualScreenHeight;

            // 작은 창으로 시작 (전체화면 아님)
            Width = 400;
            Height = 300;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            // 투명한 캔버스 생성
            _canvas = new Canvas { Width = Width, Height = Height, SnapsToDevicePixels = true };
            _canvas.Background = Brushes.Transparent;
            Content = _canvas;

            // 지오메트리 초기화 (오버레이용이지만 투명하게 유지)
            _fullGeometry = new RectangleGeometry(new Rect(0, 0, Width, Height));
            _selectionGeometry = new RectangleGeometry(new Rect(0, 0, 0, 0));

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

            // Build header content: 타사 스타일 - 심플한 UI
            var headerWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            _tbWidth = new TextBox { 
                Width = 50, 
                Height = 22, 
                TextAlignment = TextAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center, 
                VerticalContentAlignment = VerticalAlignment.Center, // 텍스트 수직 중앙
                Margin = new Thickness(0, 0, 4, 0), 
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0) 
            };
            var times = new TextBlock { Text = "x", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
            _tbHeight = new TextBox { 
                Width = 50, 
                Height = 22, 
                TextAlignment = TextAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center, 
                VerticalContentAlignment = VerticalAlignment.Center, // 텍스트 수직 중앙
                Margin = new Thickness(0, 0, 8, 0), 
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0) 
            };

            // 잠금 버튼 (금색 아이콘)
            var btnLock = new Button
            {
                Content = new TextBlock { Text = "🔓", Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)), FontSize = 13, VerticalAlignment = VerticalAlignment.Center }, // 금색
                Width = 24, Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "위치/크기 잠금"
            };
            
            var isLockedFlag = false;
            btnLock.Click += (s, e) => 
            {
                isLockedFlag = !isLockedFlag;
                var lockText = (TextBlock)btnLock.Content;
                lockText.Text = isLockedFlag ? "🔒" : "🔓";
                lockText.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // 금색 유지
                btnLock.Background = isLockedFlag ? new SolidColorBrush(Color.FromArgb(30, 255, 215, 0)) : Brushes.Transparent;
                
                // 잠금 시 Thumb 숨기기
                _thumbTopLeft.Visibility = isLockedFlag ? Visibility.Collapsed : Visibility.Visible;
                _thumbTopRight.Visibility = isLockedFlag ? Visibility.Collapsed : Visibility.Visible;
                _thumbBottomLeft.Visibility = isLockedFlag ? Visibility.Collapsed : Visibility.Visible;
                _thumbBottomRight.Visibility = isLockedFlag ? Visibility.Collapsed : Visibility.Visible;
            };
            
            // 캡처 버튼
            _btnCapture = new Button
            {
                Content = new TextBlock { Text = "캡처", Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center },
                Padding = new Thickness(8, 0, 8, 0),
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0)
            };

            // 닫기 버튼
            _btnClose = new Button
            {
                Content = new TextBlock { Text = "✕", Foreground = Brushes.White, FontSize = 10, VerticalAlignment = VerticalAlignment.Center },
                Width = 24, Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "닫기"
            };
            _btnClose.Click += (s, e) => Close();

            headerWrap.Children.Add(_tbWidth);
            headerWrap.Children.Add(times);
            headerWrap.Children.Add(_tbHeight);
            headerWrap.Children.Add(btnLock);
            headerWrap.Children.Add(_btnCapture);

            // Grid로 좌우 분리 (좌측: 컨트롤, 우측: X 버튼)
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(headerWrap, 0);
            Grid.SetColumn(_btnClose, 2);
            headerGrid.Children.Add(headerWrap);
            headerGrid.Children.Add(_btnClose);

            _headerBar = new Border
            {
                Child = headerGrid,
                Background = new SolidColorBrush(Color.FromArgb(230, 40, 40, 40)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Interactions for dragging by header - 창 전체를 이동
            _headerBar.MouseLeftButtonDown += (s, e) => 
            { 
                _isHeaderDragging = true; 
                _headerDragStart = e.GetPosition(this); // 창 기준 좌표로 변경
                Mouse.Capture(_headerBar); 
            };
            _headerBar.MouseMove += (s, e) =>
            {
                if (!_isHeaderDragging) return;
                var currentPos = e.GetPosition(this); // 창 기준 좌표
                var dx = currentPos.X - _headerDragStart.X;
                var dy = currentPos.Y - _headerDragStart.Y;
                
                // 창의 위치를 직접 변경
                var newLeft = Left + dx;
                var newTop = Top + dy;
                
                // 화면 경계 제한 제거 (모니터 끝까지 이동 가능)
                
                Left = newLeft;
                Top = newTop;
                
                // 타이틀바 위치 업데이트 (상단 충돌 시 하단 이동)
                SetRect(GetRect());
            };
            _headerBar.MouseLeftButtonUp += (s, e) => { if (Mouse.Captured == _headerBar) Mouse.Capture(null); _isHeaderDragging = false; };

            _btnCapture.Click += (s, e) => CaptureAndNotify();
            _tbWidth.LostKeyboardFocus += (s, e) => ApplyTextboxSize();
            _tbHeight.LostKeyboardFocus += (s, e) => ApplyTextboxSize();
            _tbWidth.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyTextboxSize(); };
            _tbHeight.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyTextboxSize(); };

            _canvas.Children.Add(_headerBar);

            // 작은 창 내에서 초기 사각형 설정 (창 크기 기준)
            double initW = Width * 0.8; // 창 크기의 80%
            double initH = Height * 0.7; // 창 크기의 70%
            double initL = (Width - initW) / 2;
            double initT = (Height - initH) / 2;
            SetRect(new Rect(initL, initT, initW, initH));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
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
            try
            {
                _isDragging = true;
                _dragStart = e.GetPosition(_canvas);
                _rectStart = GetRect();
                Mouse.Capture(_rect);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rect_MouseLeftButtonDown 오류: {ex.Message}");
                _isDragging = false;
            }
        }

        private void Rect_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!_isDragging) return;
                
                Point p = e.GetPosition(_canvas);
                double dx = p.X - _dragStart.X;
                double dy = p.Y - _dragStart.Y;

                Rect newRect = new Rect(_rectStart.X + dx, _rectStart.Y + dy, _rectStart.Width, _rectStart.Height);
                newRect = ClampToBounds(newRect);
                SetRect(newRect);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rect_MouseMove 오류: {ex.Message}");
                _isDragging = false;
                if (Mouse.Captured == _rect) Mouse.Capture(null);
            }
        }

        private void Rect_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (Mouse.Captured == _rect) Mouse.Capture(null);
                _isDragging = false;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rect_MouseLeftButtonUp 오류: {ex.Message}");
                _isDragging = false;
            }
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
            
            // 창 크기를 선택 영역에 맞게 동적으로 조정
            AdjustWindowSizeForRect(nr);
            
            // 조정된 창 크기 기준으로 다시 제한
            nr = ClampToBounds(nr);
            SetRect(nr);
        }

        private Rect ClampToBounds(Rect rect)
        {
            // 창 크기 기준으로 제한 (투명한 창이므로)
            double l = Math.Max(0, rect.Left);
            double t = Math.Max(0, rect.Top);
            double r = Math.Min(Width, rect.Right);
            double b = Math.Min(Height, rect.Bottom);
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

            // 픽셀 라벨 업데이트 제거 (더 이상 표시하지 않음)
            _headerBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double hbh = _headerBar.DesiredSize.Height;

            // 상단 충돌 체크: 창의 절대 위치 + 사각형 상단 위치로 화면 좌표 계산
            double screenTop = Top + rect.Top;
            bool shouldPlaceAtBottom = screenTop < 100;

            double headerLeft = rect.Left;
            double headerTop;

            if (shouldPlaceAtBottom)
            {
                // 사각형 아래쪽에 배치
                headerTop = rect.Bottom + 10;
            }
            else
            {
                // 사각형 위쪽에 배치 (기본)
                headerTop = rect.Top - hbh;
            }

            // Titlebar style: same width as selection
            _headerBar.Width = Math.Max(120, rect.Width);
            
            // Clamp horizontally and vertically
            headerLeft = Math.Max(0, Math.Min(headerLeft, Width - _headerBar.Width));
            headerTop = Math.Max(0, Math.Min(headerTop, Height - hbh));
            
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

            var newRect = new Rect(left, top, Math.Max(1, newW), Math.Max(1, newH));
            
            // 창 크기를 선택 영역에 맞게 동적으로 조정
            AdjustWindowSizeForRect(newRect);
            
            SetRect(newRect);
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

        private void AdjustWindowSizeForRect(Rect rect)
        {
            // 선택 영역에 맞춰 창 크기를 동적으로 조정 (확장 및 축소)
            const double margin = 0; // 여유 공간
            const double minWindowSize = 50; // 최소 창 크기
            
            double requiredWidth = rect.Right + margin;
            double requiredHeight = rect.Bottom + margin;
            
            // 최소 크기 보장
            requiredWidth = Math.Max(requiredWidth, minWindowSize);
            requiredHeight = Math.Max(requiredHeight, minWindowSize);
            
            // 화면 크기 제한
            double maxWidth = SystemParameters.VirtualScreenWidth * 0.9;
            double maxHeight = SystemParameters.VirtualScreenHeight * 0.9;
            
            double newWidth = Math.Min(requiredWidth, maxWidth);
            double newHeight = Math.Min(requiredHeight, maxHeight);
            
            // 창 크기가 변경되었을 때만 업데이트
            if (Math.Abs(newWidth - Width) > 1 || Math.Abs(newHeight - Height) > 1)
            {
                Width = newWidth;
                Height = newHeight;
                
                // 캔버스 크기도 함께 조정
                _canvas.Width = newWidth;
                _canvas.Height = newHeight;
                
                // 지오메트리 업데이트
                _fullGeometry.Rect = new Rect(0, 0, newWidth, newHeight);
            }
        }

        private void CaptureAndNotify()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            Rect r = GetRect();
            
            // 창의 위치를 고려한 실제 화면 좌표 계산
            int pxLeft = (int)Math.Round((Left + r.Left) * dpi.DpiScaleX);
            int pxTop = (int)Math.Round((Top + r.Top) * dpi.DpiScaleY);
            int pxWidth = (int)Math.Round(r.Width * dpi.DpiScaleX);
            int pxHeight = (int)Math.Round(r.Height * dpi.DpiScaleY);

            if (pxWidth < 5 || pxHeight < 5)
            {
                MessageBox.Show("선택 영역이 너무 작습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 창을 숨기고 캡처한 후 다시 보이게 하여 창 사각형이 캡처되지 않도록 함
            BitmapSource? image = null;
            try
            {
                // 창을 잠시 숨김
                Hide();
                
                // 약간의 지연을 주어 창이 완전히 숨겨지도록 함
                System.Threading.Thread.Sleep(10);
                
                var area = new Int32Rect(pxLeft, pxTop, pxWidth, pxHeight);
                image = ScreenCaptureUtility.CaptureArea(area);
                
                // 창을 다시 보임
                Show();
            }
            catch (Exception ex)
            {
                // 오류 발생 시에도 창을 다시 보이게 함
                Show();
                MessageBox.Show($"캡처 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (image != null)
            {
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
}
