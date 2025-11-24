using System.Windows;

namespace CatchCapture
{
    public partial class OcrResultWindow : Window
    {
        public OcrResultWindow(string text)
        {
            InitializeComponent();
            ResultTextBox.Text = text;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultTextBox.Text))
            {
                Clipboard.SetText(ResultTextBox.Text);
                MessageBox.Show("텍스트가 클립보드에 복사되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
