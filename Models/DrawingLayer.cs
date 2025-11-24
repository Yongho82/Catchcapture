using System.Windows;
using System.Windows.Media;
using CatchCapture.Utilities;

namespace CatchCapture.Models
{
    /// <summary>
    /// 그리기 레이어 타입
    /// </summary>
    public enum DrawingLayerType
    {
        Pen,
        Highlight,
        Shape,
        Text,
        Mosaic
    }

    /// <summary>
    /// 개별 그리기 레이어 (펜, 형광펜, 도형 등)
    /// </summary>
    public class DrawingLayer
    {
        public DrawingLayerType Type { get; set; }
        public Point[]? Points { get; set; }
        public Color Color { get; set; }
        public double Thickness { get; set; }
        
        // 도형용
        public Point? StartPoint { get; set; }
        public Point? EndPoint { get; set; }
        public ShapeType? ShapeType { get; set; }
        public bool IsFilled { get; set; }
        
        // 텍스트용
        public string? Text { get; set; }
        public Point? TextPosition { get; set; }
        public double FontSize { get; set; }
        public FontWeight FontWeight { get; set; }
        public FontStyle FontStyle { get; set; }
        public string FontFamily { get; set; } = "Arial";
        public bool HasShadow { get; set; }
        public bool HasUnderline { get; set; }
        
        // 모자이크용
        public Int32Rect? MosaicArea { get; set; }
        public int MosaicPixelSize { get; set; }
        
        // 레이어 식별용
        public int LayerId { get; set; }
        public bool IsErased { get; set; } = false;
    }
}
