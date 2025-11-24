using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatchCapture.Utilities;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 그리기 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 마우스 이벤트 핸들러

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ImageCanvas == null) return;

            Point clickPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseDown: 위치({clickPoint.X:F1}, {clickPoint.Y:F1}), 편집모드={currentEditMode}, isDrawingShape={isDrawingShape}");

            // 도형 모드에서의 처리
            if (currentEditMode == EditMode.Shape)
            {
                // 클릭 위치 저장
                startPoint = clickPoint;
                // 드래그는 아직 시작되지 않음
                isDragStarted = false;
                isDrawingShape = true;
                WriteLog($"도형 그리기 대기: 시작점({startPoint.X:F1}, {startPoint.Y:F1}), 도형타입={shapeType}");
                e.Handled = true;
                return;
            }

            // 다른 모드 처리
            startPoint = clickPoint;

            switch (currentEditMode)
            {
                case EditMode.Crop:
                    StartCrop();
                    break;
                case EditMode.Highlight:
                case EditMode.Pen:
                    StartDrawing();
                    break;
                case EditMode.Text:
                    AddText();
                    break;
                case EditMode.Mosaic:
                    StartMosaic();
                    break;
                case EditMode.Eraser:
                    StartEraser();
                    break;
            }
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 왼쪽 버튼이 눌려 있지 않으면 아무것도 하지 않음
            if (e.LeftButton != MouseButtonState.Pressed) return;

            if (ImageCanvas == null) return;

            Point currentPoint = e.GetPosition(ImageCanvas);

            // 도형 모드 처리
            if (currentEditMode == EditMode.Shape && isDrawingShape)
            {
                // 드래그 시작 플래그 설정
                if (!isDragStarted)
                {
                    isDragStarted = true;
                    WriteLog($"드래그 시작: 현재점({currentPoint.X:F1}, {currentPoint.Y:F1})");
                }

                // 임시 도형 생성 또는 업데이트
                if (tempShape == null)
                {
                    // 새 도형 생성
                    tempShape = CreateShape(startPoint, currentPoint);
                    if (tempShape != null)
                    {
                        ImageCanvas.Children.Add(tempShape);
                        WriteLog($"임시 도형 생성: {tempShape.GetType().Name}");
                    }
                }
                else
                {
                    // 기존 도형 업데이트
                    UpdateShapeProperties(tempShape, startPoint, currentPoint);
                }
                e.Handled = true;
                return;
            }

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    UpdateCropSelection(currentPoint);
                    break;
                case EditMode.Pen:
                case EditMode.Highlight:
                    UpdateDrawing(currentPoint);
                    break;
                case EditMode.Mosaic:
                    UpdateMosaic(currentPoint);
                    break;
                case EditMode.Eraser:
                    UpdateEraser(currentPoint);
                    break;
            }
        }

        private void ImageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 편집 모드가 없으면 아무것도 하지 않음
            if (currentEditMode == EditMode.None) return;

            if (ImageCanvas == null) return;

            Point endPoint = e.GetPosition(ImageCanvas);
            WriteLog($"MouseUp: 위치({endPoint.X:F1}, {endPoint.Y:F1}), 편집모드={currentEditMode}, isDrawingShape={isDrawingShape}, isDragStarted={isDragStarted}");

            // 도형 모드 처리
            if (currentEditMode == EditMode.Shape && isDrawingShape)
            {
                if (isDragStarted)
                {
                    WriteLog($"도형 완성: 시작({startPoint.X:F1}, {startPoint.Y:F1}), 끝({endPoint.X:F1}, {endPoint.Y:F1})");
                    ApplyShape(endPoint);
                }
                else
                {
                    WriteLog("드래그 없이 클릭만 함 - 도형 취소");
                    CleanupTemporaryShape();
                }

                isDrawingShape = false;
                isDragStarted = false;
                e.Handled = true;
                return;
            }

            // 다른 모드 처리
            switch (currentEditMode)
            {
                case EditMode.Crop:
                    FinishCrop(endPoint);
                    break;
                case EditMode.Pen:
                case EditMode.Highlight:
                    FinishDrawing();
                    break;
                case EditMode.Mosaic:
                    FinishMosaic();
                    break;
                case EditMode.Eraser:
                    FinishEraser();
                    break;
            }
        }

        #endregion

        #region 펜/형광펜 그리기

        private void StartDrawing()
        {
            drawingPoints.Clear();
            drawingPoints.Add(startPoint);

            // 라이브 스트로크 경로 생성
            liveStrokePath = new Path();
            PathGeometry pathGeometry = new PathGeometry();
            PathFigure pathFigure = new PathFigure { StartPoint = startPoint };
            liveStrokeSegment = new PolyLineSegment();
            pathFigure.Segments.Add(liveStrokeSegment);
            pathGeometry.Figures.Add(pathFigure);
            liveStrokePath.Data = pathGeometry;

            if (currentEditMode == EditMode.Pen)
            {
                liveStrokePath.Stroke = new SolidColorBrush(penColor);
                liveStrokePath.StrokeThickness = penThickness;
            }
            else // Highlight
            {
                liveStrokePath.Stroke = new SolidColorBrush(highlightColor);
                liveStrokePath.StrokeThickness = highlightThickness;
            }

            liveStrokePath.StrokeStartLineCap = PenLineCap.Round;
            liveStrokePath.StrokeEndLineCap = PenLineCap.Round;
            liveStrokePath.StrokeLineJoin = PenLineJoin.Round;

            ImageCanvas.Children.Add(liveStrokePath);
        }

        private void UpdateDrawing(Point currentPoint)
        {
            drawingPoints.Add(currentPoint);
            if (liveStrokeSegment != null)
            {
                liveStrokeSegment.Points.Add(currentPoint);
            }
        }

        private void FinishDrawing()
        {
            if (drawingPoints.Count < 2)
            {
                if (liveStrokePath != null)
                {
                    ImageCanvas.Children.Remove(liveStrokePath);
                }
                return;
            }

            SaveForUndo();

            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)currentImage.PixelWidth, (int)currentImage.PixelHeight,
                96, 96, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawImage(currentImage, new Rect(0, 0, currentImage.PixelWidth, currentImage.PixelHeight));

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(drawingPoints[0], false, false);
                    ctx.PolyLineTo(drawingPoints, true, true);
                }

                Pen pen = new Pen(
                    currentEditMode == EditMode.Pen
                        ? new SolidColorBrush(penColor)
                        : new SolidColorBrush(highlightColor),
                    currentEditMode == EditMode.Pen ? penThickness : highlightThickness
                )
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

                dc.DrawGeometry(null, pen, geometry);
            }

            rtb.Render(dv);
            currentImage = rtb;
            UpdatePreviewImage();

            if (liveStrokePath != null)
            {
                ImageCanvas.Children.Remove(liveStrokePath);
                liveStrokePath = null;
                liveStrokeSegment = null;
            }

            drawingPoints.Clear();
        }

        #endregion

        #region 모자이크

        private void StartMosaic()
        {
            if (selectionRectangle != null)
            {
                ImageCanvas.Children.Remove(selectionRectangle);
            }

            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
            };

            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);

            ImageCanvas.Children.Add(selectionRectangle);
        }

        private void UpdateMosaic(Point currentPoint)
        {
            if (selectionRectangle == null) return;

            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRectangle, left);
            Canvas.SetTop(selectionRectangle, top);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;
        }

        private void FinishMosaic()
        {
            if (selectionRectangle == null) return;

            double left = Canvas.GetLeft(selectionRectangle);
            double top = Canvas.GetTop(selectionRectangle);
            double width = selectionRectangle.Width;
            double height = selectionRectangle.Height;

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                MessageBox.Show("선택된 영역이 너무 작습니다. 다시 선택해주세요.", "알림");
                ImageCanvas.Children.Remove(selectionRectangle);
                selectionRectangle = null;
                return;
            }

            Int32Rect mosaicRect = new Int32Rect((int)left, (int)top, (int)width, (int)height);

            SaveForUndo();
            currentImage = ImageEditUtility.ApplyMosaic(currentImage, mosaicRect, mosaicSize);
            UpdatePreviewImage();

            ImageCanvas.Children.Remove(selectionRectangle);
            selectionRectangle = null;

            // 모자이크 적용 후에도 모드 유지
            currentEditMode = EditMode.Mosaic;
            ImageCanvas.Cursor = Cursors.Cross;
        }

        #endregion

        #region 지우개

        private void StartEraser()
        {
            drawingPoints.Clear();
            drawingPoints.Add(startPoint);
        }

        private void UpdateEraser(Point currentPoint)
        {
            drawingPoints.Add(currentPoint);

            SaveForUndo();
            ApplyEraserToPoints();
        }

        private void FinishEraser()
        {
            if (drawingPoints.Count > 0)
            {
                SaveForUndo();
                ApplyEraserToPoints();
            }
            drawingPoints.Clear();
        }

        private void ApplyEraserToPoints()
        {
            if (drawingPoints.Count == 0) return;

            int width = currentImage.PixelWidth;
            int height = currentImage.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            currentImage.CopyPixels(pixels, stride, 0);

            foreach (var pt in drawingPoints)
            {
                int centerX = (int)pt.X;
                int centerY = (int)pt.Y;
                int radius = (int)(eraserSize / 2);

                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                            if (distance <= radius)
                            {
                                int index = (y * stride) + (x * 4);
                                pixels[index + 3] = 0; // Alpha = 0 (투명)
                            }
                        }
                    }
                }
            }

            BitmapSource newImage = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            currentImage = newImage;
            UpdatePreviewImage();
        }

        #endregion
    }
}
