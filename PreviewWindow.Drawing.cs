using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatchCapture.Utilities;
using CatchCapture.Models;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 그리기 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 마우스 이벤트 핸들러

        private void ImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || ImageCanvas == null) return;

            Point clickPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseDown: 위치({clickPoint.X:F1}, {clickPoint.Y:F1}), 편집모드={currentEditMode}");

            // 다른 모드 처리
            startPoint = clickPoint;

            SyncEditorProperties();

            switch (currentEditMode)
            {
                case EditMode.Select:
                    Preview_SelectMouseDown(sender, e);
                    break;
                case EditMode.Crop:
                    Crop_MouseDown(sender, e);
                    break;
                case EditMode.Eraser:
                    _editorManager.StartDrawing(clickPoint, e.OriginalSource, e.ChangedButton == MouseButton.Right);
                    break;
                case EditMode.Highlight:
                case EditMode.Pen:
                case EditMode.Shape:
                case EditMode.Mosaic:
                case EditMode.Numbering:
                case EditMode.Text:
                    // [수정] 그리기 모드에서도 기존 요소를 클릭했다면 선택/드래그를 먼저 시도
                    if (Preview_SelectMouseDown(sender, e)) return;
                    
                    // 기존 요소 클릭이 아니라면 새로운 그리기 시작
                    _editorManager.StartDrawing(clickPoint, e.OriginalSource, e.ChangedButton == MouseButton.Right);
                    break;
                case EditMode.MagicWand:
                    StartMagicWandSelection();
                    break;
            }
            e.Handled = true;
        }

        // 지우개 커서
        private Ellipse? eraserCursor;

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (ImageCanvas == null) return;

            Point currentPoint = e.GetPosition(ImageCanvas);

            // [추가] 완료 힌트 라벨 위치 업데이트

            // [추가] 완료 힌트 라벨 위치 업데이트
            if (confirmHintLabel != null)
            {
                if (currentEditMode == EditMode.Crop)
                {
                    confirmHintLabel.Visibility = Visibility.Visible;
                    
                    double px = currentPoint.X + 15;
                    double py = currentPoint.Y + 15;

                    // 화면 경계 처리 (ImageCanvas 기준)
                    if (px + 120 > ImageCanvas.ActualWidth) // Estimate width 120
                        px = currentPoint.X - 130;
                    if (py + 30 > ImageCanvas.ActualHeight) // Estimate height 30
                        py = currentPoint.Y - 40;

                    Canvas.SetLeft(confirmHintLabel, px);
                    Canvas.SetTop(confirmHintLabel, py);
                }
                else
                {
                    confirmHintLabel.Visibility = Visibility.Collapsed;
                }
            }

            // 지우개 모드일 때 커서 표시 (클릭 여부 상관없이)
            if (currentEditMode == EditMode.Eraser)
            {
                UpdateEraserCursor(currentPoint);
            }
            else
            {
                HideEraserCursor();
            }

            // 마법봉 모드일 때 커서 표시 (클릭 여부 상관없이)
            if (currentEditMode == EditMode.MagicWand)
            {
                UpdateMagicWandCursor(currentPoint);
            }
            else
            {
                HideMagicWandCursor();
            }

            // 왼쪽 또는 오른쪽 버튼이 눌려 있지 않으면 드래그 로직은 실행하지 않음
            if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed) return;

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    Crop_MouseMove(sender, e);
                    break;
                case EditMode.Pen:
                case EditMode.Highlight:
                case EditMode.Shape:
                case EditMode.Mosaic:
                case EditMode.Numbering:
                    _editorManager.UpdateDrawing(currentPoint);
                    break;
                case EditMode.Eraser:
                    _editorManager.UpdateDrawing(currentPoint);
                    break;
                case EditMode.MagicWand:
                    UpdateMagicWandSelection(currentPoint);
                    break;
            }
        }

        private void UpdateEraserCursor(Point point)
        {
            if (_editorManager == null) return;

            if (eraserCursor == null)
            {
                eraserCursor = new Ellipse
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), // 반투명 흰색
                    IsHitTestVisible = false // 마우스 이벤트 통과
                };
                ImageCanvas.Children.Add(eraserCursor);
            }

            // 크기 업데이트 (지우개 크기 변경 시 반영)
            if (eraserCursor.Width != _editorManager.EraserSize)
            {
                eraserCursor.Width = _editorManager.EraserSize;
                eraserCursor.Height = _editorManager.EraserSize;
            }

            Canvas.SetLeft(eraserCursor, point.X - _editorManager.EraserSize / 2);
            Canvas.SetTop(eraserCursor, point.Y - _editorManager.EraserSize / 2);
        }

        private void HideEraserCursor()
        {
            if (eraserCursor != null)
            {
                ImageCanvas.Children.Remove(eraserCursor);
                eraserCursor = null;
            }
        }

        private void ImageCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || currentEditMode == EditMode.None) return;

            if (ImageCanvas == null) return;

            Point endPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseUp: 위치({endPoint.X:F1}, {endPoint.Y:F1}), 편집모드={currentEditMode}");

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    Crop_MouseUp(sender, e);
                    break;
                case EditMode.Eraser:
                case EditMode.Pen:
                case EditMode.Highlight:
                case EditMode.Shape:
                case EditMode.Mosaic:
                case EditMode.Numbering:
                    _editorManager.FinishDrawing();
                    break;
                case EditMode.MagicWand:
                    FinishMagicWandSelection(endPoint);
                    break;
            }
        }

        private void ImageCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // 마법봉 모드에서 영역 밖으로 나가면 선택 취소
            if (currentEditMode == EditMode.MagicWand)
            {
                CancelMagicWandSelection();
            }
            
            // 마법봉 커서 숨기기
            HideMagicWandCursor();

            // [추가] 힌트 라벨 숨기기
            if (drawHintLabel != null) drawHintLabel.Visibility = Visibility.Collapsed;
            if (confirmHintLabel != null) confirmHintLabel.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region SyncEditorProperties
        private void SyncEditorProperties()
        {
            if (_editorManager == null) return;

            _editorManager.CurrentTool = currentEditMode switch
            {
                EditMode.Pen => "펜",
                EditMode.Highlight => "형광펜",
                EditMode.Shape => "도형",
                EditMode.Mosaic => "모자이크",
                EditMode.Numbering => "넘버링",
                EditMode.Text => "텍스트",
                EditMode.Eraser => "지우개",
                _ => ""
            };
        }
        #endregion
    }
}
