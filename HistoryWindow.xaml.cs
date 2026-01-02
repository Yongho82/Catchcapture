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
using System.Windows.Media;

namespace CatchCapture
{
    public partial class HistoryWindow : Window
    {
        public ObservableCollection<HistoryItem> HistoryItems { get; set; } = new ObservableCollection<HistoryItem>();

        public HistoryWindow()
        {
            try
            {
                InitializeComponent();
                this.DataContext = this;
                
                // Allow window dragging
                this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };

                this.Loaded += HistoryWindow_Loaded;
                this.Closing += HistoryWindow_Closing;
                
                // 2. 날짜 시작, 끝은 today로 기본 설정
                DateFrom.SelectedDate = DateTime.Today;
                DateTo.SelectedDate = DateTime.Today;
                
                // Load cached history first (will be refreshed)
                LoadHistory(); 
                RefreshCounts();

                // Set default sidebar selection
                foreach (var btn in FindVisualChildren<Button>(MainGrid.Children[0]))
                {
                    if (btn.Tag?.ToString() == "All")
                    {
                        UpdateSidebarSelection(btn);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"히스토리 창 초기화 중 오류: {ex.Message}\n{ex.StackTrace}", "오류");
            }
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = Settings.Load();
            if (settings.HistoryWindowWidth > 0) this.Width = settings.HistoryWindowWidth;
            if (settings.HistoryWindowHeight > 0) this.Height = settings.HistoryWindowHeight;
            if (settings.HistoryWindowLeft != -9999) this.Left = settings.HistoryWindowLeft;
            if (settings.HistoryWindowTop != -9999) this.Top = settings.HistoryWindowTop;
            
            // Ensure List Pane is flexible (Col 1)
            if (MainGrid.ColumnDefinitions.Count >= 2)
            {
                MainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }

            // Restore Preview Pane Width (Col 3)
            if (MainGrid.ColumnDefinitions.Count >= 4)
            {
                MainGrid.ColumnDefinitions[3].Width = new GridLength(settings.HistoryPreviewPaneWidth);
            }

            // Restore GridView Column Widths
            ColDate.Width = settings.HistoryColDate;
            ColFileName.Width = settings.HistoryColFileName;
            ColMeta.Width = settings.HistoryColMeta;
            ColPin.Width = settings.HistoryColPin;
            ColFavorite.Width = settings.HistoryColFavorite;
            ColActions.Width = settings.HistoryColActions;
            ColMemo.Width = settings.HistoryColMemo;
        }

        private void HistoryWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = Settings.Load();
            settings.HistoryWindowWidth = this.Width;
            settings.HistoryWindowHeight = this.Height;
            settings.HistoryWindowLeft = this.Left;
            settings.HistoryWindowTop = this.Top;
            
            
            if (MainGrid.ColumnDefinitions.Count >= 4)
            {
                settings.HistoryPreviewPaneWidth = MainGrid.ColumnDefinitions[3].Width.Value;
            }

            // Save GridView Column Widths
            settings.HistoryColDate = ColDate.Width;
            settings.HistoryColFileName = ColFileName.Width;
            settings.HistoryColMeta = ColMeta.Width;
            settings.HistoryColPin = ColPin.Width;
            settings.HistoryColFavorite = ColFavorite.Width;
            settings.HistoryColActions = ColActions.Width;
            settings.HistoryColMemo = ColMemo.Width;
            
            settings.Save();
        }

        private void RefreshCounts()
        {
            try
            {
                TxtCountAll.Text = DatabaseManager.Instance.GetHistoryCount("All").ToString();
                TxtCountRecent7.Text = DatabaseManager.Instance.GetHistoryCount("Recent7").ToString();
                TxtCountRecent30.Text = DatabaseManager.Instance.GetHistoryCount("Recent30").ToString();
                TxtCountRecent3Months.Text = DatabaseManager.Instance.GetHistoryCount("Recent3Months").ToString();
                TxtCountRecent6Months.Text = DatabaseManager.Instance.GetHistoryCount("Recent6Months").ToString();
                TxtCountFavorite.Text = DatabaseManager.Instance.GetHistoryCount("Favorite").ToString();
                TxtCountTrash.Text = DatabaseManager.Instance.GetHistoryCount("Trash").ToString();
            }
            catch { }
        }

        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private string _currentFilter = "All";
        private string _currentSearch = "";
        private DateTime? _currentDateFrom = null;
        private DateTime? _currentDateTo = null;
        private string? _currentFileType = null;

