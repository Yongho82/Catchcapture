using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CatchCapture.Models;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class HistoryWindow : Window
    {
        public ObservableCollection<HistoryItem> HistoryItems { get; set; } = new ObservableCollection<HistoryItem>();

        public HistoryWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            
            // Allow window dragging
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            
            LoadHistory();
        }

        private string _currentFilter = "All";
        private string _currentSearch = "";

        public void LoadHistory(string filter = "All", string search = "", DateTime? dateFrom = null, DateTime? dateTo = null, string? fileType = null)
        {
            _currentFilter = filter;
            _currentSearch = search;

            try
            {
                var items = DatabaseManager.Instance.GetHistory(filter, search, dateFrom, dateTo, fileType);
                HistoryItems.Clear();
                foreach (var item in items)
                {
                    HistoryItems.Add(item);
                }
                
                LstHistory.ItemsSource = HistoryItems;
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"히스토리 로드 중 오류: {ex.Message}", "오류");
            }
        }

        private void BtnApplyAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            string search = TxtFilterFileName.Text;
            DateTime? from = DateFrom.SelectedDate;
            DateTime? to = DateTo.SelectedDate;
            string? type = (CmbFileType.SelectedItem as ComboBoxItem)?.Content?.ToString();

            LoadHistory(_currentFilter, search, from, to, type);
            PopupFilter.IsOpen = false;
        }

        private void UpdateEmptyState()
        {
            if (LstHistory.Items.Count == 0)
            {
                // You could show a specialized empty state for the list here
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

        private void BtnAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            PopupFilter.IsOpen = !PopupFilter.IsOpen;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = HistoryItems.All(i => i.IsSelected);
            foreach (var item in HistoryItems)
            {
                item.IsSelected = !allSelected;
            }
            ChkHeaderAll.IsChecked = !allSelected;
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = HistoryItems.Where(i => i.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            string msg = _currentFilter == "Trash" ? "선택한 항목을 영구 삭제하시겠습니까? (파일도 삭제됩니다)" : "선택한 항목을 휴지통으로 보내시겠습니까?";
            if (CustomMessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                {
                    DatabaseManager.Instance.DeleteCapture(item.Id, _currentFilter == "Trash");
                }
                LoadHistory(_currentFilter, _currentSearch);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory(_currentFilter, TxtSearch.Text);
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoadHistory(_currentFilter, TxtSearch.Text);
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                LoadHistory(btn.Tag?.ToString() ?? "All");
            }
        }

        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoryItem item)
            {
                DatabaseManager.Instance.ToggleFavoriteCapture(item.Id);
                item.IsFavorite = !item.IsFavorite;
                // UI update via property change (if implemented in HistoryItem)
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoryItem item)
            {
                string msg = _currentFilter == "Trash" ? "이 항목을 영구 삭제하시겠습니까? (파일도 삭제됩니다)" : "이 항목을 휴지통으로 보내시겠습니까?";
                if (CustomMessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DatabaseManager.Instance.DeleteCapture(item.Id, _currentFilter == "Trash");
                    LoadHistory(_currentFilter, _currentSearch);
                }
            }
        }

        private void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item)
            {
                PreviewEmptyState.Visibility = Visibility.Collapsed;
                UpdatePreview(item);
            }
            else
            {
                PreviewEmptyState.Visibility = Visibility.Visible;
            }
        }

        private void UpdatePreview(HistoryItem item)
        {
            try
            {
                if (System.IO.File.Exists(item.FilePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(item.FilePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ImgPreview.Source = bitmap;
                    
                    TxtPreviewSize.Text = $"{bitmap.PixelWidth} x {bitmap.PixelHeight}";
                    TxtPreviewWeight.Text = GetFileSizeString(item.FileSize);
                    TxtPreviewApp.Text = item.SourceApp;
                    TxtPreviewTitle.Text = item.SourceTitle;
                }
            }
            catch
            {
                ImgPreview.Source = null;
            }
        }

        private string GetFileSizeString(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoryItem item)
            {
                DatabaseManager.Instance.RestoreCapture(item.Id);
                LoadHistory(_currentFilter, _currentSearch);
            }
        }
    }
}
