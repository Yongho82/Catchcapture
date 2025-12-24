using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Shapes; // [Fix] Add this
using CatchCapture.Resources;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class PinnedImageWindow : Window
    {
        public PinnedImageWindow(BitmapSource image)
        {
            InitializeComponent();
            PinnedImage.Source = image;
            
            // 이미지 크기에 맞춰 윈도우 크기 설정 (테두리 2px * 2 = 4px 포함)
            this.Width = image.PixelWidth + 4;
            this.Height = image.PixelHeight + 4;

            InitializeContextMenu();
            
            // 툴팁 다국어 적용
            BtnCopy.ToolTip = LocalizationManager.GetString("Copy");
            BtnSave.ToolTip = LocalizationManager.GetString("Save");
            BtnClose.ToolTip = LocalizationManager.GetString("Close");
        }

        private void InitializeContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            
            MenuItem copyItem = new MenuItem { Header = LocalizationManager.GetString("Copy") };
            copyItem.Click += Copy_Click;
            
            MenuItem saveItem = new MenuItem { Header = LocalizationManager.GetString("Save") };
            saveItem.Click += Save_Click;
            
            MenuItem closeItem = new MenuItem { Header = LocalizationManager.GetString("Close") }; 
            closeItem.Click += Close_Click;
            
            menu.Items.Add(copyItem);
            menu.Items.Add(saveItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(closeItem);
            
            ContainerBorder.ContextMenu = menu;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = "png",
                FileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                 ScreenCaptureUtility.SaveImageToFile((BitmapSource)PinnedImage.Source, saveDialog.FileName);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
             ScreenCaptureUtility.CopyImageToClipboard((BitmapSource)PinnedImage.Source);
             
             string msg = LocalizationManager.GetString("CopiedToClipboard");
             if (msg == "CopiedToClipboard") msg = "클립보드에 복사 되었습니다.";
             StickerWindow.Show(msg);
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 호버 시 버튼 패널 및 선택 오버레이 표시
        private void MainGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            ActionPanel.Visibility = Visibility.Visible;
            if (SelectionOverlay != null) SelectionOverlay.Visibility = Visibility.Visible;
        }

        private void MainGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            if (SelectionOverlay != null) SelectionOverlay.Visibility = Visibility.Collapsed;
        }

        // 리사이즈 로직
        // Windows API SendMessage를 사용하여 시스템 리사이즈 명령 전송
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SIZE = 0xF000;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private void Resize_Init(object sender, MouseButtonEventArgs e)
        {
            Rectangle? senderRect = sender as Rectangle;
            if (senderRect == null) return;

            WindowInteropHelper helper = new WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;
            
            // 방향에 따른 sc_monitor 값 설정
            // 1:Left, 2:Right, 3:Top, 4:TopLeft, 5:TopRight, 6:Bottom, 7:BottomLeft, 8:BottomRight
            int direction = 0;
            switch(senderRect.Name)
            {
                case "ResizeW": direction = 1; break;
                case "ResizeE": direction = 2; break;
                case "ResizeN": direction = 3; break;
                case "ResizeNW": direction = 4; break;
                case "ResizeNE": direction = 5; break;
                case "ResizeS": direction = 6; break;
                case "ResizeSW": direction = 7; break;
                case "ResizeSE": direction = 8; break;
            }

            if (direction > 0)
            {
                 SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + direction), IntPtr.Zero);
            }
        }

        // 키보드 이벤트 처리 (Delete 키로 닫기, Ctrl+C로 복사)
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !e.IsRepeat)
            {
                e.Handled = true; // 이벤트가 다른 창으로 전파되는 것을 방지
                Close();
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Copy_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