        public void LoadHistory(string filter = "All", string search = "", DateTime? dateFrom = null, DateTime? dateTo = null, string? fileType = null, bool resetPage = true)
        {
            if (resetPage) _currentPage = 1;
            
            _currentFilter = filter;
            _currentSearch = search;
            _currentDateFrom = dateFrom;
            _currentDateTo = dateTo;
            _currentFileType = fileType;

            try
            {
                int totalCount = DatabaseManager.Instance.GetHistoryCount(filter, search, dateFrom, dateTo, fileType);
                _totalPages = (int)Math.Ceiling((double)totalCount / _pageSize);
                if (_totalPages < 1) _totalPages = 1;

                int offset = (_currentPage - 1) * _pageSize;
                var items = DatabaseManager.Instance.GetHistory(filter, search, dateFrom, dateTo, fileType, _pageSize, offset);
                
                HistoryItems.Clear();
                foreach (var item in items)
                {
                    HistoryItems.Add(item);
                }
                
                LstHistory.ItemsSource = HistoryItems;
                UpdateEmptyState();
                RefreshCounts();
                UpdateSelectAllButtonText();
                UpdatePaginationUI();
                
                // 휴지통 비우기 버튼 표시 여부
                if (BtnEmptyTrash != null)
                {
                    BtnEmptyTrash.Visibility = (filter == "Trash") ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"히스토리 로드 중 오류: {ex.Message}", "오류");
            }
        }

        private void UpdatePaginationUI()
        {
            TxtPageInfo.Text = $"{_currentPage} / {_totalPages}";
            
            // Generate numeric page buttons (simple version with current and neighbors)
            PanelPageNumbers.Children.Clear();
            
            int startPage = Math.Max(1, _currentPage - 2);
            int endPage = Math.Min(_totalPages, startPage + 4);
            if (endPage - startPage < 4) startPage = Math.Max(1, endPage - 4);

            for (int i = startPage; i <= endPage; i++)
            {
                var btn = new Button
                {
                    Content = i.ToString(),
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(2, 0, 2, 0),
                    Style = (Style)FindResource("PaginationButtonStyle"),
                    Tag = i
                };
                if (i == _currentPage)
                {
                    btn.Background = (SolidColorBrush)FindResource("AccentColor");
                    btn.Foreground = System.Windows.Media.Brushes.White;
                }
                btn.Click += (s, e) => {
                    _currentPage = (int)((Button)s).Tag;
                    LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false);
                };
                PanelPageNumbers.Children.Add(btn);
            }

            BtnFirstPage.IsEnabled = _currentPage > 1;
            BtnPrevPage.IsEnabled = _currentPage > 1;
            BtnNextPage.IsEnabled = _currentPage < _totalPages;
            BtnLastPage.IsEnabled = _currentPage < _totalPages;
        }

        private void BtnFirstPage_Click(object sender, RoutedEventArgs e) { _currentPage = 1; LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false); }
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) { _currentPage--; LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false); } }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { if (_currentPage < _totalPages) { _currentPage++; LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false); } }
        private void BtnLastPage_Click(object sender, RoutedEventArgs e) { _currentPage = _totalPages; LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false); }

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

        private void UpdateSelectAllButtonText()
        {
            bool allSelected = HistoryItems.Count > 0 && HistoryItems.All(i => i.IsSelected);
            BtnSelectAll.Content = allSelected ? "전체 해제" : "전체 선택";
            ChkHeaderAll.IsChecked = allSelected;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (ColCheckBox.Width == 0)
            {
                ColCheckBox.Width = 45;
            }

            bool allSelected = HistoryItems.Count > 0 && HistoryItems.All(i => i.IsSelected);
            foreach (var item in HistoryItems)
            {
                item.IsSelected = !allSelected;
            }
            
            UpdateSelectAllButtonText();
        }

        private void ChkItem_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectAllButtonText();
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

        private void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show("휴지통의 모든 항목을 영구 삭제하시겠습니까?\n(폴더의 실제 파일들도 모두 삭제됩니다)", "휴지통 비우기", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DatabaseManager.Instance.EmptyTrash();
                LoadHistory("Trash");
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
                string filter = btn.Tag?.ToString() ?? "All";
                LoadHistory(filter);
                UpdateSidebarSelection(btn);
            }
        }

        private void UpdateSidebarSelection(Button activeBtn)
        {
            // Find all buttons in the sidebar
            var buttons = FindVisualChildren<Button>(MainGrid.Children[0]); 
            foreach (var btn in buttons)
            {
                if (btn.Style == FindResource("SidebarItemStyle"))
                {
                    // Use hover background for active, transparent for others
                    btn.Background = (btn == activeBtn) ? (SolidColorBrush)FindResource("ThemeSidebarButtonHoverBackground") : System.Windows.Media.Brushes.Transparent;
                    // Slightly increase opacity for active text
                    if (btn.Content is Grid g && g.Children[0] is StackPanel sp)
                    {
                        sp.Opacity = (btn == activeBtn) ? 1.0 : 0.8;
                    }
                }
            }
        }

