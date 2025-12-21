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
            else
            {
                Canvas.SetLeft(element, left + dx);
                Canvas.SetTop(element, top + dy);
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
    }
}
