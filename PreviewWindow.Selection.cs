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
            if (_editorManager != null) _editorManager.SelectedObject = element;

            if (element is TextBox textBox)
            {
                ShowTextSelection(textBox);
                SyncSelectedObjectToGlobalSettings();
                _toolOptionsControl?.LoadEditorValues(); // [추가] UI 동기화
                return;
            }

            UpdateObjectSelectionUI();
            CreateObjectResizeHandles();
            SyncSelectedObjectToGlobalSettings();
            _toolOptionsControl?.LoadEditorValues(); // [추가] UI 동기화
            ShowSelectionOptions();
        }

        public void ApplyPropertyChangesToSelectedObject()
        {
            if (selectedObject == null) return;

            if (selectedObject is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                layer.Color = _editorManager.SelectedColor;
                layer.Thickness = _editorManager.ShapeBorderThickness;
                layer.IsFilled = _editorManager.ShapeIsFilled;
                layer.FillOpacity = _editorManager.ShapeFillOpacity;
                ShapeDrawingHelper.ApplyDrawingLayer(selectedObject, layer);
            }
            else if (selectedObject is FrameworkElement fe2 && fe2.Tag is CatchCapture.Utilities.ShapeMetadata metadata)
            {
                metadata.Color = _editorManager.SelectedColor;
                metadata.Thickness = _editorManager.ShapeBorderThickness;
                metadata.IsFilled = _editorManager.ShapeIsFilled;
                metadata.FillOpacity = _editorManager.ShapeFillOpacity;
                
                if (selectedObject is Shape s)
                {
                    s.Stroke = new SolidColorBrush(_editorManager.SelectedColor);
                    s.StrokeThickness = _editorManager.ShapeBorderThickness;
                    if (s is Rectangle || s is Ellipse)
                    {
                        s.Fill = _editorManager.ShapeIsFilled ? new SolidColorBrush(Color.FromArgb((byte)(_editorManager.ShapeFillOpacity * 255), _editorManager.SelectedColor.R, _editorManager.SelectedColor.G, _editorManager.SelectedColor.B)) : Brushes.Transparent;
                    }
                }
                else if (selectedObject is Canvas arrowCanvas)
                {
                    ShapeDrawingHelper.UpdateArrow(arrowCanvas, metadata.StartPoint, metadata.EndPoint, _editorManager.SelectedColor, _editorManager.ShapeBorderThickness);
                }
            }
            else if (selectedObject is Polyline polyline)
            {
                polyline.Stroke = new SolidColorBrush(_editorManager.SelectedColor);
                // 형광펜인 경우 투명도 유지를 위해 Opacity를 확인하거나 다시 설정할 수 있음
            }
            else if (selectedObject is TextBox textBox)
            {
                // [수정] 공용 에디터 매니저의 기능 사용 (일관성 유지)
                _editorManager?.ApplyCurrentTextSettingsToSelectedObject();
                
                // 개별 연동 (필요한 경우)
                textBox.Foreground = new SolidColorBrush(_editorManager.SelectedColor);
            }
            else if (selectedObject is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Border badge)
            {
                // [수정] 공용 에디터 매니저의 기능 사용
                _editorManager?.ApplyCurrentTextSettingsToSelectedObject();
                badge.Background = new SolidColorBrush(_editorManager.SelectedColor);
            }
        }

        private void SyncSelectedObjectToGlobalSettings()
        {
            if (selectedObject == null) return;

            if (selectedObject is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
            {
                _editorManager.SelectedColor = layer.Color;
                _editorManager.ShapeBorderThickness = layer.Thickness;
                _editorManager.ShapeIsFilled = layer.IsFilled;
                _editorManager.ShapeFillOpacity = layer.FillOpacity;
                if (layer.ShapeType.HasValue) _editorManager.CurrentShapeType = layer.ShapeType.Value;
            }
            else if (selectedObject is FrameworkElement fe2 && fe2.Tag is CatchCapture.Utilities.ShapeMetadata metadata)
            {
                _editorManager.SelectedColor = metadata.Color;
                _editorManager.ShapeBorderThickness = metadata.Thickness;
                _editorManager.ShapeIsFilled = metadata.IsFilled;
                _editorManager.ShapeFillOpacity = metadata.FillOpacity;
                _editorManager.CurrentShapeType = metadata.ShapeType;
            }
            else if (selectedObject is Polyline polyline)
            {
                if (polyline.Stroke is SolidColorBrush scb) _editorManager.SelectedColor = scb.Color;
                // 두께도 동기화 가능
                _editorManager.PenThickness = (int)polyline.StrokeThickness;
            }
            else if (selectedObject is TextBox textBox)
            {
                if (textBox.Foreground is SolidColorBrush scb) _editorManager.SelectedColor = scb.Color;
                _editorManager.TextFontSize = textBox.FontSize;
                _editorManager.TextFontFamily = textBox.FontFamily.Source;
                _editorManager.TextFontWeight = textBox.FontWeight;
                _editorManager.TextFontStyle = textBox.FontStyle;
            }
            else if (selectedObject is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Border badge)
            {
                if (badge.Background is SolidColorBrush scb) _editorManager.SelectedColor = scb.Color;
            }

            SyncEditorProperties(); // [추가] 에디터 매니저와 동기화
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

            Rect bounds = InteractiveEditor.GetElementBounds(selectedObject);
            
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
            UpdateObjectResizeHandles(InteractiveEditor.GetElementBounds(selectedObject));
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
                InteractiveEditor.ResizeElement(selectedObject, dx, dy, objectResizeDirection);
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


    }
}
