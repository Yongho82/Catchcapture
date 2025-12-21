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
        
        // 텍스트 관련
        public string TextFontFamily { get; set; } = "Arial";
        public double TextFontSize { get; set; } = 16;
        public FontWeight TextFontWeight { get; set; } = FontWeights.Normal;
        public FontStyle TextFontStyle { get; set; } = FontStyles.Normal;
        public bool TextUnderlineEnabled { get; set; } = false;
        public bool TextShadowEnabled { get; set; } = false;

        // 기타 도구
        public double MosaicIntensity { get; set; } = 15;
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
                ActionOccurred?.Invoke();
            }
        }

        public void CreateNumberingAt(Point p)
        {
            var group = new Canvas { Background = Brushes.Transparent };
            double bSize = NumberingBadgeSize;
            int myNumber = NextNumber;
            
            var badge = new Border {
                Width = bSize, Height = bSize, 
                CornerRadius = new CornerRadius(bSize/2),
                Background = new SolidColorBrush(SelectedColor),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };
            
            var txt = new TextBlock {
                Text = myNumber.ToString(),
                Foreground = GetContrastBrush(SelectedColor),
                FontWeight = FontWeights.Bold, FontSize = bSize * 0.5,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = txt;
            group.Children.Add(badge);
            Canvas.SetLeft(badge, 0);
            Canvas.SetTop(badge, 0);
            
            var note = new TextBox {
                Width = 120, MinHeight = bSize,
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
                Foreground = Brushes.White, FontSize = NumberingTextSize,
                Text = "", VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(3), TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            group.Children.Add(note);
            Canvas.SetLeft(note, bSize + 5);
            Canvas.SetTop(note, 0);

            // 확정/삭제 버튼
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var confirmBtn = new Button { 
                Content = "✓", Width = 20, Height = 20, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Green, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var deleteBtn = new Button { 
                Content = "✕", Width = 20, Height = 20, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Red, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(deleteBtn);
            group.Children.Add(btnPanel);
            Canvas.SetLeft(btnPanel, bSize + 130);
            Canvas.SetTop(btnPanel, 2);

            _canvas.Children.Add(group);
            Canvas.SetLeft(group, p.X - bSize / 2);
            Canvas.SetTop(group, p.Y - bSize / 2);
            
            _drawnElements.Add(group);
            _undoStack?.Push(group);
            NextNumber++;

            // 상호작용 로직
            confirmBtn.Click += (s, e) => {
                note.IsReadOnly = true;
                note.Background = Brushes.Transparent;
                note.BorderThickness = new Thickness(0);
                btnPanel.Visibility = Visibility.Collapsed;
                group.Background = null; // 인터랙션 종료 시 투명 배경 제거 (히트테스트 최적화)
            };

            deleteBtn.Click += (s, e) => {
                _canvas.Children.Remove(group);
                _drawnElements.Remove(group);
            };

            // 드래그 로직 (그룹 이동)
            bool isDragging = false;
            Point lastPos = new Point();
            badge.MouseLeftButtonDown += (s, e) => {
                isDragging = true;
                lastPos = e.GetPosition(_canvas);
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
            var textBox = new TextBox
            {
                MinWidth = 100,
                MinHeight = 30,
                FontSize = TextFontSize,
                FontFamily = new FontFamily(TextFontFamily),
                FontWeight = TextFontWeight,
                FontStyle = TextFontStyle,
                Foreground = new SolidColorBrush(SelectedColor),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };

            if (TextUnderlineEnabled)
            {
                textBox.TextDecorations = TextDecorations.Underline;
            }

            if (TextShadowEnabled)
            {
                textBox.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 2,
                    ShadowDepth = 1,
                    Opacity = 0.5
                };
            }

            // 그룹화하여 버튼 함께 관리 (넘버링과 동일 스타일)
            var group = new Canvas { Background = Brushes.Transparent };
            group.Children.Add(textBox);
            Canvas.SetLeft(textBox, 0);
            Canvas.SetTop(textBox, 0);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var confirmBtn = new Button { 
                Content = "✓", Width = 22, Height = 22, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Green, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var deleteBtn = new Button { 
                Content = "✕", Width = 22, Height = 22, Margin = new Thickness(2,0,0,0),
                Background = Brushes.Red, Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(deleteBtn);
            group.Children.Add(btnPanel);
            
            // 버튼 위치 동기화 로직
            void UpdateBtnPos() {
                double w = double.IsNaN(textBox.Width) ? textBox.ActualWidth : textBox.Width;
                if (w < 100) w = 100;
                Canvas.SetLeft(btnPanel, w + 5);
                Canvas.SetTop(btnPanel, 2);
            }
            textBox.SizeChanged += (s, e) => UpdateBtnPos();

            _canvas.Children.Add(group);
            Canvas.SetLeft(group, p.X);
            Canvas.SetTop(group, p.Y);

            _drawnElements.Add(group);
            _undoStack?.Push(group);

            confirmBtn.Click += (s, e) => {
                textBox.IsReadOnly = true;
                textBox.BorderThickness = new Thickness(0);
                btnPanel.Visibility = Visibility.Collapsed;
                textBox.Background = Brushes.Transparent;
                group.Background = null;
            };

            deleteBtn.Click += (s, e) => {
                _canvas.Children.Remove(group);
                _drawnElements.Remove(group);
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
                    textBox.BorderThickness = new Thickness(2);
                    // 배경은 투명이라도 테두리가 생기므로 인식 가능
                    btnPanel.Visibility = Visibility.Visible;
                    textBox.Focus();
                    e.Handled = true;
                    return;
                }
                
                isDragging = true;
                lastPos = e.GetPosition(_canvas);
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

        private Color GetContrastColor(Color c)
        {
            double brightness = (c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
            return brightness > 128 ? Colors.Black : Colors.White;
        }

        private Brush GetContrastBrush(Color c) => new SolidColorBrush(GetContrastColor(c));
    }
}
