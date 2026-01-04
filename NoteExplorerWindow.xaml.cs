using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using CatchCapture.Utilities;
using Microsoft.Data.Sqlite;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Linq;
using System.ComponentModel;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class NoteExplorerWindow : Window, INotifyPropertyChanged
    {
        private string _currentFilter = "Recent";
        private string? _currentTag = null;
        private string? _currentSearch = "";
        private int _currentPage = 1;
        private const int PAGE_SIZE = 17;
        private int _totalPages = 1;
        private string _currentSortOrder = "n.UpdatedAt DESC";

        private System.Windows.Threading.DispatcherTimer? _tipTimer;
        private int _currentTipIndex = 0;
        private List<string> _tips = new List<string>();

        private void InitializeTips()
        {
            _tips = new List<string>();
            for (int i = 1; i <= 9; i++)
            {
                // Fallback for Korean if not in resx?
                // For now, assuming standard localization, we just fetch from Manager.
                // Note: If running in Korean environment and ko.resx is missing these keys, it might fallback to default.
                // However, based on task, we prioritize removing hardcoded strings.
                
                string tip = CatchCapture.Resources.LocalizationManager.GetString($"Tip{i}");
                _tips.Add(tip);
            }
        }

        private bool _isSelectionMode;
        public bool IsSelectionMode
        {
            get { return _isSelectionMode; }
            set
            {
                if (_isSelectionMode != value)
                {
                    _isSelectionMode = value;
                    OnPropertyChanged(nameof(IsSelectionMode));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _currentViewMode = 0; // 0: List, 1: Card
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

        public static NoteExplorerWindow? Instance { get; private set; }

        public NoteExplorerWindow()
        {
            try
            {
                Instance = this;
                InitializeComponent();
                WindowHelper.FixMaximizedWindow(this);
                this.DataContext = this;
                this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
                
                InitializeTips();
                UpdateUIText();
                LoadWindowState();

                // Centralized Hyperlink Click Handler for Preview Area
                PreviewViewer.PreviewMouseLeftButtonDown += PreviewViewer_PreviewMouseLeftButtonDown;
                
                LoadNotes(filter: "Recent");
                LoadTags();
                InitializeTipTimer();
                HighlightSidebarButton(BtnFilterRecent);

                // Load saved view mode
                _currentViewMode = Settings.Load().NoteExplorerViewMode;
                ApplyViewMode();
                
                // 휴지통 자동 비우기 (설정된 기간 경과 항목)
                int retentionDays = Settings.Load().TrashRetentionDays;
                if (retentionDays > 0)
                {
                    DatabaseManager.Instance.CleanupTrash(retentionDays);
                }
                
                CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrInitExplorer") + $"{ex.Message}\n{ex.StackTrace}", CatchCapture.Resources.LocalizationManager.GetString("Error"));
            }
        }

        private void LoadWindowState()
        {
            try
            {
                var settings = Settings.Load();
                
                // Restore window size
                if (settings.NoteExplorerWidth > 0)
                    this.Width = settings.NoteExplorerWidth;
                if (settings.NoteExplorerHeight > 0)
                    this.Height = settings.NoteExplorerHeight;
                
                // Restore window position
                /* [Fix] Disabled to always center on primary screen
                if (settings.NoteExplorerLeft > -9999 && settings.NoteExplorerTop > -9999)
                {
                    this.Left = settings.NoteExplorerLeft;
                    this.Top = settings.NoteExplorerTop;
                }
                */
                
                // Restore splitter position (ColNoteList width)
                if (settings.NoteExplorerSplitterPosition > 0)
                {
                    ColNoteList.Width = new GridLength(settings.NoteExplorerSplitterPosition, GridUnitType.Pixel);
                }
            }
            catch { /* Ignore errors, use defaults */ }
        }

        private void SaveWindowState()
        {
            try
            {
                var settings = Settings.Load();
                
                // Save window size (only if not maximized)
                if (this.WindowState == WindowState.Normal)
                {
                    settings.NoteExplorerWidth = this.ActualWidth;
                    settings.NoteExplorerHeight = this.ActualHeight;
                    // [Fix] Disabled to always center on primary screen
                    // settings.NoteExplorerLeft = this.Left;
                    // settings.NoteExplorerTop = this.Top;
                }
                
                // Save splitter position (ColNoteList actual width)
                if (ColNoteList != null && ColNoteList.ActualWidth > 0)
                {
                    settings.NoteExplorerSplitterPosition = ColNoteList.ActualWidth;
                }
                
                settings.Save();
            }
            catch { /* Ignore save errors */ }
        }

        private void UpdateUIText()
        {
            this.Title = CatchCapture.Resources.LocalizationManager.GetString("NoteExplorerTitle") ?? "캐치캡처 노트 탐색기";
            
            if (TxtMyNoteTitle != null) TxtMyNoteTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("MyNote");
            if (TxtNewNoteLabel != null) TxtNewNoteLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("NewNote");
            if (BtnNoteSettingsText != null) BtnNoteSettingsText.Text = CatchCapture.Resources.LocalizationManager.GetString("NoteSettingsMenu");
            if (TxtExploreTitle != null) TxtExploreTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("Explore");
            if (TxtFilterAllText != null) TxtFilterAllText.Text = CatchCapture.Resources.LocalizationManager.GetString("AllNotes");
            if (TxtFilterGroupText != null) TxtFilterGroupText.Text = CatchCapture.Resources.LocalizationManager.GetString("GroupNote");
            if (TxtFilterTodayText != null) TxtFilterTodayText.Text = CatchCapture.Resources.LocalizationManager.GetString("Today");
            if (TxtFilterRecentText != null) TxtFilterRecentText.Text = CatchCapture.Resources.LocalizationManager.GetString("Recent7Days");
            if (TxtFilterTrashText != null) TxtFilterTrashText.Text = CatchCapture.Resources.LocalizationManager.GetString("Trash");
            if (TxtTagsTitle != null) TxtTagsTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("Tags");
            
            if (BtnSelectAll != null) BtnSelectAll.Content = CatchCapture.Resources.LocalizationManager.GetString("SelectAll");
            if (BtnDeleteSelected != null) BtnDeleteSelected.Content = CatchCapture.Resources.LocalizationManager.GetString("DeleteSelected");
            if (TxtStatusInfo != null) TxtStatusInfo.Text = CatchCapture.Resources.LocalizationManager.GetString("RecentStatusInfo");
            if (TxtSearchPlaceholder != null) TxtSearchPlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("SearchPlaceholder");
            if (TxtEmptyTrash != null) TxtEmptyTrash.Text = CatchCapture.Resources.LocalizationManager.GetString("EmptyTrash") ?? "휴지통 비우기";
            
            UpdateBottomTipBar();
            
            if (ColHeaderGroup != null) ColHeaderGroup.Text = CatchCapture.Resources.LocalizationManager.GetString("ColGroup");
            if (ColHeaderTitle != null) ColHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("ColTitle");
            if (ColHeaderContent != null) ColHeaderContent.Text = CatchCapture.Resources.LocalizationManager.GetString("ColContent");
            if (ColHeaderModified != null) ColHeaderModified.Text = CatchCapture.Resources.LocalizationManager.GetString("ColLastModified");
            
            if (ParaPreviewLoading != null) ParaPreviewLoading.Inlines.Clear();
            if (ParaPreviewLoading != null) ParaPreviewLoading.Inlines.Add(CatchCapture.Resources.LocalizationManager.GetString("PreviewLoading"));
            if (BtnEditNoteText != null) BtnEditNoteText.Text = CatchCapture.Resources.LocalizationManager.GetString("EditNote");
            
            if (BtnViewList != null) BtnViewList.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ListView");
            if (BtnViewCard != null) BtnViewCard.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("CardView");

            if (TxtRecent30Days != null) TxtRecent30Days.Text = CatchCapture.Resources.LocalizationManager.GetString("Recent30Days");
            if (TxtRecent3Months != null) TxtRecent3Months.Text = CatchCapture.Resources.LocalizationManager.GetString("Recent3Months");
            if (TxtRecent6Months != null) TxtRecent6Months.Text = CatchCapture.Resources.LocalizationManager.GetString("Recent6Months");

            if (BtnMinimize != null) BtnMinimize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Minimize");
            if (BtnMaximize != null)
            {
                BtnMaximize.ToolTip = this.WindowState == WindowState.Maximized 
                    ? CatchCapture.Resources.LocalizationManager.GetString("Restore") 
                    : CatchCapture.Resources.LocalizationManager.GetString("Maximize");
            }
            if (BtnClose != null) BtnClose.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Close");

            if (MenuSortLatest != null) MenuSortLatest.Header = CatchCapture.Resources.LocalizationManager.GetString("SortByLatest");
            if (MenuSortName != null) MenuSortName.Header = CatchCapture.Resources.LocalizationManager.GetString("SortByName");
            
            InitializeTips(); // Reload tips for current language
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SaveWindowState();
            Instance = null;
            
            if (_tipTimer != null)
            {
                _tipTimer.Stop();
                _tipTimer = null;
            }
            
            // 노트 탐색기 닫을 때 메인 윈도우 표시 (종료 중이 아닐 때만)
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null && !mainWindow.isExit)
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Show();
                mainWindow.Activate();
            }
        }

        private async void UpdateSidebarCounts()
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    int all = 0, today = 0, recent = 0, trash = 0;
                    var catCounts = new Dictionary<long, int>();

                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        
                        using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0", connection))
                            all = Convert.ToInt32(cmd.ExecuteScalar());

                        using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND date(CreatedAt, 'localtime') = date('now', 'localtime')", connection))
                            today = Convert.ToInt32(cmd.ExecuteScalar());

                        using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND CreatedAt >= date('now', 'localtime', '-7 days')", connection))
                            recent = Convert.ToInt32(cmd.ExecuteScalar());

                        using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 1", connection))
                            trash = Convert.ToInt32(cmd.ExecuteScalar());

                        using (var cmd = new SqliteCommand("SELECT CategoryId, COUNT(*) FROM Notes WHERE Status = 0 GROUP BY CategoryId", connection))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0)) catCounts[reader.GetInt64(0)] = reader.GetInt32(1);
                            }
                        }
                    }

                    var categories = DatabaseManager.Instance.GetAllCategories();
                    var categoryItems = categories.Select(cat => new CategorySidebarItem
                    {
                        Id = cat.Id,
                        Name = cat.Id == 1 ? CatchCapture.Resources.LocalizationManager.GetString("DefaultCategory") : cat.Name,
                        Color = cat.Color,
                        Count = catCounts.ContainsKey(cat.Id) ? catCounts[cat.Id] : 0
                    }).ToList();

                    return (all, today, recent, trash, categoryItems);
                });

                TxtCountAll.Text = $"({result.all})";
                TxtCountToday.Text = $"({result.today})";
                TxtCountRecent.Text = $"({result.recent})";
                TxtCountTrash.Text = $"({result.trash})";
                ItemsCategories.ItemsSource = result.categoryItems;
            }
            catch { }
        }

        private void LoadTags()
        {
            try
            {
                var tags = DatabaseManager.Instance.GetAllTags();
                ItemsTags.ItemsSource = tags;
            }
            catch { }
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
                PathMaximize.Data = Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
                BtnMaximize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Maximize");
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                PathMaximize.Data = Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,12V18H14V12H6Z");
                BtnMaximize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Restore");
            }
        }

        private void NoteExplorerWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                // Balanced distribution (approx 850px each on 1080p)
                if (ColNoteList != null) ColNoteList.Width = new GridLength(1, GridUnitType.Star);
                if (ColPreview != null) ColPreview.Width = new GridLength(1, GridUnitType.Star);
                
                if (PathMaximize != null)
                    PathMaximize.Data = Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,12V18H14V12H6Z");
            }
            else
            {
                // Default ratios for normal window
                if (ColNoteList != null) ColNoteList.Width = new GridLength(2, GridUnitType.Star);
                if (ColPreview != null) ColPreview.Width = new GridLength(1.2, GridUnitType.Star);
                
                if (PathMaximize != null)
                    PathMaximize.Data = Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _tipTimer?.Stop();
            this.Close();
        }

        private void BtnNoteSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = this;
            settingsWin.ShowNoteSettings();
            if (settingsWin.ShowDialog() == true)
            {
                // Settings might have changed (e.g. storage path), refresh if needed
                LoadNotes();
            }
        }

        public void RefreshNotes()
        {
            LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
        }

        private void InitializeTipTimer()
        {
            _tipTimer = new System.Windows.Threading.DispatcherTimer();
            _tipTimer.Interval = TimeSpan.FromSeconds(5);
            _tipTimer.Tick += (s, e) => {
                if (_currentFilter == "Trash") return;

                _currentTipIndex = (_currentTipIndex + 1) % _tips.Count;
                TxtRollingTip.Opacity = 0;
                TxtRollingTip.Text = _tips[_currentTipIndex];
                
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                TxtRollingTip.BeginAnimation(TextBlock.OpacityProperty, anim);
            };
            _tipTimer.Start();
        }

        private string BuildWhereClause(string filter, string? tag, string? search, out string joinSql)
        {
            List<string> wheres = new List<string>();
            if (filter == "Trash") wheres.Add("n.Status = 1");
            else
            {
                wheres.Add("n.Status = 0");
                if (filter == "Today") wheres.Add("date(n.CreatedAt, 'localtime') = date('now', 'localtime')");
                else if (filter == "Recent") wheres.Add("n.CreatedAt >= date('now', 'localtime', '-7 days')");
                else if (filter == "Recent30Days") wheres.Add("n.CreatedAt >= date('now', 'localtime', '-30 days')");
                else if (filter == "Recent3Months") wheres.Add("n.CreatedAt >= date('now', 'localtime', '-3 months')");
                else if (filter == "Recent6Months") wheres.Add("n.CreatedAt >= date('now', 'localtime', '-6 months')");
                else if (filter.StartsWith("Category:")) wheres.Add("n.CategoryId = " + filter.Split(':')[1]);
                else if (filter == "Default") wheres.Add("n.CategoryId = 1");
            }

            if (!string.IsNullOrEmpty(search)) wheres.Add("(n.Title LIKE $search OR n.Content LIKE $search)");

            joinSql = "";
            if (!string.IsNullOrEmpty(tag))
            {
                joinSql = " JOIN NoteTags nt ON n.Id = nt.NoteId JOIN Tags t ON nt.TagId = t.Id";
                wheres.Add("t.Name = $tag");
            }

            return wheres.Count > 0 ? " WHERE " + string.Join(" AND ", wheres) : "";
        }

        public async void LoadNotes(string filter = "All", string? tag = null, string? search = null, int page = 1)
        {
            try
            {
                _currentFilter = filter;
                _currentTag = tag;
                _currentSearch = search;
                _currentPage = page;

                // Update Status Text
                if (!string.IsNullOrEmpty(search)) TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("SearchResults"), search, "...");
                else if (!string.IsNullOrEmpty(tag)) TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("TagFilter"), tag);
                else if (filter == "Today") TxtStatusInfo.Text = CatchCapture.Resources.LocalizationManager.GetString("TodayRecords");
                else if (filter == "Recent") TxtStatusInfo.Text = CatchCapture.Resources.LocalizationManager.GetString("RecentWeekRecords");
                else if (filter == "Recent30Days") TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("RecentRecordsFormat"), CatchCapture.Resources.LocalizationManager.GetString("Recent30Days"));
                else if (filter == "Recent3Months") TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("RecentRecordsFormat"), CatchCapture.Resources.LocalizationManager.GetString("Recent3Months"));
                else if (filter == "Recent6Months") TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("RecentRecordsFormat"), CatchCapture.Resources.LocalizationManager.GetString("Recent6Months"));
                else if (filter == "Trash") TxtStatusInfo.Text = CatchCapture.Resources.LocalizationManager.GetString("TrashRecords");
                else if (filter.StartsWith("Category:"))
                {
                    long catId = long.Parse(filter.Split(':')[1]);
                    var cat = DatabaseManager.Instance.GetCategory(catId);
                    TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("GroupLabel"), cat?.Name ?? CatchCapture.Resources.LocalizationManager.GetString("Unknown"));
                }
                else TxtStatusInfo.Text = CatchCapture.Resources.LocalizationManager.GetString("AllRecords");
                
                // Show/Hide Empty Trash Button
                if (BtnEmptyTrash != null)
                {
                    BtnEmptyTrash.Visibility = (filter == "Trash") ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateBottomTipBar();

                var result = await Task.Run(() =>
                {
                    var notesList = new List<NoteViewModel>();
                    int total = 0;
                    string imgDir = DatabaseManager.Instance.GetImageFolderPath();

                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();

                        string whereClause = BuildWhereClause(filter, tag, search, out string joinSql);
                        
                        // 1. Get Total Count
                        string countSql = $@"SELECT COUNT(DISTINCT n.Id) FROM Notes n {joinSql} {whereClause}";
                        using (var command = new SqliteCommand(countSql, connection))
                        {
                            if (!string.IsNullOrEmpty(tag)) command.Parameters.AddWithValue("$tag", tag);
                            if (!string.IsNullOrEmpty(search)) command.Parameters.AddWithValue("$search", $"%{search}%");
                            total = Convert.ToInt32(command.ExecuteScalar());
                        }

                        if (total == 0) return (notesList: notesList, total: 0);

                        // 2. Load Page of Notes
                        string sql = $@"
                            SELECT n.Id, n.Title, n.Content, datetime(n.CreatedAt, 'localtime'), n.SourceApp, n.ContentXaml,
                                   c.Name as CategoryName, c.Color as CategoryColor, datetime(n.UpdatedAt, 'localtime'), n.Status, n.IsPinned, n.CategoryId
                            FROM Notes n
                            LEFT JOIN Categories c ON n.CategoryId = c.Id
                            {joinSql}
                            {whereClause}
                            ORDER BY n.IsPinned DESC, {_currentSortOrder}
                            LIMIT $limit OFFSET $offset";

                        using (var command = new SqliteCommand(sql, connection))
                        {
                            if (!string.IsNullOrEmpty(tag)) command.Parameters.AddWithValue("$tag", tag);
                            if (!string.IsNullOrEmpty(search)) command.Parameters.AddWithValue("$search", $"%{search}%");
                            command.Parameters.AddWithValue("$limit", PAGE_SIZE);
                            command.Parameters.AddWithValue("$offset", (page - 1) * PAGE_SIZE);

                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long noteId = reader.GetInt64(0);
                                    long catId = reader.IsDBNull(11) ? 1 : reader.GetInt64(11);
                                    string catName = reader.IsDBNull(6) ? CatchCapture.Resources.LocalizationManager.GetString("DefaultCategory") : reader.GetString(6);
                                    if (catId == 1) catName = CatchCapture.Resources.LocalizationManager.GetString("DefaultCategory");

                                    notesList.Add(new NoteViewModel
                                    {
                                        Id = noteId,
                                        Title = reader.IsDBNull(1) ? CatchCapture.Resources.LocalizationManager.GetString("Untitled") : reader.GetString(1),
                                        PreviewContent = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        CreatedAt = reader.GetDateTime(3),
                                        SourceApp = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                        ContentXaml = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                        CategoryName = catName,
                                        CategoryColor = reader.IsDBNull(7) ? "#8E2DE2" : reader.GetString(7),
                                        UpdatedAt = reader.GetDateTime(8),
                                        Status = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                        IsPinned = reader.IsDBNull(10) ? false : reader.GetInt32(10) == 1
                                    });
                                }
                            }
                        }

                        // 3. Batch Load Details
                        if (notesList.Count > 0)
                        {
                            var noteIds = notesList.Select(n => n.Id).ToList();
                            string idList = string.Join(",", noteIds);

                            // Images (also store paths)
                            var imagesDict = new Dictionary<long, List<BitmapSource>>();
                            var imagePathsDict = new Dictionary<long, List<string>>();
                            
                            // [최적화 1] DB에서 메타데이터만 먼저 빠르게 로드
                            var pendingImages = new List<(long NoteId, string FileName)>();
                            string imgSql = $"SELECT NoteId, FilePath FROM NoteImages WHERE NoteId IN ({idList}) ORDER BY OrderIndex ASC";
                            using (var cmd = new SqliteCommand(imgSql, connection))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    pendingImages.Add((reader.GetInt64(0), reader.GetString(1)));
                                }
                            }

                            // [최적화 2] 이미지 파일 로딩을 병렬 처리 (클라우드 환경에서 매우 효과적)
                            // 동시성 제어를 위해 lock 사용
                            object dictLock = new object();
                            
                            System.Threading.Tasks.Parallel.ForEach(pendingImages, new ParallelOptions { MaxDegreeOfParallelism = 8 }, item =>
                            {
                                string full = Path.Combine(imgDir, item.FileName);
                                if (File.Exists(full))
                                {
                                    // [최적화 3] 썸네일 크기 축소 (150px)
                                    var bmp = LoadBitmapOptimized(full);
                                    
                                    lock (dictLock)
                                    {
                                        if (bmp != null)
                                        {
                                            if (!imagesDict.ContainsKey(item.NoteId)) imagesDict[item.NoteId] = new List<BitmapSource>();
                                            imagesDict[item.NoteId].Add(bmp);
                                        }
                                        // Always store path
                                        if (!imagePathsDict.ContainsKey(item.NoteId)) imagePathsDict[item.NoteId] = new List<string>();
                                        imagePathsDict[item.NoteId].Add(full);
                                    }
                                }
                            });

                            // Tags
                            var tagsDict = new Dictionary<long, List<string>>();
                            string tagBatchSql = $@"SELECT nt.NoteId, t.Name FROM NoteTags nt JOIN Tags t ON nt.TagId = t.Id WHERE nt.NoteId IN ({idList})";
                            using (var cmd = new SqliteCommand(tagBatchSql, connection))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long nid = reader.GetInt64(0);
                                    string tname = reader.GetString(1);
                                    if (!tagsDict.ContainsKey(nid)) tagsDict[nid] = new List<string>();
                                    tagsDict[nid].Add(tname);
                                }
                            }

                            // Attachments
                            var attachDict = new Dictionary<long, List<NoteAttachment>>();
                            string attBatchSql = $"SELECT Id, NoteId, FilePath, OriginalName, FileType FROM NoteAttachments WHERE NoteId IN ({idList})";
                            using (var cmd = new SqliteCommand(attBatchSql, connection))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long attId = reader.GetInt64(0);
                                    long nid = reader.GetInt64(1);
                                    string filePath = reader.GetString(2);
                                    string originalName = reader.GetString(3);
                                    string fileType = reader.IsDBNull(4) ? "" : reader.GetString(4);

                                    if (!attachDict.ContainsKey(nid)) attachDict[nid] = new List<NoteAttachment>();
                                    attachDict[nid].Add(new NoteAttachment
                                    {
                                        Id = attId,
                                        NoteId = nid,
                                        FilePath = filePath,
                                        OriginalName = originalName,
                                        FileType = fileType
                                    });
                                }
                            }

                            foreach (var n in notesList)
                            {
                                if (imagesDict.ContainsKey(n.Id)) n.Images = imagesDict[n.Id];
                                if (imagePathsDict.ContainsKey(n.Id)) n.ImageFilePaths = imagePathsDict[n.Id];
                                n.Thumbnail = n.Images?.FirstOrDefault();
                                if (tagsDict.ContainsKey(n.Id)) n.Tags = tagsDict[n.Id];
                                if (attachDict.ContainsKey(n.Id)) n.Attachments = attachDict[n.Id];
                            }
                        }
                    }
                    return (notesList: notesList, total: total);
                });

                LstNotes.ItemsSource = result.notesList;
                
                // Toggle Empty State UI
                if (result.notesList.Count == 0)
                {
                    PanelEmptyState.Visibility = Visibility.Visible;
                    TxtEmptyState.Text = (_currentFilter == "Trash") 
                        ? CatchCapture.Resources.LocalizationManager.GetString("TrashEmpty") ?? "휴지통이 비어 있습니다"
                        : CatchCapture.Resources.LocalizationManager.GetString("NoNotes") ?? "노트가 없습니다";
                }
                else
                {
                    PanelEmptyState.Visibility = Visibility.Collapsed;
                }

                UpdatePaginationButtons(result.total);
                
                // Update Status Text with real count if it's a search
                if (!string.IsNullOrEmpty(_currentSearch))
                {
                    TxtStatusInfo.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("SearchResults"), _currentSearch, result.total);
                }
                
                // Auto-select first item if exists
                if (result.notesList.Count > 0)
                {
                    LstNotes.SelectedIndex = 0;
                }

                UpdateSidebarCounts();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{CatchCapture.Resources.LocalizationManager.GetString("ErrLoadNotes")} {ex.Message}", CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (PanelLoading != null) PanelLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateBottomTipBar()
        {
            if (TxtRollingTip == null || TxtTipLabel == null || BrdTipLabel == null) return;

            if (_currentFilter == "Trash")
            {
                // Show Trash Info in the Tip Bar
                BrdTipLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")); // Reddish for info/warning
                TxtTipLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Info") ?? "INFO";
                
                int days = Settings.Load().TrashRetentionDays;
                if (days > 0)
                {
                    TxtRollingTip.Text = string.Format(CatchCapture.Resources.LocalizationManager.GetString("TrashRetentionInfo") ?? "중요사항: 현재 휴지통 보관기간이 {0}일로 설정되었습니다. 설정을 변경하려면 노트 설정에서 수정할 수 있습니다.", days);
                }
                else
                {
                    TxtRollingTip.Text = CatchCapture.Resources.LocalizationManager.GetString("TrashRetentionPermanentInfo") ?? "중요사항: 현재 휴지통이 영구 보관되도록 설정되었습니다. 설정을 변경하려면 노트 설정에서 수정할 수 있습니다.";
                }
                TxtRollingTip.Opacity = 1;
            }
            else
            {
                // Restore Tip Display
                BrdTipLabel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E2DE2")); // Original Purple
                TxtTipLabel.Text = "TIP";
                
                if (_tips.Count > 0)
                {
                    TxtRollingTip.Text = _tips[_currentTipIndex];
                }
                TxtRollingTip.Opacity = 1;
            }
        }

        private BitmapSource? LoadBitmapOptimized(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    // [최적화] 썸네일 크기 축소 400 -> 150 (리스트 뷰에 충분)
                    bitmap.DecodePixelWidth = 150; 
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch { return null; }
        }

        private void UpdatePaginationButtons(int totalCount)
        {
            PanelPagination.Children.Clear();
            _totalPages = (int)Math.Ceiling((double)totalCount / PAGE_SIZE);
            if (_totalPages < 1) _totalPages = 1;

            TxtPageInfo.Text = $"{_currentPage} / {_totalPages}";

            // Generate numeric page buttons (simple version with current and neighbors)
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
                    LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
                };
                PanelPagination.Children.Add(btn);
            }

            BtnFirstPage.IsEnabled = _currentPage > 1;
            BtnPrevPage.IsEnabled = _currentPage > 1;
            BtnNextPage.IsEnabled = _currentPage < _totalPages;
            BtnLastPage.IsEnabled = _currentPage < _totalPages;
        }

        private void BtnFirstPage_Click(object sender, RoutedEventArgs e) { _currentPage = 1; LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage); }
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) { _currentPage--; LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage); } }
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) { if (_currentPage < _totalPages) { _currentPage++; LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage); } }
        private void BtnLastPage_Click(object sender, RoutedEventArgs e) { _currentPage = _totalPages; LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage); }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoadNotes(_currentFilter, _currentTag, TxtSearch.Text, 1);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadNotes(_currentFilter, _currentTag, TxtSearch.Text, 1);
        }


        private void LstNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstNotes.SelectedItem is NoteViewModel note)
            {
                PreviewGrid.Visibility = Visibility.Visible;
                TxtPreviewTitle.Text = note.Title;
                TxtPreviewApp.Text = string.IsNullOrEmpty(note.SourceApp) ? CatchCapture.Resources.LocalizationManager.GetString("DirectWrite") : note.SourceApp;
                TxtPreviewDate.Text = note.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                PreviewTags.ItemsSource = note.Tags;
                PreviewAttachments.ItemsSource = note.Attachments;
                
                // Use FlowDocument to show EXACT interleaved order (matches Viewer)
                if (!string.IsNullOrEmpty(note.ContentXaml))
                {
                    try
                    {
                        // [Fix] Multi-computer Cloud Sync: Redirect image paths
                        string correctedXaml = note.ContentXaml;
                        string currentImgFolder = DatabaseManager.Instance.GetImageFolderPath();
                        var regex = new System.Text.RegularExpressions.Regex(@"Source=""([^""]*[\\/]img[\\/]([^""]+))""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        correctedXaml = regex.Replace(note.ContentXaml, match =>
                        {
                            string relativePath = match.Groups[2].Value;
                            string fullPath = System.IO.Path.Combine(currentImgFolder, relativePath);
                            return $@"Source=""{fullPath}""";
                        });

                        var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Parse(correctedXaml);
                        flowDocument.PagePadding = new Thickness(0, 0, 10, 0); // Add slight right padding for scrollbar
                        flowDocument.PageWidth = double.NaN; // Allow auto-width
                        flowDocument.FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
                        flowDocument.FontSize = 15;
                        
                        // Compact preview layout
                        foreach (var block in flowDocument.Blocks)
                        {
                            block.Margin = new Thickness(0, 2, 0, 2);
                            
                            if (block is BlockUIContainer container)
                            {
                                    Grid? grid = null;
                                    if (container.Child is Border border && border.Child is Grid g1) grid = g1;
                                    else if (container.Child is Grid g2) grid = g2;

                                    bool handled = false;

                                    if (grid != null)
                                    {
                                        string? filePath = grid.Tag?.ToString();

                                        // Tag가 없거나 유튜브 주소가 아니면 FilePathHolder에서 찾기
                                        var pathHolder = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag?.ToString() == "FilePathHolder" || t.Name == "FilePathHolder" || t.Text.StartsWith("http") || t.Text.Contains("\\") || t.Text.Contains("/"));
                                        if (pathHolder != null) filePath = pathHolder.Text;

                                        // 1. 유튜브/웹 링크 처리 - 브라우저로 열기
                                        if (!string.IsNullOrEmpty(filePath) && (filePath.StartsWith("http://") || filePath.StartsWith("https://")))
                                        {
                                            grid.Cursor = Cursors.Hand;
                                            grid.PreviewMouseLeftButtonDown += (s, ev) =>
                                            {
                                                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
                                                ev.Handled = true;
                                            };
                                            handled = true;
                                        }
                                        // 2. 로컬 미디어 파일 처리 - 더블클릭으로 기본 프로그램 열기
                                        else if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                                        {
                                            grid.Cursor = Cursors.Hand;
                                            grid.MouseLeftButtonDown += (s, ev) =>
                                            {
                                                if (ev.ClickCount == 2)
                                                {
                                                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
                                                    ev.Handled = true;
                                                }
                                            };
                                            handled = true;
                                        }
                                    }

                                    // 3. 일반 이미지 처리 - PreviewWindow 팝업
                                    if (!handled)
                                    {
                                        Image? img = null;
                                        if (container.Child is Image i) img = i;
                                        else if (grid != null) img = grid.Children.OfType<Image>().FirstOrDefault();
                                        else if (container.Child is Grid g) img = g.Children.OfType<Image>().FirstOrDefault();

                                        if (img != null && img.Source is BitmapSource bitmap)
                                        {
                                            // Handle Image Size: If Width is NaN (Original) or weird, clamp it.
                                            // If user set a specific width (e.g. via slider), it will be preserved.
                                            if (double.IsNaN(img.Width) || img.Width == 0 || img.Width > 1200)
                                            {
                                                img.Width = (bitmap.PixelWidth < 360) ? bitmap.PixelWidth : 360;
                                            }
                                            
                                            // Ensure Image fits in container but allows resizing up to page width
                                            img.Stretch = Stretch.Uniform;
                                            
                                            img.Cursor = Cursors.Hand;
                                            img.PreviewMouseLeftButtonDown += (s, ev) =>
                                            {
                                                string? path = null;
                                                if (img.Tag != null) path = img.Tag.ToString();
                                                else if (img.Source is BitmapImage bi && bi.UriSource != null) path = bi.UriSource.LocalPath;

                                                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                                {
                                                    try 
                                                    {
                                                        var viewer = new ImageViewerWindow(path);
                                                        viewer.Owner = Window.GetWindow(this);
                                                        viewer.ShowDialog();
                                                    } 
                                                    catch { }
                                                }
                                            };
                                        }
                                    }
                            }
                        }

                        // [Fix] Restore image sources from stored file paths
                        // XamlWriter cannot serialize in-memory BitmapSource, so we need to reload from files
                        if (note.ImageFilePaths != null && note.ImageFilePaths.Count > 0)
                        {
                            int imageIndex = 0;
                            foreach (var block in flowDocument.Blocks.ToList())
                            {
                                if (block is BlockUIContainer container)
                                {
                                    Image? img = null;
                                    Grid? grid = null;
                                    
                                    if (container.Child is Grid g)
                                    {
                                        grid = g;
                                        img = g.Children.OfType<Image>().FirstOrDefault();
                                    }
                                    else if (container.Child is Border border && border.Child is Grid g2)
                                    {
                                        grid = g2;
                                        img = g2.Children.OfType<Image>().FirstOrDefault();
                                    }
                                    else if (container.Child is Image i)
                                    {
                                        img = i;
                                    }
                                    
                                    // Skip video/media placeholders (they have FilePathHolder)
                                    if (grid != null)
                                    {
                                        var pathHolder = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag?.ToString() == "FilePathHolder");
                                        if (pathHolder != null) continue;
                                    }
                                    
                                    // Restore image source if missing
                                    if (img != null && img.Source == null && imageIndex < note.ImageFilePaths.Count)
                                    {
                                        string fullPath = note.ImageFilePaths[imageIndex];
                                        if (File.Exists(fullPath))
                                        {
                                            try
                                            {
                                                var bitmap = new BitmapImage();
                                                bitmap.BeginInit();
                                                bitmap.UriSource = new Uri(fullPath);
                                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                                bitmap.EndInit();
                                                bitmap.Freeze();
                                                img.Source = bitmap;
                                                img.Tag = fullPath;
                                            }
                                            catch { }
                                        }
                                        imageIndex++;
                                    }
                                    else if (img != null && img.Source != null)
                                    {
                                        // Image already has source, just increment index
                                        imageIndex++;
                                    }
                                }
                            }
                        }

                        PreviewViewer.Document = flowDocument;
                        HookHyperlinks(flowDocument); // Set hand cursors
                    }
                    catch
                    {
                        SetPlainTextPreview(note.PreviewContent);
                    }
                }
                else
                {
                    SetPlainTextPreview(note.PreviewContent);
                }
            }
            else
            {
                PreviewGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void Attachment_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is NoteAttachment attachment)
            {
                try
                {
                    string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                    string fullPath = Path.Combine(attachDir, attachment.FilePath);
                    if (File.Exists(fullPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
                    }
                    else
                    {
                        CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("FileNotFound"), CatchCapture.Resources.LocalizationManager.GetString("Notice"));
                    }
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
                }
            }
        }

        private void SetPlainTextPreview(string? content)
        {
            var p = new Paragraph(new Run(content ?? ""));
            var doc = new FlowDocument(p);
            doc.PagePadding = new Thickness(0);
            doc.FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
            doc.FontSize = 15;
            PreviewViewer.Document = doc;
        }

        private void LstNotes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Open note for Viewing (Post View)
            if (LstNotes.SelectedItem is NoteViewModel note)
            {
                long currentNoteId = note.Id;
                
                // Allow multiple windows (Modeless)
                var viewWin = new NoteViewWindow(note.Id);
                // viewWin.Owner = this; // Removed Owner to allow independent layering
                viewWin.Show();
                
                viewWin.Closed += (s, args) => 
                {
                    // Refresh list when viewer closes to reflect any potential edits
                     if (this.IsLoaded)
                     {
                        LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
                        LoadTags();

                        // Restore selection
                        var noteToSelect = LstNotes.Items.OfType<NoteViewModel>().FirstOrDefault(n => n.Id == currentNoteId);
                        if (noteToSelect != null)
                        {
                            LstNotes.SelectedItem = noteToSelect;
                            LstNotes.ScrollIntoView(noteToSelect);
                        }
                     }
                };
            }
        }

        private void BtnNewNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open NoteInputWindow in "New Note" mode (empty)
                var noteInput = new NoteInputWindow(null); 
                noteInput.Owner = this;
                if (noteInput.ShowDialog() == true)
                {
                    LoadNotes(); // Refresh list after saving
                    LoadTags();
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenNoteInput") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditNote_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LstNotes.SelectedItem is NoteViewModel note)
                {
                    var inputWin = new NoteInputWindow(note.Id);
                    inputWin.Owner = this;
                    if (inputWin.ShowDialog() == true)
                    {
                        LoadNotes(); // Refresh list after edit
                        LoadTags();
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenNoteEdit") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long noteId)
            {
                if (CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ConfirmDeleteNote"), CatchCapture.Resources.LocalizationManager.GetString("ConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    // Release potential file locks by clearing preview
                    PreviewGrid.Visibility = Visibility.Collapsed;
                    PreviewViewer.Document = null;

                    DeleteNote(noteId);
                    LoadNotes(); // Refresh
                    LoadTags();  // Refresh tags after cleanup
                }
            }
        }

        private void DeleteNote(long noteId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    // Soft delete: Update status to 1 (Trash)
                    string checkStatusSql = "SELECT Status FROM Notes WHERE Id = $id";
                    long status = 0;
                    using (var cmd = new SqliteCommand(checkStatusSql, connection))
                    {
                        cmd.Parameters.AddWithValue("$id", noteId);
                        var result = cmd.ExecuteScalar();
                        status = result != null ? (long)result : 0L;
                    }

                    if (status == 1)
                    {
                        // Already in trash, hard delete
                        // 1. Get file paths to delete from disk BEFORE deleting from DB (due to cascade)
                        var filesToDelete = new List<string>();
                        
                        // Get images
                        string getImagesSql = "SELECT FilePath FROM NoteImages WHERE NoteId = $id";
                        string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                        using (var cmd = new SqliteCommand(getImagesSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string fileName = reader.GetString(0);
                                    filesToDelete.Add(Path.Combine(imgDir, fileName));
                                }
                            }
                        }

                        // Get attachments
                        string getAttachmentsSql = "SELECT FilePath FROM NoteAttachments WHERE NoteId = $id";
                        string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                        using (var cmd = new SqliteCommand(getAttachmentsSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string fileName = reader.GetString(0);
                                    filesToDelete.Add(Path.Combine(attachDir, fileName));
                                }
                            }
                        }

                        // 2. Delete from DB
                        string deleteSql = "DELETE FROM Notes WHERE Id = $id";
                        using (var cmd = new SqliteCommand(deleteSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Delete physical files
                        foreach (var filePath in filesToDelete)
                        {
                            try
                            {
                                bool isImage = filePath.Contains(Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar);
                                string rootDir = isImage ? imgDir : attachDir;
                                
                                // Get relative path from rootDir for DB lookup
                                string relativePath = filePath.Substring(rootDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                
                                bool isReferenced = isImage 
                                    ? DatabaseManager.Instance.IsImageReferenced(relativePath, noteId)
                                    : DatabaseManager.Instance.IsAttachmentReferenced(relativePath, noteId);

                                if (!isReferenced && File.Exists(filePath))
                                {
                                    File.SetAttributes(filePath, FileAttributes.Normal);
                                    File.Delete(filePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"파일 삭제 오류 ({filePath}): {ex.Message}");
                            }
                        }

                        // 4. Cleanup orphaned tags (tags not used by any notes)
                        string cleanupTagsSql = "DELETE FROM Tags WHERE Id NOT IN (SELECT DISTINCT TagId FROM NoteTags)";
                        using (var cmd = new SqliteCommand(cleanupTagsSql, connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Move to trash
                        string softDeleteSql = "UPDATE Notes SET Status = 1, DeletedAt = datetime('now', 'localtime') WHERE Id = $id";
                        using (var cmd = new SqliteCommand(softDeleteSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrDelete") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
            }
        }

        private void BtnToggleSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                // Check current state (if sender is checkbox, use its IsChecked, otherwise check if all are selected)
                bool newValue;
                if (sender is CheckBox chk)
                {
                    newValue = chk.IsChecked ?? false;
                }
                else
                {
                    bool allSelected = notes.Count() > 0 && notes.All(n => n.IsSelected);
                    newValue = !allSelected;
                }

                foreach (var note in notes) note.IsSelected = newValue;
                
                // Update selection mode visibility
                IsSelectionMode = newValue;
                
                UpdateSelectionUI(newValue);
            }
        }

        private void UpdateSelectionUI(bool allSelected)
        {
            if (BtnSelectAll != null)
            {
                BtnSelectAll.Content = allSelected
                    ? CatchCapture.Resources.LocalizationManager.GetString("UnselectAll") ?? "전체 해제"
                    : CatchCapture.Resources.LocalizationManager.GetString("SelectAll") ?? "전체 선택";
            }
            if (ChkSelectAllHeader != null)
            {
                // Unsubscribe temporarily to avoid recursion if we want, but since we are just setting state it's fine
                ChkSelectAllHeader.IsChecked = allSelected;
            }
        }

        private void BtnUnselectAll_Click(object sender, RoutedEventArgs e)
        {
            IsSelectionMode = false;
            UpdateSelectionUI(false);
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                foreach (var note in notes) note.IsSelected = false;
            }
        }

        private void ApplyViewMode()
        {
            if (LstNotes == null) return;

            // Save to settings
            var settings = Settings.Load();
            settings.NoteExplorerViewMode = _currentViewMode;
            settings.Save();

            if (_currentViewMode == 0) // List
            {
                LstNotes.ItemContainerStyle = (Style)FindResource("NoteItemStyle");
                LstNotes.ItemTemplate = (DataTemplate)FindResource("ListTemplate");
                
                // Set ItemsPanel to VirtualizingStackPanel (default)
                var template = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
                LstNotes.ItemsPanel = template;
                
                if (ColHeaderBorder != null) ColHeaderBorder.Visibility = Visibility.Visible;
            }
            else // Card
            {
                LstNotes.ItemContainerStyle = (Style)FindResource("NoteCardStyle");
                LstNotes.ItemTemplate = (DataTemplate)FindResource("CardTemplate");
                
                // Set ItemsPanel to WrapPanel
                var factory = new FrameworkElementFactory(typeof(WrapPanel));
                // factory.SetValue(WrapPanel.ItemWidthProperty, 260.0); // Can add this if we want fixed wrapping
                var template = new ItemsPanelTemplate(factory);
                LstNotes.ItemsPanel = template;
                
                if (ColHeaderBorder != null) ColHeaderBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mode)
            {
                CurrentViewMode = (mode == "Card") ? 1 : 0;
            }
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            // 1. Check for checked items (CheckBox)
            var checkedNotes = new List<NoteViewModel>();
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                checkedNotes = notes.Where(n => n.IsSelected).ToList();
            }

            // 2. Check for highlighted items (ListBox selection)
            var highlightedNotes = new List<NoteViewModel>();
            if (LstNotes.SelectedItems.Count > 0)
            {
                foreach (var item in LstNotes.SelectedItems)
                {
                    if (item is NoteViewModel note && !checkedNotes.Any(n => n.Id == note.Id))
                    {
                        highlightedNotes.Add(note);
                    }
                }
            }

            // Combine both
            var notesToDelete = new List<NoteViewModel>(checkedNotes);
            notesToDelete.AddRange(highlightedNotes);

            if (notesToDelete.Count == 0)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("PleaseSelectNotesToDelete"), CatchCapture.Resources.LocalizationManager.GetString("Notice"));
                return;
            }

            if (CatchCapture.CustomMessageBox.Show(string.Format(CatchCapture.Resources.LocalizationManager.GetString("ConfirmDeleteSelectedNotes"), notesToDelete.Count), CatchCapture.Resources.LocalizationManager.GetString("ConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // Release potential file locks by clearing preview
                PreviewGrid.Visibility = Visibility.Collapsed;
                PreviewViewer.Document = null;

                foreach (var note in notesToDelete)
                {
                    DeleteNote(note.Id);
                }
                LoadNotes(); // Refresh
                LoadTags();  // Refresh tags after cleanup
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filter)
            {
                TxtSearch.Text = "";
                LoadNotes(filter: filter, page: 1);
                HighlightSidebarButton(btn);
                
                // Toggle Recent Sub-filters
                if (filter == "Recent")
                {
                     if (PanelRecentSubFilters.Visibility == Visibility.Visible)
                     {
                         PanelRecentSubFilters.Visibility = Visibility.Collapsed;
                     }
                     else
                     {
                         PanelRecentSubFilters.Visibility = Visibility.Visible;
                     }
                }
                else if (!filter.StartsWith("Recent")) // Hide if other main filters clicked
                {
                    PanelRecentSubFilters.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void LstNotes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                BtnDeleteSelected_Click(sender, e);
            }
        }
        
        private void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            if (CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("TrashEmptyWarning"), 
                CatchCapture.Resources.LocalizationManager.GetString("ConfirmDelete"), 
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    // Release potential file locks
                    PreviewGrid.Visibility = Visibility.Collapsed;
                    PreviewViewer.Document = null;

                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        
                        // 1. Get images to delete
                        var imagesToDelete = new List<string>();
                        string imgSql = "SELECT FilePath FROM NoteImages WHERE NoteId IN (SELECT Id FROM Notes WHERE Status = 1)";
                        using (var cmd = new SqliteCommand(imgSql, connection))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) imagesToDelete.Add(reader.GetString(0));
                        }
                        
                        // 2. Get attachments to delete
                        var attachToDelete = new List<string>();
                        string attachSql = "SELECT FilePath FROM NoteAttachments WHERE NoteId IN (SELECT Id FROM Notes WHERE Status = 1)";
                        using (var cmd = new SqliteCommand(attachSql, connection))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) attachToDelete.Add(reader.GetString(0));
                        }

                        // 3. Delete from DB
                        using (var cmd = new SqliteCommand("DELETE FROM Notes WHERE Status = 1", connection))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        
                        // 4. Delete physical files (Only if NOT referenced by other active notes)
                         string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                        foreach (var file in imagesToDelete)
                        {
                            try 
                            { 
                                if (!DatabaseManager.Instance.IsImageReferenced(file))
                                {
                                    string fullPath = Path.Combine(imgDir, file);
                                    if(File.Exists(fullPath)) File.Delete(fullPath); 
                                }
                            } catch { }
                        }
                         string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                        foreach (var file in attachToDelete)
                        {
                            try 
                            { 
                                if (!DatabaseManager.Instance.IsAttachmentReferenced(file))
                                {
                                    string fullPath = Path.Combine(attachDir, file);
                                    if(File.Exists(fullPath)) File.Delete(fullPath); 
                                }
                            } catch { }
                        }
                    }
                    
                    LoadNotes("Trash"); // Refresh current view
                    UpdateSidebarCounts(); // Refresh counts
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("TrashEmptyComplete"), CatchCapture.Resources.LocalizationManager.GetString("Notice"));
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("TrashEmptyFailed") + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
                }
            }
        }


        private void HighlightSidebarButton(Button activeBtn)
        {
            try
            {
                // Traverse up to find the main sidebar StackPanel
                DependencyObject obj = activeBtn;
                StackPanel? mainPanel = null;
                while (obj != null)
                {
                    if (obj is StackPanel sp && sp.Margin.Top == 20) // The one inside ScrollViewer
                    {
                        mainPanel = sp;
                        break;
                    }
                    obj = VisualTreeHelper.GetParent(obj);
                }

                if (mainPanel != null)
                {
                    ClearUidRecursive(mainPanel);
                    activeBtn.Uid = "Active";
                }
            }
            catch { }
        }

        private void ClearUidRecursive(DependencyObject parent)
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button b) b.Uid = "";
                
                ClearUidRecursive(child);
            }
        }

        private void BtnTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagName)
            {
                TxtSearch.Text = "";
                LoadNotes(tag: tagName, page: 1);
                HighlightSidebarButton(btn);
            }
        }

        private void BtnClearTags_Click(object sender, RoutedEventArgs e)
        {
            if (CatchCapture.CustomMessageBox.Show("정말로 모든 태그를 삭제하시겠습니까?\n모든 노트에서 태그 정보가 제거됩니다.", "태그 비우기", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // 1. 모든 노트-태그 연결 삭제
                                using (var cmd = new SqliteCommand("DELETE FROM NoteTags", connection, transaction))
                                {
                                    cmd.ExecuteNonQuery();
                                }

                                // 2. 모든 태그 정의 삭제
                                using (var cmd = new SqliteCommand("DELETE FROM Tags", connection, transaction))
                                {
                                    cmd.ExecuteNonQuery();
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }

                    // UI 갱신
                    LoadTags();
                    // 만약 현재 태그 필터링 중이었다면 해제
                    string? nextTag = _currentTag;
                    if (!string.IsNullOrEmpty(nextTag)) nextTag = null; // 태그 비웠으니 태그 필터 해제
                    
                    LoadNotes(_currentFilter, nextTag, _currentSearch, 1);
                    CatchCapture.CustomMessageBox.Show("모든 태그가 삭제되었습니다.", "알림");
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show($"태그 삭제 실패: {ex.Message}", "오류");
                }
            }
        }

        private void BtnDeleteSingleTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagName)
            {
                if (CatchCapture.CustomMessageBox.Show($"태그 '#{tagName}'를 삭제하시겠습니까?\n해당 태그가 적용된 노트에서 이 태그만 제거됩니다.", "태그 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                        {
                            connection.Open();
                            using (var transaction = connection.BeginTransaction())
                            {
                                try
                                {
                                    // 1. Get Tag Id
                                    long tagId = -1;
                                    using (var cmd = new SqliteCommand("SELECT Id FROM Tags WHERE Name = $name", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("$name", tagName);
                                        var result = cmd.ExecuteScalar();
                                        if (result != null && result != DBNull.Value)
                                        {
                                            tagId = Convert.ToInt64(result);
                                        }
                                    }

                                    if (tagId != -1)
                                    {
                                        // 2. Delete NoteTags relations
                                        using (var cmd = new SqliteCommand("DELETE FROM NoteTags WHERE TagId = $tagId", connection, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("$tagId", tagId);
                                            cmd.ExecuteNonQuery();
                                        }

                                        // 3. Delete Tag definition
                                        using (var cmd = new SqliteCommand("DELETE FROM Tags WHERE Id = $tagId", connection, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("$tagId", tagId);
                                            cmd.ExecuteNonQuery();
                                        }

                                        transaction.Commit();
                                    }
                                }
                                catch
                                {
                                    transaction.Rollback();
                                    throw;
                                }
                            }
                        }

                        // UI refresh
                        LoadTags();
                        
                        // If current filter was this tag, reset it
                        if (_currentTag == tagName)
                        {
                            _currentTag = null;
                            LoadNotes(_currentFilter, null, _currentSearch, 1);
                        }
                        else
                        {
                            // Just reload current list to reflect tag removal
                             LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
                        }
                        
                        CatchCapture.CustomMessageBox.Show($"태그 '#{tagName}'가 삭제되었습니다.", "알림");
                    }
                    catch (Exception ex)
                    {
                        CatchCapture.CustomMessageBox.Show($"태그 삭제 실패: {ex.Message}", "오류");
                    }
                }
            }
        }

        private void BtnToggleCategories_Click(object sender, RoutedEventArgs e)
        {
            if (BrdCategories.Visibility == Visibility.Visible)
            {
                BrdCategories.Visibility = Visibility.Collapsed;
                // Closed Folder Outline
                PathGroupIcon.Data = Geometry.Parse("M3,6 A2,2 0 0,1 5,4 H9 L11,6 H19 A2,2 0 0,1 21,8 V18 A2,2 0 0,1 19,20 H5 A2,2 0 0,1 3,18 Z");
            }
            else
            {
                BrdCategories.Visibility = Visibility.Visible;
                // Open Folder Outline
                PathGroupIcon.Data = Geometry.Parse("M21,10 H6.5 C5,10 4,11.5 4,12.5 L6,20 H21 L23,12 C23,11 22,10 21,10 M3,20 H6 L4,12 C4,9 6,6 9,6 H19 C20,6 21,7 21,8 M9,6 L7,4 H5 C3,4 2,5 2,7 V19");
            }
        }

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var win = new CategoryManagementWindow();
            win.Owner = this;
            win.ShowDialog();
            UpdateSidebarCounts();
        }

        private void BtnSortCategories_Click(object sender, RoutedEventArgs e)
        {
            if (BtnSortCategories.ContextMenu != null)
            {
                BtnSortCategories.ContextMenu.PlacementTarget = BtnSortCategories;
                BtnSortCategories.ContextMenu.IsOpen = true;
            }
        }

        private void BtnSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string sort)
            {
                _currentSortOrder = sort;
                LoadNotes(_currentFilter, _currentTag, _currentSearch, 1);
            }
        }

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long id)
            {
                TxtSearch.Text = "";
                LoadNotes(filter: $"Category:{id}");
                HighlightSidebarButton(btn);
            }
        }

        private void BtnRestoreItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long noteId)
            {
                if (CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ConfirmRestoreNote"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                        {
                            connection.Open();
                            using (var command = new SqliteCommand("UPDATE Notes SET Status = 0 WHERE Id = $id", connection))
                            {
                                command.Parameters.AddWithValue("$id", noteId);
                                command.ExecuteNonQuery();
                            }
                        }
                        LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
                        UpdateSidebarCounts();
                    }
                    catch (Exception ex)
                    {
                        CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrRestoreNote") + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
                    }
                }
            }
        }

        private void BtnPinItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is NoteViewModel note)
            {
                try
                {
                    bool newPinned = !note.IsPinned;
                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        using (var command = new SqliteCommand("UPDATE Notes SET IsPinned = $isPinned WHERE Id = $id", connection))
                        {
                            command.Parameters.AddWithValue("$isPinned", newPinned ? 1 : 0);
                            command.Parameters.AddWithValue("$id", note.Id);
                            command.ExecuteNonQuery();
                        }
                    }
                    
                    // Update UI state
                    note.IsPinned = newPinned;
                    
                    // Re-sort to bring pinned to top
                    LoadNotes(_currentFilter, _currentTag, _currentSearch, _currentPage);
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrPinNote") + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
                }
            }
        }
    }

    public class NoteViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private long _id;
        public long Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }

        private string? _title;
        public string? Title { get => _title; set { _title = value; OnPropertyChanged(nameof(Title)); } }

        private string? _previewContent;
        public string? PreviewContent { get => _previewContent; set { _previewContent = value; OnPropertyChanged(nameof(PreviewContent)); OnPropertyChanged(nameof(TruncatedContent)); } }

        private DateTime _createdAt;
        public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); } }

        private DateTime _updatedAt;
        public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnPropertyChanged(nameof(UpdatedAt)); OnPropertyChanged(nameof(FormattedDate)); } }

        private string? _sourceApp;
        public string? SourceApp { get => _sourceApp; set { _sourceApp = value; OnPropertyChanged(nameof(SourceApp)); } }
        
        private string? _contentXaml;
        public string? ContentXaml { get => _contentXaml; set { _contentXaml = value; OnPropertyChanged(nameof(ContentXaml)); } }

        private string _categoryName = "기본";
        public string CategoryName { get => _categoryName; set { _categoryName = value; OnPropertyChanged(nameof(CategoryName)); } }

        private string _categoryColor = "#8E2DE2";
        public string CategoryColor { get => _categoryColor; set { _categoryColor = value; OnPropertyChanged(nameof(CategoryColor)); } }
        
        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); } }

        private List<BitmapSource> _images = new List<BitmapSource>();
        public List<BitmapSource> Images { get => _images; set { _images = value; OnPropertyChanged(nameof(Images)); } }

        private List<string> _imageFilePaths = new List<string>();
        public List<string> ImageFilePaths { get => _imageFilePaths; set { _imageFilePaths = value; OnPropertyChanged(nameof(ImageFilePaths)); } }

        private List<string> _tags = new List<string>();
        public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(nameof(Tags)); } }

        private bool _isPinned;
        public bool IsPinned { get => _isPinned; set { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); } }

        private List<NoteAttachment> _attachments = new List<NoteAttachment>();
        public List<NoteAttachment> Attachments { get => _attachments; set { _attachments = value; OnPropertyChanged(nameof(Attachments)); } }

        private int _status;
        public int Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(IsVisibleInTrash)); } }

        public Visibility IsVisibleInTrash => Status == 1 ? Visibility.Visible : Visibility.Collapsed;

        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
        }

        public string TruncatedContent 
        {
            get 
            {
                if (string.IsNullOrEmpty(PreviewContent)) return "";
                // Return first non-empty line, let XAML handles trimming via TextTrimming="CharacterEllipsis"
                var lines = PreviewContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    // Remove Object Replacement Character (\uFFFC) and Zero Width Space (\u200B)
                    // ZWS is used for styling anchors but is invisible, causing "empty" looking lines to be selected
                    string trimmed = line.Replace("\uFFFC", "").Replace("\u200B", "").Trim();
                    
                    if (!string.IsNullOrEmpty(trimmed)) 
                    {
                        // Deduplication: If this line is essentially the same as the Title, skip it.
                        // This achieves the "Title = Line 1, Preview = Line 2" effect for auto-generated titles.
                        if (!string.IsNullOrEmpty(Title) && trimmed.Equals(Title.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        return trimmed;
                    }
                }
                return "";
            }
        }

        public string FormattedDate => UpdatedAt.ToString("yyyy-MM-dd HH:mm");
    }

    public partial class NoteExplorerWindow
    {
        private void PreviewViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FlowDocumentScrollViewer viewer)
            {
                var position = e.GetPosition(viewer);
                // Hit test to find the element under the mouse
                IInputElement inputElement = viewer.InputHitTest(position);
                
                if (inputElement is DependencyObject obj)
                {
                    DependencyObject? parent = obj;
                    // Walk up the tree to find a Hyperlink
                    while (parent != null && !(parent is Hyperlink) && !(parent is FlowDocumentScrollViewer))
                    {
                        var next = LogicalTreeHelper.GetParent(parent);
                        if (next == null && parent is FrameworkElement fe) next = fe.Parent ?? fe.TemplatedParent;
                        parent = next;
                    }

                    if (parent is Hyperlink link && link.NavigateUri != null)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.NavigateUri.AbsoluteUri) { UseShellExecute = true });
                            e.Handled = true; // Consume event
                        }
                        catch (Exception ex)
                        {
                            CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message);
                        }
                    }
                }
            }
        }

        private void HookHyperlinks(FlowDocument doc)
        {
            // Set Hand cursor for all hyperlinks in the document
            var enumerator = doc.Blocks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                SetCursorOnHyperlinks(enumerator.Current);
            }
        }

        private void SetCursorOnHyperlinks(DependencyObject obj)
        {
            if (obj is Hyperlink link)
            {
                link.Cursor = Cursors.Hand;
                if (link.Foreground == null || (link.Foreground is SolidColorBrush b && b.Color == Colors.Black))
                {
                    link.Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204));
                }
                // Remove existing handler first (prevents duplicate subscriptions)
                link.RequestNavigate -= Hyperlink_RequestNavigate;
                // Attach RequestNavigate event to make hyperlink clickable
                link.RequestNavigate += Hyperlink_RequestNavigate;
            }
            else if (obj is InlineUIContainer container)
            {
                if (container.Child is FrameworkElement fe && fe.Tag is string url && url.StartsWith("http"))
                {
                    fe.Cursor = Cursors.Hand;
                    fe.MouseLeftButtonDown += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                            e.Handled = true;
                        }
                        catch { }
                    };

                    // Context Menu
                    var contextMenu = new ContextMenu();
                    var copyMenuItem = new MenuItem { Header = "URL 복사" };
                    copyMenuItem.Click += (s, e) => { try { Clipboard.SetText(url); } catch { } };
                    contextMenu.Items.Add(copyMenuItem);
                    fe.ContextMenu = contextMenu;
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(obj))
            {
                if (child is DependencyObject d) SetCursorOnHyperlinks(d);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message);
            }
        }
    }

    public class CategorySidebarItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#8E2DE2";
        public int Count { get; set; }
        public string CountText => $"({Count})";
    }
}
