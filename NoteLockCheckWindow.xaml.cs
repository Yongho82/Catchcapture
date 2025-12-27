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
                CatchCapture.CustomMessageBox.Show("비밀번호가 일치하지 않습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                CatchCapture.CustomMessageBox.Show("등록된 힌트가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CatchCapture.CustomMessageBox.Show($"비밀번호 힌트: {_hint}", "힌트", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
