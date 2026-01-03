using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// 이미지를 둥근 모서리로 처리하거나 특수 모양으로 가공하는 유틸리티 클래스입니다.
    /// 사용자 정의 및 커스텀 로직 추가가 용이하도록 독립된 파일로 구성되었습니다.
    /// </summary>
    public static class EdgeCaptureHelper
    {
        public static Bitmap? GetRoundedBitmap(Bitmap source, int radius)
        {
            if (source == null) return null;

            int width = source.Width;
            int height = source.Height;

            // 999는 퍼펙트 서클(최대 곡률)로 처리
            int actualRadius = radius;
            if (radius >= 999)
            {
                actualRadius = Math.Min(width, height) / 2;
            }

            // 원본 크기에서 투명 배경을 가진 출력용 비트맵 생성
            Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(target))
            {
                g.Clear(Color.Transparent);
                
                // 외곽선 품질 설정: AntiAlias를 사용하여 부드러운 경계 구현 (전체 스케일링을 배제하여 선명도 유지)
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, width, height), actualRadius))
                {
                    // 원본 이미지를 텍스처 브러시로 사용하여 경로 내부를 채움 (1:1 매핑)
                    using (TextureBrush brush = new TextureBrush(source))
                    {
                        brush.WrapMode = System.Drawing.Drawing2D.WrapMode.Clamp;
                        g.FillPath(brush, path);
                    }
                }
            }

            return target;
        }

        /// <summary>
        /// 사각형 영역 내에서 둥근 모서리를 가진 GraphicsPath를 생성합니다.
        /// </summary>
        public static GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;

            // 반지름이 0 이하이면 그냥 일반 사각형 반환
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // 반지름이 이미지 크기를 넘지 않도록 제한
            if (diameter > bounds.Width) diameter = bounds.Width;
            if (diameter > bounds.Height) diameter = bounds.Height;

            RectangleF arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);

            // 좌측 상단
            path.AddArc(arc, 180, 90);

            // 우측 상단
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // 우측 하단
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // 좌측 하단
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// WPF BitmapSource를 둥근 모서리로 처리하여 반환합니다.
        /// </summary>
        public static System.Windows.Media.Imaging.BitmapSource CreateRoundedCapture(System.Windows.Media.Imaging.BitmapSource source, int radius)
        {
            if (source == null) return source;

            // BitmapSource → GDI+ Bitmap 변환
            Bitmap gdiBitmap;
            using (var memoryStream = new System.IO.MemoryStream())
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                encoder.Save(memoryStream);
                memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
                gdiBitmap = new Bitmap(memoryStream);
            }

            // 둥근 모서리 적용
            var roundedBitmap = GetRoundedBitmap(gdiBitmap, radius);
            if (roundedBitmap == null) return source;

            // GDI+ Bitmap → BitmapSource 변환
            var hBitmap = roundedBitmap.GetHbitmap();
            try
            {
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                DeleteObject(hBitmap);
                gdiBitmap.Dispose();
                roundedBitmap.Dispose();
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
