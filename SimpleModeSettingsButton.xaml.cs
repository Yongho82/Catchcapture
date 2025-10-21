using System;
using System.Windows;

namespace CatchCapture
{
    public partial class SimpleModeSettingsButton : Window
    {
        private Window _anchor;
        public SimpleModeSettingsButton(Window anchor)
        {
            InitializeComponent();
            _anchor = anchor;
        }

        public void PositionBelow(Window anchor)
        {
            // Place just below the anchor window, centered
            double x = anchor.Left + (anchor.Width - this.Width) / 2;
            double y = anchor.Top + anchor.Height + 6; // small gap
            this.Left = x;
            this.Top = y;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = _anchor;
            win.ShowDialog();
        }
    }
}
