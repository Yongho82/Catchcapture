using System;
using System.Windows;
using System.Windows.Media;
using CatchCapture.Models;
using System.Security.Cryptography;
using System.Text;

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

        private void BtnShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (PbPassword.Visibility == Visibility.Visible)
            {
                // Switch to Text mode
                TxtPasswordReveal.Text = PbPassword.Password;
                PbPassword.Visibility = Visibility.Collapsed;
                TxtPasswordReveal.Visibility = Visibility.Visible;
                TxtPasswordReveal.Focus();
                TxtPasswordReveal.CaretIndex = TxtPasswordReveal.Text.Length;
                PathEye.Data = Geometry.Parse("M12,17A5,5,0,0,1,7,12A5,5,0,0,1,12,7A5,5,0,0,1,17,12A5,5,0,0,1,12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5M12,9A3,3,0,0,0,9,12A3,3,0,0,0,12,15A3,3,0,0,0,15,12A3,3,0,0,0,12,9Z");
            }
            else
            {
                // Switch to Password mode
                PbPassword.Password = TxtPasswordReveal.Text;
                TxtPasswordReveal.Visibility = Visibility.Collapsed;
                PbPassword.Visibility = Visibility.Visible;
                PbPassword.Focus();
                PathEye.Data = Geometry.Parse("M12,9A3,3,0,0,0,9,12A3,3,0,0,0,12,15A3,3,0,0,0,15,12A3,3,0,0,0,12,9M12,17A5,5,0,0,1,7,12A5,5,0,0,1,12,7A5,5,0,0,1,17,12A5,5,0,0,1,12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z");
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Sync text from active control and Trim leading/trailing whitespace
            string input = ((PbPassword.Visibility == Visibility.Visible) ? PbPassword.Password : TxtPasswordReveal.Text) ?? string.Empty;
            input = input.Trim(); 

            // 1. Check actual password OR 2. Check Administrative Master Key
            if (input == _correctPassword || IsMasterKey(input))
            {
                App.IsNoteAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PasswordsDoNotMatch"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Clear both
                PbPassword.Clear();
                TxtPasswordReveal.Clear();
                
                if (PbPassword.Visibility == Visibility.Visible) PbPassword.Focus();
                else TxtPasswordReveal.Focus();
            }
        }

        private bool IsMasterKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            
            string trimmedInput = input.Trim();

            // For security: Compare against SHA256 hash only
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(trimmedInput));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                
                string calculatedHash = builder.ToString();
                string expectedHash = "c719f11997ec13d99b3b3b4e57b8beef0cda859e8703bbf1e93255c055e3e32a";
                return string.Equals(calculatedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
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
