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
    /// 즉시편집(SnippingWindow)과 이미지편집(PreviewWindow)에서 공통으로 사용하는
    /// 인터랙티브 편집 로직 유틸리티 클래스
    /// </summary>
    public static class InteractiveEditor
    {
        public static bool IsPointInElement(Point pt, UIElement element)
        {
            if (element is Polyline polyline)
            {
                foreach (var p in polyline.Points)
                {
                    if (Math.Abs(p.X - pt.X) < 10 && Math.Abs(p.Y - pt.Y) < 10) return true;
                }
            }
            else if (element is TextBox textBox)
            {
                double l = Canvas.GetLeft(textBox);
                double t = Canvas.GetTop(textBox);
                if (double.IsNaN(l)) l = 0;
                if (double.IsNaN(t)) t = 0;
                return pt.X >= l && pt.X <= l + textBox.ActualWidth && pt.Y >= t && pt.Y <= t + textBox.ActualHeight;
            }
            else if (element is Shape shape)
            {
                if (shape is Line line)
                {
                    return DistanceFromPointToLine(pt, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2)) < 10;
                }
                else
                {
                    double l = Canvas.GetLeft(shape);
                    double t = Canvas.GetTop(shape);
                    if (double.IsNaN(l)) l = 0;
                    if (double.IsNaN(t)) t = 0;
                    double w = shape.Width;
                    double h = shape.Height;
                    if (double.IsNaN(w)) w = shape.ActualWidth;
                    if (double.IsNaN(h)) h = shape.ActualHeight;
                    return pt.X >= l && pt.X <= l + w && pt.Y >= t && pt.Y <= t + h;
                }
            }
            else if (element is Canvas groupCanvas)
            {
                // Numbering group 등
                foreach (var child in groupCanvas.Children)
                {
                    if (child is UIElement ue && IsPointInElement(pt, ue)) return true;
                }
                
                // Canvas 자체 영역도 체크 (태그나 라벨 등이 있을 수 있음)
                double cl = Canvas.GetLeft(groupCanvas);
                double ct = Canvas.GetTop(groupCanvas);
                if (double.IsNaN(cl)) cl = 0;
                if (double.IsNaN(ct)) ct = 0;
                if (pt.X >= cl && pt.X <= cl + groupCanvas.ActualWidth && pt.Y >= ct && pt.Y <= ct + groupCanvas.ActualHeight) return true;
            }
            return false;
        }

        public static double DistanceFromPointToLine(Point pt, Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));

            double t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            if (t < 0) return Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
            if (t > 1) return Math.Sqrt(Math.Pow(pt.X - p2.X, 2) + Math.Pow(pt.Y - p2.Y, 2));

            Point proj = new Point(p1.X + t * dx, p1.Y + t * dy);
            return Math.Sqrt(Math.Pow(pt.X - proj.X, 2) + Math.Pow(pt.Y - proj.Y, 2));
        }

        public static void MoveElement(UIElement element, double dx, double dy)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            if (element is Line line)
            {
                line.X1 += dx; line.Y1 += dy;
                line.X2 += dx; line.Y2 += dy;
            }
            else if (element is Polyline polyline)
            {
                var points = polyline.Points;
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new Point(points[i].X + dx, points[i].Y + dy);
                }
            }
            else if (element is Canvas groupCanvas && (groupCanvas.Tag is ShapeMetadata || groupCanvas.Tag is CatchCapture.Models.DrawingLayer))
            {
                // Numbering group or Arrow group
                Canvas.SetLeft(element, left + dx);
                Canvas.SetTop(element, top + dy);

                // Arrows might have children that need explicit coordinate updates 
                foreach(var child in groupCanvas.Children)
                {
                    if (child is Line l) { l.X1 += dx; l.Y1 += dy; l.X2 += dx; l.Y2 += dy; }
                    else if (child is Polygon p) { for (int i = 0; i < p.Points.Count; i++) p.Points[i] = new Point(p.Points[i].X + dx, p.Points[i].Y + dy); }
                }
            }
            else
            {
                Canvas.SetLeft(element, left + dx);
                Canvas.SetTop(element, top + dy);
            }

            // Sync Metadata
            if (element is FrameworkElement fe)
            {
                if (fe.Tag is ShapeMetadata sm)
                {
                    sm.StartPoint = new Point(sm.StartPoint.X + dx, sm.StartPoint.Y + dy);
                    sm.EndPoint = new Point(sm.EndPoint.X + dx, sm.EndPoint.Y + dy);
                }
                else if (fe.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    if (layer.StartPoint.HasValue) layer.StartPoint = new Point(layer.StartPoint.Value.X + dx, layer.StartPoint.Value.Y + dy);
                    if (layer.EndPoint.HasValue) layer.EndPoint = new Point(layer.EndPoint.Value.X + dx, layer.EndPoint.Value.Y + dy);
                    if (layer.TextPosition.HasValue) layer.TextPosition = new Point(layer.TextPosition.Value.X + dx, layer.TextPosition.Value.Y + dy);
                }
            }
        }
        
        public static Rect GetElementBounds(UIElement element)
        {
            if (element is Polyline polyline)
            {
                if (polyline.Points.Count == 0) return Rect.Empty;
                double minX = polyline.Points.Min(p => p.X);
                double minY = polyline.Points.Min(p => p.Y);
                double maxX = polyline.Points.Max(p => p.X);
                double maxY = polyline.Points.Max(p => p.Y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            else if (element is Line line)
            {
                return new Rect(new Point(Math.Min(line.X1, line.X2), Math.Min(line.Y1, line.Y2)),
                                new Point(Math.Max(line.X1, line.X2), Math.Max(line.Y1, line.Y2)));
            }
            else if (element is Canvas groupCanvas)
            {
                if (groupCanvas.Children.Count == 0) return Rect.Empty;
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                bool found = false;
                foreach (UIElement child in groupCanvas.Children)
                {
                    Rect r = GetElementBounds(child);
                    if (r != Rect.Empty)
                    {
                        if (r.Left < minX) minX = r.Left;
                        if (r.Top < minY) minY = r.Top;
                        if (r.Right > maxX) maxX = r.Right;
                        if (r.Bottom > maxY) maxY = r.Bottom;
                        found = true;
                    }
                }
                return found ? new Rect(minX, minY, maxX - minX, maxY - minY) : Rect.Empty;
            }
            else
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                
                FrameworkElement fe = (FrameworkElement)element;
                double w = fe.Width;
                double h = fe.Height;
                if (double.IsNaN(w)) w = fe.ActualWidth;
                if (double.IsNaN(h)) h = fe.ActualHeight;
                
                return new Rect(left, top, w, h);
            }
        }

        public static void RemoveInteractiveElement(Canvas canvas, UIElement element)
        {
            if (element == null) return;

            // Remove the element itself
            if (canvas.Children.Contains(element))
                canvas.Children.Remove(element);

            // If it's a TextBox or other element with associated buttons in Tag
            if (element is FrameworkElement fe && fe.Tag != null)
            {
                if (fe.Tag is ValueTuple<Button, Button> tagButtons)
                {
                    var confirmButton = tagButtons.Item1;
                    var cancelButton = tagButtons.Item2;
                    if (confirmButton != null && canvas.Children.Contains(confirmButton))
                        canvas.Children.Remove(confirmButton);
                    if (cancelButton != null && canvas.Children.Contains(cancelButton))
                        canvas.Children.Remove(cancelButton);
                }
            }
        }

        public static void ResizeElement(UIElement element, double dx, double dy, string dir)
        {
            if (element is Shape shape && (shape is Rectangle || shape is Ellipse))
            {
                double left = Canvas.GetLeft(shape);
                double top = Canvas.GetTop(shape);
                double width = shape.Width;
                double height = shape.Height;

                if (dir.Contains("W")) { left += dx; width -= dx; }
                if (dir.Contains("E")) { width += dx; }
                if (dir.Contains("N")) { top += dy; height -= dy; }
                if (dir.Contains("S")) { height += dy; }

                if (width < 5) width = 5;
                if (height < 5) height = 5;

                shape.Width = width;
                shape.Height = height;
                Canvas.SetLeft(shape, left);
                Canvas.SetTop(shape, top);

                if (shape.Tag is ShapeMetadata metadata)
                {
                    metadata.StartPoint = new Point(left, top);
                    metadata.EndPoint = new Point(left + width, top + height);
                }
                else if (shape.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    layer.StartPoint = new Point(left, top);
                    layer.EndPoint = new Point(left + width, top + height);
                }
            }
            else if (element is TextBox textBox)
            {
                double left = Canvas.GetLeft(textBox);
                double top = Canvas.GetTop(textBox);
                double width = textBox.ActualWidth;
                double height = textBox.ActualHeight;

                if (dir.Contains("W")) { left += dx; width -= dx; }
                if (dir.Contains("E")) { width += dx; }
                if (dir.Contains("N")) { top += dy; height -= dy; }
                if (dir.Contains("S")) { height += dy; }

                if (width < 20) width = 20;
                if (height < 20) height = 20;

                textBox.Width = width;
                textBox.Height = height;
                Canvas.SetLeft(textBox, left);
                Canvas.SetTop(textBox, top);

                if (textBox.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    layer.TextPosition = new Point(left, top);
                }
            }
            else if (element is Line line)
            {
                if (dir == "NW" || dir == "W" || dir == "N") { line.X1 += dx; line.Y1 += dy; }
                else if (dir == "SE" || dir == "E" || dir == "S") { line.X2 += dx; line.Y2 += dy; }

                if (line.Tag is ShapeMetadata metadata)
                {
                    metadata.StartPoint = new Point(line.X1, line.Y1);
                    metadata.EndPoint = new Point(line.X2, line.Y2);
                }
                else if (line.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    layer.StartPoint = new Point(line.X1, line.Y1);
                    layer.EndPoint = new Point(line.X2, line.Y2);
                }
            }
            else if (element is Canvas arrowCanvas)
            {
                ShapeMetadata? sm = arrowCanvas.Tag as ShapeMetadata;
                CatchCapture.Models.DrawingLayer? layer = arrowCanvas.Tag as CatchCapture.Models.DrawingLayer;

                Point start = sm?.StartPoint ?? layer?.StartPoint ?? new Point(0, 0);
                Point end = sm?.EndPoint ?? layer?.EndPoint ?? new Point(0, 0);
                Color color = sm?.Color ?? layer?.Color ?? Colors.Black;
                double thickness = sm?.Thickness ?? layer?.Thickness ?? 2;

                if (dir == "NW" || dir == "W" || dir == "N") { start = new Point(start.X + dx, start.Y + dy); }
                else if (dir == "SE" || dir == "E" || dir == "S") { end = new Point(end.X + dx, end.Y + dy); }

                if (sm != null) { sm.StartPoint = start; sm.EndPoint = end; }
                if (layer != null) { layer.StartPoint = start; layer.EndPoint = end; }

                ShapeDrawingHelper.UpdateArrow(arrowCanvas, start, end, color, thickness);
            }
        }
    }
}
