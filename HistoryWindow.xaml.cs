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
using System.ComponentModel;

using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture
{
    public partial class HistoryWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private int _currentViewMode = 0; // 0: List, 1: Card
        private GridView? _historyGridView; // To keep reference
        public int CurrentViewMode
        {
            get => _currentViewMode;
            set
            {
                if (_currentViewMode != value)
                {
                    _currentViewMode = value;
                    OnPropertyChanged(nameof(CurrentViewMode));
                    ApplyViewMode();
                }
            }
        }
        public bool IsSelectionActive => HistoryItems.Any(i => i.IsSelected);
        public ObservableCollection<HistoryItem> HistoryItems { get; set; } = new ObservableCollection<HistoryItem>();
        private System.Windows.Threading.DispatcherTimer? _tipTimer;
        private int _currentTipIndex = 0;
        private List<string> _tips = new List<string>();

        public HistoryWindow()
        {
            try
            {
                InitializeComponent();
                this.DataContext = this;
                UpdateUIText();
                
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

                InitializeTips();
                InitializeTipTimer();
                
                CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => 
                {
                    InitializeTips();
                    UpdateUIText();
                };

                // Restore View Mode
                var settings = Models.Settings.Load();
                _historyGridView = LstHistory.View as GridView;
                _currentViewMode = settings.HistoryViewMode;
                ApplyViewMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.GetString("ErrInitHistory")}{ex.Message}\n{ex.StackTrace}", LocalizationManager.GetString("Error"));
            }
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = Settings.Load();
            
            // 크기 복원 시 최소 크기 강제 적용 (짜부되는 현상 방지)
            if (settings.HistoryWindowWidth > 0) 
            {
                this.Width = Math.Max(this.MinWidth, settings.HistoryWindowWidth);
            }
            
            if (settings.HistoryWindowHeight > 0) 
            {
                this.Height = Math.Max(this.MinHeight, settings.HistoryWindowHeight);
            }

            // [Fix] Disable saved position to ensure window opens centered on primary screen
            // if (settings.HistoryWindowLeft != -9999) this.Left = settings.HistoryWindowLeft;
            // if (settings.HistoryWindowTop != -9999) this.Top = settings.HistoryWindowTop;
            
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
            ColMeta.Width = settings.HistoryColMeta; // Restored
            ColPin.Width = settings.HistoryColPin;
            ColFavorite.Width = settings.HistoryColFavorite;
            ColActions.Width = settings.HistoryColActions;
            // ColMemo Removed (Old location) - It's now in main columns
            if (ColMemo != null) ColMemo.Width = settings.HistoryColMemo; // Safety check
        }

        private void HistoryWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = Settings.Load();
            
            // ★ 중요: 창이 Normal 상태일 때만 크기/위치 저장 (최소화 상태 저장 방지)
            // 비정상적으로 작아진 상태도 저장하지 않도록 최소 크기 체크 추가
            if (this.WindowState == WindowState.Normal && this.Width >= this.MinWidth && this.Height >= this.MinHeight)
            {
                settings.HistoryWindowWidth = this.Width;
                settings.HistoryWindowHeight = this.Height;
                // [Fix] Disable saved position to ensure window opens centered on primary screen
                // settings.HistoryWindowLeft = this.Left;
                // settings.HistoryWindowTop = this.Top;
                
                if (MainGrid.ColumnDefinitions.Count >= 4)
                {
                    settings.HistoryPreviewPaneWidth = MainGrid.ColumnDefinitions[3].Width.Value;
                }

                // Save GridView Column Widths
                settings.HistoryColDate = ColDate.Width;
                settings.HistoryColFileName = ColFileName.Width;
                settings.HistoryColMeta = ColMeta.Width; // Restored
                settings.HistoryColPin = ColPin.Width;
                settings.HistoryColFavorite = ColFavorite.Width;
                settings.HistoryColActions = ColActions.Width;
                if (ColMemo != null) settings.HistoryColMemo = ColMemo.Width;
                
                settings.Save();
            }

            if (_tipTimer != null)
            {
                _tipTimer.Stop();
                _tipTimer = null;
            }
        }

        private async void RefreshCounts()
        {
            try
            {
                var counts = await System.Threading.Tasks.Task.Run(() => DatabaseManager.Instance.GetHistorySummaryCounts());
                
                TxtCountAll.Text = counts["All"].ToString();
                TxtCountRecent7.Text = counts["Recent7"].ToString();
                TxtCountRecent30.Text = counts["Recent30"].ToString();
                TxtCountRecent3Months.Text = counts["Recent3Months"].ToString();
                TxtCountRecent6Months.Text = counts["Recent6Months"].ToString();
                TxtCountPinned.Text = counts["Pinned"].ToString();
                TxtCountFavorite.Text = counts["Favorite"].ToString();
                TxtCountTrash.Text = counts["Trash"].ToString();
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

            // 팁 바 업데이트
            UpdateBottomTipBar();

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
                
                // Update Search Results Text
                if (TxtStatusInfo != null)
                {
                    if (!string.IsNullOrEmpty(search))
                    {
                        TxtStatusInfo.Text = string.Format(LocalizationManager.GetString("SearchResults") ?? "검색 결과 {0} ({1})", search, totalCount);
                    }
                    else
                    {
                        // Show filter description when no search
                        string filterKey = _currentFilter switch
                        {
                            "All" => "AllNotes",
                            "Recent7" => "Recent7Days",
                            "Recent30" => "Recent30Days",
                            "Recent3Months" => "Recent3Months",
                            "Recent6Months" => "Recent6Months",
                            "Favorite" => "Favorite",
                            "Trash" => "Trash",
                            _ => "AllNotes"
                        };
                        string filterName = LocalizationManager.GetString(filterKey) ?? _currentFilter;
                        TxtStatusInfo.Text = filterName;
                    }
                }
                
                // 휴지통 비우기 버튼 표시 여부
                if (BtnEmptyTrash != null)
                {
                    BtnEmptyTrash.Visibility = (filter == "Trash") ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"{LocalizationManager.GetString("ErrLoadHistory")}{ex.Message}", LocalizationManager.GetString("Error"));
            }
        }

        private void UpdateUIText()
        {
            this.Title = LocalizationManager.GetString("HistoryWindowTitle") ?? "히스토리 - 캐치캡처";
            if (TxtSearchPlaceholder != null) TxtSearchPlaceholder.Text = LocalizationManager.GetString("HistorySearchPlaceholder") ?? "파일명, 파일내용, 앱 이름 등 검색...";
            if (TxtTotalListLabel != null) TxtTotalListLabel.Text = LocalizationManager.GetString("TotalList") ?? "전체 리스트";
            if (BtnSync != null)
            {
                BtnSync.ToolTip = LocalizationManager.GetString("HistorySyncTooltip") ?? "최신 기록 가져오기";
                if (TxtSync != null) TxtSync.Text = LocalizationManager.GetString("HistorySync") ?? "동기화";
            }
            
            // Preview labels
            if (TxtSimpleMemoLabel != null) TxtSimpleMemoLabel.Text = LocalizationManager.GetString("SimpleMemo") ?? "간단 메모";
            if (BtnEditMemo != null) BtnEditMemo.Content = LocalizationManager.GetString("Edit") ?? "수정";
            if (TxtDetailsLabel != null) TxtDetailsLabel.Text = LocalizationManager.GetString("Details") ?? "상세 정보";
            if (TxtSizeLabel != null) TxtSizeLabel.Text = LocalizationManager.GetString("SizeLabel") ?? "크기:";
            if (TxtCapacityLabel != null) TxtCapacityLabel.Text = LocalizationManager.GetString("CapacityLabel") ?? "용량:";
            if (TxtAppLabel != null) TxtAppLabel.Text = LocalizationManager.GetString("AppLabel") ?? "앱:";
            if (TxtPathLabel != null) TxtPathLabel.Text = LocalizationManager.GetString("PathLabel") ?? "상세 경로";
            if (TxtTitleLabel != null) TxtTitleLabel.Text = LocalizationManager.GetString("TitleLabel") ?? "전체 제목:";
            if (TxtEmptySelection != null) TxtEmptySelection.Text = LocalizationManager.GetString("SelectAnItemToViewDetails") ?? "상세 내역을 보려면 항목을 선택하세요";

            // Buttons tooltips
            if (BtnPreviewEdit != null) BtnPreviewEdit.ToolTip = LocalizationManager.GetString("ImageEditorTooltip") ?? "이미지 편집";
            if (BtnPreviewNote != null) BtnPreviewNote.ToolTip = LocalizationManager.GetString("SaveToMyNoteTooltip") ?? "내 노트로 저장";
            if (BtnPreviewPin != null) BtnPreviewPin.ToolTip = LocalizationManager.GetString("PinToScreenTooltip") ?? "화면에 고정";
            if (BtnImgPreviewGrid != null) BtnImgPreviewGrid.ToolTip = LocalizationManager.GetString("OpenWithDefaultViewer") ?? "윈도우 기본 뷰어로 열기";

            // Filter buttons
            if (TxtAllFilter != null) TxtAllFilter.Text = LocalizationManager.GetString("All") ?? "전체 기록";
            if (TxtRecent7Filter != null) TxtRecent7Filter.Text = LocalizationManager.GetString("Recent7Days") ?? "최근 7일";
            if (TxtRecent30Filter != null) TxtRecent30Filter.Text = LocalizationManager.GetString("Recent30Days") ?? "최근 30일";
            if (TxtRecent3MonthsFilter != null) TxtRecent3MonthsFilter.Text = LocalizationManager.GetString("Recent3Months") ?? "최근 3개월";
            if (TxtRecent6MonthsFilter != null) TxtRecent6MonthsFilter.Text = LocalizationManager.GetString("Recent6Months") ?? "최근 6개월";
            if (TxtPinnedFilter != null) TxtPinnedFilter.Text = LocalizationManager.GetString("Pinned") ?? "고정됨";
            if (TxtFavoriteFilter != null) TxtFavoriteFilter.Text = LocalizationManager.GetString("Favorite") ?? "즐겨찾기";
            if (TxtTrashFilter != null) TxtTrashFilter.Text = LocalizationManager.GetString("Trash") ?? "휴지통";
            if (TxtSettingsFilter != null) TxtSettingsFilter.Text = LocalizationManager.GetString("HistorySettings") ?? "히스토리 설정";

            if (BtnSelectAll != null) BtnSelectAll.Content = LocalizationManager.GetString("SelectAll") ?? "전체 선택";
            if (BtnDeleteSelected != null) BtnDeleteSelected.Content = LocalizationManager.GetString("DeleteSelected") ?? "선택 삭제";
            if (BtnEmptyTrash != null) BtnEmptyTrash.Content = LocalizationManager.GetString("EmptyTrash") ?? "휴지통 비우기";

            if (BtnViewList != null) BtnViewList.ToolTip = LocalizationManager.GetString("ListView") ?? "리스트형";
            if (BtnViewCard != null) BtnViewCard.ToolTip = LocalizationManager.GetString("CardView") ?? "카드형";

            // [New] Localize TitleBar Title and Grid Headers
            if (TxtTitleBarTitle != null) TxtTitleBarTitle.Text = LocalizationManager.GetString("HistoryTitle") ?? "히스토리";
            
            if (ColDate != null) ColDate.Header = LocalizationManager.GetString("HistoryColDate") ?? "날짜 및 시각";
            if (ColFileName != null) ColFileName.Header = LocalizationManager.GetString("HistoryColFileName") ?? "파일명";
            if (ColMeta != null) ColMeta.Header = LocalizationManager.GetString("HistoryColMeta") ?? "출처/제목";
            // ColMemo seems to be in the grid view in XAML but was commented out in code. Checking XAML... 
            // XAML has ColMemo at line 629. So we should update it.
            if (ColMemo != null) ColMemo.Header = LocalizationManager.GetString("HistoryColMemo") ?? "메모";
            if (ColActions != null) ColActions.Header = LocalizationManager.GetString("HistoryColActions") ?? "작업";

            // Localize Filter Popup
            if (TxtFilterTitle != null) TxtFilterTitle.Text = LocalizationManager.GetString("HistoryFilterTitle");
            if (TxtFilterDateLabel != null) TxtFilterDateLabel.Text = LocalizationManager.GetString("HistoryFilterDate");
            if (TxtFilterFileNameLabel != null) TxtFilterFileNameLabel.Text = LocalizationManager.GetString("HistoryFilterFileName");
            if (TxtFilterFileTypeLabel != null) TxtFilterFileTypeLabel.Text = LocalizationManager.GetString("HistoryFilterFileType");
            if (CbiTypeAll != null) CbiTypeAll.Content = LocalizationManager.GetString("HistoryFilterTypeAll");
            if (CbiTypeImage != null) CbiTypeImage.Content = LocalizationManager.GetString("HistoryFilterTypeImage");
            if (CbiTypeMedia != null) CbiTypeMedia.Content = LocalizationManager.GetString("HistoryFilterTypeMedia");
            if (BtnApplyAdvancedFilter != null) BtnApplyAdvancedFilter.Content = LocalizationManager.GetString("HistoryFilterApply");
        }

        private void InitializeTips()
        {
            _tips = new List<string>();
            for (int i = 1; i <= 6; i++)
            {
                string tip = CatchCapture.Resources.LocalizationManager.GetString($"HistoryTip{i}");
                if (!string.IsNullOrEmpty(tip))
                    _tips.Add(tip);
            }
            if (_tips.Count > 0 && TxtRollingTip != null)
            {
                TxtRollingTip.Text = _tips[_currentTipIndex];
            }
        }

        private void UpdateBottomTipBar()
        {
            if (_currentFilter == "Trash")
            {
                // INFO 모드로 전환
                if (TxtTipLabel != null) TxtTipLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Info") ?? "INFO";
                if (BrdTipLabel != null) BrdTipLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));

                int days = Settings.Load().HistoryTrashRetentionDays;
                if (days > 0)
                    TxtRollingTip.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("TrashRetentionInfo") ?? "...", days);
                else
                    TxtRollingTip.Text = CatchCapture.Resources.LocalizationManager.GetString("TrashRetentionPermanentInfo") ?? "...";
            }
            else
            {
                // TIP 모드로 복구
                if (TxtTipLabel != null) TxtTipLabel.Text = LocalizationManager.GetString("Tip") ?? "TIP";
                if (BrdTipLabel != null) BrdTipLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD"));

                // 현재 팁 표시 (타이머가 돌고 있으므로 인덱스 기반 표시)
                if (_tips != null && _tips.Count > 0 && TxtRollingTip != null)
                {
                    TxtRollingTip.Text = _tips[_currentTipIndex];
                }
            }
        }

        private void InitializeTipTimer()
        {
            _tipTimer = new System.Windows.Threading.DispatcherTimer();
            // ★ 최적화: 유휴 상태 CPU 점유율 감소를 위해 간격 확대 (5초 -> 15초)
            _tipTimer.Interval = TimeSpan.FromSeconds(15);
            _tipTimer.Tick += (s, e) => {
                if (_currentFilter == "Trash") return; // 휴지통에서는 팁 회전 중지

                // ★ 최적화: 창이 활성화된 상태일 때만 애니메이션 및 팁 변경 실행
                if (!this.IsActive) return;

                if (_tips.Count == 0 || TxtRollingTip == null) return;

                _currentTipIndex = (_currentTipIndex + 1) % _tips.Count;
                TxtRollingTip.Opacity = 0;
                TxtRollingTip.Text = _tips[_currentTipIndex];
                
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                TxtRollingTip.BeginAnimation(TextBlock.OpacityProperty, anim);
            };
            _tipTimer.Start();
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
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(2, 0, 2, 0),
                    Style = (Style)FindResource("PaginationButtonStyle"),
                    Tag = (i == _currentPage) ? "Active" : ""
                };
                int targetPage = i;
                btn.Click += (s, e) => {
                    _currentPage = targetPage;
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
            bool anySelected = HistoryItems.Any(i => i.IsSelected);
            bool allSelected = HistoryItems.Count > 0 && HistoryItems.All(i => i.IsSelected);
            BtnSelectAll.Content = allSelected ? LocalizationManager.GetString("UnselectAll") : LocalizationManager.GetString("SelectAll");
            if (ChkHeaderAll != null) ChkHeaderAll.IsChecked = allSelected;

            // Handle CheckBox column visibility in List Mode
            if (CurrentViewMode == 0 && ColCheckBox != null)
            {
                ColCheckBox.Width = anySelected ? 45 : 0;
            }

            OnPropertyChanged(nameof(IsSelectionActive));
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
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

            string msg = _currentFilter == "Trash" ? LocalizationManager.GetString("PermanentDeleteConfirmMessage") : LocalizationManager.GetString("DeleteConfirmMessage");
            if (CustomMessageBox.Show(msg, LocalizationManager.GetString("DeleteConfirmTitle"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                {
                    DatabaseManager.Instance.DeleteCapture(item.Id, _currentFilter == "Trash");
                }
                LoadHistory(_currentFilter, _currentSearch);
                TriggerLiveSync();
            }
        }

        private void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show(LocalizationManager.GetString("EmptyTrashConfirmMessage"), LocalizationManager.GetString("EmptyTrashTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DatabaseManager.Instance.EmptyTrash();
                LoadHistory("Trash");
                TriggerLiveSync();
                CustomMessageBox.Show(LocalizationManager.GetString("EmptyTrashComplete"), LocalizationManager.GetString("Notice"));
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

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowHistorySettings();
            win.ShowDialog();
            // 설정 변경(저장 경로 등)이 있을 수 있으므로 새로고침
            LoadHistory();
            RefreshCounts();
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
                TriggerLiveSync();
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
                TriggerLiveSync();
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
                    CustomMessageBox.Show($"{LocalizationManager.GetString("ErrOpenFile")}{ex.Message}", LocalizationManager.GetString("Error"));
                }
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            HistoryItem? item = (sender as Button)?.DataContext as HistoryItem ?? LstHistory.SelectedItem as HistoryItem;
            if (item == null) return;

            string msg = _currentFilter == "Trash" ? LocalizationManager.GetString("PermanentDeleteConfirmMessage") : LocalizationManager.GetString("DeleteConfirmMessage");
            if (CustomMessageBox.Show(msg, LocalizationManager.GetString("DeleteConfirmTitle"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                DatabaseManager.Instance.DeleteCapture(item.Id, _currentFilter == "Trash");
                LoadHistory(_currentFilter, _currentSearch);
                TriggerLiveSync();
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

        private void LstHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item)
            {
                if (System.IO.File.Exists(item.FilePath))
                {
                    // 이미지 확장자 체크
                    string ext = System.IO.Path.GetExtension(item.FilePath).ToLower();
                    string[] imgExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
                    
                    if (imgExts.Contains(ext))
                    {
                        // 이미지인 경우 단순 프리뷰 창 열기
                        var previewWin = new HistoryItemPreviewWindow(item);
                        previewWin.Show();
                    }
                    else
                    {
                        // 동영상/오디오인 경우 기본 뷰어로 열기
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"{LocalizationManager.GetString("ErrOpenFile")}{ex.Message}", LocalizationManager.GetString("Error"));
                        }
                    }
                }
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

        private async void UpdatePreview(HistoryItem item)
        {
            if (item == null)
            {
                ImgPreview.Source = null;
                PreviewEmptyState.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                PreviewEmptyState.Visibility = Visibility.Collapsed;
                
                // Clear current preview to avoid showing stale data
                ImgPreview.Source = null;
                ImgPreview.Opacity = 0.5;
                
                TxtPreviewPath.Text = item.FilePath;
                
                // Update Memo Display
                TxtMemoDisplay.Text = string.IsNullOrEmpty(item.Memo) ? LocalizationManager.GetString("NoMemo") : item.Memo;
                TxtMemoDisplay.Opacity = string.IsNullOrEmpty(item.Memo) ? 0.4 : 0.8;
                EditMemoBox.Text = item.Memo;
                EditMemoBox.Visibility = Visibility.Collapsed;
                TxtMemoDisplay.Visibility = Visibility.Visible;
                BtnEditMemo.Content = LocalizationManager.GetString("Edit");

                // Default metadata (DB values first)
                TxtPreviewSize.Text = !string.IsNullOrEmpty(item.Resolution) ? item.Resolution : "-";
                TxtPreviewWeight.Text = GetFileSizeString(item.FileSize);
                TxtPreviewApp.Text = item.SourceApp;
                TxtPreviewTitle.Text = item.SourceTitle;

                if (System.IO.File.Exists(item.FilePath))
                {
                    string previewPath = item.FilePath;
                    
                    bool isMedia = item.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                   item.FilePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                   item.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                    
                    bool isAudio = item.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

                    if (isMedia)
                    {
                        BtnPreviewEdit.Visibility = Visibility.Collapsed;
                        BtnPreviewNote.Visibility = Visibility.Collapsed;
                        MediaOverlay.Visibility = Visibility.Visible;
                        
                        TxtPreviewFormat.Text = System.IO.Path.GetExtension(item.FilePath).ToUpper().Replace(".", "");
                        
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

                        string thumbPath = item.FilePath + ".preview.png";
                        if (System.IO.File.Exists(thumbPath))
                        {
                            previewPath = thumbPath;
                        }
                        else
                        {
                            if (isAudio) ImgPreview.Source = null;
                            else ImgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/icons/videocamera.png"));
                            ImgPreview.Opacity = 0.5;
                            previewPath = ""; // No need to load further
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
                        var bitmap = await ThumbnailManager.LoadThumbnailAsync(previewPath, 0); 
                        
                        if (bitmap != null)
                        {
                            ImgPreview.Source = bitmap;
                            ImgPreview.Opacity = 1.0;
                            
                            if (!isMedia)
                            {
                                TxtPreviewSize.Text = $"{bitmap.PixelWidth} x {bitmap.PixelHeight}";
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[UpdatePreview] Failed to load bitmap: {previewPath}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdatePreview] File not found: {previewPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview Load Error: {ex.Message}");
                ImgPreview.Source = null;
            }
        }

        private void ImgPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (LstHistory.SelectedItem is HistoryItem item && System.IO.File.Exists(item.FilePath))
                {
                    // 이미지 확장자 체크
                    string ext = System.IO.Path.GetExtension(item.FilePath).ToLower();
                    string[] imgExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

                    if (imgExts.Contains(ext))
                    {
                        // 이미지인 경우 우리 전용 프리뷰 창 열기
                        var previewWin = new HistoryItemPreviewWindow(item);
                        previewWin.Show();
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"{LocalizationManager.GetString("ErrOpenFile")}{ex.Message}", LocalizationManager.GetString("Error"));
                        }
                    }
                }
            }
        }

        private async void BtnPreviewEdit_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item && System.IO.File.Exists(item.FilePath))
            {
                long selectedId = item.Id; // 현재 ID 저장
                try
                {
                    var bitmap = await Utilities.ThumbnailManager.LoadThumbnailAsync(item.FilePath, 0);
                    if (bitmap == null)
                    {
                         CustomMessageBox.Show(LocalizationManager.GetString("ErrOpenFile"), LocalizationManager.GetString("Error"));
                         return;
                    }
                    
                    var previewWin = new PreviewWindow(bitmap!, 0);
                    previewWin.Owner = this;
                    previewWin.Title = LocalizationManager.GetString("ImageEditorTooltip");
                    
                    // Subscribe to image update event to save changes back to file
                    previewWin.ImageUpdated += (s, ev) =>
                    {
                        try
                        {
                            // Save the edited image back to the original file
                            ScreenCaptureUtility.SaveImageToFile(ev.NewImage, item.FilePath);
                            
                            // Update the UI preview
                            Dispatcher.Invoke(() => {
                                // 리스트를 다시 로드하여 데이터 동기화
                                LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false);
                                
                                // ID로 다시 항목을 찾아 선택 상태 복구
                                var newItem = HistoryItems.FirstOrDefault(i => i.Id == selectedId);
                                if (newItem != null)
                                {
                                    LstHistory.SelectedItem = newItem;
                                    UpdatePreview(newItem);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show($"{LocalizationManager.GetString("ErrSaveImage")}{ex.Message}", LocalizationManager.GetString("Error"));
                        }
                    };
                    
                    previewWin.ShowDialog(); // ShowDialog로 창을 닫을 때까지 대기하도록 하여 포커스 흐트러짐 방지
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"{LocalizationManager.GetString("ErrOpenFile")}{ex.Message}", LocalizationManager.GetString("Error"));
                }
            }
        }

        private async void BtnPreviewNote_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item && System.IO.File.Exists(item.FilePath))
            {
                try
                {
                    BitmapSource? image = ImgPreview.Source as BitmapSource;
                    if (image == null)
                    {
                        image = await Utilities.ThumbnailManager.LoadThumbnailAsync(item.FilePath, 0);
                    }

                    if (image == null) return;
                    
                    var noteWin = new NoteInputWindow(image, item.SourceApp, item.SourceTitle);
                    noteWin.Show();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"{LocalizationManager.GetString("ErrOpenNoteInput")}{ex.Message}", LocalizationManager.GetString("Error"));
                }
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
                TriggerLiveSync();
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
                    BtnEditMemo.Content = LocalizationManager.GetString("Save");
                }
                else
                {
                    // Save
                    string newMemo = EditMemoBox.Text.Trim();
                    DatabaseManager.Instance.UpdateCaptureMemo(item.Id, newMemo);
                    
                    // Reload list to refresh view (e.g. apply FirstLineConverter)
                    long selectedId = item.Id;
                    LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType, false);
                    
                    // Restore Selection
                    var reloadedItem = HistoryItems.FirstOrDefault(i => i.Id == selectedId);
                    if (reloadedItem != null)
                    {
                        LstHistory.SelectedItem = reloadedItem;
                        LstHistory.ScrollIntoView(reloadedItem);
                    }
                    TriggerLiveSync();
                }
            }
        }
        private async void BtnPreviewPin_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is HistoryItem item && System.IO.File.Exists(item.FilePath))
            {
                try
                {
                    BitmapSource? image = ImgPreview.Source as BitmapSource;
                    if (image == null)
                    {
                        image = await Utilities.ThumbnailManager.LoadThumbnailAsync(item.FilePath, 0);
                    }

                    if (image == null) return;
                    
                    var pinnedWindow = new PinnedImageWindow(image);
                    pinnedWindow.Show();
                    
                    // 화면 중앙에 배치
                    pinnedWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"{LocalizationManager.GetString("ErrPinImage")}{ex.Message}", LocalizationManager.GetString("Error"));
                }
            }
        }
        private void ApplyViewMode()
        {
            if (LstHistory == null || _historyGridView == null) return;

            // Save to settings
            var settings = Models.Settings.Load();
            settings.HistoryViewMode = _currentViewMode;
            settings.Save();

            if (_currentViewMode == 0) // List
            {
                LstHistory.View = _historyGridView;
                ScrollViewer.SetHorizontalScrollBarVisibility(LstHistory, ScrollBarVisibility.Auto);
                // Restore ItemsPanel to VirtualizingStackPanel (default for ListView with GridView)
                var template = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
                LstHistory.ItemsPanel = template;
            }
            else // Card
            {
                LstHistory.View = null; // ListBox behavior
                LstHistory.ItemTemplate = (DataTemplate)FindResource("HistoryCardTemplate");
                ScrollViewer.SetHorizontalScrollBarVisibility(LstHistory, ScrollBarVisibility.Disabled);
                
                // Re-introduced VirtualizingWrapPanel
                var factory = new FrameworkElementFactory(typeof(VirtualizingWrapPanel));
                factory.SetValue(VirtualizingWrapPanel.ItemWidthProperty, 210.0);
                factory.SetValue(VirtualizingWrapPanel.ItemHeightProperty, 260.0);
                var template = new ItemsPanelTemplate(factory);
                LstHistory.ItemsPanel = template;
            }

            // Force layout update after panel change
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LstHistory.UpdateLayout();
                // Rebind to force panel to recalculate
                var currentSource = LstHistory.ItemsSource;
                LstHistory.ItemsSource = null;
                LstHistory.ItemsSource = currentSource;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }


        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mode)
            {
                CurrentViewMode = (mode == "List") ? 0 : 1;
            }
        }

        private async void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CatchCapture.CustomMessageBox.Show(
                    LocalizationManager.GetString("SyncConfirm") ?? "클라우드(OneDrive, Dropbox 등) 사용 시 데이터 반영까지 약 1~3분 정도 소요될 수 있습니다.\n\n지금 바로 최신 기록을 가져오시겠습니까?",
                    LocalizationManager.GetString("SyncInfoTitle") ?? "동기화 안내",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    // 수동 동기화: 클라우드에서 최신 데이터를 강제로 받아옴
                    await Task.Run(() => CatchCapture.Utilities.DatabaseManager.Instance.SyncFromCloudToLocal());

                    // 목록 갱신
                    LoadHistory(_currentFilter, _currentSearch, _currentDateFrom, _currentDateTo, _currentFileType);
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("SyncFailed")}{ex.Message}", LocalizationManager.GetString("Error"));
            }
        }

        private System.Threading.CancellationTokenSource? _syncDebounceCts;

        /// <summary>
        /// ★ 최적화: 라이브 싱크 디바운싱 (2초)
        /// 사용자가 연속해서 수정/삭제를 할 경우 마지막 작업 후 2초 뒤에 한 번만 싱크를 수행합니다.
        /// </summary>
        private void TriggerLiveSync()
        {
            _syncDebounceCts?.Cancel();
            _syncDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _syncDebounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000, token); // 2초 대기
                    if (token.IsCancellationRequested) return;

                    DatabaseManager.Instance.SyncToCloud(true);
                    DatabaseManager.Instance.RemoveLock();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    LogToFile($"LiveSync Error: {ex.Message}");
                }
            }, token);
        }

        private void LogToFile(string message)
        {
            try { DatabaseManager.Instance.LogToFile(message); } catch { }
        }
    }
}
