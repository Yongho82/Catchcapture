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
        private bool Preview_SelectMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 리사이즈 핸들이나 이미 선택된 요소의 부속 버튼 클릭 시 무시
            if (e.OriginalSource is FrameworkElement fe && (fe.Name.StartsWith("ResizeHandle") || fe.Name == "RotationHandle" || fe.Parent is Button || fe is Button))
                return false;

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
                // 모든 인터랙티브 요소는 이제 공통 드래그 로직 또는 자체 핸들을 사용합니다.
                SaveForUndo(); // 드래그 전 상태 저장
                isDraggingObject = true;
                objectDragLastPoint = clickPoint;
                ImageCanvas.CaptureMouse();
                
                ImageCanvas.MouseMove -= Preview_SelectMouseMove;
                ImageCanvas.MouseMove += Preview_SelectMouseMove;
                ImageCanvas.MouseLeftButtonUp -= Preview_SelectMouseUp;
                ImageCanvas.MouseLeftButtonUp += Preview_SelectMouseUp;
                ImageCanvas.LostMouseCapture -= (s, ev) => { isDraggingObject = false; };
                return true;
            }
            else
            {
                // 선택 모드일 때만 빈 곳 클릭 시 선택 해제
                if (currentEditMode == EditMode.Select)
                {
                    DeselectObject();
                }
                return false;
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

            UpdateObjectSelectionUI();
            CreateObjectResizeHandles();
            SyncSelectedObjectToGlobalSettings();
            _toolOptionsControl?.LoadEditorValues(); // [추가] UI 동기화
            _toolOptionsControl?.LoadEditorValues(); // [추가] UI 동기화
            // ShowSelectionOptions(); // [Changed] Don't auto-show options popup
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
            else if (selectedObject is FrameworkElement fe2 && fe2.Tag is CatchCapture.Utilities.ShapeMetadata metadata && _editorManager != null)
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
            else if (selectedObject is Polyline polyline && _editorManager != null)
            {
                polyline.Stroke = new SolidColorBrush(_editorManager.SelectedColor);
                // 형광펜인 경우 투명도 유지를 위해 Opacity를 확인하거나 다시 설정할 수 있음
            }
            else if (selectedObject is TextBox textBox && _editorManager != null)
            {
                // [수정] 공용 에디터 매니저의 기능 사용 (일관성 유지)
                _editorManager.ApplyCurrentTextSettingsToSelectedObject();
                
                // 개별 연동 (필요한 경우)
                textBox.Foreground = new SolidColorBrush(_editorManager.SelectedColor);
            }
            else if (selectedObject is Canvas canvas && canvas.Children.Count > 0 && _editorManager != null)
            {
                // [수정] 공용 에디터 매니저의 기능 사용 (텍스트/넘버링 공통)
                _editorManager.ApplyCurrentTextSettingsToSelectedObject();
                
                // 넘버링 배지 색상 별도 처리
                if (canvas.Children[0] is Border badge)
                {
                    badge.Background = new SolidColorBrush(_editorManager.SelectedColor);
                }
            }
        }

        private void SyncSelectedObjectToGlobalSettings()
        {
            if (selectedObject == null || _editorManager == null) return;

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
            else if (selectedObject is Canvas canvas && _editorManager != null)
            {
                var tb = _editorManager.FindTextBox(canvas);
                if (tb != null)
                {
                    if (tb.Foreground is SolidColorBrush scb) _editorManager.SelectedColor = scb.Color;
                    _editorManager.TextFontSize = tb.FontSize;
                    _editorManager.TextFontFamily = tb.FontFamily.Source;
                    _editorManager.TextFontWeight = tb.FontWeight;
                    _editorManager.TextFontStyle = tb.FontStyle;
                }
                
                if (canvas.Children.Count > 0 && canvas.Children[0] is Border badge)
                {
                    if (badge.Background is SolidColorBrush scb) _editorManager.SelectedColor = scb.Color;
                }
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
            else if (selectedObject is Canvas canvas && _editorManager?.FindTextBox(canvas) != null)
            {
                ShowTextOptions();
            }
        }

        private void DeselectObject()
        {
            if (selectedObject != null)
            {
                // [추가] 텍스트박스인 경우 편집 모드 종료
                var tb = InteractiveEditor.FindTextBox(selectedObject);
                if (tb != null)
                {
                    tb.IsReadOnly = true;
                    tb.Focusable = true;
                }
            }

            selectedObject = null;
            if (_editorManager != null) _editorManager.SelectedObject = null;

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
            
            // Get current rotation
            double rotation = 0;
            if (selectedObject.RenderTransform is RotateTransform rt)
            {
                rotation = rt.Angle;
            }

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

            // Apply rotation to selection border
            objectSelectionBorder.Width = bounds.Width + 6;
            objectSelectionBorder.Height = bounds.Height + 6;
            Canvas.SetLeft(objectSelectionBorder, bounds.Left - 3);
            Canvas.SetTop(objectSelectionBorder, bounds.Top - 3);
            
            if (rotation != 0)
            {
                objectSelectionBorder.RenderTransformOrigin = new Point(0.5, 0.5);
                objectSelectionBorder.RenderTransform = new RotateTransform(rotation);
            }
            else
            {
                objectSelectionBorder.RenderTransform = null;
            }

            Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

            // Update Buttons Position with rotation
            Point confirmPos = new Point(bounds.Right - 18, bounds.Top - 15);
            Point rotatedConfirmPos = rotation != 0 ? RotatePoint(confirmPos, center, rotation) : confirmPos;

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
            Canvas.SetLeft(objectConfirmButton, rotatedConfirmPos.X);
            Canvas.SetTop(objectConfirmButton, rotatedConfirmPos.Y);

            Point deletePos = new Point(bounds.Right + 5, bounds.Top - 15);
            Point rotatedDeletePos = rotation != 0 ? RotatePoint(deletePos, center, rotation) : deletePos;

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
            Canvas.SetLeft(objectDeleteButton, rotatedDeletePos.X);
            Canvas.SetTop(objectDeleteButton, rotatedDeletePos.Y);
            
            UpdateObjectResizeHandles(bounds, rotation);
        }

        private Point RotatePoint(Point point, Point center, double angle)
        {
            double rad = angle * Math.PI / 180;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return new Point(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
        }



        private Ellipse? objectRotationHandle;
        private bool isRotatingObject = false;
        private double initialRotationAngle = 0;
        
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

            // [New] Rotation Handle
            objectRotationHandle = new Ellipse
            {
                Name = "RotationHandle",
                Width = 10, Height = 10, 
                Fill = Brushes.White, 
                Stroke = Brushes.Red, 
                StrokeThickness = 2, 
                Cursor = Cursors.Hand,
                ToolTip = "회전"
            };
            objectRotationHandle.MouseLeftButtonDown += ObjectRotationHandle_MouseDown;
            ImageCanvas.Children.Add(objectRotationHandle);
            Panel.SetZIndex(objectRotationHandle, 2010);

            // Initial generic update call (will be refined in specific update method)
            if (selectedObject != null)
            {
                double angle = 0;
                if (selectedObject.RenderTransform is RotateTransform rt) angle = rt.Angle;
                UpdateObjectResizeHandles(InteractiveEditor.GetElementBounds(selectedObject), angle);
            }
        }

        private void RemoveObjectResizeHandles() { 
            foreach (var h in objectResizeHandles) ImageCanvas.Children.Remove(h); 
            objectResizeHandles.Clear();
            if (objectRotationHandle != null)
            {
                ImageCanvas.Children.Remove(objectRotationHandle);
                objectRotationHandle = null;
            }
        }

        private void UpdateObjectResizeHandles(Rect bounds, double rotation = 0)
        {
            Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

            foreach (var handle in objectResizeHandles)
            {
                string dir = handle.Name.Replace("ResizeHandle_", "");
                // Calculate center position of handle (before rotation)
                double cx = 0, cy = 0;
                
                switch (dir) {
                    case "NW": cx = bounds.Left; cy = bounds.Top; break;
                    case "N": cx = bounds.Left + bounds.Width / 2; cy = bounds.Top; break;
                    case "NE": cx = bounds.Right; cy = bounds.Top; break;
                    case "W": cx = bounds.Left; cy = bounds.Top + bounds.Height / 2; break;
                    case "E": cx = bounds.Right; cy = bounds.Top + bounds.Height / 2; break;
                    case "SW": cx = bounds.Left; cy = bounds.Bottom; break;
                    case "S": cx = bounds.Left + bounds.Width / 2; cy = bounds.Bottom; break;
                    case "SE": cx = bounds.Right; cy = bounds.Bottom; break;
                }

                // Apply rotation
                Point handleCenter = new Point(cx, cy);
                if (rotation != 0)
                {
                    handleCenter = RotatePoint(handleCenter, center, rotation);
                }

                // Set Canvas position (top-left of handle, handle size is 8x8)
                Canvas.SetLeft(handle, handleCenter.X - 4);
                Canvas.SetTop(handle, handleCenter.Y - 4);
            }

            // Update Rotation Handle Position (Above Top-Center)
            if (objectRotationHandle != null)
            {
                Point rotHandleCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top - 25);
                if (rotation != 0)
                {
                    rotHandleCenter = RotatePoint(rotHandleCenter, center, rotation);
                }

                Canvas.SetLeft(objectRotationHandle, rotHandleCenter.X - 5); // Handle size 10x10
                Canvas.SetTop(objectRotationHandle, rotHandleCenter.Y - 5);
            }
        }

        private void ObjectResizeHandle_MouseDown(object sender, MouseButtonEventArgs e, string direction)
        {
            // [수정] 핸들이 클릭되었다면 현재 모드와 상관없이 리사이즈 허용 (핸들이 보인다는 것은 조작 가능하다는 뜻)
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

        // [New] Rotation Logic
        private void ObjectRotationHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
             // [수정] 회전 핸들이 클릭되었다면 현재 모드와 상관없이 회전 허용
             if (selectedObject == null) return;
             
             SaveForUndo();
             isRotatingObject = true;
             
             // Initial Angle check
             double currentAngle = 0;
             if (selectedObject.RenderTransform is RotateTransform rt) currentAngle = rt.Angle;
             // If complex transform, simplified to just taking the first rotate or 0
             
             initialRotationAngle = currentAngle;
             
             ImageCanvas.CaptureMouse();
             ImageCanvas.MouseMove -= ObjectRotation_MouseMove;
             ImageCanvas.MouseMove += ObjectRotation_MouseMove;
             ImageCanvas.MouseLeftButtonUp -= ObjectRotation_MouseUp;
             ImageCanvas.MouseLeftButtonUp += ObjectRotation_MouseUp;
             
             e.Handled = true;
        }

        private void ObjectRotation_MouseMove(object sender, MouseEventArgs e)
        {
            if (isRotatingObject && selectedObject != null)
            {
                Rect bounds = InteractiveEditor.GetElementBounds(selectedObject);
                Point center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                Point currentPos = e.GetPosition(ImageCanvas);
                
                // Calculate angle
                double angle = Math.Atan2(currentPos.Y - center.Y, currentPos.X - center.X) * 180 / Math.PI;
                // Adjust to make Up (Top-Center) be near 0 relative to where handle is? 
                // Handle is at Top (-90 degrees in math). 
                angle += 90; 

                // Shift key for 15 degree snapping
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    angle = Math.Round(angle / 15) * 15;
                }

                selectedObject.RenderTransformOrigin = new Point(0.5, 0.5);
                selectedObject.RenderTransform = new RotateTransform(angle);
                
                // Update Tag metadata if present so persistence works immediately
                if (selectedObject is FrameworkElement fe && fe.Tag is CatchCapture.Models.DrawingLayer layer)
                {
                    layer.Rotation = angle;
                }
                
                // Update selection UI to rotate with the object
                UpdateObjectSelectionUI();
            }
        }
        
        private void ObjectRotation_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isRotatingObject)
            {
                isRotatingObject = false;
                ImageCanvas.ReleaseMouseCapture();
                ImageCanvas.MouseMove -= ObjectRotation_MouseMove;
                ImageCanvas.MouseLeftButtonUp -= ObjectRotation_MouseUp;
                e.Handled = true;
            }
        }
    }
}

