using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CatchCapture.Utilities
{
    public static class ImageEditUtility
    {
        public static BitmapSource CropImage(BitmapSource source, Int32Rect rect)
        {
            CroppedBitmap croppedBitmap = new CroppedBitmap(source, rect);
            return croppedBitmap;
        }

        public static BitmapSource RotateImage(BitmapSource source)
        {
            // 90도 회전
            TransformedBitmap transformedBitmap = new TransformedBitmap(source, new RotateTransform(90));
            return transformedBitmap;
        }

        public static BitmapSource FlipImage(BitmapSource source, bool horizontal)
        {
            ScaleTransform transform;
            if (horizontal)
            {
                transform = new ScaleTransform(-1, 1, source.PixelWidth / 2.0, 0);
            }
            else
            {
                transform = new ScaleTransform(1, -1, 0, source.PixelHeight / 2.0);
            }

            TransformedBitmap transformedBitmap = new TransformedBitmap(source, transform);
            return transformedBitmap;
        }

        public static BitmapSource ApplyHighlight(BitmapSource source, Point[] points, Color color, double thickness)
        {
            if (points == null || points.Length < 2)
                return source;

            // 렌더링을 위한 DrawingVisual 생성
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 원본 이미지 그리기
                drawingContext.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                // 하이라이트 그리기
                Pen pen = new Pen(new SolidColorBrush(color), thickness);
                pen.LineJoin = PenLineJoin.Round;

                for (int i = 0; i < points.Length - 1; i++)
                {
                    drawingContext.DrawLine(pen, points[i], points[i + 1]);
                }
            }

            // DrawingVisual을 RenderTargetBitmap으로 변환
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                source.PixelWidth, source.PixelHeight,
                source.DpiX, source.DpiY,
                PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            return renderTargetBitmap;
        }

        public static BitmapSource ApplyMosaic(BitmapSource source, Int32Rect area, int pixelSize)
        {
            // 원본 이미지를 픽셀 배열로 변환
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4; // BGRA 형식
            byte[] pixels = new byte[height * stride];
            source.CopyPixels(pixels, stride, 0);

            // 모자이크 영역 계산
            int startX = Math.Max(0, area.X);
            int startY = Math.Max(0, area.Y);
            int endX = Math.Min(width, area.X + area.Width);
            int endY = Math.Min(height, area.Y + area.Height);

            // 모자이크 적용
            for (int y = startY; y < endY; y += pixelSize)
            {
                for (int x = startX; x < endX; x += pixelSize)
                {
                    // 블록의 평균 색상 계산
                    int blockEndX = Math.Min(x + pixelSize, endX);
                    int blockEndY = Math.Min(y + pixelSize, endY);
                    int totalR = 0, totalG = 0, totalB = 0, totalA = 0, count = 0;

                    for (int by = y; by < blockEndY; by++)
                    {
                        for (int bx = x; bx < blockEndX; bx++)
                        {
                            int index = by * stride + bx * 4;
                            totalB += pixels[index];
                            totalG += pixels[index + 1];
                            totalR += pixels[index + 2];
                            totalA += pixels[index + 3];
                            count++;
                        }
                    }

                    byte avgB = (byte)(totalB / count);
                    byte avgG = (byte)(totalG / count);
                    byte avgR = (byte)(totalR / count);
                    byte avgA = (byte)(totalA / count);

                    // 블록에 평균 색상 적용
                    for (int by = y; by < blockEndY; by++)
                    {
                        for (int bx = x; bx < blockEndX; bx++)
                        {
                            int index = by * stride + bx * 4;
                            pixels[index] = avgB;
                            pixels[index + 1] = avgG;
                            pixels[index + 2] = avgR;
                            pixels[index + 3] = avgA;
                        }
                    }
                }
            }

            // 픽셀 배열을 다시 BitmapSource로 변환
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, source.DpiX, source.DpiY, source.Format, null);
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

            return writeableBitmap;
        }

        public static BitmapSource ApplyEraser(BitmapSource source, Point[] points, double radius)
        {
            if (points == null || points.Length < 1)
                return source;

            // 렌더링을 위한 DrawingVisual 생성
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 원본 이미지 그리기
                drawingContext.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                // 지우개 효과 적용
                SolidColorBrush eraserBrush = new SolidColorBrush(Colors.White);
                foreach (Point point in points)
                {
                    drawingContext.DrawEllipse(eraserBrush, null, point, radius, radius);
                }
            }

            // DrawingVisual을 RenderTargetBitmap으로 변환
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                source.PixelWidth, source.PixelHeight,
                source.DpiX, source.DpiY,
                PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            return renderTargetBitmap;
        }

        public static BitmapSource AddText(BitmapSource source, string text, Point position, Color color, double fontSize)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 원본 이미지 그리기
                drawingContext.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                // 텍스트 그리기
                FormattedText formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    fontSize,
                    new SolidColorBrush(color),
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                drawingContext.DrawText(formattedText, position);
            }

            // DrawingVisual을 RenderTargetBitmap으로 변환
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                source.PixelWidth, source.PixelHeight,
                source.DpiX, source.DpiY,
                PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);

            return renderTargetBitmap;
        }
    }
} 