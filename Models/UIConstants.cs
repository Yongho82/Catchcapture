using System.Collections.Generic;
using System.Windows.Media;

namespace CatchCapture.Models
{
    public static class UIConstants
    {
        // PreviewWindow and SnippingWindow unified color palette
        public static readonly Color[] SharedColorPalette = new Color[]
        {
            // Row 1
            Colors.Black,
            Color.FromRgb(128, 128, 128),
            Colors.White,
            Colors.Red,
            Color.FromRgb(255, 193, 7),
            Color.FromRgb(40, 167, 69),
            // Row 2
            Color.FromRgb(32, 201, 151),
            Color.FromRgb(23, 162, 184),
            Color.FromRgb(0, 123, 255),
            Color.FromRgb(108, 117, 125),
            Color.FromRgb(220, 53, 69),
            Color.FromRgb(255, 133, 27),
            // Row 3
            Color.FromRgb(111, 66, 193),
            Color.FromRgb(232, 62, 140),
            Color.FromRgb(13, 110, 253),
            Color.FromRgb(25, 135, 84),
            Color.FromRgb(102, 16, 242),
            Colors.Transparent
        };
    }
}
