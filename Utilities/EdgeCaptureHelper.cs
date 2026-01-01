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
        /// <summary>
        /// 원본 비트맵을 지정된 반지름만큼 둥글게 깎아서 새로운 비트맵으로 반환합니다.
        /// </summary>
        /// <param name="source">원본 이미지</param>
        /// <param name="radius">모서리 반지름 (px)</param>
        /// <returns>둥근 모서리가 적용된 투명 배경 비트맵</returns>
        public static Bitmap? GetRoundedBitmap(Bitmap source, int radius)
        {
            if (source == null) return null;

            // 투명도를 지원하는 32비트 ARGB 비트맵 생성
            Bitmap target = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(target))
            {
                // 최고 품질의 렌더링 설정
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                
                // [추가] 합성 품질 설정 - 안티앨리어싱 품질 향상의 핵심
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.CompositingMode = CompositingMode.SourceOver;

                // 999는 퍼펙트 서클(최대 곡률)로 처리
                int actualRadius = radius;
                if (radius >= 999)
                {
                    actualRadius = Math.Min(source.Width, source.Height) / 2;
                }

                // 둥근 사각형 경로 생성
                using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, source.Width, source.Height), actualRadius))
                {
                    // [개선] SetClip 방식은 경계면이 거칠어지므로 TextureBrush + FillPath 방식을 사용합니다.
                    using (TextureBrush brush = new TextureBrush(source))
                    {
                        // [추가] Wrap 모드 설정으로 경계 아티팩트 방지
                        brush.WrapMode = System.Drawing.Drawing2D.WrapMode.TileFlipXY;
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
    }
}
