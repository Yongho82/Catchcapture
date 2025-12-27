using System.Windows;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class NotePasswordWindow : Window
    {
        public string? Password { get; private set; }
        public string? Hint { get; private set; }

        public NotePasswordWindow(string? existingPassword = null, string? existingHint = null)
        {
            InitializeComponent();
            // Do not pre-fill existing password for security
            TxtHint.Text = existingHint;
            
            this.MouseDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PbNewPassword.Password))
            {
                CustomMessageBox.Show("비밀번호를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PbNewPassword.Password != PbConfirmPassword.Password)
            {
                CustomMessageBox.Show("비밀번호가 일치하지 않습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtHint.Text))
            {
                CustomMessageBox.Show("비밀번호 힌트를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Password = PbNewPassword.Password;
            Hint = TxtHint.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
