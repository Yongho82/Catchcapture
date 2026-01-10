using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CatchCapture.Models;
using System.Diagnostics;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// 캡처 영역 선택 시 '빨간색 점선' 드래그 동작과 UI를 담당하는 클래스입니다.
    /// 기존 SnippingWindow 등에 산재된 선택 로직을 통합 관리합니다.
    /// </summary>
    public class CaptureSelectionOverlay : IDisposable
    {
        private readonly Canvas _canvas;
        private readonly GeometryGroup _overlayGeometryGroup;
        
        // 시각적 요소
        private Rectangle? _selectionRectangle;
        private TextBlock? _sizeTextBlock;
        private RectangleGeometry? _selectionGeometry;
        private System.Windows.Shapes.Path? _overlayPath;
        
        // 상태 변수
        private Point _startPoint;
        private Rect _pendingRect;
        private bool _hasPendingUpdate;
        private bool _isSelecting;
        private Stopwatch _moveStopwatch = new Stopwatch();
        private const int MinMoveIntervalMs = 0; // 즉시 업데이트 (부드러운 드래그)
        private Point _lastUpdatePoint;
        private const double MinMoveDelta = 0.5; // 더 민감한 반응
        private string? _lastSizeText;
        private double _cornerRadius = 0;

        public bool IsSelecting => _isSelecting;
        public Rect CurrentRect => _pendingRect;
        public double CornerRadius 
        { 
            get => _cornerRadius; 
            set { _cornerRadius = value; UpdateVisuals(_pendingRect); } 
        }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="canvas">드로잉을 수행할 캔버스</param>
        /// <param name="initOverlay">기본 오버레이(어두운 배경)를 초기화할지 여부</param>
        public CaptureSelectionOverlay(Canvas canvas, bool initOverlay = true)
        {
            _canvas = canvas;
            _overlayGeometryGroup = new GeometryGroup { FillRule = FillRule.EvenOdd };

            if (initOverlay)
            {
                InitializeOverlay();
            }
            
            InitializeSelectionUI();
            
            // 렌더링 이벤트 구독 (부드러운 업데이트)
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void InitializeOverlay()
        {
            var settings = Settings.Load();

            // 전체 화면 어둡게 처리
            double vWidth = SystemParameters.VirtualScreenWidth;
            double vHeight = SystemParameters.VirtualScreenHeight;

            var fullScreenGeometry = new RectangleGeometry(new Rect(0, 0, vWidth, vHeight));
            if (fullScreenGeometry.CanFreeze) fullScreenGeometry.Freeze();
            
            // 구멍이 뚫릴 영역 (초기엔 0)
            _selectionGeometry = new RectangleGeometry(new Rect(0, 0, 0, 0));

            _overlayGeometryGroup.Children.Add(fullScreenGeometry);
            _overlayGeometryGroup.Children.Add(_selectionGeometry);

            Color overlayColor;
            try { overlayColor = (Color)ColorConverter.ConvertFromString(settings.OverlayBackgroundColor); }
            catch { overlayColor = Color.FromArgb(140, 0, 0, 0); }

            _overlayPath = new System.Windows.Shapes.Path
            {
                Data = _overlayGeometryGroup,
                Fill = new SolidColorBrush(overlayColor),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_overlayPath, 1000); // 딤 배경은 중간 레벨
            
            // 렌더링 최적화
            if (_overlayPath.Fill is SolidColorBrush sb && sb.CanFreeze) sb.Freeze();
            RenderOptions.SetCachingHint(_overlayPath, CachingHint.Cache);
            RenderOptions.SetCacheInvalidationThresholdMinimum(_overlayPath, 0.5);
            RenderOptions.SetCacheInvalidationThresholdMaximum(_overlayPath, 2.0);
            _overlayPath.CacheMode = new BitmapCache();

            _canvas.Children.Add(_overlayPath);
        }

        private void InitializeSelectionUI()
        {
            var settings = Settings.Load();

            // 라인 색상
            Color strokeColor;
            try { strokeColor = (Color)ColorConverter.ConvertFromString(settings.CaptureLineColor); }
            catch { strokeColor = Colors.Red; }
            var strokeBrush = new SolidColorBrush(strokeColor);
            strokeBrush.Freeze();
            
            // 라인 스타일
            DoubleCollection? dashArray = null;
            if (settings.CaptureLineStyle == "Dash") dashArray = new DoubleCollection { 4, 3 };
            else if (settings.CaptureLineStyle == "Dot") dashArray = new DoubleCollection { 1, 3 };
            else if (settings.CaptureLineStyle == "DashDot") dashArray = new DoubleCollection { 4, 3, 1, 3 };
            
            if (dashArray != null) dashArray.Freeze();

            _selectionRectangle = new Rectangle
            {
                Stroke = strokeBrush,
                StrokeThickness = settings.CaptureLineThickness,
                StrokeDashArray = dashArray,
                StrokeDashCap = PenLineCap.Square,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Visibility = Visibility.Collapsed
            };
            
            // GPU 가속 적용 (부드러운 드래그)
            _selectionRectangle.CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = true };
            RenderOptions.SetEdgeMode(_selectionRectangle, EdgeMode.Aliased);
            
            _canvas.Children.Add(_selectionRectangle);

            // 사이즈 텍스트
            _sizeTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Visibility = Visibility.Collapsed
            };
            TextOptions.SetTextFormattingMode(_sizeTextBlock, TextFormattingMode.Display);
            if (_sizeTextBlock.Background is SolidColorBrush sb && sb.CanFreeze) sb.Freeze();
            
            _canvas.Children.Add(_sizeTextBlock);
        }

        /// <summary>
        /// 드래그 시작 (MouseDown)
        /// </summary>
        public void StartSelection(Point point)
        {
            _startPoint = point;
            _isSelecting = true;
            _hasPendingUpdate = false;
            _moveStopwatch.Restart();

            if (_selectionRectangle != null)
            {
                _selectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(_selectionRectangle, point.X);
                Canvas.SetTop(_selectionRectangle, point.Y);
                _selectionRectangle.Width = 0;
                _selectionRectangle.Height = 0;
            }

            if (_sizeTextBlock != null)
            {
                _sizeTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 드래그 중 이동 (MouseMove)
        /// </summary>
        public void UpdateSelection(Point currentPoint)
        {
            if (!_isSelecting) return;

            // [최적화] 최소 이동 거리 체크만 수행 (딜레이 없음)
            if (Math.Abs(currentPoint.X - _lastUpdatePoint.X) < MinMoveDelta &&
                Math.Abs(currentPoint.Y - _lastUpdatePoint.Y) < MinMoveDelta)
            {
                return;
            }
            _lastUpdatePoint = currentPoint;

            // 사각형 계산
            double left = Math.Min(_startPoint.X, currentPoint.X);
            double top = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            _pendingRect = new Rect(left, top, width, height);
            
            // [최적화] 즉시 업데이트 (CompositionTarget.Rendering 대기 없이)
            UpdateVisualsImmediate(_pendingRect);
        }

        /// <summary>
        /// 즉시 시각적 업데이트 (드래그 중 호출, 오버레이 구멍 제외)
        /// </summary>
        private void UpdateVisualsImmediate(Rect rect)
        {
            // 선택 사각형만 빠르게 업데이트 (오버레이 구멍은 CompositionTarget.Rendering에서)
            if (_selectionRectangle != null && rect.Width > 0.1 && rect.Height > 0.1)
            {
                _selectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(_selectionRectangle, rect.Left);
                Canvas.SetTop(_selectionRectangle, rect.Top);
                _selectionRectangle.Width = rect.Width;
                _selectionRectangle.Height = rect.Height;

                double actualRadius = _cornerRadius;
                if (_cornerRadius >= 999) actualRadius = Math.Min(rect.Width, rect.Height) / 2;
                _selectionRectangle.RadiusX = actualRadius;
                _selectionRectangle.RadiusY = actualRadius;
            }

            // 사이즈 텍스트 (간소화)
            if (_sizeTextBlock != null && rect.Width > 10 && rect.Height > 10)
            {
                string text = $"{(int)rect.Width} x {(int)rect.Height}";
                if (text != _lastSizeText)
                {
                    _sizeTextBlock.Text = text;
                    _lastSizeText = text;
                }
                _sizeTextBlock.Visibility = Visibility.Visible;
                Canvas.SetLeft(_sizeTextBlock, rect.Left + rect.Width - 80);
                Canvas.SetTop(_sizeTextBlock, rect.Top + rect.Height - 28);
            }

            // 오버레이 구멍은 렌더링 루프에서 처리 (성능)
            _hasPendingUpdate = true;
        }

        /// <summary>
        /// 드래그 종료 (MouseUp)
        /// </summary>
        /// <param name="hideVisuals">종료 후 시각적 요소를 숨길지 여부</param>
        /// <returns>최종 선택된 사각형</returns>
        public Rect EndSelection(bool hideVisuals = true)
        {
            _isSelecting = false;
            
            // 확정된 UI 상태 업데이트 ensure call
            UpdateVisuals(_pendingRect);
            _hasPendingUpdate = false;

            if (hideVisuals)
            {
                if (_selectionRectangle != null) _selectionRectangle.Visibility = Visibility.Collapsed;
                if (_sizeTextBlock != null) _sizeTextBlock.Visibility = Visibility.Collapsed;
            }

            return _pendingRect;
        }

        /// <summary>
        /// 외부에서 선택 영역 좌표를 직접 설정 (리사이즈 등)
        /// </summary>
        public void SetRect(Rect rect)
        {
            _pendingRect = rect;
            UpdateVisuals(rect);
            // 렌더링 루프에서 중복 갱신되지 않도록 false 설정
            _hasPendingUpdate = false; 
        }

        /// <summary>
        /// 시각적 요소의 가시성 설정
        /// </summary>
        public void SetVisibility(Visibility visibility)
        {
            if (_selectionRectangle != null) 
            {
                _selectionRectangle.Visibility = visibility;
                if (visibility == Visibility.Visible) Panel.SetZIndex(_selectionRectangle, 2000);
            }
            if (_sizeTextBlock != null) 
            {
                _sizeTextBlock.Visibility = (visibility == Visibility.Visible && _pendingRect.Width > 5) ? Visibility.Visible : Visibility.Collapsed;
                if (_sizeTextBlock.Visibility == Visibility.Visible) Panel.SetZIndex(_sizeTextBlock, 2001);
            }
        }

        /// <summary>
        /// 크기 텍스트 가시성만 별도로 설정
        /// </summary>
        public void SetSizeTextVisibility(Visibility visibility)
        {
            if (_sizeTextBlock != null) 
            {
                _sizeTextBlock.Visibility = (visibility == Visibility.Visible && _pendingRect.Width > 5) ? Visibility.Visible : Visibility.Collapsed;
                if (_sizeTextBlock.Visibility == Visibility.Visible) Panel.SetZIndex(_sizeTextBlock, 2001);
            }
        }

        /// <summary>
        /// 어두운 배경 오버레이의 가시성 설정
        /// </summary>
        public void SetOverlayVisibility(Visibility visibility)
        {
            if (_overlayPath != null) _overlayPath.Visibility = visibility;
        }

        /// <summary>
        /// 선택 초기화
        /// </summary>
        public void Reset()
        {
            _isSelecting = false;
            _hasPendingUpdate = false;
            if (_selectionRectangle != null) _selectionRectangle.Visibility = Visibility.Collapsed;
            if (_sizeTextBlock != null) _sizeTextBlock.Visibility = Visibility.Collapsed;
            
            // 오버레이 구멍 메우기
            if (_selectionGeometry != null)
            {
                _selectionGeometry.Rect = new Rect(0, 0, 0, 0);
            }
        }

        /// <summary>
        /// 실제 화면 렌더링 루프 (CompositionTarget.Rendering)
        /// </summary>
        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (!_hasPendingUpdate || !_isSelecting) return;
            
            UpdateVisuals(_pendingRect);
            _hasPendingUpdate = false;
        }

        private void UpdateVisuals(Rect rect)
        {
            try
            {
                if (_selectionRectangle != null)
                {
                    if (rect.Width < 0.1 || rect.Height < 0.1)
                    {
                        _selectionRectangle.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _selectionRectangle.Visibility = Visibility.Visible;
                        Canvas.SetLeft(_selectionRectangle, rect.Left);
                        Canvas.SetTop(_selectionRectangle, rect.Top);
                        _selectionRectangle.Width = rect.Width;
                        _selectionRectangle.Height = rect.Height;

                        // 엣지 캡처를 위한 둥근 모서리 적용
                        double actualRadius = _cornerRadius;
                        if (_cornerRadius >= 999) 
                            actualRadius = Math.Min(rect.Width, rect.Height) / 2;

                        _selectionRectangle.RadiusX = actualRadius;
                        _selectionRectangle.RadiusY = actualRadius;
                        
                        // [최적화] Z-Index 보장
                        Panel.SetZIndex(_selectionRectangle, 2000);
                    }
                }

                if (_selectionGeometry != null)
                {
                    _selectionGeometry.Rect = rect;
                    
                    // 오버레이 구멍(Geometry)도 똑같이 둥글게 처리
                    double actualRadius = _cornerRadius;
                    if (_cornerRadius >= 999) 
                        actualRadius = Math.Min(rect.Width, rect.Height) / 2;

                    _selectionGeometry.RadiusX = actualRadius;
                    _selectionGeometry.RadiusY = actualRadius;
                }

                if (_sizeTextBlock != null)
                {
                    if (rect.Width > 5 && rect.Height > 5)
                    {
                        _sizeTextBlock.Visibility = Visibility.Visible;
                        Panel.SetZIndex(_sizeTextBlock, 2001);

                        string text = $"{(int)Math.Max(0, rect.Width)} x {(int)Math.Max(0, rect.Height)}";
                        
                        if (text != _lastSizeText)
                        {
                            _sizeTextBlock.Text = text;
                            _sizeTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            _lastSizeText = text;
                        }
                        
                        double tw = _sizeTextBlock.DesiredSize.Width;
                        double th = _sizeTextBlock.DesiredSize.Height;
                        if (tw <= 0) tw = 60; // Fallback
                        if (th <= 0) th = 20;

                        const double margin = 8;
                        
                        // 위치 계산 및 화면 밖으로 나가지 않게 보정
                        double left = rect.Left + rect.Width - tw - margin;
                        double top = rect.Top + rect.Height - th - margin;
                        
                        if (left < rect.Left + margin) left = rect.Left + margin;
                        if (top < rect.Top + margin) top = rect.Top + margin;

                        Canvas.SetLeft(_sizeTextBlock, left);
                        Canvas.SetTop(_sizeTextBlock, top);
                    }
                    else
                    {
                        _sizeTextBlock.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateVisuals error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            // 필요한 경우 UI 요소 제거 로직 추가
        }
    }
}
