using System;
using System.Windows;
using System.Windows.Input;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBox()
        {
            InitializeComponent();
            
            // 창 드래그 이동 지원
            MouseLeftButtonDown += (s, e) => 
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };

            // 화면 캡처에서 제외
            Loaded += (s, e) =>
            {
                var interop = new System.Windows.Interop.WindowInteropHelper(this);
                SetWindowDisplayAffinity(interop.Handle, WDA_EXCLUDEFROMCAPTURE);
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        /// <summary>
        /// 커스텀 메시지 박스 표시 (정적 메서드)
        /// </summary>
        public static MessageBoxResult Show(string message, string? title = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, double width = 0)
        {
            // UI 스레드에서 실행되도록 보장 (비동기 호출 등에서 안전하게)
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(message, title, button, image, width));
            }

            var msgBox = new CustomMessageBox();
            if (width > 0) msgBox.Width = width;
            msgBox.TitleText.Text = title ?? LocalizationManager.GetString("Notice");
            // 마침표+공백이 있으면 줄바꿈으로 변경하여 가독성 향상
            msgBox.MessageText.Text = message.Replace(". ", ".\n");
            
            // 부모 창 설정 (가능하다면)
            if (Application.Current != null && Application.Current.Windows.Count > 0)
            {
                // 활성화된 윈도우를 주인으로 설정
                foreach (Window win in Application.Current.Windows)
                {
                    if (win.IsActive && win.IsVisible)
                    {
                        msgBox.Owner = win;
                        break;
                    }
                }
            }

            // 버튼 설정
            msgBox.BtnLeft.Visibility = Visibility.Visible;
            msgBox.BtnRight.Visibility = Visibility.Visible;

            switch (button)
            {
                case MessageBoxButton.OK:
                    msgBox.BtnLeft.Visibility = Visibility.Collapsed;
                    msgBox.BtnRight.Content = LocalizationManager.GetString("BtnOK");
                    msgBox.BtnRight.Tag = MessageBoxResult.OK;
                    msgBox.BtnRight.IsDefault = true;
                    // 왼쪽이 숨겨지면 StackPanel 내부 정렬에 의해 오른쪽 버튼이 중앙에 올 수 있음 (StackPanel HorizontalAlignment="Center")
                    break;
                    
                case MessageBoxButton.OKCancel:
                    msgBox.BtnLeft.Content = LocalizationManager.GetString("BtnCancel");
                    msgBox.BtnLeft.Tag = MessageBoxResult.Cancel;
                    msgBox.BtnLeft.IsCancel = true;

                    msgBox.BtnRight.Content = LocalizationManager.GetString("BtnOK");
                    msgBox.BtnRight.Tag = MessageBoxResult.OK;
                    msgBox.BtnRight.IsDefault = true;
                    break;
                    
                case MessageBoxButton.YesNo:
                case MessageBoxButton.YesNoCancel:
                    msgBox.BtnLeft.Content = LocalizationManager.GetString("BtnNo");
                    msgBox.BtnLeft.Tag = MessageBoxResult.No;
                    
                    msgBox.BtnRight.Content = LocalizationManager.GetString("BtnYes");
                    msgBox.BtnRight.Tag = MessageBoxResult.Yes;
                    msgBox.BtnRight.IsDefault = true;
                    break;
            }

            msgBox.ShowDialog();
            return msgBox.Result;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel; // X버튼은 기본적으로 취소
            Close();
        }

        private void BtnLeft_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is MessageBoxResult res)
            {
                Result = res;
            }
            else
            {
                // Tag가 없으면 기본값 (보통 Cancel/No)
                Result = MessageBoxResult.Cancel;
            }
            Close();
        }

        private void BtnRight_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is MessageBoxResult res)
            {
                Result = res;
            }
            else
            {
                Result = MessageBoxResult.OK;
            }
            Close();
        }
    }
}
