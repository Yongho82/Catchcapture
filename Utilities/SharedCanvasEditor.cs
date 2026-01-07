using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CatchCapture.Models;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// SnippingWindow의 깔끔한 로직을 본따 만든 공유 편집 기능 클래스.
    /// 펜, 형광펜, 도형, 텍스트, 모자이크, 넘버링 등을 통합 관리합니다.
    /// </summary>
    public class SharedCanvasEditor
    {
        private readonly Canvas _canvas;
        private readonly List<UIElement> _drawnElements;
        private readonly Stack<UIElement> _undoStack;
        public UIElement? SelectedObject { get; set; }
        public string CurrentTool { get; set; } = "펜";
        public Color SelectedColor { get; set; } = Colors.Red;
        public double PenThickness { get; set; } = 3;
        public double HighlightThickness { get; set; } = 8;
        public double HighlightOpacity { get; set; } = 0.5;
        
        // 도형 관련
        public ShapeType CurrentShapeType { get; set; } = ShapeType.Rectangle;
        public double ShapeBorderThickness { get; set; } = 2;
        public bool ShapeIsFilled { get; set; } = false;
        public double ShapeFillOpacity { get; set; } = 0.5;

        // 넘버링 관련
        public int NextNumber { get; set; } = 1;
        public double NumberingBadgeSize { get; set; } = 24;
        public double NumberingTextSize { get; set; } = 12;
        public Color NumberingBadgeColor { get; set; } = Colors.Red;
        public Color NumberingNoteColor { get; set; } = Colors.White;
        
        // 텍스트 관련
        public string TextFontFamily { get; set; } = "Arial";
        public double TextFontSize { get; set; } = 16;
        public FontWeight TextFontWeight { get; set; } = FontWeights.Normal;
        public FontStyle TextFontStyle { get; set; } = FontStyles.Normal;
        public bool TextUnderlineEnabled { get; set; } = false;
        public bool TextShadowEnabled { get; set; } = false;

        // 기타 도구
        public double MosaicIntensity { get; set; } = 15;
        public bool UseBlur { get; set; } = false;
        public double EraserSize { get; set; } = 20;

        // 마법봉 관련
        public int MagicWandTolerance { get; set; } = 32;
        public bool MagicWandContiguous { get; set; } = true;

        private Polyline? _currentPolyline;
        private Point _lastDrawPoint;
        private bool _isDrawingShape = false;
        private Point _shapeStartPoint;
        private UIElement? _tempShape;
        private Rectangle? _tempMosaicSelection;

        public event Action<Rect>? MosaicRequired;
        public event Action? ActionOccurred;
        public event Action<UIElement>? ElementAdded;

        public SharedCanvasEditor(Canvas canvas, List<UIElement> drawnElements, Stack<UIElement> undoStack)
        {
            _canvas = canvas;
            _drawnElements = drawnElements;
            _undoStack = undoStack;
        }

        public void StartDrawing(Point clickPoint, object originalSource)
        {
            // 툴바 클릭 등은 무시
            if (originalSource is FrameworkElement fe && (fe is Button || fe.Parent is Button || fe.TemplatedParent is Button))
                return;

            if (CurrentTool == "지우개")
            {
                PerformEraser(clickPoint);
                return;
            }

            if (CurrentTool == "넘버링")
            {
                CreateNumberingAt(clickPoint);
                return;
            }

            if (CurrentTool == "텍스트")
            {
                AddTextAt(clickPoint);
                return;
            }

            if (CurrentTool == "도형")
            {
                _isDrawingShape = true;
                _shapeStartPoint = clickPoint;
                _tempShape = ShapeDrawingHelper.CreateShape(
                    CurrentShapeType, _shapeStartPoint, _shapeStartPoint, 
                    SelectedColor, ShapeBorderThickness, ShapeIsFilled, ShapeFillOpacity);
                if (_tempShape != null) _canvas.Children.Add(_tempShape);
                _canvas.CaptureMouse();
                return;
            }

            if (CurrentTool == "모자이크")
            {
                _isDrawingShape = true;
                _shapeStartPoint = clickPoint;
                _tempMosaicSelection = new Rectangle
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0))
                };
                Canvas.SetLeft(_tempMosaicSelection, clickPoint.X);
                Canvas.SetTop(_tempMosaicSelection, clickPoint.Y);
                _canvas.Children.Add(_tempMosaicSelection);
                _canvas.CaptureMouse();
                return;
            }

            if (CurrentTool == "펜" || CurrentTool == "형광펜")
            {
                _lastDrawPoint = clickPoint;
                _currentPolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(SelectedColor),
                    StrokeThickness = CurrentTool == "형광펜" ? HighlightThickness : PenThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Opacity = CurrentTool == "형광펜" ? HighlightOpacity : 1.0
                };
                _currentPolyline.Points.Add(_lastDrawPoint);
                _canvas.Children.Add(_currentPolyline);
                _drawnElements.Add(_currentPolyline);
                _undoStack?.Push(_currentPolyline);
                _canvas.CaptureMouse();
            }
        }

        public void UpdateDrawing(Point currentPoint)
        {
            if (CurrentTool == "도형" && _isDrawingShape && _tempShape != null)
            {
                ShapeDrawingHelper.UpdateShapeProperties(_tempShape, _shapeStartPoint, currentPoint, SelectedColor, ShapeBorderThickness);
                return;
            }

            if (CurrentTool == "모자이크" && _isDrawingShape && _tempMosaicSelection != null)
            {
                double left = Math.Min(_shapeStartPoint.X, currentPoint.X);
                double top = Math.Min(_shapeStartPoint.Y, currentPoint.Y);
                double width = Math.Abs(currentPoint.X - _shapeStartPoint.X);
                double height = Math.Abs(currentPoint.Y - _shapeStartPoint.Y);
                _tempMosaicSelection.Width = width;
                _tempMosaicSelection.Height = height;
                Canvas.SetLeft(_tempMosaicSelection, left);
                Canvas.SetTop(_tempMosaicSelection, top);
                return;
            }

            if (_currentPolyline != null)
            {
                double dist = Math.Sqrt(Math.Pow(currentPoint.X - _lastDrawPoint.X, 2) + Math.Pow(currentPoint.Y - _lastDrawPoint.Y, 2));
                if (dist > 2)
                {
                    int steps = (int)(dist / 2);
                    for (int i = 1; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        _currentPolyline.Points.Add(new Point(_lastDrawPoint.X + (currentPoint.X - _lastDrawPoint.X) * t, _lastDrawPoint.Y + (currentPoint.Y - _lastDrawPoint.Y) * t));
                    }
                }
                _currentPolyline.Points.Add(currentPoint);
                _lastDrawPoint = currentPoint;
            }
        }

        public void FinishDrawing()
        {
            if (CurrentTool == "도형")
            {
                if (_isDrawingShape && _tempShape != null)
                {
                    _drawnElements.Add(_tempShape);
                    _undoStack?.Push(_tempShape);
                    var added = _tempShape;
                    _tempShape = null;
                    _isDrawingShape = false;
                    ElementAdded?.Invoke(added);
                    ActionOccurred?.Invoke();
                }
                _canvas.ReleaseMouseCapture();
                return;
            }

            if (CurrentTool == "모자이크")
            {
                if (_isDrawingShape && _tempMosaicSelection != null)
                {
                    Rect rect = new Rect(Canvas.GetLeft(_tempMosaicSelection), Canvas.GetTop(_tempMosaicSelection), _tempMosaicSelection.Width, _tempMosaicSelection.Height);
                    MosaicRequired?.Invoke(rect);
                    ActionOccurred?.Invoke(); // 작업 발생 알림 (Redo 스택 클리어 등)
                    _canvas.Children.Remove(_tempMosaicSelection);
                    _tempMosaicSelection = null;
                    _isDrawingShape = false;
                }
                _canvas.ReleaseMouseCapture();
                return;
            }

            if (_currentPolyline != null)
            {
                var added = _currentPolyline;
                _currentPolyline = null;
                _canvas.ReleaseMouseCapture();
                ElementAdded?.Invoke(added);
                ActionOccurred?.Invoke();
            }
        }

        public void PerformEraser(Point p)
        {
            UIElement? toRemove = null;
            for (int i = _drawnElements.Count - 1; i >= 0; i--)
            {
                if (InteractiveEditor.IsPointInElement(p, _drawnElements[i]))
                {
                    toRemove = _drawnElements[i];
                    break;
                }
            }

            if (toRemove != null)
            {
                _canvas.Children.Remove(toRemove);
                _drawnElements.Remove(toRemove);
                
                // [추가] 지우개로 번호를 지웠을 때도 다음 번호 재계산
                RecalculateNextNumber();
                
                ActionOccurred?.Invoke();
            }
        }

        public void CreateNumberingAt(Point p)
        {
            // [추가] 번호 생성 전 유동적으로 다음 번호 계산 (중복 방지 및 빈 번호 채우기)
            RecalculateNextNumber();

            var group = new Canvas { Background = Brushes.Transparent };
            double bSize = NumberingBadgeSize;
            int myNumber = NextNumber;
            
            var badge = new Border {
                Width = bSize, Height = bSize, 
                CornerRadius = new CornerRadius(bSize/2),
                Background = new SolidColorBrush(NumberingBadgeColor),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };
            
            var txt = new TextBlock {
                Text = myNumber.ToString(),
                Foreground = GetContrastBrush(NumberingBadgeColor),
                FontWeight = FontWeights.Bold, FontSize = bSize * 0.5,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = txt;
            group.Children.Add(badge);
            Canvas.SetLeft(badge, 0);
            Canvas.SetTop(badge, 0);
            
            // 텍스트 박스 컨테이너 (Grid)를 사용하여 점선 테두리와 리사이즈 핸들 배치
            var noteGrid = new Grid { MinWidth = 50, MinHeight = bSize };
            
            var note = new TextBox {
                Width = 120, // 기본 너비
                MinHeight = bSize, 
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(NumberingNoteColor), 
                FontSize = NumberingTextSize,
                Text = "", VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(3), 
                TextWrapping = TextWrapping.Wrap, // 줄바꿈 활성화
                AcceptsReturn = true
            };
            
            // 편집 모드에서의 점선 테두리
            var dashedBorder = new Rectangle {
                Stroke = Brushes.White,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                IsHitTestVisible = false
            };
            
            // 리사이즈 핸들 (우측 하단)
            var resizeHandle = new Rectangle {
                Width = 8, Height = 8,
                Fill = Brushes.White, Stroke = Brushes.DimGray, StrokeThickness = 1,
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -2, -2)
            };

            noteGrid.Children.Add(dashedBorder);
            noteGrid.Children.Add(note);
            noteGrid.Children.Add(resizeHandle);
            
            ApplyTextStyleToTextBox(note);
            
            group.Children.Add(noteGrid);
            Canvas.SetLeft(noteGrid, bSize + 5);
            Canvas.SetTop(noteGrid, 0);

            // 확정/삭제 버튼
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var confirmBtn = new Button { 
                Content = "✓", Width = 24, Height = 24, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Green, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12, FontWeight = FontWeights.Bold
            };
            var deleteBtn = new Button { 
                Content = "✕", Width = 24, Height = 24, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Red, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12, FontWeight = FontWeights.Bold
            };
            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(deleteBtn);
            group.Children.Add(btnPanel);

            // 버튼 위치 동기화 로직
            void UpdateBtnPos() {
                double bWidth = badge.ActualWidth > 0 ? badge.ActualWidth : bSize;
                double nWidth = noteGrid.ActualWidth > 0 ? noteGrid.ActualWidth : note.Width;
                Canvas.SetLeft(btnPanel, bWidth + nWidth + 10);
                Canvas.SetTop(btnPanel, 2);
            }
            noteGrid.SizeChanged += (s, e) => UpdateBtnPos();
            badge.SizeChanged += (s, e) => UpdateBtnPos();

            _canvas.Children.Add(group);
            Canvas.SetLeft(group, p.X - bSize / 2);
            Canvas.SetTop(group, p.Y - bSize / 2);
            
            _drawnElements.Add(group);
            _undoStack?.Push(group);
            SelectedObject = group;
            NextNumber++;

            // 상호작용 로직
            confirmBtn.Click += (s, e) => {
                note.IsReadOnly = true;
                dashedBorder.Visibility = Visibility.Collapsed;
                resizeHandle.Visibility = Visibility.Collapsed;
                btnPanel.Visibility = Visibility.Collapsed;
                group.Background = null;
            };

            deleteBtn.Click += (s, e) => {
                _canvas.Children.Remove(group);
                _drawnElements.Remove(group);
                RecalculateNextNumber();
            };

            // 리사이즈 로직
            bool isResizing = false;
            Point lastResizePos = new Point();
            resizeHandle.MouseLeftButtonDown += (s, e) => {
                isResizing = true;
                lastResizePos = e.GetPosition(_canvas);
                resizeHandle.CaptureMouse();
                e.Handled = true;
            };
            resizeHandle.MouseMove += (s, e) => {
                if (isResizing) {
                    Point currentPos = e.GetPosition(_canvas);
                    double dx = currentPos.X - lastResizePos.X;
                    double dy = currentPos.Y - lastResizePos.Y;
                    
                    note.Width = Math.Max(40, (double.IsNaN(note.Width) ? note.ActualWidth : note.Width) + dx);
                    // 높이는 텍스트에 따라 자동 조절되길 원할 수도 있으므로 명시적 Height보다는 MinHeight 유지가 좋을 수 있음
                    // 하지만 사용자가 명시적 높이를 원한다면:
                    note.Height = Math.Max(bSize, (double.IsNaN(note.Height) ? note.ActualHeight : note.Height) + dy);
                    
                    lastResizePos = currentPos;
                    e.Handled = true;
                }
            };
            resizeHandle.MouseLeftButtonUp += (s, e) => {
                isResizing = false;
                resizeHandle.ReleaseMouseCapture();
                e.Handled = true;
            };

            // 드래그 및 더블 클릭 수정 로직 (그룹 이동)
            bool isDragging = false;
            Point lastPos = new Point();
            
            MouseButtonEventHandler doubleClickEdit = (s, e) => {
                if (e.ClickCount == 2) {
                    note.IsReadOnly = false;
                    dashedBorder.Visibility = Visibility.Visible;
                    resizeHandle.Visibility = Visibility.Visible;
                    btnPanel.Visibility = Visibility.Visible;
                    SelectedObject = group; 
                    note.Focus();
                    e.Handled = true;
                }
            };
            badge.PreviewMouseLeftButtonDown += doubleClickEdit;
            note.PreviewMouseLeftButtonDown += doubleClickEdit;

            badge.MouseLeftButtonDown += (s, e) => {
                isDragging = true;
                lastPos = e.GetPosition(_canvas);
                SelectedObject = group; 
                badge.CaptureMouse();
                e.Handled = true;
            };
            badge.MouseMove += (s, e) => {
                if (isDragging) {
                    Point currentPos = e.GetPosition(_canvas);
                    double dx = currentPos.X - lastPos.X;
                    double dy = currentPos.Y - lastPos.Y;
                    Canvas.SetLeft(group, Canvas.GetLeft(group) + dx);
                    Canvas.SetTop(group, Canvas.GetTop(group) + dy);
                    lastPos = currentPos;
                    e.Handled = true;
                }
            };
            badge.MouseLeftButtonUp += (s, e) => {
                isDragging = false;
                badge.ReleaseMouseCapture();
                e.Handled = true;
            };

            ElementAdded?.Invoke(group);
            ActionOccurred?.Invoke();
            
            note.Focus();
        }

        public void AddTextAt(Point p)
        {
            // 텍스트 박스 컨테이너 (Grid)를 사용하여 점선 테두리와 리사이즈 핸들 배치
            var nodeGrid = new Grid { MinWidth = 100, MinHeight = 30 };

            var textBox = new TextBox
            {
                Width = 150, // 기본 너비
                MinHeight = 30,
                FontSize = TextFontSize,
                Foreground = new SolidColorBrush(SelectedColor),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            
            // 편집 모드에서의 점선 테두리
            var dashedBorder = new Rectangle {
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeDashArray = new DoubleCollection { 3, 2 },
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
                IsHitTestVisible = false
            };
            
            // 리사이즈 핸들 (우측 하단)
            var resizeHandle = new Rectangle {
                Width = 10, Height = 10,
                Fill = Brushes.White, Stroke = Brushes.DeepSkyBlue, StrokeThickness = 1,
                Cursor = Cursors.SizeNWSE,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -2, -2)
            };

            nodeGrid.Children.Add(dashedBorder);
            nodeGrid.Children.Add(textBox);
            nodeGrid.Children.Add(resizeHandle);
            
            ApplyTextStyleToTextBox(textBox);

            // 그룹화하여 버튼 함께 관리 (넘버링과 동일 스타일)
            var group = new Canvas { Background = Brushes.Transparent };
            group.Children.Add(nodeGrid);
            Canvas.SetLeft(nodeGrid, 0);
            Canvas.SetTop(nodeGrid, 0);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var confirmBtn = new Button { 
                Content = "✓", Width = 24, Height = 24, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Green, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12, FontWeight = FontWeights.Bold
            };
            var deleteBtn = new Button { 
                Content = "✕", Width = 24, Height = 24, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Red, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12, FontWeight = FontWeights.Bold
            };
            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(deleteBtn);
            group.Children.Add(btnPanel);
            
            // 버튼 위치 동기화 로직
            void UpdateBtnPos() {
                double w = nodeGrid.ActualWidth > 0 ? nodeGrid.ActualWidth : textBox.Width;
                Canvas.SetLeft(btnPanel, w + 5);
                Canvas.SetTop(btnPanel, 2);
            }
            nodeGrid.SizeChanged += (s, e) => UpdateBtnPos();

            _canvas.Children.Add(group);
            Canvas.SetLeft(group, p.X);
            Canvas.SetTop(group, p.Y);

            _drawnElements.Add(group);
            _undoStack?.Push(group);
            SelectedObject = group;

            confirmBtn.Click += (s, e) => {
                textBox.IsReadOnly = true;
                dashedBorder.Visibility = Visibility.Collapsed;
                resizeHandle.Visibility = Visibility.Collapsed;
                btnPanel.Visibility = Visibility.Collapsed;
                textBox.Background = Brushes.Transparent;
                group.Background = null;
            };

            deleteBtn.Click += (s, e) => {
                _canvas.Children.Remove(group);
                _drawnElements.Remove(group);
            };

            // 리사이즈 로직
            bool isResizing = false;
            Point lastResizePos = new Point();
            resizeHandle.MouseLeftButtonDown += (s, e) => {
                isResizing = true;
                lastResizePos = e.GetPosition(_canvas);
                resizeHandle.CaptureMouse();
                e.Handled = true;
            };
            resizeHandle.MouseMove += (s, e) => {
                if (isResizing) {
                    Point currentPos = e.GetPosition(_canvas);
                    double dx = currentPos.X - lastResizePos.X;
                    double dy = currentPos.Y - lastResizePos.Y;
                    
                    textBox.Width = Math.Max(40, (double.IsNaN(textBox.Width) ? textBox.ActualWidth : textBox.Width) + dx);
                    textBox.Height = Math.Max(30, (double.IsNaN(textBox.Height) ? textBox.ActualHeight : textBox.Height) + dy);
                    
                    lastResizePos = currentPos;
                    e.Handled = true;
                }
            };
            resizeHandle.MouseLeftButtonUp += (s, e) => {
                isResizing = false;
                resizeHandle.ReleaseMouseCapture();
                e.Handled = true;
            };

            // 드래그 로직 (그룹 이동)
            bool isDragging = false;
            Point lastPos = new Point();
            group.PreviewMouseLeftButtonDown += (s, e) => {
                // ReadOnly일 때만 드래그 허용 (편집 중엔 텍스트 선택 우선)
                if (!textBox.IsReadOnly) return;
                
                // 더블 클릭 시 수정 모드 활성화
                if (e.ClickCount == 2)
                {
                    textBox.IsReadOnly = false;
                    dashedBorder.Visibility = Visibility.Visible;
                    resizeHandle.Visibility = Visibility.Visible;
                    btnPanel.Visibility = Visibility.Visible;
                    SelectedObject = group; 
                    textBox.Focus();
                    e.Handled = true;
                    return;
                }
                
                isDragging = true;
                lastPos = e.GetPosition(_canvas);
                SelectedObject = group; 
                group.CaptureMouse();
                e.Handled = true;
            };
            group.MouseMove += (s, e) => {
                if (isDragging) {
                    Point currentPos = e.GetPosition(_canvas);
                    Canvas.SetLeft(group, Canvas.GetLeft(group) + (currentPos.X - lastPos.X));
                    Canvas.SetTop(group, Canvas.GetTop(group) + (currentPos.Y - lastPos.Y));
                    lastPos = currentPos;
                }
            };
            group.MouseLeftButtonUp += (s, e) => {
                isDragging = false;
                group.ReleaseMouseCapture();
            };

            UpdateBtnPos();
            
            ElementAdded?.Invoke(group);
            ActionOccurred?.Invoke();
            textBox.Focus();
        }

        public void ResetNumbering()
        {
            NextNumber = 1;
        }

        public void RecalculateNextNumber()
        {
            var existingNumbers = new HashSet<int>();
            foreach (var el in _drawnElements)
            {
                if (el is Canvas group && group.Children.Count >= 1 && group.Children[0] is Border badge)
                {
                    if (badge.Child is TextBlock tb && int.TryParse(tb.Text, out int num))
                    {
                        existingNumbers.Add(num);
                    }
                }
            }

            // 1부터 시작하여 비어있는 가장 작은 번호를 찾음
            int next = 1;
            while (existingNumbers.Contains(next))
            {
                next++;
            }
            NextNumber = next;
        }

        public void ApplyCurrentTextSettingsToSelectedObject()
        {
            if (SelectedObject == null) return;

            TextBox? tb = null;
            if (SelectedObject is TextBox t) tb = t;
            else if (SelectedObject is Canvas group)
            {
                // Numbering or grouped text
                foreach (var child in group.Children)
                {
                    if (child is TextBox note) { tb = note; break; }
                }

                // If Numbering, also maybe resize badge
                if (group.Children.Count >= 1 && group.Children[0] is Border badge)
                {
                    double bSize = NumberingBadgeSize;
                    badge.Width = badge.Height = bSize;
                    badge.CornerRadius = new CornerRadius(bSize / 2);
                    badge.Background = new SolidColorBrush(NumberingBadgeColor);
                    if (badge.Child is TextBlock btb) 
                    { 
                        btb.FontSize = bSize * 0.5;
                        btb.Foreground = GetContrastBrush(NumberingBadgeColor);
                    }
                    
                    // 배지 크기 변경에 맞춰 노트 위치도 조정
                    if (tb != null) 
                    {
                        Canvas.SetLeft(tb, bSize + 5);
                    }
                }
            }

            if (tb != null)
            {
                ApplyTextStyleToTextBox(tb);
                tb.FontSize = (CurrentTool == "넘버링") ? NumberingTextSize : TextFontSize;
                tb.Foreground = new SolidColorBrush((CurrentTool == "넘버링") ? NumberingNoteColor : SelectedColor);
                
                // [추가] 포커스 복원하여 즉시 편집 가능하게 함
                if (!tb.IsReadOnly) tb.Focus();
            }
        }

        private void ApplyTextStyleToTextBox(TextBox tb)
        {
            tb.FontFamily = new FontFamily(TextFontFamily);
            tb.FontWeight = TextFontWeight;
            tb.FontStyle = TextFontStyle;
            
            if (TextUnderlineEnabled)
            {
                tb.TextDecorations = TextDecorations.Underline;
            }
            else
            {
                tb.TextDecorations = null;
            }

            if (TextShadowEnabled)
            {
                tb.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 1,
                    Opacity = 0.5
                };
            }
            else
            {
                tb.Effect = null;
            }
        }

        private Color GetContrastColor(Color c)
        {
            double brightness = (c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
            return brightness > 128 ? Colors.Black : Colors.White;
        }

        private Brush GetContrastBrush(Color c) => new SolidColorBrush(GetContrastColor(c));
    }
}
