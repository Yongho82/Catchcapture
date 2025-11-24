using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatchCapture.Utilities;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 도형 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 도형 생성 및 적용

        private UIElement? CreateShape(Point start, Point current)
        {
            if (ImageCanvas == null) return null;

            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            WriteLog($"CreateShape 호출: 타입={shapeType}, 위치({left:F1}, {top:F1}), 크기({width:F1}x{height:F1})");

            Shape? shape = null;

            switch (shapeType)
            {
                case ShapeType.Rectangle:
                    shape = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness,
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb(80, shapeColor.R, shapeColor.G, shapeColor.B)) : Brushes.Transparent
                    };
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                    break;

                case ShapeType.Ellipse:
                    shape = new Ellipse
                    {
                        Width = width,
                        Height = height,
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness,
                        Fill = shapeIsFilled ? new SolidColorBrush(Color.FromArgb(80, shapeColor.R, shapeColor.G, shapeColor.B)) : Brushes.Transparent
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
                        Stroke = new SolidColorBrush(shapeColor),
                        StrokeThickness = shapeBorderThickness
                    };
                    break;

                case ShapeType.Arrow:
                    return CreateArrow(start, current);
            }

            if (shape != null)
            {
                WriteLog($"도형 생성 성공: {shape.GetType().Name}");
            }
            else
            {
                WriteLog("도형 생성 실패: shape is null");
            }

            return shape;
        }

        private UIElement CreateArrow(Point start, Point end)
        {
            Canvas arrowCanvas = new Canvas();

            // 화살표 몸통 (선)
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(shapeColor),
                StrokeThickness = shapeBorderThickness
            };
            arrowCanvas.Children.Add(line);

            // 화살표 머리 계산
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowLength = 15;
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
                Fill = new SolidColorBrush(shapeColor),
                Stroke = new SolidColorBrush(shapeColor),
                StrokeThickness = shapeBorderThickness
            };
            arrowCanvas.Children.Add(arrowHead);

            return arrowCanvas;
        }

        private void UpdateShapeProperties(UIElement shape, Point start, Point current)
        {
            if (shape == null) return;

            double left = Math.Min(start.X, current.X);
            double top = Math.Min(start.Y, current.Y);
            double width = Math.Abs(current.X - start.X);
            double height = Math.Abs(current.Y - start.Y);

            if (shape is Rectangle rect)
            {
                rect.Width = width;
                rect.Height = height;
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
            }
            else if (shape is Ellipse ellipse)
            {
                ellipse.Width = width;
                ellipse.Height = height;
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
            }
            else if (shape is Line shapeLine)
            {
                shapeLine.X1 = start.X;
                shapeLine.Y1 = start.Y;
                shapeLine.X2 = current.X;
                shapeLine.Y2 = current.Y;
            }
            else if (shape is Canvas arrowCanvas)
            {
                // 화살표는 다시 생성
                arrowCanvas.Children.Clear();
                
                Line line = new Line
                {
                    X1 = start.X,
                    Y1 = start.Y,
                    X2 = current.X,
                    Y2 = current.Y,
                    Stroke = new SolidColorBrush(shapeColor),
                    StrokeThickness = shapeBorderThickness
                };
                arrowCanvas.Children.Add(line);

                double angle = Math.Atan2(current.Y - start.Y, current.X - start.X);
                double arrowLength = 15;
                double arrowAngle = Math.PI / 6;

                Point arrowPoint1 = new Point(
                    current.X - arrowLength * Math.Cos(angle - arrowAngle),
                    current.Y - arrowLength * Math.Sin(angle - arrowAngle)
                );

                Point arrowPoint2 = new Point(
                    current.X - arrowLength * Math.Cos(angle + arrowAngle),
                    current.Y - arrowLength * Math.Sin(angle + arrowAngle)
                );

                Polygon arrowHead = new Polygon
                {
                    Points = new PointCollection { current, arrowPoint1, arrowPoint2 },
                    Fill = new SolidColorBrush(shapeColor),
                    Stroke = new SolidColorBrush(shapeColor),
                    StrokeThickness = shapeBorderThickness
                };
                arrowCanvas.Children.Add(arrowHead);
            }
        }

        private void ApplyShape(Point endPoint)
        {
            if (tempShape == null)
            {
                WriteLog("ApplyShape: tempShape is null");
                return;
            }

            WriteLog($"ApplyShape 시작: tempShape={tempShape.GetType().Name}");

            SaveForUndo();

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)currentImage.PixelWidth, (int)currentImage.PixelHeight,
                96, 96, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawImage(currentImage, new Rect(0, 0, currentImage.PixelWidth, currentImage.PixelHeight));

                if (tempShape is Shape shape)
                {
                    double left = Canvas.GetLeft(shape);
                    double top = Canvas.GetTop(shape);

                    dc.PushTransform(new TranslateTransform(left, top));

                    if (shape is Rectangle rect)
                    {
                        dc.DrawRectangle(rect.Fill, new Pen(rect.Stroke, rect.StrokeThickness),
                            new Rect(0, 0, rect.Width, rect.Height));
                    }
                    else if (shape is Ellipse ellipse)
                    {
                        dc.DrawEllipse(ellipse.Fill, new Pen(ellipse.Stroke, ellipse.StrokeThickness),
                            new Point(ellipse.Width / 2, ellipse.Height / 2), ellipse.Width / 2, ellipse.Height / 2);
                    }
                    else if (shape is Line shapeLine)
                    {
                        dc.DrawLine(new Pen(shapeLine.Stroke, shapeLine.StrokeThickness),
                            new Point(shapeLine.X1, shapeLine.Y1), new Point(shapeLine.X2, shapeLine.Y2));
                    }

                    dc.Pop();
                }
                else if (tempShape is Canvas arrowCanvas)
                {
                    VisualBrush vb = new VisualBrush(arrowCanvas);
                    dc.DrawRectangle(vb, null, new Rect(0, 0, currentImage.PixelWidth, currentImage.PixelHeight));
                }
            }

            rtb.Render(dv);
            currentImage = rtb;
            UpdatePreviewImage();

            CleanupTemporaryShape();
            WriteLog("ApplyShape 완료");
        }

        private void CleanupTemporaryShape()
        {
            if (tempShape != null)
            {
                WriteLog($"CleanupTemporaryShape: {tempShape.GetType().Name} 제거");
                ImageCanvas.Children.Remove(tempShape);
                tempShape = null;
            }
            else
            {
                WriteLog("CleanupTemporaryShape: tempShape is already null");
            }
        }

        #endregion

        #region 도형 타입 버튼 업데이트

        private void UpdateShapeTypeButtons()
        {
            if (rectButton != null)
                rectButton.Background = (shapeType == ShapeType.Rectangle) ? Brushes.LightBlue : Brushes.Transparent;

            if (ellipseButton != null)
                ellipseButton.Background = (shapeType == ShapeType.Ellipse) ? Brushes.LightBlue : Brushes.Transparent;

            if (lineButton != null)
                lineButton.Background = (shapeType == ShapeType.Line) ? Brushes.LightBlue : Brushes.Transparent;

            if (arrowButton != null)
                arrowButton.Background = (shapeType == ShapeType.Arrow) ? Brushes.LightBlue : Brushes.Transparent;
        }

        #endregion
    }
}
