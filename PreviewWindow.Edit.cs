using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 편집 기능 (partial class)
    /// SnippingWindow의 우수한 텍스트 기능 이식
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 자르기 (Crop) - Excel Style

        // Fields for Excel-like cropping
        private Rectangle? _cropBorder;
        private List<Rectangle> _cropHandles = new List<Rectangle>();
        private List<Rectangle> _cropDimRects = new List<Rectangle>(); // Top, Bottom, Left, Right
        
        private Rect _cropArea;
        private bool _isCropResizing = false;
        private bool _isCropMoving = false;
        private string _activeCropHandle = "";
        private Point _cropDragStart;
        private Rect _cropOriginalArea;

        // Initialize Crop Mode
        public void InitializeCropMode()
        {
            CleanupCropUI(); // Safety cleanup

            if (currentImage == null) return;

            // Initial crop area: Full image size (WPF units for consistency with UI)
            _cropArea = new Rect(0, 0, currentImage.Width, currentImage.Height);

            // Create Dim Rects (Order: Top, Bottom, Left, Right)
            for (int i = 0; i < 4; i++)
            {
                var rect = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), IsHitTestVisible = false };
                _cropDimRects.Add(rect);
                ImageCanvas.Children.Add(rect);
            }

            // Create Border (Visual for the crop area)
            _cropBorder = new Rectangle
            {
                Stroke = Brushes.Red, // 흰색에서 빨간색으로 변경
                StrokeThickness = 2,    // 두께를 1에서 2로 강화
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Fill = Brushes.Transparent,
                IsHitTestVisible = false, // [사용자 요청] 실수로 움직이는 것을 방지하기 위해 이동 기능 끔
                Cursor = Cursors.Arrow
            };
            ImageCanvas.Children.Add(_cropBorder);

            // Create Handles
            string[] tags = { "NW", "N", "NE", "E", "SE", "S", "SW", "W" };
            foreach (var tag in tags)
            {
                var handle = new Rectangle
                {
                    Width = 14, Height = 14, // 10x10에서 14x14로 크기 확대
                    Fill = Brushes.White,
                    Stroke = Brushes.Red,    // 외곽선을 검정에서 빨간색으로 변경
                    StrokeThickness = 1.5,
                    Tag = tag
                };
                
                // Set Cursor
                if (tag == "N" || tag == "S") handle.Cursor = Cursors.SizeNS;
                else if (tag == "E" || tag == "W") handle.Cursor = Cursors.SizeWE;
                else if (tag == "NW" || tag == "SE") handle.Cursor = Cursors.SizeNWSE;
                else handle.Cursor = Cursors.SizeNESW;

                _cropHandles.Add(handle);
                ImageCanvas.Children.Add(handle);
            }

            UpdateCropVisuals();
        }



        private void UpdateCropVisuals()
        {
            if (_cropBorder == null) return;
            
            // Border
            Canvas.SetLeft(_cropBorder, _cropArea.X);
            Canvas.SetTop(_cropBorder, _cropArea.Y);
            _cropBorder.Width = _cropArea.Width;
            _cropBorder.Height = _cropArea.Height;

            // Dim Rects
            double w = ImageCanvas.Width;
            double h = ImageCanvas.Height;
            
            // Top
            var top = _cropDimRects[0];
            Canvas.SetLeft(top, 0); Canvas.SetTop(top, 0);
            top.Width = w; top.Height = Math.Max(0, _cropArea.Top);

            // Bottom
            var bottom = _cropDimRects[1];
            Canvas.SetLeft(bottom, 0); Canvas.SetTop(bottom, _cropArea.Bottom);
            bottom.Width = w; bottom.Height = Math.Max(0, h - _cropArea.Bottom);

            // Left
            var left = _cropDimRects[2];
            Canvas.SetLeft(left, 0); Canvas.SetTop(left, _cropArea.Top);
            left.Width = Math.Max(0, _cropArea.Left); left.Height = _cropArea.Height;

            // Right
            var right = _cropDimRects[3];
            Canvas.SetLeft(right, _cropArea.Right); Canvas.SetTop(right, _cropArea.Top);
            right.Width = Math.Max(0, w - _cropArea.Right); right.Height = _cropArea.Height;

            // Handles
            foreach (var handle in _cropHandles)
            {
                double hx = 0, hy = 0;
                string tag = handle.Tag as string ?? "";
                double hw = handle.Width, hh = handle.Height;

                switch(tag)
                {
                    case "NW": hx = _cropArea.Left - hw/2; hy = _cropArea.Top - hh/2; break;
                    case "N":  hx = _cropArea.Left + _cropArea.Width/2 - hw/2; hy = _cropArea.Top - hh/2; break;
                    case "NE": hx = _cropArea.Right - hw/2; hy = _cropArea.Top - hh/2; break;
                    case "E":  hx = _cropArea.Right - hw/2; hy = _cropArea.Top + _cropArea.Height/2 - hh/2; break;
                    case "SE": hx = _cropArea.Right - hw/2; hy = _cropArea.Bottom - hh/2; break;
                    case "S":  hx = _cropArea.Left + _cropArea.Width/2 - hw/2; hy = _cropArea.Bottom - hh/2; break;
                    case "SW": hx = _cropArea.Left - hw/2; hy = _cropArea.Bottom - hh/2; break;
                    case "W":  hx = _cropArea.Left - hw/2; hy = _cropArea.Top + _cropArea.Height/2 - hh/2; break;
                }
                Canvas.SetLeft(handle, hx);
                Canvas.SetTop(handle, hy);
            }
        }

        public void CleanupCropUI()
        {
            if (_cropBorder != null) ImageCanvas.Children.Remove(_cropBorder);
            foreach (var r in _cropDimRects) ImageCanvas.Children.Remove(r);
            foreach (var h in _cropHandles) ImageCanvas.Children.Remove(h);

            _cropDimRects.Clear();
            _cropHandles.Clear();
            _cropBorder = null;
        }

        
        public void Crop_MouseDown(object sender, MouseButtonEventArgs e)
        {
             var source = e.OriginalSource as FrameworkElement;
             if (source == null) return;
             
             // Check Handles (Need to check if it's one of our handles)
             if (source is Rectangle handle && _cropHandles.Contains(handle))
             {
                 _isCropResizing = true;
                 _activeCropHandle = handle.Tag as string ?? "";
                 _cropDragStart = e.GetPosition(ImageCanvas);
                 _cropOriginalArea = _cropArea;
                 handle.CaptureMouse();
                 e.Handled = true;
                 return;
             }

             // Check Border (Move)
             if (source == _cropBorder)
             {
                 _isCropMoving = true;
                 _cropDragStart = e.GetPosition(ImageCanvas);
                 _cropOriginalArea = _cropArea;
                 _cropBorder.CaptureMouse();
                 e.Handled = true;
             }
        }

        public void Crop_MouseMove(object sender, MouseEventArgs e)
        {
             if (!_isCropResizing && !_isCropMoving) return;

             Point current = e.GetPosition(ImageCanvas);

             if (_isCropResizing)
             {
                 double dx = current.X - _cropDragStart.X;
                 double dy = current.Y - _cropDragStart.Y;
                 
                 Rect r = _cropOriginalArea;
                 
                 // Apply delta based on handle
                 if (_activeCropHandle.Contains("W")) { r.X += dx; r.Width -= dx; }
                 if (_activeCropHandle.Contains("E")) { r.Width += dx; }
                 if (_activeCropHandle.Contains("N")) { r.Y += dy; r.Height -= dy; }
                 if (_activeCropHandle.Contains("S")) { r.Height += dy; }

                 // Normalize positive width/height
                 if (r.Width < 10) { 
                     if (_activeCropHandle.Contains("W")) r.X -= (10 - r.Width);
                     r.Width = 10; 
                 }
                 if (r.Height < 10) {
                     if (_activeCropHandle.Contains("N")) r.Y -= (10 - r.Height);
                     r.Height = 10;
                 }
                 
                 _cropArea = r;
                 UpdateCropVisuals();
             }
             else if (_isCropMoving)
             {
                 double dx = current.X - _cropDragStart.X;
                 double dy = current.Y - _cropDragStart.Y;
                 
                 double newX = _cropOriginalArea.X + dx;
                 double newY = _cropOriginalArea.Y + dy;
                 
                 // Bounds check
                 double maxX = ImageCanvas.Width - _cropArea.Width;
                 double maxY = ImageCanvas.Height - _cropArea.Height;
                 
                 newX = Math.Max(0, Math.Min(newX, maxX));
                 newY = Math.Max(0, Math.Min(newY, maxY));

                 _cropArea = new Rect(newX, newY, _cropArea.Width, _cropArea.Height);
                 UpdateCropVisuals();
             }
        }

        public void Crop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCropResizing)
            {
                _isCropResizing = false;
                // Find visible handle to release capture? 
                // We captured the source handle. The sender is ImageCanvas, but e.Source might be the handle?
                // Actually we captured on the source element.
                // We should release capture on the element that captured it.
                // But simpler: Mouse.Capture(null);
                Mouse.Capture(null);
            }
            if (_isCropMoving)
            {
                _isCropMoving = false;
                if (_cropBorder != null) _cropBorder.ReleaseMouseCapture();
            }
        }

        public void ConfirmCrop(object? sender = null, RoutedEventArgs? e = null)
        {
            if (_cropArea.Width > 0 && _cropArea.Height > 0)
            {
                SaveForUndo();

                try {
                    if (currentImage == null) return;

                    // WPF 단위 좌표를 실제 이미지 픽셀 단위로 변환
                    double scaleX = currentImage.PixelWidth / currentImage.Width;
                    double scaleY = currentImage.PixelHeight / currentImage.Height;

                    Int32Rect cropRect = new Int32Rect(
                        (int)Math.Round(_cropArea.X * scaleX), 
                        (int)Math.Round(_cropArea.Y * scaleY), 
                        (int)Math.Round(_cropArea.Width * scaleX), 
                        (int)Math.Round(_cropArea.Height * scaleY)
                    );

                    // 경계값 체크
                    cropRect.X = Math.Max(0, Math.Min(cropRect.X, currentImage.PixelWidth - 1));
                    cropRect.Y = Math.Max(0, Math.Min(cropRect.Y, currentImage.PixelHeight - 1));
                    cropRect.Width = Math.Max(1, Math.Min(cropRect.Width, currentImage.PixelWidth - cropRect.X));
                    cropRect.Height = Math.Max(1, Math.Min(cropRect.Height, currentImage.PixelHeight - cropRect.Y));
                    
                    if (cropRect.Width <= 0 || cropRect.Height <= 0) return;

                    CroppedBitmap croppedBitmap = new CroppedBitmap(currentImage, cropRect);
                    currentImage = croppedBitmap;

                    // 원본 이미지 참조 갱신 및 레이어 초기화 (자르기 후 정합성 유지)
                    originalImage = currentImage;
                    drawingLayers.Clear();

                    UpdatePreviewImage();
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message);
                }
            }

            CleanupCropUI();
            currentEditMode = EditMode.None;
            ImageCanvas.Cursor = Cursors.Arrow;
            // Update buttons state
            SetActiveToolButton(null);
        }

        public void CancelCrop(object? sender = null, RoutedEventArgs? e = null)
        {
            CleanupCropUI();
            currentEditMode = EditMode.None;
            ImageCanvas.Cursor = Cursors.Arrow;
            SetActiveToolButton(null);
        }
        
        #endregion



        #region 마법봉 (배경 제거)

        // 마법봉 설정
        private int magicWandTolerance => _editorManager?.MagicWandTolerance ?? 32; // 색상 허용 오차 (0-255)
        private bool magicWandContiguous => _editorManager?.MagicWandContiguous ?? true; // 연속 영역만 선택
        
        // 마법봉 드래그 관련
        private bool isMagicWandDragging = false;
        private Point magicWandStartPoint;
        private Rectangle? magicWandSelectionRect;
        private Border? magicWandCursor; // 마법봉 커서 (마우스 따라다니는 아이콘)

        /// <summary>
        /// 마법봉 커서 표시/업데이트
        /// </summary>
        public void UpdateMagicWandCursor(Point point)
        {
            if (currentEditMode != EditMode.MagicWand)
            {
                HideMagicWandCursor();
                return;
            }

            if (magicWandCursor == null)
            {
                // 툴팁 텍스트가 포함된 커서 생성
                var tooltipPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Background = new SolidColorBrush(Color.FromArgb(220, 50, 50, 50)),
                    IsHitTestVisible = false
                };
                
                var icon = new TextBlock
                {
                    Text = "✨",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 4, 4, 4)
                };
                
                var tooltip = new TextBlock
                {
                    Text = CatchCapture.Models.LocalizationManager.Get("MagicWandTip"),
                    FontSize = 11,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 6, 4)
                };
                
                tooltipPanel.Children.Add(icon);
                tooltipPanel.Children.Add(tooltip);
                
                magicWandCursor = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(4),
                    IsHitTestVisible = false,
                    Child = tooltipPanel
                };
                Panel.SetZIndex(magicWandCursor, 9999);
                ImageCanvas.Children.Add(magicWandCursor);
            }

            Canvas.SetLeft(magicWandCursor, point.X + 15);
            Canvas.SetTop(magicWandCursor, point.Y + 15);
        }

        /// <summary>
        /// 마법봉 커서 숨기기
        /// </summary>
        public void HideMagicWandCursor()
        {
            if (magicWandCursor != null)
            {
                ImageCanvas.Children.Remove(magicWandCursor);
                magicWandCursor = null;
            }
        }

        /// <summary>
        /// 마법봉 선택 시작 (드래그 준비)
        /// </summary>
        private void StartMagicWandSelection()
        {
            isMagicWandDragging = true;
            magicWandStartPoint = startPoint;
            
            // 마법봉 커서 숨기기 (클릭 방해 방지)
            HideMagicWandCursor();
            
            // 선택 영역 미리보기 사각형 생성
            magicWandSelectionRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 255)),
                IsHitTestVisible = false // 클릭 이벤트 통과
            };
            
            Canvas.SetLeft(magicWandSelectionRect, startPoint.X);
            Canvas.SetTop(magicWandSelectionRect, startPoint.Y);
            magicWandSelectionRect.Width = 0;
            magicWandSelectionRect.Height = 0;
            
            ImageCanvas.Children.Add(magicWandSelectionRect);
        }

        /// <summary>
        /// 마법봉 드래그 중 (선택 영역 업데이트)
        /// </summary>
        public void UpdateMagicWandSelection(Point currentPoint)
        {
            if (!isMagicWandDragging || magicWandSelectionRect == null) return;
            
            double x = Math.Min(magicWandStartPoint.X, currentPoint.X);
            double y = Math.Min(magicWandStartPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - magicWandStartPoint.X);
            double height = Math.Abs(currentPoint.Y - magicWandStartPoint.Y);
            
            Canvas.SetLeft(magicWandSelectionRect, x);
            Canvas.SetTop(magicWandSelectionRect, y);
            magicWandSelectionRect.Width = width;
            magicWandSelectionRect.Height = height;
        }

        /// <summary>
        /// 마법봉 선택 완료 (드래그 끝)
        /// </summary>
        public void FinishMagicWandSelection(Point endPoint)
        {
            if (!isMagicWandDragging) return;
            
            isMagicWandDragging = false;
            
            // 선택 영역 제거
            if (magicWandSelectionRect != null)
            {
                ImageCanvas.Children.Remove(magicWandSelectionRect);
                magicWandSelectionRect = null;
            }
            
            // 드래그 거리 계산
            double dragDistance = Math.Sqrt(
                Math.Pow(endPoint.X - magicWandStartPoint.X, 2) + 
                Math.Pow(endPoint.Y - magicWandStartPoint.Y, 2));
            
            if (dragDistance < 5)
            {
                // 드래그 없음 → 포인트 클릭 마법봉
                ApplyMagicWand();
            }
            else
            {
                // 드래그 있음 → 사각형 영역 내 배경 제거
                int x1 = (int)Math.Min(magicWandStartPoint.X, endPoint.X);
                int y1 = (int)Math.Min(magicWandStartPoint.Y, endPoint.Y);
                int x2 = (int)Math.Max(magicWandStartPoint.X, endPoint.X);
                int y2 = (int)Math.Max(magicWandStartPoint.Y, endPoint.Y);
                
                ApplyMagicWandInRegion(x1, y1, x2, y2);
            }
        }

        /// <summary>
        /// 마법봉 선택 취소 (영역 밖으로 나갈 때)
        /// </summary>
        public void CancelMagicWandSelection()
        {
            isMagicWandDragging = false;
            
            // 선택 영역 제거
            if (magicWandSelectionRect != null)
            {
                ImageCanvas.Children.Remove(magicWandSelectionRect);
                magicWandSelectionRect = null;
            }
            
            // 커서 숨기기
            HideMagicWandCursor();
        }

        /// <summary>
        /// 지정된 영역 내의 배경 제거 (드래그 영역)
        /// </summary>
        private void ApplyMagicWandInRegion(int x1, int y1, int x2, int y2)
        {
            if (currentImage == null) return;
            
            try
            {
                // 클릭 위치로 기준 색상 결정 (영역의 첫 번째 픽셀)
                int refX = Math.Clamp(x1, 0, currentImage.PixelWidth - 1);
                int refY = Math.Clamp(y1, 0, currentImage.PixelHeight - 1);
                
                // Undo 스택에 현재 상태 저장 (이미지 + 레이어)
                SaveForUndo();
                /* Manual stack management replaced by SaveForUndo() to prevent crashes */

                // BitmapSource를 WriteableBitmap으로 변환
                WriteableBitmap writeable = new WriteableBitmap(currentImage);
                
                int width = writeable.PixelWidth;
                int height = writeable.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                writeable.CopyPixels(pixels, stride, 0);

                // 엣지 맵 계산 (텍스트/윤곽선 보호용)
                double[,] edgeMap = CalculateEdgeMap(pixels, width, height, stride);

                // 기준 색상 가져오기
                int refIndex = (refY * stride) + (refX * 4);
                byte targetB = pixels[refIndex];
                byte targetG = pixels[refIndex + 1];
                byte targetR = pixels[refIndex + 2];

                // 영역 내에서만 비슷한 색상 제거 (엣지 보호 적용)
                for (int y = Math.Max(0, y1); y < Math.Min(height, y2); y++)
                {
                    for (int x = Math.Max(0, x1); x < Math.Min(width, x2); x++)
                    {
                        int index = (y * stride) + (x * 4);
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];

                        // 엣지 강도 확인
                        double edgeStrength = edgeMap[x, y];

                        if (IsColorSimilarWithEdgeProtection(r, g, b, targetR, targetG, targetB, magicWandTolerance, edgeStrength))
                        {
                            pixels[index + 3] = 0; // Alpha = 0 (투명)
                        }
                    }
                }

                // 경계 부드럽게 처리 (안티앨리어싱 잔여물 제거)
                SmoothTransparentEdges(pixels, width, height, stride, targetR, targetG, targetB, magicWandTolerance);

                // 수정된 픽셀 적용
                writeable.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

                currentImage = writeable;
                PreviewImage.Source = currentImage;
                UpdatePreviewImage();  
                UpdateUndoRedoButtons();
            }
            catch (Exception ex)
            {
                WriteLog($"ApplyMagicWandInRegion 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 마법봉 허용 오차 설정
        /// </summary>
        public void SetMagicWandTolerance(int tolerance)
        {
            if (_editorManager != null) _editorManager.MagicWandTolerance = Math.Clamp(tolerance, 0, 255);
        }

        /// <summary>
        /// 마법봉 연속 영역 모드 설정
        /// </summary>
        public void SetMagicWandContiguous(bool contiguous)
        {
            if (_editorManager != null) _editorManager.MagicWandContiguous = contiguous;
        }

        /// <summary>
        /// 마법봉으로 클릭한 영역의 배경 제거
        /// </summary>
        public void ApplyMagicWand()
        {
            if (currentImage == null) return;

            try
            {
                // magicWandStartPoint 사용 (드래그 시작점)
                Point clickPoint = magicWandStartPoint;
                
                // 클릭 좌표가 이미지 범위 내인지 확인
                int x = (int)clickPoint.X;
                int y = (int)clickPoint.Y;
                
                WriteLog($"ApplyMagicWand: 클릭 좌표({x}, {y}), 이미지 크기({currentImage.PixelWidth}, {currentImage.PixelHeight})");
                
                if (x < 0 || x >= currentImage.PixelWidth || y < 0 || y >= currentImage.PixelHeight)
                {
                    WriteLog($"ApplyMagicWand: 좌표가 이미지 범위 밖 - 무시");
                    return;
                }

                // Undo 스택에 현재 상태 저장 (이미지 + 레이어)
                SaveForUndo();
                /* Manual stack management replaced by SaveForUndo() to prevent crashes */

                // BitmapSource를 WriteableBitmap으로 변환
                WriteableBitmap writeable = new WriteableBitmap(currentImage);
                
                // 픽셀 데이터 추출
                int width = writeable.PixelWidth;
                int height = writeable.PixelHeight;
                int stride = width * 4; // BGRA
                byte[] pixels = new byte[height * stride];
                writeable.CopyPixels(pixels, stride, 0);

                // 클릭한 픽셀의 색상 가져오기
                int clickIndex = (y * stride) + (x * 4);
                byte targetB = pixels[clickIndex];
                byte targetG = pixels[clickIndex + 1];
                byte targetR = pixels[clickIndex + 2];

                if (magicWandContiguous)
                {
                    // 연속 영역만 제거 (Flood Fill)
                    FloodFillRemove(pixels, width, height, stride, x, y, targetR, targetG, targetB);
                }
                else
                {
                    // 이미지 전체에서 비슷한 색상 제거
                    RemoveSimilarColors(pixels, width, height, stride, targetR, targetG, targetB);
                }

                // 경계 부드럽게 처리 (안티앨리어싱 잔여물 제거)
                SmoothTransparentEdges(pixels, width, height, stride, targetR, targetG, targetB, magicWandTolerance);

                // 수정된 픽셀을 WriteableBitmap에 적용
                writeable.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

                // 결과 이미지 업데이트 및 화면 갱신
                currentImage = writeable;
                PreviewImage.Source = currentImage;
                UpdatePreviewImage();
                UpdateUndoRedoButtons();
            }
            catch (Exception ex)
            {
                WriteLog($"ApplyMagicWand 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Flood Fill 알고리즘으로 연속된 영역 투명하게 처리 (엣지 보호 적용)
        /// </summary>
        private void FloodFillRemove(byte[] pixels, int width, int height, int stride,
            int startX, int startY, byte targetR, byte targetG, byte targetB)
        {
            // 엣지 맵 계산 (텍스트/윤곽선 보호용)
            double[,] edgeMap = CalculateEdgeMap(pixels, width, height, stride);
            
            bool[,] visited = new bool[width, height];
            Queue<(int x, int y)> queue = new Queue<(int, int)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                // 범위 체크
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                // 이미 방문한 픽셀
                if (visited[x, y])
                    continue;

                visited[x, y] = true;

                int index = (y * stride) + (x * 4);
                byte b = pixels[index];
                byte g = pixels[index + 1];
                byte r = pixels[index + 2];
                byte a = pixels[index + 3];

                // 이미 투명한 픽셀은 건너뜀
                if (a == 0)
                    continue;

                // 엣지 강도 확인
                double edgeStrength = edgeMap[x, y];

                // 색상 비교 (엣지 보호 적용)
                if (IsColorSimilarWithEdgeProtection(r, g, b, targetR, targetG, targetB, magicWandTolerance, edgeStrength))
                {
                    // 투명하게 처리
                    pixels[index + 3] = 0; // Alpha = 0

                    // 4방향 이웃 추가
                    queue.Enqueue((x + 1, y));
                    queue.Enqueue((x - 1, y));
                    queue.Enqueue((x, y + 1));
                    queue.Enqueue((x, y - 1));
                }
            }
        }

        private void MagicWandButton_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentEditMode();
            currentEditMode = EditMode.MagicWand;
            ImageCanvas.Cursor = Cursors.Pen; // 펜 커서 사용
            SetActiveToolButton(MagicWandToolButton);
        }

        private void MagicWandOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.MagicWand)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.MagicWand;
                SetActiveToolButton(MagicWandToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == MagicWandToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowMagicWandOptions();
            }
        }

        /// <summary>
        /// 이미지 전체에서 비슷한 색상을 모두 투명하게 처리 (엣지 보호 적용)
        /// </summary>
        private void RemoveSimilarColors(byte[] pixels, int width, int height, int stride,
            byte targetR, byte targetG, byte targetB)
        {
            // 엣지 맵 계산 (텍스트/윤곽선 보호용)
            double[,] edgeMap = CalculateEdgeMap(pixels, width, height, stride);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + (x * 4);
                    byte b = pixels[index];
                    byte g = pixels[index + 1];
                    byte r = pixels[index + 2];

                    // 엣지 강도 확인
                    double edgeStrength = edgeMap[x, y];

                    if (IsColorSimilarWithEdgeProtection(r, g, b, targetR, targetG, targetB, magicWandTolerance, edgeStrength))
                    {
                        pixels[index + 3] = 0; // Alpha = 0
                    }
                }
            }
        }

        /// <summary>
        /// 두 색상이 허용 오차 내에서 비슷한지 확인 (HSL 색상 공간 기반)
        /// </summary>
        private bool IsColorSimilar(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, int tolerance)
        {
            // HSL로 변환하여 비교 (더 자연스러운 색상 인식)
            var (h1, s1, l1) = RgbToHsl(r1, g1, b1);
            var (h2, s2, l2) = RgbToHsl(r2, g2, b2);

            // 허용 오차를 0-1 범위로 정규화
            double tolNorm = tolerance / 255.0;

            // 밝기(Luminance) 차이 - 가장 중요
            double diffL = Math.Abs(l1 - l2);
            
            // 채도가 낮으면(무채색) 색상 차이는 무시
            double diffH = 0;
            if (s1 > 0.1 && s2 > 0.1)
            {
                // 색상환에서의 거리 (0~0.5 범위로 정규화)
                diffH = Math.Abs(h1 - h2);
                if (diffH > 0.5) diffH = 1.0 - diffH;
                diffH *= 2; // 0~1 범위로
            }

            // 채도 차이
            double diffS = Math.Abs(s1 - s2);

            // 가중치 적용 (밝기 > 채도 > 색상)
            double totalDiff = (diffL * 0.5) + (diffS * 0.3) + (diffH * 0.2);

            return totalDiff <= tolNorm;
        }

        /// <summary>
        /// RGB를 HSL로 변환
        /// </summary>
        private (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            double h = 0, s = 0, l = (max + min) / 2.0;

            if (delta > 0)
            {
                s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

                if (max == rd)
                    h = ((gd - bd) / delta + (gd < bd ? 6 : 0)) / 6.0;
                else if (max == gd)
                    h = ((bd - rd) / delta + 2) / 6.0;
                else
                    h = ((rd - gd) / delta + 4) / 6.0;
            }

            return (h, s, l);
        }

        /// <summary>
        /// 엣지 강도 맵 생성 (Sobel 연산자)
        /// </summary>
        private double[,] CalculateEdgeMap(byte[] pixels, int width, int height, int stride)
        {
            double[,] edgeMap = new double[width, height];

            // Sobel 커널
            int[,] sobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] sobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double gx = 0, gy = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int idx = ((y + ky) * stride) + ((x + kx) * 4);
                            // 그레이스케일 값 (가중 평균)
                            double gray = pixels[idx + 2] * 0.299 + pixels[idx + 1] * 0.587 + pixels[idx] * 0.114;
                            
                            gx += gray * sobelX[ky + 1, kx + 1];
                            gy += gray * sobelY[ky + 1, kx + 1];
                        }
                    }

                    // 엣지 강도 (0~1 정규화)
                    edgeMap[x, y] = Math.Min(1.0, Math.Sqrt(gx * gx + gy * gy) / 255.0);
                }
            }

            return edgeMap;
        }

        /// <summary>
        /// 엣지 보호를 적용한 색상 유사성 검사
        /// </summary>
        private bool IsColorSimilarWithEdgeProtection(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, 
            int tolerance, double edgeStrength)
        {
            // 엣지 강도가 높으면 (텍스트, 윤곽선 등) 보호
            // edgeThreshold: 0.15 이상이면 엣지로 판단
            const double edgeThreshold = 0.15;
            
            if (edgeStrength >= edgeThreshold)
            {
                // 엣지 영역은 더 엄격한 허용 오차 적용 (50% 감소)
                tolerance = (int)(tolerance * 0.5);
            }

            return IsColorSimilar(r1, g1, b1, r2, g2, b2, tolerance);
        }

        /// <summary>
        /// 투명 영역 경계를 부드럽게 처리 (안티앨리어싱 잔여물 제거)
        /// </summary>
        private void SmoothTransparentEdges(byte[] pixels, int width, int height, int stride, 
            byte targetR, byte targetG, byte targetB, int tolerance)
        {
            // 1단계: 투명 영역과 인접한 반투명 픽셀 찾기
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = (y * stride) + (x * 4);
                    byte a = pixels[index + 3];
                    
                    // 이미 투명하거나 완전 불투명이 아닌 픽셀은 건너뜀
                    if (a == 0) continue;
                    
                    // 주변 8방향에 투명 픽셀이 있는지 확인
                    int transparentNeighbors = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int ni = ((y + dy) * stride) + ((x + dx) * 4);
                            if (pixels[ni + 3] == 0) transparentNeighbors++;
                        }
                    }
                    
                    // 주변에 투명 픽셀이 있으면 (경계 픽셀)
                    if (transparentNeighbors > 0)
                    {
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];
                        
                        // 배경색과 비슷한 정도에 따라 알파값 조정
                        double similarity = CalculateColorSimilarity(r, g, b, targetR, targetG, targetB);
                        double tolNorm = tolerance / 255.0;
                        
                        if (similarity < tolNorm * 1.5) // 허용 오차의 1.5배 이내
                        {
                            // 유사도에 따라 부분 투명 처리
                            double alphaFactor = similarity / (tolNorm * 1.5);
                            byte newAlpha = (byte)(a * alphaFactor);
                            
                            // 최소 알파값 적용 (너무 투명해지는 것 방지)
                            if (newAlpha < 30 && transparentNeighbors >= 3)
                            {
                                pixels[index + 3] = 0; // 완전 투명
                            }
                            else if (newAlpha < a)
                            {
                                pixels[index + 3] = newAlpha;
                            }
                        }
                    }
                }
            }
            
            // 2단계: 색상 오염 제거 (배경색 성분 제거)
            DecontaminateColors(pixels, width, height, stride, targetR, targetG, targetB);
        }

        /// <summary>
        /// 색상 오염 제거 - 반투명 픽셀에서 배경색 성분 제거
        /// </summary>
        private void DecontaminateColors(byte[] pixels, int width, int height, int stride,
            byte targetR, byte targetG, byte targetB)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + (x * 4);
                    byte a = pixels[index + 3];
                    
                    // 반투명 픽셀만 처리 (0 < alpha < 255)
                    if (a > 0 && a < 255)
                    {
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];
                        
                        // 알파 비율 계산
                        double alphaRatio = a / 255.0;
                        
                        // 배경색 성분 제거 (premultiplied alpha 역산)
                        // 원본색 = (현재색 - 배경색 * (1-alpha)) / alpha
                        if (alphaRatio > 0.1)
                        {
                            double newR = (r - targetR * (1 - alphaRatio)) / alphaRatio;
                            double newG = (g - targetG * (1 - alphaRatio)) / alphaRatio;
                            double newB = (b - targetB * (1 - alphaRatio)) / alphaRatio;
                            
                            // 범위 클램핑
                            pixels[index + 2] = (byte)Math.Clamp(newR, 0, 255);
                            pixels[index + 1] = (byte)Math.Clamp(newG, 0, 255);
                            pixels[index] = (byte)Math.Clamp(newB, 0, 255);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 색상 유사도 계산 (0.0 ~ 1.0, 낮을수록 유사)
        /// </summary>
        private double CalculateColorSimilarity(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            var (h1, s1, l1) = RgbToHsl(r1, g1, b1);
            var (h2, s2, l2) = RgbToHsl(r2, g2, b2);

            double diffL = Math.Abs(l1 - l2);
            double diffH = 0;
            if (s1 > 0.1 && s2 > 0.1)
            {
                diffH = Math.Abs(h1 - h2);
                if (diffH > 0.5) diffH = 1.0 - diffH;
                diffH *= 2;
            }
            double diffS = Math.Abs(s1 - s2);

            return (diffL * 0.5) + (diffS * 0.3) + (diffH * 0.2);
        }

        #endregion
    }
}
