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
            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        private void UpdateUIText()
        {
            this.Title = CatchCapture.Resources.LocalizationManager.GetString("NotePasswordTitle") ?? "노트 비밀번호 설정";
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("NotePasswordTitle");
            if (TxtNewPassLabel != null) TxtNewPassLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("NewPassword");
            if (TxtConfirmPassLabel != null) TxtConfirmPassLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("ConfirmPassword");
            if (TxtHintLabel != null) TxtHintLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("PasswordHintRequired");
            if (TxtWarning != null) TxtWarning.Text = CatchCapture.Resources.LocalizationManager.GetString("PasswordLossWarning");
            
            if (BtnCancel != null) BtnCancel.Content = CatchCapture.Resources.LocalizationManager.GetString("BtnCancel");
            if (BtnSave != null) BtnSave.Content = CatchCapture.Resources.LocalizationManager.GetString("Save");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PbNewPassword.Password))
            {
                CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PleaseEnterPassword"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PbNewPassword.Password != PbConfirmPassword.Password)
            {
                CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PasswordsDoNotMatch"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtHint.Text))
            {
                CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PleaseEnterPasswordHint"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
