using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
        
        // 최소 영역 크기
        private const double MIN_WIDTH = 100;
        private const double MIN_HEIGHT = 100;
        
        public RecordingOverlay()
        {
            InitializeComponent();
            
            // 기본 선택 영역 (화면 중앙 800x600)
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
            // 주 모니터 화면 크기로 설정
            var screen = SystemParameters.WorkArea;
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;
            
            // 선택 영역을 주 모니터 중앙에 배치
            _selectionArea = new Rect(
                (screen.Width - 800) / 2,
                (screen.Height - 600) / 2,
                800,
                600
            );
            
            UpdateVisuals();
            AreaChanged?.Invoke(this, _selectionArea);
        }
        
        private void RecordingOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }
        
        private void RecordingOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
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
            
            var current = e.GetPosition(this);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            
            double newLeft = _originalArea.Left + dx;
            double newTop = _originalArea.Top + dy;
            
            // 화면 경계 체크
            double screenWidth = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenHeight = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            newLeft = Math.Max(0, Math.Min(newLeft, screenWidth - _selectionArea.Width));
            newTop = Math.Max(0, Math.Min(newTop, screenHeight - _selectionArea.Height));
            
            _selectionArea = new Rect(newLeft, newTop, _selectionArea.Width, _selectionArea.Height);
            UpdateVisuals();
        }
        
        private void SelectionBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SelectionBorder.ReleaseMouseCapture();
                AreaChanged?.Invoke(this, _selectionArea);
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
            
            _selectionArea = new Rect(l, t, w, h);
            UpdateVisuals();
        }
        
        private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                Mouse.Capture(null);
                AreaChanged?.Invoke(this, _selectionArea);
            }
        }
        
        #endregion
    }
}
