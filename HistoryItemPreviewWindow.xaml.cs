using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Models;
using CatchCapture.Utilities;
using Microsoft.Data.Sqlite;

namespace CatchCapture
{
    public partial class HistoryItemPreviewWindow : Window
    {
        private HistoryItem _item;
        
        public HistoryItemPreviewWindow(HistoryItem item)
        {
            InitializeComponent();
            _item = item;
            this.DataContext = item;
            
            LoadItem();
        }

        private void LoadItem()
        {
            if (_item == null) return;
            
            try
            {
                TxtPreviewPath.Text = _item.FilePath;
                
                // Update Memo Display
                TxtMemoDisplay.Text = string.IsNullOrEmpty(_item.Memo) ? "메모가 없습니다." : _item.Memo;
                TxtMemoDisplay.Opacity = string.IsNullOrEmpty(_item.Memo) ? 0.4 : 0.8;
                EditMemoBox.Text = _item.Memo;
                EditMemoBox.Visibility = Visibility.Collapsed;
                TxtMemoDisplay.Visibility = Visibility.Visible;
                BtnEditMemo.Content = "수정";

                // 기본 메타데이터 설정
                TxtPreviewSize.Text = !string.IsNullOrEmpty(_item.Resolution) ? _item.Resolution : "-";
                TxtPreviewWeight.Text = GetFileSizeString(_item.FileSize);
                TxtPreviewApp.Text = _item.SourceApp;
                TxtPreviewTitle.Text = _item.SourceTitle;

                if (System.IO.File.Exists(_item.FilePath))
                {
                    string previewPath = _item.FilePath;
                    
                    // 동영상 파일인 경우 썸네일 탐색
                    bool isMedia = _item.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                   _item.FilePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                   _item.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                    
                    bool isAudio = _item.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

                    if (isMedia)
                    {
                        // 미디어 파일은 편집 및 노트 저장 비활성화 (현재 이미지 전용)
                        BtnPreviewEdit.Visibility = Visibility.Collapsed;
                        BtnPreviewNote.Visibility = Visibility.Collapsed;
                        MediaOverlay.Visibility = Visibility.Visible;
                        
                        // 포맷 표시
                        TxtPreviewFormat.Text = System.IO.Path.GetExtension(_item.FilePath).ToUpper().Replace(".", "");
                        
                        if (isAudio)
                        {
                            VideoPlayOverlay.Visibility = Visibility.Collapsed;
                            AudioIconOverlay.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            VideoPlayOverlay.Visibility = Visibility.Visible;
                            AudioIconOverlay.Visibility = Visibility.Collapsed;
                        }

                        string thumbPath = _item.FilePath + ".preview.png";
                        if (System.IO.File.Exists(thumbPath))
                        {
                            previewPath = thumbPath;
                        }
                        else
                        {
                            if (isAudio) ImgPreview.Source = null;
                            else ImgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/icons/videocamera.png"));
                            ImgPreview.Opacity = 0.5;
                        }
                    }
                    else
                    {
                        BtnPreviewEdit.Visibility = Visibility.Visible;
                        BtnPreviewNote.Visibility = Visibility.Visible;
                        MediaOverlay.Visibility = Visibility.Collapsed;
                    }

                    if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(previewPath);
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        
                        ImgPreview.Source = bitmap;
                        ImgPreview.Opacity = 1.0;

                        // 이미지가 있으면 실제 크기 정보 다시 한번 확인 (이미지인 경우만)
                        if (!isMedia)
                        {
                            TxtPreviewSize.Text = $"{bitmap.PixelWidth} x {bitmap.PixelHeight}";
                        }
                    }
                }
                else
                {
                    ImgPreview.Source = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview Load Error: {ex.Message}");
            }
        }
        
        private string GetFileSizeString(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double)bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            
            if (PathMaximize == null) return;

            if (this.WindowState == WindowState.Maximized)
            {
                // Restore icon
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,12V18H12V12H6Z");
            }
            else
            {
                // Maximize icon
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,4H20V20H4V4M6,6V18H18V6H6Z");
            }
        }

        private void BtnEditMemo_Click(object sender, RoutedEventArgs e)
        {
            if (EditMemoBox.Visibility == Visibility.Collapsed)
            {
                // 편집 모드 시작
                EditMemoBox.Visibility = Visibility.Visible;
                TxtMemoDisplay.Visibility = Visibility.Collapsed;
                // 스크롤뷰어 숨김
                ScrollMemoDisplay.Visibility = Visibility.Collapsed;
                BtnEditMemo.Content = "저장";
                EditMemoBox.Focus();
            }
            else
            {
                // 저장
                string newMemo = EditMemoBox.Text;
                DatabaseManager.Instance.UpdateCaptureMemo(_item.Id, newMemo);
                _item.Memo = newMemo;
                
                EditMemoBox.Visibility = Visibility.Collapsed;
                TxtMemoDisplay.Visibility = Visibility.Visible;
                ScrollMemoDisplay.Visibility = Visibility.Visible;
                
                TxtMemoDisplay.Text = string.IsNullOrEmpty(newMemo) ? "메모가 없습니다." : newMemo;
                TxtMemoDisplay.Opacity = string.IsNullOrEmpty(newMemo) ? 0.4 : 0.8;
                BtnEditMemo.Content = "수정";
            }
        }

        private void BtnPreviewEdit_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.File.Exists(_item.FilePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_item.FilePath);
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    var previewWin = new PreviewWindow(bitmap, 0);
                    previewWin.Owner = this;
                    
                    previewWin.ImageUpdated += (s, ev) =>
                    {
                        try
                        {
                            ScreenCaptureUtility.SaveImageToFile(ev.NewImage, _item.FilePath);
                            LoadItem(); // Reload changes
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"이미지 저장 중 오류가 발생했습니다: {ex.Message}", "오류");
                        }
                    };
                    
                    previewWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"편집창을 열 수 없습니다: {ex.Message}", "오류");
                }
            }
        }

        private void BtnPreviewNote_Click(object sender, RoutedEventArgs e)
        {
             if (System.IO.File.Exists(_item.FilePath))
            {
                try
                {
                    BitmapSource? image = ImgPreview.Source as BitmapSource;
                    if (image == null)
                    {
                         var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(_item.FilePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        image = bitmap;
                    }

                    // 노트 생성 윈도우 직접 호출
                    var noteWin = new NoteInputWindow(image, _item.SourceApp, _item.SourceTitle);
                    noteWin.Show();
                    CustomMessageBox.Show("새 노트가 생성되었습니다.", "알림");
                    // 창 닫기 (선택사항)
                    this.Close();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"노트 생성 실패: {ex.Message}", "오류");
                }
            }
        }

        private void BtnPreviewPin_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.File.Exists(_item.FilePath))
            {
                try
                {
                    BitmapSource? image = ImgPreview.Source as BitmapSource;
                    if (image == null)
                    {
                         var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(_item.FilePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        image = bitmap;
                    }
                    
                    var pinnedWindow = new PinnedImageWindow(image);
                    pinnedWindow.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"고정 실패: {ex.Message}", "오류");
                }
            }
        }

         private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_item.FilePath))
            {
                try
                {
                    string argument = "/select, \"" + _item.FilePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"폴더를 열 수 없습니다: {ex.Message}", "오류");
                }
            }
        }
    }
}
