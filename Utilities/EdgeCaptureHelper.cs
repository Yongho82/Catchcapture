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

            // 반지름이 0이면 가공 없이 원본 클론 반환 (직각 유지 및 아티팩트 방지)
            if (actualRadius <= 0)
            {
                return new Bitmap(source);
            }

            // 원본 크기에서 투명 배경을 가진 출력용 비트맵 생성 (투명 검정 배경)
            Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(target))
            {
                // 투명한 검은색으로 초기화하여 안티앨리어싱 시 흰색 테두리 방지
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                
                // 부드러운 곡선을 위해 안티앨리어싱 및 고품질 보간 적용
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy; // 배경색 혼합을 차단하여 흰색 테두리 방지

                using (GraphicsPath path = GetRoundedRectanglePath(new Rectangle(0, 0, width, height), actualRadius))
                {
                    // 텍스처 브러시 사용 (이미지 밖 영역 참조 차단을 위해 Clamp 설정)
                    using (TextureBrush brush = new TextureBrush(source))
                    {
                        brush.WrapMode = WrapMode.Clamp;
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
        public static System.Windows.Media.Imaging.BitmapSource? CreateRoundedCapture(System.Windows.Media.Imaging.BitmapSource source, int radius)
        {
            if (source == null || radius <= 0) return source;

            try 
            {
                int width = (int)source.PixelWidth;
                int height = (int)source.PixelHeight;
                double actualRadius = (radius >= 999) ? Math.Min(width, height) / 2.0 : radius;

                var visual = new System.Windows.Media.DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    var brush = new System.Windows.Media.ImageBrush(source) { Stretch = System.Windows.Media.Stretch.None };
                    dc.DrawRoundedRectangle(brush, null, new System.Windows.Rect(0, 0, width, height), actualRadius, actualRadius);
                }

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, source.DpiX, source.DpiY, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(visual);
                rtb.Freeze();
                return rtb;
            }
            catch { return source; }
        }

        /// <summary>
        /// WPF BitmapSource에 둥근 모서리, 테두리 라인, 그림자 효과를 적용합니다.
        /// </summary>
        public static System.Windows.Media.Imaging.BitmapSource? CreateEdgeLineCapture(
            System.Windows.Media.Imaging.BitmapSource source, 
            double radius, 
            double borderThickness, 
            System.Windows.Media.Color borderColor,
            bool hasShadow,
            double shadowBlur,
            double shadowDepth,
            double shadowOpacity)
        {
            if (source == null) return null;

            try
            {
                int sourceWidth = (int)source.PixelWidth;
                int sourceHeight = (int)source.PixelHeight;
                double actualRadius = (radius >= 99) ? Math.Min(sourceWidth, sourceHeight) / 2.0 : radius;

                // 그림자 및 테두리 두께를 고려한 여백(Padding) 계산 (정수화하여 흔들림 방지)
                double padding = Math.Ceiling((hasShadow ? (shadowBlur + shadowDepth) : 0) + (borderThickness / 2.0) + 1);
                int targetWidth = (int)(sourceWidth + padding * 2);
                int targetHeight = (int)(sourceHeight + padding * 2);

                var border = new System.Windows.Controls.Border
                {
                    Width = sourceWidth,
                    Height = sourceHeight,
                    CornerRadius = new System.Windows.CornerRadius(actualRadius),
                    Background = new System.Windows.Media.ImageBrush(source) { Stretch = System.Windows.Media.Stretch.None },
                    BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor),
                    BorderThickness = new System.Windows.Thickness(borderThickness),
                    SnapsToDevicePixels = true
                };

                if (hasShadow)
                {
                    border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = System.Windows.Media.Colors.Black,
                        BlurRadius = shadowBlur,
                        ShadowDepth = shadowDepth,
                        Opacity = shadowOpacity,
                        Direction = 315
                    };
                }

                // [개선] 그림자가 잘리지 않도록 최상위 Grid 컨테이너 사용
                var container = new System.Windows.Controls.Grid
                {
                    Width = targetWidth,
                    Height = targetHeight,
                    Background = System.Windows.Media.Brushes.Transparent,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };
                container.Children.Add(border);

                // 레이아웃 강제 수행
                container.Measure(new System.Windows.Size(targetWidth, targetHeight));
                container.Arrange(new System.Windows.Rect(0, 0, targetWidth, targetHeight));
                border.Arrange(new System.Windows.Rect(padding, padding, sourceWidth, sourceHeight));
                container.UpdateLayout();

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(targetWidth, targetHeight, source.DpiX, source.DpiY, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(container);
                rtb.Freeze();
                return rtb;
            }
            catch { return source; }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// 엣지 캡처 프리셋 레벨(1~5)에 따른 최적화된 파라미터를 반환합니다.
        /// (Radius, ShadowBlur, ShadowDepth, ShadowOpacity, BorderThickness)
        /// 사용자가 편집창 설정과 분리된 '단순화된 5단계' 캡처를 원함.
        /// </summary>
        public static (int Radius, double Blur, double Depth, double Opacity, double Thickness) GetPresetSettings(int level)
        {
            return level switch
            {
                1 => (12, 0, 0, 0.0, 0.0),   // Soft (No Shadow, No Line)
                2 => (25, 0, 0, 0.0, 0.0),   // Smooth (No Shadow, No Line)
                3 => (50, 0, 0, 0.0, 0.0),   // Classic (No Shadow, No Line)
                4 => (100, 0, 0, 0.0, 0.0),  // Capsule (No Shadow, No Line)
                5 => (999, 0, 0, 0.0, 0.0),  // Circle (No Shadow, No Line)
                _ => (50, 0, 0, 0.0, 0.0)    // Default
            };
        }
    }
}
