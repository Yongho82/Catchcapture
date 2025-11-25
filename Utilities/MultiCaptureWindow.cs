using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// 멀티캡처 윈도우 - 여러 영역을 캡처하고 하나의 이미지로 합성
    /// </summary>
    public class MultiCaptureWindow : Window, IDisposable
    {
        private Canvas canvas;
        private BitmapSource screenshot;
        private List<CapturedRegion> capturedRegions = new List<CapturedRegion>();
        
        private Point startPoint;
        private Rectangle? currentRectangle;
        private bool isSelecting = false;
        
        private TextBlock guideText;
        private Border guideBorder;
        
        // 멀티모니터 좌표 오프셋
        private double offsetX;
        private double offsetY;
        
        public BitmapSource? FinalCompositeImage { get; private set; }
        public List<BitmapSource>? IndividualImages { get; private set; }
        
        private class CapturedRegion
        {
            public Int32Rect Area { get; set; }
            public BitmapSource Image { get; set; }
            public Rectangle VisualRectangle { get; set; }
            
            public CapturedRegion(Int32Rect area, BitmapSource image, Rectangle rect)
            {
                Area = area;
                Image = image;
                VisualRectangle = rect;
            }
        }
        
        public MultiCaptureWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            // 멀티모니터 지원: SnippingWindow와 동일한 방식
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vWidth = SystemParameters.VirtualScreenWidth;
            double vHeight = SystemParameters.VirtualScreenHeight;

            // 디버깅: 모니터 정보를 바탕화면 파일로 저장
            try
            {
                var allScreens = Forms.Screen.AllScreens;
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var debugFilePath = System.IO.Path.Combine(desktopPath, "MultiCapture_Debug.txt");
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== 멀티캡처 모니터 정보 ===");
                sb.AppendLine($"시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine($"SystemParameters:");
                sb.AppendLine($"  Left: {vLeft}");
                sb.AppendLine($"  Top: {vTop}");
                sb.AppendLine($"  Width: {vWidth}");
                sb.AppendLine($"  Height: {vHeight}");
                sb.AppendLine();
                sb.AppendLine($"총 모니터 수: {allScreens.Length}");
                for (int i = 0; i < allScreens.Length; i++)
                {
                    var screen = allScreens[i];
                    sb.AppendLine($"모니터 {i + 1}:");
                    sb.AppendLine($"  Left: {screen.Bounds.Left}");
                    sb.AppendLine($"  Top: {screen.Bounds.Top}");
                    sb.AppendLine($"  Width: {screen.Bounds.Width}");
                    sb.AppendLine($"  Height: {screen.Bounds.Height}");
                    sb.AppendLine($"  Primary: {screen.Primary}");
                }
                sb.AppendLine();
                int formsMinX = allScreens.Min(s => s.Bounds.Left);
                int formsMinY = allScreens.Min(s => s.Bounds.Top);
                int formsMaxX = allScreens.Max(s => s.Bounds.Right);
                int formsMaxY = allScreens.Max(s => s.Bounds.Bottom);
                sb.AppendLine($"Forms.Screen 계산값:");
                sb.AppendLine($"  MinX: {formsMinX}");
                sb.AppendLine($"  MinY: {formsMinY}");
                sb.AppendLine($"  MaxX: {formsMaxX}");
                sb.AppendLine($"  MaxY: {formsMaxY}");
                sb.AppendLine($"  Total Width: {formsMaxX - formsMinX}");
                sb.AppendLine($"  Total Height: {formsMaxY - formsMinY}");
                sb.AppendLine();
                sb.AppendLine($"사용 중인 오프셋:");
                sb.AppendLine($"  offsetX: {vLeft}");
                sb.AppendLine($"  offsetY: {vTop}");
                sb.AppendLine($"==================");
                
                System.IO.File.WriteAllText(debugFilePath, sb.ToString());
            }
            catch { }

            Left = vLeft;
            Top = vTop;
            Width = vWidth;
            Height = vHeight;
            WindowState = WindowState.Normal; // Important: Normal to respect manual Left/Top/Size across monitors

            
            // 전체 화면 스크린샷 캡처
            this.Hide();
            System.Threading.Thread.Sleep(10);
            screenshot = ScreenCaptureUtility.CaptureScreen();

            // 멀티모니터 오프셋 계산
            offsetX = vLeft;
            offsetY = vTop;
            
            // 캔버스 설정
            canvas = new Canvas
            {
                Width = vWidth,
                Height = vHeight
            };

            // 배경 이미지 설정
            var backgroundImage = new Image
            {
                Source = screenshot,
                Stretch = Stretch.None,
                Opacity = 0.3
            };
            // 캔버스 내 좌표는 (0, 0)부터 시작 (창 자체가 이미 VirtualScreen 위치에 있음)
            Canvas.SetLeft(backgroundImage, 0);
            Canvas.SetTop(backgroundImage, 0);
            canvas.Children.Add(backgroundImage);
            
            // 가이드 텍스트
            guideText = new TextBlock
            {
                Text = "영역을 드래그하여 캡처하세요. Enter: 완료 | ESC: 취소",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 5, 10, 5)
            };
            
            guideBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                CornerRadius = new CornerRadius(5),
                Child = guideText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var grid = new Grid();
            grid.Children.Add(canvas);
            grid.Children.Add(guideBorder);
            
            Content = grid;
            
            // 이벤트 핸들러
            MouseLeftButtonDown += MultiCaptureWindow_MouseLeftButtonDown;
            MouseLeftButtonUp += MultiCaptureWindow_MouseLeftButtonUp;
            MouseMove += MultiCaptureWindow_MouseMove;
            KeyDown += MultiCaptureWindow_KeyDown;
            
            Loaded += (s, e) =>
            {
                this.Show();
                this.Activate();
                this.Focus();
            };
        }
        
        private void MultiCaptureWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);
            isSelecting = true;
            
            // 새로운 선택 사각형 생성
            currentRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0))
            };
            
            Canvas.SetLeft(currentRectangle, startPoint.X);
            Canvas.SetTop(currentRectangle, startPoint.Y);
            canvas.Children.Add(currentRectangle);
            
            e.Handled = true;
        }
        
        private void MultiCaptureWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting || currentRectangle == null) return;
            
            var currentPoint = e.GetPosition(canvas);
            
            var x = Math.Min(startPoint.X, currentPoint.X);
            var y = Math.Min(startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - startPoint.X);
            var height = Math.Abs(currentPoint.Y - startPoint.Y);
            
            Canvas.SetLeft(currentRectangle, x);
            Canvas.SetTop(currentRectangle, y);
            currentRectangle.Width = width;
            currentRectangle.Height = height;
        }
        
        private void MultiCaptureWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting || currentRectangle == null) return;
            
            isSelecting = false;
            
            var endPoint = e.GetPosition(canvas);
            
            var x = (int)Math.Min(startPoint.X, endPoint.X);
            var y = (int)Math.Min(startPoint.Y, endPoint.Y);
            var width = (int)Math.Abs(endPoint.X - startPoint.X);
            var height = (int)Math.Abs(endPoint.Y - startPoint.Y);
            
            // 최소 크기 체크
            if (width < 5 || height < 5)
            {
                canvas.Children.Remove(currentRectangle);
                currentRectangle = null;
                return;
            }
            
            // 캔버스 좌표는 창 기준 상대 좌표이므로, 스크린샷 좌표로 변환
            // 캔버스 좌표 + 절대 오프셋 = 스크린샷 내부 좌표
            int screenshotX = (int)x;
            int screenshotY = (int)y;
            
            // 화면 경계 체크
            if (screenshotX < 0) { width += screenshotX; screenshotX = 0; }
            if (screenshotY < 0) { height += screenshotY; screenshotY = 0; }
            if (screenshotX + width > screenshot.PixelWidth) width = screenshot.PixelWidth - screenshotX;
            if (screenshotY + height > screenshot.PixelHeight) height = screenshot.PixelHeight - screenshotY;
            
            if (width <= 0 || height <= 0)
            {
                canvas.Children.Remove(currentRectangle);
                currentRectangle = null;
                return;
            }
            
            try
            {
                // 선택 영역 캡처 (스크린샷 좌표 사용)
                var area = new Int32Rect(screenshotX, screenshotY, width, height);
                var croppedBitmap = new CroppedBitmap(screenshot, area);
                
                // 복사본 생성 (freeze)
                var capturedImage = new WriteableBitmap(croppedBitmap);
                capturedImage.Freeze();
                
                // 시각적 표시 업데이트 (확정된 영역)
                currentRectangle.Stroke = Brushes.Lime;
                currentRectangle.StrokeThickness = 3;
                currentRectangle.Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0));
                
                // 영역 번호 표시
                var numberText = new TextBlock
                {
                    Text = (capturedRegions.Count + 1).ToString(),
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(200, 0, 200, 0)),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(8, 4, 8, 4)
                };
                
                Canvas.SetLeft(numberText, x + 5);
                Canvas.SetTop(numberText, y + 5);
                canvas.Children.Add(numberText);
                
                // 캡처된 영역 저장 (스크린샷 좌표로 저장)
                capturedRegions.Add(new CapturedRegion(area, capturedImage, currentRectangle));
                
                // 가이드 텍스트 업데이트
                UpdateGuideText();
                
                currentRectangle = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"영역 캡처 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                if (currentRectangle != null)
                {
                    canvas.Children.Remove(currentRectangle);
                    currentRectangle = null;
                }
            }
            
            e.Handled = true;
        }
        
        private void UpdateGuideText()
        {
            if (guideText != null)
            {
                if (capturedRegions.Count == 0)
                    guideText.Text = "영역을 드래그하여 선택하세요\n[Enter]: 모두 합쳐서 저장  |  [F1]: 각각 따로 저장  |  [ESC]: 취소\n[우클릭]: 마지막 영역 취소";
                else
                    guideText.Text = $"{capturedRegions.Count}개 선택됨\n[Enter]: 모두 합쳐서 저장  |  [F1]: 각각 따로 저장  |  [ESC]: 취소\n[우클릭]: 마지막 영역 취소";
            }
        }
        
        private void MultiCaptureWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (capturedRegions.Count > 0)
                {
                    ComposeImages();
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("최소 1개 이상의 영역을 캡처해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F1)
            {
                // 개별 이미지 저장 모드
                if (capturedRegions.Count > 0)
                {
                    IndividualImages = capturedRegions.Select(r => r.Image).ToList();
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("최소 1개 이상의 영역을 캡처해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                // 마지막 캡처 취소
                if (capturedRegions.Count > 0)
                {
                    var lastRegion = capturedRegions[capturedRegions.Count - 1];
                    canvas.Children.Remove(lastRegion.VisualRectangle);
                    capturedRegions.RemoveAt(capturedRegions.Count - 1);
                    UpdateGuideText();
                }
                e.Handled = true;
            }
        }
        
        private void ComposeImages()
        {
            if (capturedRegions.Count == 0) return;
            
            try
            {
                // 모든 캡처 영역을 포함할 수 있는 최소 크기 계산
                int minX = capturedRegions.Min(r => r.Area.X);
                int minY = capturedRegions.Min(r => r.Area.Y);
                int maxX = capturedRegions.Max(r => r.Area.X + r.Area.Width);
                int maxY = capturedRegions.Max(r => r.Area.Y + r.Area.Height);
                
                int compositeWidth = maxX - minX;
                int compositeHeight = maxY - minY;
                
                // 크기 검증
                if (compositeWidth <= 0 || compositeHeight <= 0)
                {
                    MessageBox.Show("합성 이미지 크기가 유효하지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (compositeWidth > 32000 || compositeHeight > 32000)
                {
                    MessageBox.Show($"합성 이미지가 너무 큽니다 ({compositeWidth}x{compositeHeight}). 최대 크기는 32000x32000입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 최종 이미지 생성
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 투명 배경
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        null,
                        new Rect(0, 0, compositeWidth, compositeHeight)
                    );
                    
                    // 각 캡처된 영역을 상대 위치에 그리기
                    foreach (var region in capturedRegions)
                    {
                        var relativeX = region.Area.X - minX;
                        var relativeY = region.Area.Y - minY;
                        
                        drawingContext.DrawImage(
                            region.Image,
                            new Rect(relativeX, relativeY, region.Area.Width, region.Area.Height)
                        );
                    }
                }
                
                // RenderTargetBitmap으로 변환
                var renderBitmap = new RenderTargetBitmap(
                    compositeWidth,
                    compositeHeight,
                    96, 96,
                    PixelFormats.Pbgra32
                );
                
                renderBitmap.Render(drawingVisual);
                renderBitmap.Freeze();
                
                FinalCompositeImage = renderBitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 합성 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                FinalCompositeImage = null;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                capturedRegions.Clear();
                canvas?.Children.Clear();
            }
        }
    }
}
