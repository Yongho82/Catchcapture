using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;

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
                    CultureInfo.CurrentCulture,
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

        // 확장된 텍스트 추가 메서드 - 더 많은 서식 옵션 지원
        public static BitmapSource AddEnhancedText(BitmapSource source, string text, Point position, TextFormatOptions options)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 원본 이미지 그리기
                drawingContext.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));

                // 텍스트 그림자 효과 적용
                if (options.ApplyShadow)
                {
                    // 그림자용 FormattedText 객체 생성
                    FormattedText shadowText = new FormattedText(
                        text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily(options.FontFamily), options.FontStyle, options.FontWeight, options.FontStretch),
                        options.FontSize,
                        new SolidColorBrush(options.ShadowColor),
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    // 그림자 텍스트의 서식 설정
                    shadowText.TextAlignment = options.TextAlignment;
                    if (options.MaxWidth > 0)
                    {
                        shadowText.MaxTextWidth = options.MaxWidth;
                    }
                    
                    // 그림자용 텍스트 장식 설정
                    if (options.TextDecoration != null)
                    {
                        shadowText.SetTextDecorations(options.TextDecoration, 0, text.Length);
                    }
                    
                    // 그림자 텍스트 그리기
                    Point shadowPosition = new Point(
                        position.X + options.ShadowOffset.X, 
                        position.Y + options.ShadowOffset.Y);
                    drawingContext.DrawText(shadowText, shadowPosition);
                }
                
                // 메인 FormattedText 객체 생성 및 설정
                FormattedText formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily(options.FontFamily), options.FontStyle, options.FontWeight, options.FontStretch),
                    options.FontSize,
                    new SolidColorBrush(options.TextColor),
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                // 텍스트 정렬 설정
                formattedText.TextAlignment = options.TextAlignment;
                
                // 텍스트 최대 너비 설정 (자동 줄바꿈 활성화)
                if (options.MaxWidth > 0)
                {
                    formattedText.MaxTextWidth = options.MaxWidth;
                }
                
                // 텍스트 장식 설정 (밑줄 등)
                if (options.TextDecoration != null)
                {
                    formattedText.SetTextDecorations(options.TextDecoration, 0, text.Length);
                }
                
                // 메인 텍스트 그리기
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
        
        // 텍스트 그림자 효과 적용 메서드 - 사용하지 않음
        private static void DrawTextWithShadow(DrawingContext ctx, FormattedText text, Point position, Color shadowColor, Vector shadowOffset)
        {
            // 이 메서드는 사용하지 않음 
        }
    }
    
    // 텍스트 서식 옵션을 위한 클래스
    public class TextFormatOptions
    {
        // 기본 속성
        public string FontFamily { get; set; } = "Arial";
        public FontWeight FontWeight { get; set; } = FontWeights.Normal;
        public FontStyle FontStyle { get; set; } = FontStyles.Normal;
        public FontStretch FontStretch { get; set; } = FontStretches.Normal;
        public double FontSize { get; set; } = 16;
        public Color TextColor { get; set; } = Colors.Black;
        public double MaxWidth { get; set; } = 0; // 0은 자동 줄바꿈 없음
        public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
        public TextDecorationCollection TextDecoration { get; set; } = null;
        
        // 그림자 효과 속성
        public bool ApplyShadow { get; set; } = false;
        public Color ShadowColor { get; set; } = Color.FromArgb(128, 0, 0, 0);
        public Vector ShadowOffset { get; set; } = new Vector(1, 1);
        
        // 빠른 생성을 위한 정적 메서드
        public static TextFormatOptions DefaultOptions(Color textColor, double fontSize)
        {
            return new TextFormatOptions
            {
                TextColor = textColor,
                FontSize = fontSize
            };
        }
        
        public static TextFormatOptions WithShadow(Color textColor, double fontSize)
        {
            return new TextFormatOptions
            {
                TextColor = textColor,
                FontSize = fontSize,
                ApplyShadow = true
            };
        }
        
        public static TextFormatOptions WithUnderline(Color textColor, double fontSize)
        {
            var options = new TextFormatOptions
            {
                TextColor = textColor,
                FontSize = fontSize
            };
            options.TextDecoration = new TextDecorationCollection();
            options.TextDecoration.Add(TextDecorations.Underline);
            return options;
        }
        
        public static TextFormatOptions BoldText(Color textColor, double fontSize)
        {
            return new TextFormatOptions
            {
                TextColor = textColor,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold
            };
        }
    }
} 