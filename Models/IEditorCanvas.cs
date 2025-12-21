using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CatchCapture.Models
{
    public interface IEditorCanvas
    {
        Canvas MainCanvas { get; }
        List<UIElement> DrawnElements { get; }
        Color SelectedColor { get; set; }
        double PenThickness { get; set; }
        double HighlightThickness { get; set; }
        double ShapeThickness { get; set; }
        double HighlightOpacity { get; set; }
        string CurrentTool { get; set; }
        
        void SaveForUndo();
        void DeselectObject();
        void SelectObject(UIElement element);
        void UpdateObjectSelectionUI();
    }
}
