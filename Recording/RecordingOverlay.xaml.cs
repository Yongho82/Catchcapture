using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace CatchCapture.Recording
{
    /// <summary>
    /// 녹화 영역 선택 오버레이 - 리사이즈 가능한 영역 선택
    /// </summary>
    public partial class RecordingOverlay : Window
    {

        
        // 현재 선택 영역
        private Rect _selectionArea;
        
        // 드래그 관련
        private bool _isDragging = false;
        private bool _isResizing = false;
        private Point _dragStart;
        private string _resizeHandle = "";
        private Rect _originalArea;
        
        // 영역 변경 이벤트
        public event EventHandler<Rect>? AreaChanged;
        public event EventHandler? AreaSelectionEnded; // 선택 모드 종료 시 (완료 또는 취소)
        
        // ESC 키 이벤트 (부모 창에서 처리하도록)
        public event EventHandler? EscapePressed;
        
        // 최소 영역 크기
        private const double MIN_WIDTH = 100;
        private const double MIN_HEIGHT = 100;
        
        [DllImport("user32.dll")]
        private static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        public RecordingOverlay()
        {
            InitializeComponent();
            
            this.Loaded += (s, e) =>
            {
                var interop = new System.Windows.Interop.WindowInteropHelper(this);
                SetWindowDisplayAffinity(interop.Handle, WDA_EXCLUDEFROMCAPTURE);
            };

            
            // 기본 선택 영역 (주 모니터 중앙 800x600)
            var screen = SystemParameters.WorkArea;
            _selectionArea = new Rect(
                (screen.Width - 800) / 2,
                (screen.Height - 600) / 2,
                800,
                600
            );
            
            Loaded += RecordingOverlay_Loaded;
            SizeChanged += RecordingOverlay_SizeChanged;
            MouseMove += Overlay_MouseMove;
            MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
            KeyDown += RecordingOverlay_KeyDown;
        }
        
        /// <summary>
        /// 선택 영역 속성
        /// </summary>
        public Rect SelectionArea
        {
            get => _selectionArea;
            set
            {
                _selectionArea = value;
                UpdateVisuals();
                AreaChanged?.Invoke(this, _selectionArea);
            }
        }
        
        #region 초기화
        
        private void RecordingOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // 모든 모니터를 커버하도록 설정 (VirtualScreen 사용)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
            
            // 선택 영역은 주 모니터 중앙에 배치 (오버레이 기준 상대 좌표로 계산)
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen?.Bounds 
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            
            // 물리적 좌표(pixels) -> 논리적 좌표(units) 변환
            Point logicalTopLeft = this.PointFromScreen(new Point(primaryScreen.X, primaryScreen.Y));
            Point logicalBottomRight = this.PointFromScreen(new Point(primaryScreen.Right, primaryScreen.Bottom));
            
            double w = logicalBottomRight.X - logicalTopLeft.X;
            double h = logicalBottomRight.Y - logicalTopLeft.Y;
            
            _selectionArea = new Rect(
                logicalTopLeft.X + (w - 800) / 2,
                logicalTopLeft.Y + (h - 600) / 2,
                800, 600);
            
            // Windows 11 호환성: 명시적으로 Topmost와 Activate 호출
            this.Topmost = true;
            this.Activate();
            this.Focus();
            
            UpdateVisuals();
            AreaChanged?.Invoke(this, _selectionArea);
        }

        // Helper method to show/hide handles
        private void ShowHandles(bool show)
        {
             var visibility = show ? Visibility.Visible : Visibility.Collapsed;
             if (HandleNW != null) HandleNW.Visibility = visibility;
             if (HandleN != null) HandleN.Visibility = visibility;
             if (HandleNE != null) HandleNE.Visibility = visibility;
             if (HandleE != null) HandleE.Visibility = visibility;
             if (HandleSE != null) HandleSE.Visibility = visibility;
             if (HandleS != null) HandleS.Visibility = visibility;
             if (HandleSW != null) HandleSW.Visibility = visibility;
             if (HandleW != null) HandleW.Visibility = visibility;
             if (SizeLabelBorder != null) SizeLabelBorder.Visibility = visibility;
        }

        // 영역 선택 모드 멤버 변수
        private Rect _savedSelectionArea; 
        private Point _newSelectionStart;
        private bool _isSelectingNew = false;
        
        public bool IsSelectingNewArea => SelectionHitTarget != null && SelectionHitTarget.Visibility == Visibility.Visible;

        private void SelectionHitTarget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSelectingNew = true;
            _newSelectionStart = e.GetPosition(this);
            SelectionHitTarget.CaptureMouse();
            
            // 초기 0 크기 설정
            _selectionArea = new Rect(_newSelectionStart, new Size(0,0));
            UpdateVisuals();
            
            // 크로스헤어 업데이트
            if (CrosshairX != null && CrosshairY != null)
            {
                CrosshairX.X1 = 0; CrosshairX.X2 = ActualWidth;
                CrosshairX.Y1 = _newSelectionStart.Y; CrosshairX.Y2 = _newSelectionStart.Y;
                
                CrosshairY.X1 = _newSelectionStart.X; CrosshairY.X2 = _newSelectionStart.X;
                CrosshairY.Y1 = 0; CrosshairY.Y2 = ActualHeight;
            }
        }

        public void StartNewSelectionMode()
        {
            if (SelectionHitTarget == null) return;

            // 현재 영역 저장 (취소 시 복구용)
            // 단, 현재 이미 유효한 크기라면 저장, 아니면 기본값 저장
            if (_selectionArea.Width > 1 && _selectionArea.Height > 1)
            {
                _savedSelectionArea = _selectionArea;
            }
            else
            {
                 var screen = SystemParameters.WorkArea;
                 _savedSelectionArea = new Rect((screen.Width - 800) / 2, (screen.Height - 600) / 2, 800, 600);
            }

            // 현재 선택 영역 초기화 (전체 화면이 어두워짐)
            _selectionArea = new Rect(0, 0, 0, 0);
            UpdateVisuals();

            // 핸들 및 라벨 숨기기
            ShowHandles(false);
            
            // 히트 타겟 활성화
            SelectionHitTarget.Visibility = Visibility.Visible;
            
            // 크로스헤어 활성화
            if (CrosshairX != null) CrosshairX.Visibility = Visibility.Visible;
            if (CrosshairY != null) CrosshairY.Visibility = Visibility.Visible;
        }

        public void CancelNewSelectionMode()
        {
            if (SelectionHitTarget == null) return;

            // 영역 복구
            SelectionArea = _savedSelectionArea;

            // 핸들 다시 표시
            ShowHandles(true);
            SelectionHitTarget.Visibility = Visibility.Collapsed;
            
            // 크로스헤어 숨김
            if (CrosshairX != null) CrosshairX.Visibility = Visibility.Collapsed;
            if (CrosshairY != null) CrosshairY.Visibility = Visibility.Collapsed;

            // 드래그 상태 초기화
            _isSelectingNew = false;
            SelectionHitTarget.ReleaseMouseCapture();
            
            AreaSelectionEnded?.Invoke(this, EventArgs.Empty);
        }

        // ... existing code ...

        private void SelectionHitTarget_MouseMove(object sender, MouseEventArgs e)
        {
            var current = e.GetPosition(this);
            
            // 크로스헤어 업데이트
            if (CrosshairX != null && CrosshairY != null)
            {
                CrosshairX.X1 = 0; CrosshairX.X2 = ActualWidth;
                CrosshairX.Y1 = current.Y; CrosshairX.Y2 = current.Y;
                
                CrosshairY.X1 = current.X; CrosshairY.X2 = current.X;
                CrosshairY.Y1 = 0; CrosshairY.Y2 = ActualHeight;
            }

            if (!_isSelectingNew) return;
            
            double x = Math.Min(_newSelectionStart.X, current.X);
            double y = Math.Min(_newSelectionStart.Y, current.Y);
            double w = Math.Abs(_newSelectionStart.X - current.X);
            double h = Math.Abs(_newSelectionStart.Y - current.Y);
            
            _selectionArea = new Rect(x, y, w, h);
            UpdateVisuals();
        }

        private void SelectionHitTarget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelectingNew)
            {
                _isSelectingNew = false;
                SelectionHitTarget.ReleaseMouseCapture();
                SelectionHitTarget.Visibility = Visibility.Collapsed;
                
                // 크로스헤어 숨김
                if (CrosshairX != null) CrosshairX.Visibility = Visibility.Collapsed;
                if (CrosshairY != null) CrosshairY.Visibility = Visibility.Collapsed;
                
                // 크기가 너무 작으면 기본 크기로 복원 (실수 방지)
                if (_selectionArea.Width < 50 || _selectionArea.Height < 50)
                {
                    var screen = SystemParameters.WorkArea;
                    _selectionArea = new Rect((screen.Width - 800) / 2, (screen.Height - 600) / 2, 800, 600);
                    UpdateVisuals();
                }

                // 핸들 다시 표시
                ShowHandles(true);
                AreaChanged?.Invoke(this, _selectionArea);
                AreaSelectionEnded?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private void RecordingOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }
        
        private void RecordingOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // 부모 창(RecordingWindow)에서 종료 처리하도록 이벤트 발생
                EscapePressed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 녹화 모드 설정 - dim 영역과 핸들 숨기고 테두리만 표시
        /// </summary>
        public void SetRecordingMode(bool isRecording)
        {
            if (isRecording)
            {
                // dim 영역 숨김
                DimTop.Visibility = Visibility.Collapsed;
                DimBottom.Visibility = Visibility.Collapsed;
                DimLeft.Visibility = Visibility.Collapsed;
                DimRight.Visibility = Visibility.Collapsed;
                
                // 핸들 숨김
                HandleNW.Visibility = Visibility.Collapsed;
                HandleN.Visibility = Visibility.Collapsed;
                HandleNE.Visibility = Visibility.Collapsed;
                HandleE.Visibility = Visibility.Collapsed;
                HandleSE.Visibility = Visibility.Collapsed;
                HandleS.Visibility = Visibility.Collapsed;
                HandleSW.Visibility = Visibility.Collapsed;
                HandleW.Visibility = Visibility.Collapsed;
                
                // 크기 라벨 숨김
                SizeLabelBorder.Visibility = Visibility.Collapsed;
                
                // 테두리는 표시 유지
                SelectionBorder.Visibility = Visibility.Visible;
            }
            else
            {
                // 모든 요소 다시 표시
                DimTop.Visibility = Visibility.Visible;
                DimBottom.Visibility = Visibility.Visible;
                DimLeft.Visibility = Visibility.Visible;
                DimRight.Visibility = Visibility.Visible;
                
                HandleNW.Visibility = Visibility.Visible;
                HandleN.Visibility = Visibility.Visible;
                HandleNE.Visibility = Visibility.Visible;
                HandleE.Visibility = Visibility.Visible;
                HandleSE.Visibility = Visibility.Visible;
                HandleS.Visibility = Visibility.Visible;
                HandleSW.Visibility = Visibility.Visible;
                HandleW.Visibility = Visibility.Visible;
                
                SizeLabelBorder.Visibility = Visibility.Visible;
                SelectionBorder.Visibility = Visibility.Visible;
            }
        }
        
        #endregion
        
        #region 시각적 업데이트
        
        /// <summary>
        /// 선택 영역 시각적 요소 업데이트
        /// </summary>
        private void UpdateVisuals()
        {
            if (OverlayCanvas == null) return;
            
            double screenWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            
            double l = _selectionArea.Left;
            double t = _selectionArea.Top;
            double r = _selectionArea.Right;
            double b = _selectionArea.Bottom;
            double w = _selectionArea.Width;
            double h = _selectionArea.Height;
            
            // 4분할 dim 영역 업데이트
            // 상단 영역 (전체 너비, 선택 영역 위)
            Canvas.SetLeft(DimTop, 0);
            Canvas.SetTop(DimTop, 0);
            DimTop.Width = screenWidth;
            DimTop.Height = Math.Max(0, t);
            
            // 하단 영역 (전체 너비, 선택 영역 아래)
            Canvas.SetLeft(DimBottom, 0);
            Canvas.SetTop(DimBottom, b);
            DimBottom.Width = screenWidth;
            DimBottom.Height = Math.Max(0, screenHeight - b);
            
            // 좌측 영역 (선택 영역과 같은 높이)
            Canvas.SetLeft(DimLeft, 0);
            Canvas.SetTop(DimLeft, t);
            DimLeft.Width = Math.Max(0, l);
            DimLeft.Height = h;
            
            // 우측 영역 (선택 영역과 같은 높이)
            Canvas.SetLeft(DimRight, r);
            Canvas.SetTop(DimRight, t);
            DimRight.Width = Math.Max(0, screenWidth - r);
            DimRight.Height = h;
            
            // 선택 영역 테두리
            Canvas.SetLeft(SelectionBorder, l);
            Canvas.SetTop(SelectionBorder, t);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;
            
            // 크기 라벨 (선택 영역 상단 중앙)
            SizeLabel.Text = $"{(int)w} x {(int)h}";
            Canvas.SetLeft(SizeLabelBorder, l + (w - SizeLabelBorder.ActualWidth) / 2);
            Canvas.SetTop(SizeLabelBorder, Math.Max(5, t - 30));
            
            // 리사이즈 핸들 위치
            UpdateHandlePositions();
            
            // 그리기 캔버스 (선택 영역과 동일한 위치/크기)
            if (DrawingCanvas != null)
            {
                Canvas.SetLeft(DrawingCanvas, l);
                Canvas.SetTop(DrawingCanvas, t);
                DrawingCanvas.Width = w;
                DrawingCanvas.Height = h;
            }
            
            // 도형 캔버스 (선택 영역과 동일한 위치/크기)
            if (ShapeCanvas != null)
            {
                Canvas.SetLeft(ShapeCanvas, l);
                Canvas.SetTop(ShapeCanvas, t);
                ShapeCanvas.Width = w;
                ShapeCanvas.Height = h;
            }
            
            // 새 영역 선택 히트 박스 크기 업데이트
            if (SelectionHitTarget != null)
            {
                Canvas.SetLeft(SelectionHitTarget, 0);
                Canvas.SetTop(SelectionHitTarget, 0);
                SelectionHitTarget.Width = screenWidth;
                SelectionHitTarget.Height = screenHeight;
            }
        }

        /// <summary>
        /// 리사이즈 핸들 위치 업데이트
        /// </summary>
        private void UpdateHandlePositions()
        {
            double l = _selectionArea.Left;
            double t = _selectionArea.Top;
            double r = _selectionArea.Right;
            double b = _selectionArea.Bottom;
            double cx = l + _selectionArea.Width / 2;
            double cy = t + _selectionArea.Height / 2;
            
            double offset = 5; // 핸들 크기의 절반
            
            Canvas.SetLeft(HandleNW, l - offset);
            Canvas.SetTop(HandleNW, t - offset);
            
            Canvas.SetLeft(HandleN, cx - offset);
            Canvas.SetTop(HandleN, t - offset);
            
            Canvas.SetLeft(HandleNE, r - offset);
            Canvas.SetTop(HandleNE, t - offset);
            
            Canvas.SetLeft(HandleE, r - offset);
            Canvas.SetTop(HandleE, cy - offset);
            
            Canvas.SetLeft(HandleSE, r - offset);
            Canvas.SetTop(HandleSE, b - offset);
            
            Canvas.SetLeft(HandleS, cx - offset);
            Canvas.SetTop(HandleS, b - offset);
            
            Canvas.SetLeft(HandleSW, l - offset);
            Canvas.SetTop(HandleSW, b - offset);
            
            Canvas.SetLeft(HandleW, l - offset);
            Canvas.SetTop(HandleW, cy - offset);
        }
        
        #endregion
        
        #region 영역 이동 (드래그)
        
        private void SelectionBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            _originalArea = _selectionArea;
            SelectionBorder.CaptureMouse();
            e.Handled = true;
        }
        
        private void SelectionBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            

            
            // 기본 드래그 동작
            var current = e.GetPosition(this);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            
            double newLeftDefault = _originalArea.Left + dx;
            double newTopDefault = _originalArea.Top + dy;
            
            // 화면 경계 체크
            double screenWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            newLeftDefault = Math.Max(0, Math.Min(newLeftDefault, screenWidth - _originalArea.Width));
            newTopDefault = Math.Max(0, Math.Min(newTopDefault, screenHeight - _originalArea.Height));
            
            // 스냅되지 않은 경우 원래 크기로 복귀
            SelectionArea = new Rect(newLeftDefault, newTopDefault, _originalArea.Width, _originalArea.Height);
        }
        
        private void SelectionBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SelectionBorder.ReleaseMouseCapture();
            }
        }
        
        #endregion
        
        #region 리사이즈 핸들
        
        private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle handle && handle.Tag is string tag)
            {
                _isResizing = true;
                _resizeHandle = tag;
                _dragStart = e.GetPosition(this);
                _originalArea = _selectionArea;
                handle.CaptureMouse();
                e.Handled = true;
            }
        }
        
        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing) return;
            
            var current = e.GetPosition(this);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            
            double l = _originalArea.Left;
            double t = _originalArea.Top;
            double w = _originalArea.Width;
            double h = _originalArea.Height;
            
            switch (_resizeHandle)
            {
                case "NW": l += dx; w -= dx; t += dy; h -= dy; break;
                case "N": t += dy; h -= dy; break;
                case "NE": w += dx; t += dy; h -= dy; break;
                case "E": w += dx; break;
                case "SE": w += dx; h += dy; break;
                case "S": h += dy; break;
                case "SW": l += dx; w -= dx; h += dy; break;
                case "W": l += dx; w -= dx; break;
            }
            
            // 최소 크기 보장
            if (w < MIN_WIDTH)
            {
                if (_resizeHandle.Contains("W")) l = _originalArea.Right - MIN_WIDTH;
                w = MIN_WIDTH;
            }
            if (h < MIN_HEIGHT)
            {
                if (_resizeHandle.Contains("N")) t = _originalArea.Bottom - MIN_HEIGHT;
                h = MIN_HEIGHT;
            }
            
            // 화면 경계 체크
            double screenWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            l = Math.Max(0, l);
            t = Math.Max(0, t);
            if (l + w > screenWidth) w = screenWidth - l;
            if (t + h > screenHeight) h = screenHeight - t;
            
            SelectionArea = new Rect(l, t, w, h);
        }
        
        private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                Mouse.Capture(null);
            }
        }
        
        #endregion

        #region 그리기 도구

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11; // X축 DPI 스케일
            }
            return 1.0;
        }

        public void SetDrawingMode(bool enabled)
        {
            if (DrawingCanvas != null)
            {
                DrawingCanvas.IsHitTestVisible = enabled;
                if (enabled)
                {
                    // 마우스 이벤트를 받기 위해 거의 투명한 배경 설정 (완전 투명은 클릭 통과됨)
                    DrawingCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                    
                    DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    
                    // DPI 스케일에 맞게 펜 두께 조정 (녹화 결과와 화면 표시 일치)
                    double dpiScale = GetDpiScale();
                    double adjustedThickness = 3 / dpiScale;
                    
                    // 기본 펜 설정 (빨강)
                    DrawingCanvas.DefaultDrawingAttributes.Color = Colors.Red; 
                    DrawingCanvas.DefaultDrawingAttributes.Width = adjustedThickness;
                    DrawingCanvas.DefaultDrawingAttributes.Height = adjustedThickness;
                    DrawingCanvas.DefaultDrawingAttributes.IsHighlighter = false;
                }
                else
                {
                    DrawingCanvas.Background = Brushes.Transparent;
                }
            }
        }

        public void SetPenMode(Color color, double thickness, bool isHighlighter)
        {
            _isEraserMode = false;
            _isNumberingMode = false;
            _currentShapeTool = ShapeToolType.None;
            
            // ShapeCanvas 비활성화
            if (ShapeCanvas != null)
            {
                ShapeCanvas.IsHitTestVisible = false;
                ShapeCanvas.Background = Brushes.Transparent;
            }
            
            // DrawingCanvas 활성화
            if (DrawingCanvas != null)
            {
                DrawingCanvas.IsHitTestVisible = true;
                DrawingCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
                DrawingCanvas.DefaultDrawingAttributes.Color = color;
                
                // DPI 스케일에 맞게 펜 두께 조정
                double dpiScale = GetDpiScale();
                double adjustedThickness = thickness / dpiScale;
                
                DrawingCanvas.DefaultDrawingAttributes.Width = adjustedThickness;
                DrawingCanvas.DefaultDrawingAttributes.Height = adjustedThickness;
                DrawingCanvas.DefaultDrawingAttributes.IsHighlighter = isHighlighter;
            }
        }

        public void SetEraserMode()
        {
            if (DrawingCanvas != null)
            {
                DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            }
            
            // 도형 지우기 모드 활성화
            _isEraserMode = true;
            _isNumberingMode = false;
            _currentShapeTool = ShapeToolType.None;
            
            if (ShapeCanvas != null)
            {
                ShapeCanvas.IsHitTestVisible = true;
                ShapeCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                ShapeCanvas.Cursor = Cursors.Pen; // 펜 커서 (지우개 느낌)
                
                ShapeCanvas.MouseLeftButtonDown -= ShapeCanvas_MouseLeftButtonDown;
                ShapeCanvas.MouseLeftButtonDown += ShapeCanvas_MouseLeftButtonDown_Eraser;
            }
        }
        
        private bool _isEraserMode = false;
        
        private void ShapeCanvas_MouseLeftButtonDown_Eraser(object sender, MouseButtonEventArgs e)
        {
            if (ShapeCanvas == null || !_isEraserMode) return;
            
            var pos = e.GetPosition(ShapeCanvas);
            
            // 클릭한 위치에 있는 도형 찾기 (역순으로 검색 - 위에 있는 것부터)
            for (int i = ShapeCanvas.Children.Count - 1; i >= 0; i--)
            {
                var element = ShapeCanvas.Children[i];
                
                // 요소의 경계 박스 계산
                Rect bounds = GetElementBounds(element);
                
                // 클릭 위치가 요소 범위 안에 있는지 확인 (약간의 여유 추가)
                Rect hitRect = new Rect(bounds.X - 5, bounds.Y - 5, bounds.Width + 10, bounds.Height + 10);
                
                if (hitRect.Contains(pos))
                {
                    // 번호 매기기였다면 카운터 감소
                    if (element is Border)
                    {
                        _numberingCounter = Math.Max(1, _numberingCounter - 1);
                    }
                    
                    ShapeCanvas.Children.RemoveAt(i);
                    _redoStack.Clear();
                    break;
                }
            }
        }

        private Rect GetElementBounds(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            
            double width = 0, height = 0;
            
            if (element is FrameworkElement fe)
            {
                width = fe.ActualWidth > 0 ? fe.ActualWidth : (double.IsNaN(fe.Width) ? 0 : fe.Width);
                height = fe.ActualHeight > 0 ? fe.ActualHeight : (double.IsNaN(fe.Height) ? 0 : fe.Height);
            }
            
            // Canvas (화살표 등)인 경우 자식 요소들의 경계 계산
            if (element is Canvas canvas && canvas.Children.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                
                foreach (UIElement child in canvas.Children)
                {
                    if (child is System.Windows.Shapes.Line line)
                    {
                        minX = Math.Min(minX, Math.Min(line.X1, line.X2));
                        minY = Math.Min(minY, Math.Min(line.Y1, line.Y2));
                        maxX = Math.Max(maxX, Math.Max(line.X1, line.X2));
                        maxY = Math.Max(maxY, Math.Max(line.Y1, line.Y2));
                    }
                    else if (child is Polygon polygon)
                    {
                        foreach (var point in polygon.Points)
                        {
                            minX = Math.Min(minX, point.X);
                            minY = Math.Min(minY, point.Y);
                            maxX = Math.Max(maxX, point.X);
                            maxY = Math.Max(maxY, point.Y);
                        }
                    }
                }
                
                if (minX != double.MaxValue)
                {
                    left = minX;
                    top = minY;
                    width = maxX - minX;
                    height = maxY - minY;
                }
            }
            
            // Line인 경우
            if (element is System.Windows.Shapes.Line singleLine)
            {
                left = Math.Min(singleLine.X1, singleLine.X2);
                top = Math.Min(singleLine.Y1, singleLine.Y2);
                width = Math.Abs(singleLine.X2 - singleLine.X1);
                height = Math.Abs(singleLine.Y2 - singleLine.Y1);
                
                // 수평/수직 선은 두께가 0이 되므로 최소 크기 보장
                width = Math.Max(width, 10);
                height = Math.Max(height, 10);
            }
            
            return new Rect(left, top, Math.Max(width, 10), Math.Max(height, 10));
        }

        public void ClearDrawing()
        {
            if (DrawingCanvas != null)
            {
                DrawingCanvas.Strokes.Clear();
            }
            if (ShapeCanvas != null)
            {
                ShapeCanvas.Children.Clear();
            }
            _numberingCounter = 1; // 번호 초기화
        }

        // 도형 그리기 관련
        private enum ShapeToolType { None, Rectangle, Ellipse, Line, Arrow }
        private ShapeToolType _currentShapeTool = ShapeToolType.None;
        private bool _isNumberingMode = false;
        private int _numberingCounter = 1;
        private Point _shapeStartPoint;
        private UIElement? _tempShape;
        private bool _isDrawingShape = false;
        private Color _currentShapeColor = Colors.Red;

        public void SetShapeTool(string toolName, Color color)
        {
            _currentShapeColor = color;
            _isNumberingMode = false;
            _isEraserMode = false;
            
            // InkCanvas 비활성화
            if (DrawingCanvas != null)
            {
                DrawingCanvas.IsHitTestVisible = false;
            }
            
            // ShapeCanvas 활성화
            if (ShapeCanvas != null)
            {
                ShapeCanvas.IsHitTestVisible = true;
                ShapeCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                ShapeCanvas.Cursor = Cursors.Cross;
                
                // 이벤트 핸들러 등록 (중복 방지)
                ShapeCanvas.MouseLeftButtonDown -= ShapeCanvas_MouseLeftButtonDown;
                ShapeCanvas.MouseMove -= ShapeCanvas_MouseMove;
                ShapeCanvas.MouseLeftButtonUp -= ShapeCanvas_MouseLeftButtonUp;
                
                ShapeCanvas.MouseLeftButtonDown += ShapeCanvas_MouseLeftButtonDown;
                ShapeCanvas.MouseMove += ShapeCanvas_MouseMove;
                ShapeCanvas.MouseLeftButtonUp += ShapeCanvas_MouseLeftButtonUp;
            }
            
            _currentShapeTool = toolName.ToLower() switch
            {
                "rectangle" => ShapeToolType.Rectangle,
                "ellipse" => ShapeToolType.Ellipse,
                "line" => ShapeToolType.Line,
                "arrow" => ShapeToolType.Arrow,
                _ => ShapeToolType.None
            };
        }

        public void SetNumberingMode(Color color)
        {
            _isNumberingMode = true;
            _currentShapeColor = color;
            _currentShapeTool = ShapeToolType.None;
            _isEraserMode = false;
            
            // InkCanvas 비활성화
            if (DrawingCanvas != null)
            {
                DrawingCanvas.IsHitTestVisible = false;
            }
            
            // ShapeCanvas 활성화
            if (ShapeCanvas != null)
            {
                ShapeCanvas.IsHitTestVisible = true;
                ShapeCanvas.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                ShapeCanvas.Cursor = Cursors.Hand;
                
                ShapeCanvas.MouseLeftButtonDown -= ShapeCanvas_MouseLeftButtonDown;
                ShapeCanvas.MouseMove -= ShapeCanvas_MouseMove;
                ShapeCanvas.MouseLeftButtonUp -= ShapeCanvas_MouseLeftButtonUp;
                
                ShapeCanvas.MouseLeftButtonDown += ShapeCanvas_MouseLeftButtonDown;
            }
        }

        private void ShapeCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ShapeCanvas == null) return;
            
            var pos = e.GetPosition(ShapeCanvas);
            
            if (_isNumberingMode)
            {
                // 번호 매기기: 클릭한 위치에 번호 배지 추가
                var badge = CreateNumberBadge(_numberingCounter, pos);
                ShapeCanvas.Children.Add(badge);
                _numberingCounter++;
                _redoStack.Clear(); // 새 작업 시 Redo 스택 초기화
            }
            else if (_currentShapeTool != ShapeToolType.None)
            {
                // 도형 그리기 시작
                _shapeStartPoint = pos;
                _isDrawingShape = true;
                ShapeCanvas.CaptureMouse();
            }
        }

        private void ShapeCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawingShape || ShapeCanvas == null) return;
            
            var currentPos = e.GetPosition(ShapeCanvas);
            
            // 임시 도형 제거
            if (_tempShape != null)
            {
                ShapeCanvas.Children.Remove(_tempShape);
            }
            
            // 새 임시 도형 생성
            _tempShape = CreateShape(_shapeStartPoint, currentPos, _currentShapeTool, _currentShapeColor, true);
            if (_tempShape != null)
            {
                ShapeCanvas.Children.Add(_tempShape);
            }
        }

        private void ShapeCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawingShape || ShapeCanvas == null) return;
            
            _isDrawingShape = false;
            ShapeCanvas.ReleaseMouseCapture();
            
            var endPos = e.GetPosition(ShapeCanvas);
            
            // 임시 도형 제거
            if (_tempShape != null)
            {
                ShapeCanvas.Children.Remove(_tempShape);
                _tempShape = null;
            }
            
            // 최종 도형 추가
            var finalShape = CreateShape(_shapeStartPoint, endPos, _currentShapeTool, _currentShapeColor, false);
            if (finalShape != null)
            {
                ShapeCanvas.Children.Add(finalShape);
                // Undo 스택에 추가
                _redoStack.Clear(); // 새 작업 시 Redo 스택 초기화
            }
        }

        private UIElement? CreateShape(Point start, Point end, ShapeToolType type, Color color, bool isTemp)
        {
            double dpiScale = GetDpiScale();
            double strokeThickness = 2 / dpiScale;
            
            var brush = new SolidColorBrush(color);
            var pen = new Pen(brush, strokeThickness);
            
            switch (type)
            {
                case ShapeToolType.Rectangle:
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = brush,
                        StrokeThickness = strokeThickness,
                        Width = Math.Abs(end.X - start.X),
                        Height = Math.Abs(end.Y - start.Y)
                    };
                    Canvas.SetLeft(rect, Math.Min(start.X, end.X));
                    Canvas.SetTop(rect, Math.Min(start.Y, end.Y));
                    return rect;
                    
                case ShapeToolType.Ellipse:
                    var ellipse = new System.Windows.Shapes.Ellipse
                    {
                        Stroke = brush,
                        StrokeThickness = strokeThickness,
                        Width = Math.Abs(end.X - start.X),
                        Height = Math.Abs(end.Y - start.Y)
                    };
                    Canvas.SetLeft(ellipse, Math.Min(start.X, end.X));
                    Canvas.SetTop(ellipse, Math.Min(start.Y, end.Y));
                    return ellipse;
                    
                case ShapeToolType.Line:
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = start.X,
                        Y1 = start.Y,
                        X2 = end.X,
                        Y2 = end.Y,
                        Stroke = brush,
                        StrokeThickness = strokeThickness
                    };
                    return line;
                    
                case ShapeToolType.Arrow:
                    return CreateArrow(start, end, brush, strokeThickness);
                    
                default:
                    return null;
            }
        }

        private UIElement CreateArrow(Point start, Point end, Brush brush, double thickness)
        {
            var group = new Canvas();
            
            // 화살표 선
            var line = new System.Windows.Shapes.Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = brush,
                StrokeThickness = thickness
            };
            group.Children.Add(line);
            
            // 화살표 머리 (삼각형)
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 10;
            double arrowAngle = Math.PI / 6; // 30도
            
            Point arrowPoint1 = new Point(
                end.X - arrowLength * Math.Cos(angle - arrowAngle),
                end.Y - arrowLength * Math.Sin(angle - arrowAngle)
            );
            Point arrowPoint2 = new Point(
                end.X - arrowLength * Math.Cos(angle + arrowAngle),
                end.Y - arrowLength * Math.Sin(angle + arrowAngle)
            );
            
            var arrowHead = new Polygon
            {
                Points = new PointCollection { end, arrowPoint1, arrowPoint2 },
                Fill = brush,
                Stroke = brush,
                StrokeThickness = thickness
            };
            group.Children.Add(arrowHead);
            
            return group;
        }

        private UIElement CreateNumberBadge(int number, Point position)
        {
            double dpiScale = GetDpiScale();
            double badgeSize = 24 / dpiScale;
            double fontSize = 12 / dpiScale;
            
            var badge = new Border
            {
                Width = badgeSize,
                Height = badgeSize,
                CornerRadius = new CornerRadius(badgeSize / 2),
                Background = new SolidColorBrush(_currentShapeColor),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2 / dpiScale),
                Child = new TextBlock
                {
                    Text = number.ToString(),
                    Foreground = Brushes.White,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            
            Canvas.SetLeft(badge, position.X - badgeSize / 2);
            Canvas.SetTop(badge, position.Y - badgeSize / 2);
            
            return badge;
        }

        // Undo/Redo 관련
        private Stack<object> _undoStack = new Stack<object>();
        private Stack<object> _redoStack = new Stack<object>();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Z: Undo
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                PerformUndo();
                e.Handled = true;
            }
            // Ctrl+Y: Redo
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                PerformRedo();
                e.Handled = true;
            }
        }

        public void PerformUndo()
        {
            // Shape Undo (우선순위: 도형이 나중에 그려지므로)
            if (ShapeCanvas != null && ShapeCanvas.Children.Count > 0)
            {
                var lastShape = ShapeCanvas.Children[ShapeCanvas.Children.Count - 1];
                _undoStack.Push(new { Type = "Shape", Data = lastShape });
                ShapeCanvas.Children.RemoveAt(ShapeCanvas.Children.Count - 1);
                
                // 번호 매기기였다면 카운터 감소
                if (lastShape is Border)
                {
                    _numberingCounter = Math.Max(1, _numberingCounter - 1);
                }
                return;
            }

            // InkCanvas Undo
            if (DrawingCanvas != null && DrawingCanvas.Strokes.Count > 0)
            {
                var lastStroke = DrawingCanvas.Strokes[DrawingCanvas.Strokes.Count - 1];
                _undoStack.Push(new { Type = "Stroke", Data = lastStroke });
                DrawingCanvas.Strokes.RemoveAt(DrawingCanvas.Strokes.Count - 1);
            }
        }

        public void PerformRedo()
        {
            if (_undoStack.Count == 0) return;

            var item = _undoStack.Pop();
            var itemType = item.GetType().GetProperty("Type")?.GetValue(item)?.ToString();
            var itemData = item.GetType().GetProperty("Data")?.GetValue(item);

            if (itemType == "Stroke" && itemData is System.Windows.Ink.Stroke stroke)
            {
                DrawingCanvas?.Strokes.Add(stroke);
            }
            else if (itemType == "Shape" && itemData is UIElement shape)
            {
                ShapeCanvas?.Children.Add(shape);
                
                // 번호 매기기였다면 카운터 증가
                if (shape is Border)
                {
                    _numberingCounter++;
                }
            }
        }

        #endregion
        



    }
}
