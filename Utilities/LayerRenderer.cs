using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// 레이어 기반 렌더링 유틸리티
    /// </summary>
    public static class LayerRenderer
    {
        /// <summary>
        /// 원본 이미지와 모든 레이어를 합성하여 최종 이미지 생성
        /// </summary>
        public static BitmapSource RenderLayers(BitmapSource baseImage, List<DrawingLayer> layers)
        {
            if (layers == null || layers.Count == 0)
                return baseImage;

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 1. 원본 이미지 그리기
                drawingContext.DrawImage(baseImage, new Rect(0, 0, baseImage.PixelWidth, baseImage.PixelHeight));

                // 2. 각 레이어를 순서대로 렌더링 (인터랙티브 레이어 제외)
                foreach (var layer in layers.Where(l => !l.IsErased && !l.IsInteractive))
                {
                    RenderLayer(drawingContext, layer);
                }
            }

            // DrawingVisual을 RenderTargetBitmap으로 변환
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                baseImage.PixelWidth, baseImage.PixelHeight,
                baseImage.DpiX, baseImage.DpiY,
                PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            return renderTargetBitmap;
        }

        /// <summary>
        /// 개별 레이어 렌더링
        /// </summary>
        private static void RenderLayer(DrawingContext dc, DrawingLayer layer)
        {
            switch (layer.Type)
            {
                case DrawingLayerType.Pen:
                    RenderPenLayer(dc, layer);
                    break;
                case DrawingLayerType.Highlight:
                    RenderHighlightLayer(dc, layer);
                    break;
                case DrawingLayerType.Shape:
                    RenderShapeLayer(dc, layer);
                    break;
                case DrawingLayerType.Text:
                    RenderTextLayer(dc, layer);
                    break;
                case DrawingLayerType.Mosaic:
                    // 모자이크는 이미지 픽셀 조작이 필요하므로 별도 처리
                    break;
            }
        }

        /// <summary>
        /// 펜 레이어 렌더링
        /// </summary>
        private static void RenderPenLayer(DrawingContext dc, DrawingLayer layer)
        {
            if (layer.Points == null || layer.Points.Length < 2)
                return;

            var brush = new SolidColorBrush(layer.Color);
            if (brush.CanFreeze) brush.Freeze();

            Pen pen = new Pen(brush, layer.Thickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (pen.CanFreeze) pen.Freeze();

            for (int i = 0; i < layer.Points.Length - 1; i++)
            {
                dc.DrawLine(pen, layer.Points[i], layer.Points[i + 1]);
            }
        }

        /// <summary>
        /// 형광펜 레이어 렌더링
        /// </summary>
        private static void RenderHighlightLayer(DrawingContext dc, DrawingLayer layer)
        {
            if (layer.Points == null || layer.Points.Length < 2)
                return;

            var highlightBrush = new SolidColorBrush(layer.Color);
            if (highlightBrush.CanFreeze) highlightBrush.Freeze();

            Pen pen = new Pen(highlightBrush, layer.Thickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (pen.CanFreeze) pen.Freeze();

            // 연속 경로로 렌더링
            var geometry = new StreamGeometry();
            using (var gc = geometry.Open())
            {
                gc.BeginFigure(layer.Points[0], false, false);
                var rest = layer.Points.Skip(1).ToArray();
                if (rest.Length > 0)
                    gc.PolyLineTo(rest, true, true);
            }
            if (geometry.CanFreeze) geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        /// <summary>
        /// 도형 레이어 렌더링
        /// </summary>
        private static void RenderShapeLayer(DrawingContext dc, DrawingLayer layer)
        {
            if (layer.StartPoint == null || layer.EndPoint == null || layer.ShapeType == null)
                return;

            Point start = layer.StartPoint.Value;
            Point end = layer.EndPoint.Value;

            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);

            Pen pen = new Pen(new SolidColorBrush(layer.Color), layer.Thickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (pen.CanFreeze) pen.Freeze();

            Brush fillBrush = layer.IsFilled 
                ? new SolidColorBrush(Color.FromArgb(128, layer.Color.R, layer.Color.G, layer.Color.B)) 
                : Brushes.Transparent;

            switch (layer.ShapeType.Value)
            {
                case ShapeType.Rectangle:
                    dc.DrawRectangle(fillBrush, pen, new Rect(left, top, width, height));
                    break;

                case ShapeType.Ellipse:
                    dc.DrawEllipse(fillBrush, pen, new Point(left + width / 2, top + height / 2), width / 2, height / 2);
                    break;

                case ShapeType.Line:
                    dc.DrawLine(pen, start, end);
                    break;

                case ShapeType.Arrow:
                    DrawArrow(dc, start, end, pen, layer.Thickness);
                    break;
            }
        }

        /// <summary>
        /// 화살표 그리기
        /// </summary>
        private static void DrawArrow(DrawingContext dc, Point startPoint, Point endPoint, Pen pen, double thickness)
        {
            // 화살표 선 그리기
            dc.DrawLine(pen, startPoint, endPoint);

            // 화살표 머리 크기 계산
            double arrowLength = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));
            double arrowHeadWidth = Math.Min(10, arrowLength / 3);

            // 화살표 각도 계산
            double angle = Math.Atan2(endPoint.Y - startPoint.Y, endPoint.X - startPoint.X);
            double arrowHeadAngle1 = angle + Math.PI / 6; // 30도
            double arrowHeadAngle2 = angle - Math.PI / 6; // -30도

            // 화살표 머리 끝점 계산
            Point arrowHead1 = new Point(
                endPoint.X - arrowHeadWidth * Math.Cos(arrowHeadAngle1),
                endPoint.Y - arrowHeadWidth * Math.Sin(arrowHeadAngle1));

            Point arrowHead2 = new Point(
                endPoint.X - arrowHeadWidth * Math.Cos(arrowHeadAngle2),
                endPoint.Y - arrowHeadWidth * Math.Sin(arrowHeadAngle2));

            // 화살표 머리 그리기
            dc.DrawLine(pen, endPoint, arrowHead1);
            dc.DrawLine(pen, endPoint, arrowHead2);
        }

        /// <summary>
        /// 텍스트 레이어 렌더링
        /// </summary>
        private static void RenderTextLayer(DrawingContext dc, DrawingLayer layer)
        {
            if (string.IsNullOrEmpty(layer.Text) || layer.TextPosition == null)
                return;

            var typeface = new Typeface(
                new FontFamily(layer.FontFamily),
                layer.FontStyle,
                layer.FontWeight,
                FontStretches.Normal);

            FormattedText formattedText = new FormattedText(
                layer.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                layer.FontSize,
                new SolidColorBrush(layer.Color),
                1.0);

            // 밑줄 추가
            if (layer.HasUnderline)
            {
                formattedText.SetTextDecorations(TextDecorations.Underline);
            }

            // 그림자 효과
            if (layer.HasShadow)
            {
                FormattedText shadowText = new FormattedText(
                    layer.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    layer.FontSize,
                    new SolidColorBrush(Colors.Black),
                    1.0);

                Point shadowPosition = new Point(
                    layer.TextPosition.Value.X + 1,
                    layer.TextPosition.Value.Y + 1);
                dc.DrawText(shadowText, shadowPosition);
            }

            dc.DrawText(formattedText, layer.TextPosition.Value);
        }

        /// <summary>
        /// 특정 영역과 겹치는 레이어 찾기 (지우개용)
        /// </summary>
        public static List<int> FindLayersInRegion(List<DrawingLayer> layers, Point center, double radius)
        {
            var affectedLayers = new List<int>();

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer.IsErased)
                    continue;

                bool isAffected = false;

                // 펜/형광펜 레이어 체크
                if ((layer.Type == DrawingLayerType.Pen || layer.Type == DrawingLayerType.Highlight) 
                    && layer.Points != null)
                {
                    foreach (var point in layer.Points)
                    {
                        double distance = Math.Sqrt(
                            Math.Pow(point.X - center.X, 2) + 
                            Math.Pow(point.Y - center.Y, 2));
                        
                        if (distance <= radius + layer.Thickness / 2)
                        {
                            isAffected = true;
                            break;
                        }
                    }
                }
                // 도형 레이어 체크
                else if (layer.Type == DrawingLayerType.Shape 
                    && layer.StartPoint != null && layer.EndPoint != null)
                {
                    Rect shapeBounds = new Rect(layer.StartPoint.Value, layer.EndPoint.Value);
                    Rect eraserBounds = new Rect(
                        center.X - radius, center.Y - radius,
                        radius * 2, radius * 2);
                    
                    if (shapeBounds.IntersectsWith(eraserBounds))
                    {
                        isAffected = true;
                    }
                }
                // 텍스트 레이어 체크
                else if (layer.Type == DrawingLayerType.Text && layer.TextPosition != null)
                {
                    double distance = Math.Sqrt(
                        Math.Pow(layer.TextPosition.Value.X - center.X, 2) + 
                        Math.Pow(layer.TextPosition.Value.Y - center.Y, 2));
                    
                    if (distance <= radius + layer.FontSize)
                    {
                        isAffected = true;
                    }
                }

                if (isAffected)
                {
                    affectedLayers.Add(i);
                }
            }

            return affectedLayers;
        }
    }
}
