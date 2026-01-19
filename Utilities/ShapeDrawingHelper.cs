using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// 도형의 속성을 저장하기 위한 메타데이터 클래스 (SnippingWindow 등에서 사용)
    /// </summary>
    public class ShapeMetadata
    {
        public ShapeType ShapeType { get; set; }
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public Color Color { get; set; }
        public double Thickness { get; set; }
        public bool IsFilled { get; set; }
        public double FillOpacity { get; set; }
        public double Rotation { get; set; }
    }

    /// <summary>
    /// 도형 그리기 및 업데이트를 담당하는 공통 유틸리티 클래스
    /// </summary>
    public static class ShapeDrawingHelper
    {
        /// <summary>
        /// 지정된 타입의 도형 UI 요소를 생성합니다.
        /// </summary>
        public static FrameworkElement? CreateShape(ShapeType shapeType, Point start, Point current, Color color, double thickness, bool isFilled, double fillOpacity)
        {
            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            Shape? shape = null;

            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    shape = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = thickness,
                        Fill = isFilled ? new SolidColorBrush(Color.FromArgb((byte)(fillOpacity * 255), color.R, color.G, color.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    break;

                case ShapeType.Ellipse:
                    shape = new Ellipse
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = thickness,
                        Fill = isFilled ? new SolidColorBrush(Color.FromArgb((byte)(fillOpacity * 255), color.R, color.G, color.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    break;

                case ShapeType.Line:
                    shape = new Line
                    {
                        X1 = start.X,
                        Y1 = start.Y,
                        X2 = current.X,
                        Y2 = current.Y,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = thickness,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    break;

                case ShapeType.Arrow:
                    var arrow = CreateArrow(start, current, color, thickness);
                    if (arrow is Canvas arrowCanvas)
                    {
                        arrowCanvas.Tag = new ShapeMetadata
                        {
                            ShapeType = ShapeType.Arrow,
                            StartPoint = start,
                            EndPoint = current,
                            Color = color,
                            Thickness = thickness
                        };
                    }
                    return arrow;
            }

            if (shape != null)
            {
                shape.Tag = new ShapeMetadata
                {
                    ShapeType = shapeType,
                    StartPoint = start,
                    EndPoint = current,
                    Color = color,
                    Thickness = thickness,
                    IsFilled = isFilled,
                    FillOpacity = fillOpacity
                };
            }

            return shape;
        }

        public static void UpdateShapeProperties(UIElement shape, Point start, Point current, Color color, double thickness)
        {
            if (shape == null) return;

            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            // 메타데이터 업데이트 (SnippingWindow용)
            if (shape is FrameworkElement fe && fe.Tag is ShapeMetadata metadata)
            {
                metadata.StartPoint = start;
                metadata.EndPoint = current;
                metadata.Color = color;
                metadata.Thickness = thickness;
            }

            if (shape is Rectangle rect)
            {
                rect.Width = width;
                rect.Height = height;
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Stroke = new SolidColorBrush(color);
                rect.StrokeThickness = thickness;
            }
            else if (shape is Ellipse ellipse)
            {
                ellipse.Width = width;
                ellipse.Height = height;
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
                ellipse.Stroke = new SolidColorBrush(color);
                ellipse.StrokeThickness = thickness;
            }
            else if (shape is Line shapeLine)
            {
                shapeLine.X1 = start.X;
                shapeLine.Y1 = start.Y;
                shapeLine.X2 = current.X;
                shapeLine.Y2 = current.Y;
                shapeLine.Stroke = new SolidColorBrush(color);
                shapeLine.StrokeThickness = thickness;
            }
            else if (shape is Canvas arrowCanvas)
            {
                UpdateArrow(arrowCanvas, start, current, color, thickness);
            }
        }

        public static FrameworkElement CreateArrow(Point start, Point end, Color color, double thickness)
        {
            Canvas arrowCanvas = new Canvas();
            UpdateArrow(arrowCanvas, start, end, color, thickness);
            return arrowCanvas;
        }

        public static void UpdateArrow(Canvas arrowCanvas, Point start, Point end, Color color, double thickness)
        {
            arrowCanvas.Children.Clear();

            // 화살표 몸통 (선)
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            arrowCanvas.Children.Add(line);

            // 화살표 머리 계산
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 15 + thickness * 2;
            double arrowAngle = Math.PI / 6;

            Point arrowPoint1 = new Point(
                end.X - arrowLength * Math.Cos(angle - arrowAngle),
                end.Y - arrowLength * Math.Sin(angle - arrowAngle)
            );

            Point arrowPoint2 = new Point(
                end.X - arrowLength * Math.Cos(angle + arrowAngle),
                end.Y - arrowLength * Math.Sin(angle + arrowAngle)
            );

            Polygon arrowHead = new Polygon
            {
                Points = new PointCollection { end, arrowPoint1, arrowPoint2 },
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 1 // 머리의 테두리는 얇게 고정
            };
            arrowCanvas.Children.Add(arrowHead);
        }

        public static void ApplyDrawingLayer(UIElement element, CatchCapture.Models.DrawingLayer layer)
        {
            if (element is FrameworkElement fe)
            {
                if (layer.Rotation != 0)
                {
                    fe.RenderTransformOrigin = new Point(0.5, 0.5);
                    fe.RenderTransform = new RotateTransform(layer.Rotation);
                }
                else
                {
                    fe.RenderTransform = null;
                }
            }

            if (element is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(layer.Color);
                shape.StrokeThickness = layer.Thickness;
                if (shape is Rectangle || shape is Ellipse)
                {
                    shape.Fill = layer.IsFilled ? 
                        new SolidColorBrush(Color.FromArgb((byte)(layer.FillOpacity * 255), layer.Color.R, layer.Color.G, layer.Color.B)) : 
                        Brushes.Transparent;
                    
                    if (layer.StartPoint.HasValue && layer.EndPoint.HasValue)
                    {
                        var start = layer.StartPoint.Value;
                        var end = layer.EndPoint.Value;
                        shape.Width = Math.Abs(end.X - start.X);
                        shape.Height = Math.Abs(end.Y - start.Y);
                        Canvas.SetLeft(shape, Math.Min(start.X, end.X));
                        Canvas.SetTop(shape, Math.Min(start.Y, end.Y));
                    }
                }
                else if (shape is Line line && layer.StartPoint.HasValue && layer.EndPoint.HasValue)
                {
                    line.X1 = layer.StartPoint.Value.X;
                    line.Y1 = layer.StartPoint.Value.Y;
                    line.X2 = layer.EndPoint.Value.X;
                    line.Y2 = layer.EndPoint.Value.Y;
                }
            }
            else if (element is Canvas arrowCanvas && layer.StartPoint.HasValue && layer.EndPoint.HasValue)
            {
                UpdateArrow(arrowCanvas, layer.StartPoint.Value, layer.EndPoint.Value, layer.Color, layer.Thickness);
            }
        }
    }
}
