using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CatchCapture.Models;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// SnippingWindow의 깔끔한 편집 로직을 통합 관리하는 매니저 클래스
    /// </summary>
    public class CanvasEditorManager
    {
        private readonly Canvas _canvas;
        private readonly List<UIElement> _drawnElements;
        private readonly Stack<UIElement> _undoStack;
        
        public string CurrentTool { get; set; } = "";
        public Color SelectedColor { get; set; } = Colors.Red;
        public int PenThickness { get; set; } = 3;
        public int HighlightThickness { get; set; } = 8;
        public double HighlightOpacity { get; set; } = 0.5;
        public ShapeType CurrentShapeType { get; set; } = ShapeType.Rectangle;
        public double ShapeBorderThickness { get; set; } = 2;
        public bool ShapeIsFilled { get; set; } = false;
        public double ShapeFillOpacity { get; set; } = 0.5;
        
        private Polyline? _currentPolyline;
        private Point _lastDrawPoint;
        private bool _isDrawingShape = false;
        private Point _shapeStartPoint;
        private UIElement? _tempShape;
        private Rectangle? _tempMosaicSelection;
        
        public event Action<Rect>? OnMosaicRequired;
        public event Action? OnActionOccurred;

        public CanvasEditorManager(Canvas canvas, List<UIElement> drawnElements, Stack<UIElement> undoStack)
        {
            _canvas = canvas;
            _drawnElements = drawnElements;
            _undoStack = undoStack;
        }

        public void HandleMouseDown(Point clickPoint, object originalSource)
        {
            if (originalSource is FrameworkElement fe && (fe is Button || fe.Parent is Button || fe.TemplatedParent is Button))
                return;

            if (CurrentTool == "넘버링")
            {
                // 넘버링 생성 로직은 별도 메서드로 분리하거나 외부에서 처리
                return;
            }

            if (CurrentTool == "도형")
            {
                _isDrawingShape = true;
                _shapeStartPoint = clickPoint;
                _tempShape = ShapeDrawingHelper.CreateShape(
                    CurrentShapeType, _shapeStartPoint, _shapeStartPoint, 
                    SelectedColor, ShapeBorderThickness, ShapeIsFilled, ShapeFillOpacity);
                
                if (_tempShape != null)
                {
                    _canvas.Children.Add(_tempShape);
                }
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
                    StrokeLineJoin = PenLineJoin.Round
                };

                if (CurrentTool == "형광펜")
                {
                    _currentPolyline.Opacity = HighlightOpacity;
                }
                
                _currentPolyline.Points.Add(_lastDrawPoint);
                _canvas.Children.Add(_currentPolyline);
                _drawnElements.Add(_currentPolyline);
                _undoStack.Push(_currentPolyline);
                _canvas.CaptureMouse();
            }
        }

        public void HandleMouseMove(Point currentPoint)
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

            if ((CurrentTool == "펜" || CurrentTool == "형광펜") && _currentPolyline != null)
            {
                double distance = Math.Sqrt(Math.Pow(currentPoint.X - _lastDrawPoint.X, 2) + Math.Pow(currentPoint.Y - _lastDrawPoint.Y, 2));
                if (distance > 2)
                {
                    int steps = (int)(distance / 2);
                    for (int i = 1; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        Point interpolated = new Point(
                            _lastDrawPoint.X + (currentPoint.X - _lastDrawPoint.X) * t,
                            _lastDrawPoint.Y + (currentPoint.Y - _lastDrawPoint.Y) * t);
                        _currentPolyline.Points.Add(interpolated);
                    }
                }
                _currentPolyline.Points.Add(currentPoint);
                _lastDrawPoint = currentPoint;
            }
        }

        public void HandleMouseUp()
        {
            if (CurrentTool == "도형")
            {
                if (_isDrawingShape && _tempShape != null)
                {
                    _drawnElements.Add(_tempShape);
                    _undoStack.Push(_tempShape);
                    _tempShape = null;
                    _isDrawingShape = false;
                    OnActionOccurred?.Invoke();
                }
                _canvas.ReleaseMouseCapture();
                return;
            }

            if (CurrentTool == "모자이크")
            {
                if (_isDrawingShape && _tempMosaicSelection != null)
                {
                    Rect rect = new Rect(Canvas.GetLeft(_tempMosaicSelection), Canvas.GetTop(_tempMosaicSelection), _tempMosaicSelection.Width, _tempMosaicSelection.Height);
                    OnMosaicRequired?.Invoke(rect);
                    _canvas.Children.Remove(_tempMosaicSelection);
                    _tempMosaicSelection = null;
                    _isDrawingShape = false;
                }
                _canvas.ReleaseMouseCapture();
                return;
            }

            if (_currentPolyline != null)
            {
                _currentPolyline = null;
                _canvas.ReleaseMouseCapture();
                OnActionOccurred?.Invoke();
            }
        }
    }
}
