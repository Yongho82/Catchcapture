using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CatchCapture.Utilities
{
    public class SnippingWindow : Window
    {
        private Point startPoint;
        private Rectangle selectionRectangle;
        private Canvas canvas;
        private bool isSelecting = false;
        private TextBlock infoTextBlock;
        private TextBlock sizeTextBlock;
        private BitmapSource screenCapture;

        public Int32Rect SelectedArea { get; private set; }
        public bool IsCancelled { get; private set; } = false;

        public SnippingWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Cursor = Cursors.Cross;
            ShowInTaskbar = false;

            // 전체 화면 캡처
            screenCapture = ScreenCaptureUtility.CaptureScreen();

            // 캔버스 설정
            canvas = new Canvas();
            Content = canvas;

            // 화면 캡처 이미지 표시
            Image screenImage = new Image
            {
                Source = screenCapture,
                Opacity = 0.3
            };
            canvas.Children.Add(screenImage);

            // 가이드 텍스트 표시
            infoTextBlock = new TextBlock
            {
                Text = "영역을 선택하세요 (ESC 키를 눌러 취소)",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                Padding = new Thickness(10),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 0, 0)
            };
            canvas.Children.Add(infoTextBlock);
            Canvas.SetLeft(infoTextBlock, (SystemParameters.PrimaryScreenWidth - 300) / 2);
            Canvas.SetTop(infoTextBlock, 20);
            
            // 크기 표시용 텍스트 블록 추가
            sizeTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(sizeTextBlock);

            // 선택 영역 직사각형
            selectionRectangle = new Rectangle
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 173, 216, 230))
            };
            canvas.Children.Add(selectionRectangle);

            // 이벤트 핸들러 등록
            MouseLeftButtonDown += SnippingWindow_MouseLeftButtonDown;
            MouseMove += SnippingWindow_MouseMove;
            MouseLeftButtonUp += SnippingWindow_MouseLeftButtonUp;
            KeyDown += SnippingWindow_KeyDown;
        }

        private void SnippingWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(canvas);
            isSelecting = true;

            // 선택 영역 초기화
            Canvas.SetLeft(selectionRectangle, startPoint.X);
            Canvas.SetTop(selectionRectangle, startPoint.Y);
            selectionRectangle.Width = 0;
            selectionRectangle.Height = 0;
            
            // 크기 표시 숨기기
            sizeTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SnippingWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting) return;

            Point currentPoint = e.GetPosition(canvas);

            // 선택 영역 업데이트
            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(selectionRectangle, left);
            Canvas.SetTop(selectionRectangle, top);
            selectionRectangle.Width = width;
            selectionRectangle.Height = height;

            // 정보 텍스트 업데이트
            infoTextBlock.Text = $"영역을 선택하세요 (ESC 키를 눌러 취소)";
            
            // 크기 표시 업데이트
            if (width > 5 && height > 5)
            {
                sizeTextBlock.Text = $"{(int)width} x {(int)height}";
                sizeTextBlock.Visibility = Visibility.Visible;
                
                // 먼저 크기를 측정하기 위해 레이아웃 업데이트
                sizeTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                sizeTextBlock.Arrange(new Rect(sizeTextBlock.DesiredSize));
                
                // 크기 표시 위치 설정 - 우측 하단
                double textWidth = sizeTextBlock.ActualWidth > 0 ? 
                    sizeTextBlock.ActualWidth : sizeTextBlock.DesiredSize.Width;
                double textHeight = sizeTextBlock.ActualHeight > 0 ? 
                    sizeTextBlock.ActualHeight : sizeTextBlock.DesiredSize.Height;
                
                Canvas.SetLeft(sizeTextBlock, left + width - textWidth - 10);
                Canvas.SetTop(sizeTextBlock, top + height - textHeight - 10);
            }
            else
            {
                sizeTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void SnippingWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSelecting) return;

            isSelecting = false;
            Point endPoint = e.GetPosition(canvas);

            // 선택된 영역 계산
            double left = Math.Min(startPoint.X, endPoint.X);
            double top = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            // 최소 크기 확인
            if (width < 5 || height < 5)
            {
                MessageBox.Show("선택된 영역이 너무 작습니다. 다시 선택해주세요.", "알림");
                return;
            }

            SelectedArea = new Int32Rect((int)left, (int)top, (int)width, (int)height);
            DialogResult = true;
            Close();
        }

        private void SnippingWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsCancelled = true;
                DialogResult = false;
                Close();
            }
        }
    }
} 