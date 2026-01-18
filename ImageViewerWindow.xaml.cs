using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using Microsoft.Win32;

using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class ImageViewerWindow : Window
    {
        private Point _origin;
        private Point _start;
        private string _imagePath;
        private bool _isDragging = false;

        public ImageViewerWindow(string imagePath)
        {
            InitializeComponent();
            _imagePath = imagePath;
            LoadImage(imagePath);
            
            UpdateUIText();

            this.KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        private void UpdateUIText()
        {
            if (TxtAppTitle != null) TxtAppTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("ImageViewerTitle");
            if (BtnZoomReset != null) BtnZoomReset.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ImageViewerResetZoomTooltip");
            if (BtnPin != null) BtnPin.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("PinToScreenTooltip") ?? "화면에 고정";
            if (BtnCopy != null) BtnCopy.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ImageViewerCopyTooltip");
            if (BtnSaveAs != null) BtnSaveAs.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ImageViewerSaveAsTooltip");
        }

        private async void LoadImage(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                // Use ThumbnailManager for robust loading (decodeWidth 0 means full size)
                var bitmap = await Utilities.ThumbnailManager.LoadThumbnailAsync(path, 0);
                if (bitmap == null)
                {
                    TxtFileName.Text = "Error: Failed to decode image.";
                    return;
                }

                ImgDisplay.Source = bitmap;
                TxtFileName.Text = Path.GetFileName(path);
                TxtResolution.Text = $"({bitmap.PixelWidth} × {bitmap.PixelHeight})";

                FitToWindow();
            }
            catch (Exception ex)
            {
                TxtFileName.Text = "Error: " + ex.Message;
            }
        }

        private void FitToWindow()
        {
            if (ImgDisplay.Source == null || ViewerGrid.ActualWidth == 0) return;

            double border = 60;
            double wScale = (ViewerGrid.ActualWidth - border) / ImgDisplay.Source.Width;
            double hScale = (ViewerGrid.ActualHeight - border) / ImgDisplay.Source.Height;
            double scale = Math.Min(wScale, hScale);

            if (scale > 1.0) scale = 1.0; 

            ImgScale.ScaleX = scale;
            ImgScale.ScaleY = scale;

            CenterImage();
            UpdateHUD();
        }

        private void CenterImage()
        {
            if (ImgDisplay.Source == null || ViewerGrid.ActualWidth == 0) return;
            ImgTranslate.X = (ViewerGrid.ActualWidth - ImgDisplay.Source.Width * ImgScale.ScaleX) / 2;
            ImgTranslate.Y = (ViewerGrid.ActualHeight - ImgDisplay.Source.Height * ImgScale.ScaleY) / 2;
        }

        private void UpdateHUD()
        {
            TxtZoomLevel.Text = $"{(ImgScale.ScaleX * 100):0}%";
        }

        private void ViewerGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point p = e.GetPosition(ImgDisplay);
            double scale = e.Delta > 0 ? 1.1 : 0.9;
            double newScale = ImgScale.ScaleX * scale;

            if (newScale < 0.05) newScale = 0.05;
            if (newScale > 15.0) newScale = 15.0;

            double mX = p.X * ImgScale.ScaleX + ImgTranslate.X;
            double mY = p.Y * ImgScale.ScaleY + ImgTranslate.Y;

            ImgScale.ScaleX = newScale;
            ImgScale.ScaleY = newScale;

            ImgTranslate.X = mX - p.X * ImgScale.ScaleX;
            ImgTranslate.Y = mY - p.Y * ImgScale.ScaleY;

            UpdateHUD();
            e.Handled = true;
        }

        private void ViewerGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                FitToWindow();
                return;
            }

            _start = e.GetPosition(ViewerGrid);
            _origin = new Point(ImgTranslate.X, ImgTranslate.Y);
            _isDragging = true;
            ViewerGrid.CaptureMouse();
        }

        private void ViewerGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Vector v = e.GetPosition(ViewerGrid) - _start;
                ImgTranslate.X = _origin.X + v.X;
                ImgTranslate.Y = _origin.Y + v.Y;
            }
        }

        private void ViewerGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ViewerGrid.ReleaseMouseCapture();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,10V18H14V10H6Z");
            }
            
            // Re-center/fit after state change
            Dispatcher.BeginInvoke(new Action(() => { FitToWindow(); }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ImgScale.ScaleX = 1.0;
            ImgScale.ScaleY = 1.0;
            CenterImage();
            UpdateHUD();
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (ImgDisplay.Source is BitmapSource bs)
            {
                var pinnedWindow = new PinnedImageWindow(bs);
                pinnedWindow.Show();
                pinnedWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try 
            { 
                if (ImgDisplay.Source is BitmapSource bs) 
                {
                    Clipboard.SetImage(bs); 
                    string msg = CatchCapture.Resources.LocalizationManager.GetString("CopiedToClipboard");
                    if (string.IsNullOrEmpty(msg) || msg == "CopiedToClipboard") msg = "클립보드에 복사 되었습니다.";
                    StickerWindow.Show(msg);
                }
            } 
            catch { }
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog { FileName = Path.GetFileName(_imagePath), Filter = "Image Files|*.png;*.jpg;*.webp;*.bmp|All Files|*.*" };
                if (dialog.ShowDialog() == true) File.Copy(_imagePath, dialog.FileName, true);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
