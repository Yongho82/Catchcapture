using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class PreviewWindow : Window
    {
        private void Preview_SelectMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (currentEditMode != EditMode.Select) return;

            // 리사이즈 핸들이나 이미 선택된 요소의 부속 버튼 클릭 시 무시
            if (e.OriginalSource is FrameworkElement fe && (fe.Name.StartsWith("ResizeHandle") || fe.Parent is Button || fe is Button))
                return;

            Point clickPoint = e.GetPosition(ImageCanvas);

            // 요소 찾기 (역순으로 검색하여 가장 위에 있는 요소 선택)
            UIElement? clickedElement = null;
            for (int i = drawnElements.Count - 1; i >= 0; i--)
            {
                var element = drawnElements[i];
                if (InteractiveEditor.IsPointInElement(clickPoint, element))
                {
                    clickedElement = element;
                    break;
                }
            }

            if (clickedElement != null)
            {
                SelectObject(clickedElement);
                if (clickedElement is not TextBox) // 텍스트박스는 자체 드래그 로직 사용
                {
                    SaveForUndo(); // 드래그 전 상태 저장
                    isDraggingObject = true;
                    objectDragLastPoint = clickPoint;
                    ImageCanvas.CaptureMouse();
                    
                    ImageCanvas.MouseMove -= Preview_SelectMouseMove;
                    ImageCanvas.MouseMove += Preview_SelectMouseMove;
                    ImageCanvas.MouseLeftButtonUp -= Preview_SelectMouseUp;
                    ImageCanvas.MouseLeftButtonUp += Preview_SelectMouseUp;
                    ImageCanvas.LostMouseCapture -= (s, ev) => { isDraggingObject = false; };
                }
            }
            else
            {
                DeselectObject();
            }
        }

        private void Preview_SelectMouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingObject && selectedObject != null)
            {
                Point currentPoint = e.GetPosition(ImageCanvas);
                double dx = currentPoint.X - objectDragLastPoint.X;
                double dy = currentPoint.Y - objectDragLastPoint.Y;

                InteractiveEditor.MoveElement(selectedObject, dx, dy);
                objectDragLastPoint = currentPoint;
                UpdateObjectSelectionUI();
            }
        }

        private void Preview_SelectMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingObject)
            {
                isDraggingObject = false;
                ImageCanvas.ReleaseMouseCapture();
                ImageCanvas.MouseMove -= Preview_SelectMouseMove;
                ImageCanvas.MouseLeftButtonUp -= Preview_SelectMouseUp;
            }
        }

        private void SelectObject(UIElement element)
        {
            if (selectedObject == element) return;
            DeselectObject();

            selectedObject = element;

            if (element is TextBox textBox)
            {
                ShowTextSelection(textBox);
                SyncSelectedObjectToGlobalSettings();
                return;
            }

            UpdateObjectSelectionUI();
            CreateObjectResizeHandles();
            SyncSelectedObjectToGlobalSettings();
            ShowSelectionOptions();
        }

        public void ApplyPropertyChangesToSelectedObject()
        {
            if (selectedObject == null) return;

            if (selectedObject is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                layer.Color = shapeColor;
                layer.Thickness = shapeBorderThickness;
                layer.IsFilled = shapeIsFilled;
                layer.FillOpacity = shapeFillOpacity;
                ShapeDrawingHelper.ApplyDrawingLayer(selectedObject, layer);
            }
            else if (selectedObject is TextBox textBox)
            {
                textBox.Foreground = new SolidColorBrush(textColor);
                textBox.FontSize = textSize;
                textBox.FontFamily = new FontFamily(textFontFamily);
                textBox.FontWeight = textFontWeight;
                textBox.FontStyle = textFontStyle;
                // textBox values are usually applied via TextBox_TextChanged or similarly,
                // but for style changes we apply them directly.
            }
        }

        private void SyncSelectedObjectToGlobalSettings()
        {
            if (selectedObject == null) return;

            if (selectedObject is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                shapeColor = layer.Color;
                shapeBorderThickness = layer.Thickness;
                shapeIsFilled = layer.IsFilled;
                shapeFillOpacity = layer.FillOpacity;
                if (layer.ShapeType.HasValue) shapeType = layer.ShapeType.Value;
            }
            else if (selectedObject is TextBox textBox)
            {
                if (textBox.Foreground is SolidColorBrush scb) textColor = scb.Color;
                textSize = textBox.FontSize;
                textFontFamily = textBox.FontFamily.Source;
                textFontWeight = textBox.FontWeight;
                textFontStyle = textBox.FontStyle;
            }
        }

        private void ShowSelectionOptions()
        {
            if (selectedObject == null) return;

            if (selectedObject is Shape || selectedObject is Canvas)
            {
                ShowShapeOptionsPopup();
            }
            else if (selectedObject is TextBox)
            {
                ShowTextOptions();
            }
        }

        private void DeselectObject()
        {
            if (selectedObject != null)
            {
                if (selectedObject is TextBox) ClearTextSelection();
                selectedObject = null;
            }

            // UI 요소 강제 제거
            if (objectSelectionBorder != null)
            {
                ImageCanvas.Children.Remove(objectSelectionBorder);
                objectSelectionBorder = null;
            }
            if (objectDeleteButton != null)
            {
                ImageCanvas.Children.Remove(objectDeleteButton);
                objectDeleteButton = null;
            }
            if (objectConfirmButton != null)
            {
                ImageCanvas.Children.Remove(objectConfirmButton);
                objectConfirmButton = null;
            }
            RemoveObjectResizeHandles();
            
            isDraggingObject = false;
            isResizingObject = false;
            Mouse.Capture(null);
            
            ImageCanvas.MouseMove -= Preview_SelectMouseMove;
            ImageCanvas.MouseLeftButtonUp -= Preview_SelectMouseUp;
            ImageCanvas.MouseMove -= ObjectResizeHandle_MouseMove;
            ImageCanvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
        }

        private void UpdateObjectSelectionUI()
        {
            if (selectedObject == null) return;

            Rect bounds = GetElementBounds(selectedObject);
            
            if (objectSelectionBorder == null)
            {
                objectSelectionBorder = new Rectangle
                {
                    Stroke = Brushes.DeepSkyBlue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false
                };
                ImageCanvas.Children.Add(objectSelectionBorder);
            }

            objectSelectionBorder.Width = bounds.Width + 6;
            objectSelectionBorder.Height = bounds.Height + 6;
            Canvas.SetLeft(objectSelectionBorder, bounds.Left - 3);
            Canvas.SetTop(objectSelectionBorder, bounds.Top - 3);

            if (objectConfirmButton == null)
            {
                objectConfirmButton = new Button
                {
                    Content = "✓",
                    Width = 20,
                    Height = 20,
                    Background = Brushes.Green,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Confirm") ?? "확정"
                };
                objectConfirmButton.Click += (s, e) => {
                    DeselectObject();
                };
                ImageCanvas.Children.Add(objectConfirmButton);
            }
            Canvas.SetLeft(objectConfirmButton, bounds.Right - 18);
            Canvas.SetTop(objectConfirmButton, bounds.Top - 15);

            if (objectDeleteButton == null)
            {
                objectDeleteButton = new Button
                {
                    Content = "✕",
                    Width = 20,
                    Height = 20,
                    Background = Brushes.Red,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Delete") ?? "삭제"
                };
                objectDeleteButton.Click += (s, e) => {
                    UIElement? toRemove = selectedObject;
                    if (toRemove != null)
                    {
                        if (toRemove is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
                        {
                            SaveForUndo();
                            drawingLayers.Remove(layer);
                        }
                        
                        DeselectObject();
                        ImageCanvas.Children.Remove(toRemove);
                        drawnElements.Remove(toRemove);
                    }
                };
                ImageCanvas.Children.Add(objectDeleteButton);
            }
            Canvas.SetLeft(objectDeleteButton, bounds.Right + 5);
            Canvas.SetTop(objectDeleteButton, bounds.Top - 15);
            
            UpdateObjectResizeHandles(bounds);
        }

        private Rect GetElementBounds(UIElement element)
        {
            if (element is Line line) return new Rect(new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
            if (element is Polyline polyline)
            {
                double minX = polyline.Points.Min(p => p.X);
                double minY = polyline.Points.Min(p => p.Y);
                double maxX = polyline.Points.Max(p => p.X);
                double maxY = polyline.Points.Max(p => p.Y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            if (element is Canvas canvasElement)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (UIElement child in canvasElement.Children)
                {
                    Rect r = GetElementBounds(child);
                    if (r.Left < minX) minX = r.Left;
                    if (r.Top < minY) minY = r.Top;
                    if (r.Right > maxX) maxX = r.Right;
                    if (r.Bottom > maxY) maxY = r.Bottom;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }

            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            double width = (element is FrameworkElement fe) ? fe.ActualWidth : 0;
            if (width == 0 && element is Shape s) width = s.Width;
            double height = (element is FrameworkElement fe2) ? fe2.ActualHeight : 0;
            if (height == 0 && element is Shape s2) height = s2.Height;
            
            return new Rect(left, top, width, height);
        }

        private bool IsPointInElement(Point pt, UIElement element)
        {
            return InteractiveEditor.IsPointInElement(pt, element);
        }

        private void MoveElement(UIElement element, double dx, double dy)
        {
            if (element is Line line) { line.X1 += dx; line.Y1 += dy; line.X2 += dx; line.Y2 += dy; }
            else if (element is Polyline polyline)
            {
                for (int i = 0; i < polyline.Points.Count; i++)
                {
                    var p = polyline.Points[i];
                    polyline.Points[i] = new Point(p.X + dx, p.Y + dy);
                }
            }
            else if (element is Canvas arrowCanvas)
            {
                foreach (var child in arrowCanvas.Children)
                {
                    if (child is Line l) { l.X1 += dx; l.Y1 += dy; l.X2 += dx; l.Y2 += dy; }
                    else if (child is Polygon p) { for (int i = 0; i < p.Points.Count; i++) { var pt = p.Points[i]; p.Points[i] = new Point(pt.X + dx, pt.Y + dy); } }
                }
            }
            else
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + dx);
                Canvas.SetTop(element, Canvas.GetTop(element) + dy);
            }

            if (element is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                if (layer.StartPoint.HasValue) layer.StartPoint = new Point(layer.StartPoint.Value.X + dx, layer.StartPoint.Value.Y + dy);
                if (layer.EndPoint.HasValue) layer.EndPoint = new Point(layer.EndPoint.Value.X + dx, layer.EndPoint.Value.Y + dy);
                if (layer.TextPosition.HasValue) layer.TextPosition = new Point(layer.TextPosition.Value.X + dx, layer.TextPosition.Value.Y + dy);
            }
        }

        private void CreateObjectResizeHandles()
        {
            RemoveObjectResizeHandles();
            if (selectedObject == null || selectedObject is Polyline) return;

            string[] directions = { "NW", "N", "NE", "W", "E", "SW", "S", "SE" };
            Cursor[] cursors = { Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE, Cursors.SizeWE, Cursors.SizeNESW, Cursors.SizeNS, Cursors.SizeNWSE };

            for (int i = 0; i < directions.Length; i++)
            {
                var handle = new Rectangle
                {
                    Name = "ResizeHandle_" + directions[i],
                    Width = 8, Height = 8, Fill = Brushes.White, Stroke = Brushes.DeepSkyBlue, StrokeThickness = 1, Cursor = cursors[i]
                };
                string dir = directions[i];
                handle.MouseLeftButtonDown += (s, e) => ObjectResizeHandle_MouseDown(s, e, dir);
                ImageCanvas.Children.Add(handle);
                Panel.SetZIndex(handle, 2010);
                objectResizeHandles.Add(handle);
            }
            UpdateObjectResizeHandles(GetElementBounds(selectedObject));
        }

        private void RemoveObjectResizeHandles() { foreach (var h in objectResizeHandles) ImageCanvas.Children.Remove(h); objectResizeHandles.Clear(); }

        private void UpdateObjectResizeHandles(Rect bounds)
        {
            foreach (var handle in objectResizeHandles)
            {
                string dir = handle.Name.Replace("ResizeHandle_", "");
                double left = 0, top = 0;
                switch (dir) {
                    case "NW": left = bounds.Left - 4; top = bounds.Top - 4; break;
                    case "N": left = bounds.Left + bounds.Width / 2 - 4; top = bounds.Top - 4; break;
                    case "NE": left = bounds.Right - 4; top = bounds.Top - 4; break;
                    case "W": left = bounds.Left - 4; top = bounds.Top + bounds.Height / 2 - 4; break;
                    case "E": left = bounds.Right - 4; top = bounds.Top + bounds.Height / 2 - 4; break;
                    case "SW": left = bounds.Left - 4; top = bounds.Bottom - 4; break;
                    case "S": left = bounds.Left + bounds.Width / 2 - 4; top = bounds.Bottom - 4; break;
                    case "SE": left = bounds.Right - 4; top = bounds.Bottom - 4; break;
                }
                Canvas.SetLeft(handle, left); Canvas.SetTop(handle, top);
            }
        }

        private void ObjectResizeHandle_MouseDown(object sender, MouseButtonEventArgs e, string direction)
        {
            if (currentEditMode != EditMode.Select) return;
            if (sender is UIElement handle)
            {
                SaveForUndo(); // 리사이즈 전 상태 저장
                isResizingObject = true;
                objectResizeDirection = direction;
                objectDragLastPoint = e.GetPosition(ImageCanvas);
                
                ImageCanvas.CaptureMouse(); // 이미지 캔버스에서 캡처하여 범위를 벗어나도 이벤트를 받도록 함
                ImageCanvas.MouseMove -= ObjectResizeHandle_MouseMove;
                ImageCanvas.MouseMove += ObjectResizeHandle_MouseMove;
                ImageCanvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
                ImageCanvas.MouseLeftButtonUp += ObjectResizeHandle_MouseUp;
                
                e.Handled = true;
            }
        }

        private void ObjectResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizingObject && selectedObject != null)
            {
                Point currentPoint = e.GetPosition(ImageCanvas);
                double dx = currentPoint.X - objectDragLastPoint.X;
                double dy = currentPoint.Y - objectDragLastPoint.Y;
                ResizeElement(selectedObject, dx, dy, objectResizeDirection);
                objectDragLastPoint = currentPoint;
                UpdateObjectSelectionUI();
            }
        }

        private void ObjectResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isResizingObject)
            {
                isResizingObject = false;
                ImageCanvas.ReleaseMouseCapture();
                ImageCanvas.MouseMove -= ObjectResizeHandle_MouseMove;
                ImageCanvas.MouseLeftButtonUp -= ObjectResizeHandle_MouseUp;
                e.Handled = true;
            }
        }

        private void ResizeElement(UIElement element, double dx, double dy, string dir)
        {
            if (element is Shape shape && (shape is Rectangle || shape is Ellipse))
            {
                double left = Canvas.GetLeft(shape), top = Canvas.GetTop(shape), width = shape.Width, height = shape.Height;
                if (dir.Contains("W")) { left += dx; width -= dx; } if (dir.Contains("E")) { width += dx; }
                if (dir.Contains("N")) { top += dy; height -= dy; } if (dir.Contains("S")) { height += dy; }
                if (width < 5) width = 5; if (height < 5) height = 5;
                shape.Width = width; shape.Height = height; Canvas.SetLeft(shape, left); Canvas.SetTop(shape, top);
                if (shape.Tag is CatchCapture.Models.DrawingLayer layer) { layer.StartPoint = new Point(left, top); layer.EndPoint = new Point(left + width, top + height); }
            }
            else if (element is TextBox textBox)
            {
                double left = Canvas.GetLeft(textBox), top = Canvas.GetTop(textBox), width = textBox.ActualWidth, height = textBox.ActualHeight;
                if (dir.Contains("W")) { left += dx; width -= dx; } if (dir.Contains("E")) { width += dx; }
                if (dir.Contains("N")) { top += dy; height -= dy; } if (dir.Contains("S")) { height += dy; }
                if (width < 20) width = 20; if (height < 20) height = 20;
                textBox.Width = width; textBox.Height = height; Canvas.SetLeft(textBox, left); Canvas.SetTop(textBox, top);
                if (textBox.Tag is CatchCapture.Models.DrawingLayer layer) { layer.TextPosition = new Point(left, top); }
            }
            else if (element is Line line)
            {
                if (dir == "NW" || dir == "W" || dir == "N") { line.X1 += dx; line.Y1 += dy; }
                else if (dir == "SE" || dir == "E" || dir == "S") { line.X2 += dx; line.Y2 += dy; }
                if (line.Tag is CatchCapture.Models.DrawingLayer layer) { layer.StartPoint = new Point(line.X1, line.Y1); layer.EndPoint = new Point(line.X2, line.Y2); }
            }
            else if (element is Canvas arrowCanvas && arrowCanvas.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                var start = layer.StartPoint ?? new Point(0, 0);
                var end = layer.EndPoint ?? new Point(0, 0);
                if (dir == "NW" || dir == "W" || dir == "N") { start = new Point(start.X + dx, start.Y + dy); }
                else if (dir == "SE" || dir == "E" || dir == "S") { end = new Point(end.X + dx, end.Y + dy); }
                layer.StartPoint = start;
                layer.EndPoint = end;
                ShapeDrawingHelper.UpdateArrow(arrowCanvas, start, end, layer.Color, layer.Thickness);
            }
        }

        private double DistanceFromPointToLine(Point pt, Point p1, Point p2)
        {
            double length = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            if (length == 0) return Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
            double t = ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / (length * length);
            t = Math.Max(0, Math.Min(1, t));
            return Math.Sqrt(Math.Pow(pt.X - (p1.X + t * (p2.X - p1.X)), 2) + Math.Pow(pt.Y - (p1.Y + t * (p2.Y - p1.Y)), 2));
        }
    }
}
