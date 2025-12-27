using System;
using System.Windows;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class NoteLockCheckWindow : Window
    {
        private string _correctPassword;
        private string? _hint;

        public NoteLockCheckWindow(string correctPassword, string? hint)
        {
            InitializeComponent();
            _correctPassword = correctPassword;
            _hint = hint;
            PbPassword.Focus();

            this.MouseLeftButtonDown += (s, e) => {
                try { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) this.DragMove(); } catch { }
            };

            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        private void UpdateUIText()
        {
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("NoteUnlock");
            if (TxtHeaderSub != null) TxtHeaderSub.Text = CatchCapture.Resources.LocalizationManager.GetString("ProtectedContent");
            if (BtnHint != null) BtnHint.Content = CatchCapture.Resources.LocalizationManager.GetString("ShowHint");
            if (TxtPasswordLabel != null) TxtPasswordLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Password");
            if (BtnCancel != null) BtnCancel.Content = CatchCapture.Resources.LocalizationManager.GetString("Cancel");
            if (BtnConfirm != null) BtnConfirm.Content = CatchCapture.Resources.LocalizationManager.GetString("Unlock");
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (PbPassword.Password == _correctPassword)
            {
                App.IsNoteAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PasswordsDoNotMatch"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                PbPassword.Clear();
                PbPassword.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnHint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_hint))
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoHintRegistered"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CatchCapture.CustomMessageBox.Show(string.Format(CatchCapture.Resources.LocalizationManager.GetString("PasswordHintMessage"), _hint), CatchCapture.Resources.LocalizationManager.GetString("Hint"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