#pragma warning disable CS8604
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
#pragma warning restore CS8604

        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoryItem item)
            {
                DatabaseManager.Instance.ToggleFavoriteCapture(item.Id);
                item.IsFavorite = !item.IsFavorite;
                RefreshCounts();
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is HistoryItem item)
            {
                DatabaseManager.Instance.TogglePinCapture(item.Id);
                item.IsPinned = !item.IsPinned;
                // Pin status changes sorting, so reload
                LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item && !string.IsNullOrEmpty(item.FilePath))
            {
                try
                {
                    string argument = "/select, \"" + item.FilePath + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"폴더를 열 수 없습니다: {ex.Message}", "오류");
                }
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            HistoryItem? item = (sender as Button)?.DataContext as HistoryItem ?? LstHistory.SelectedItem as HistoryItem;
            if (item == null) return;

            string msg = _currentFilter == "Trash" ? "이 항목을 영구 삭제하시겠습니까? (파일도 삭제됩니다)" : "이 항목을 휴지통으로 보내시겠습니까?";
            if (CustomMessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                DatabaseManager.Instance.DeleteCapture(item.Id, _currentFilter == "Trash");
                LoadHistory(_currentFilter, _currentSearch);
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

        private void LstHistory_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                // If there are checked items, delete them instead of just the selected one
                var checkedItems = HistoryItems.Where(i => i.IsSelected).ToList();
                if (checkedItems.Count > 0)
                {
                    BtnDeleteSelected_Click(this, new RoutedEventArgs());
                }
                else if (LstHistory.SelectedItem is HistoryItem selectedItem)
                {
                    // If no items checked, delete the focused item
                    BtnDeleteItem_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void UpdatePreview(HistoryItem item)
        {
            try
            {
                TxtPreviewPath.Text = item.FilePath;
                
                // Update Memo Display
                TxtMemoDisplay.Text = string.IsNullOrEmpty(item.Memo) ? "메모가 없습니다." : item.Memo;
                TxtMemoDisplay.Opacity = string.IsNullOrEmpty(item.Memo) ? 0.4 : 0.8;
                EditMemoBox.Text = item.Memo;
                EditMemoBox.Visibility = Visibility.Collapsed;
                TxtMemoDisplay.Visibility = Visibility.Visible;
                BtnEditMemo.Content = "수정";

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

        private void BtnEditMemo_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item)
            {
                if (EditMemoBox.Visibility == Visibility.Collapsed)
                {
                    // Swtich to Edit Mode
                    TxtMemoDisplay.Visibility = Visibility.Collapsed;
                    EditMemoBox.Visibility = Visibility.Visible;
                    EditMemoBox.Focus();
                    EditMemoBox.SelectAll();
                    BtnEditMemo.Content = "저장";
                }
                else
                {
                    // Save and switch back
                    string newMemo = EditMemoBox.Text.Trim();
                    DatabaseManager.Instance.UpdateCaptureMemo(item.Id, newMemo);
                    item.Memo = newMemo;
                    
                    TxtMemoDisplay.Text = string.IsNullOrEmpty(newMemo) ? "메모가 없습니다." : newMemo;
                    TxtMemoDisplay.Opacity = string.IsNullOrEmpty(newMemo) ? 0.4 : 0.8;
                    
                    EditMemoBox.Visibility = Visibility.Collapsed;
                    TxtMemoDisplay.Visibility = Visibility.Visible;
                    BtnEditMemo.Content = "수정";
                }
            }
        }
    }
}
