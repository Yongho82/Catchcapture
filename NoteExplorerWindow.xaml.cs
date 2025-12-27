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
        private string _currentSortOrder = "n.UpdatedAt DESC";

        private System.Windows.Threading.DispatcherTimer? _tipTimer;
        private int _currentTipIndex = 0;
        private readonly List<string> _tips = new List<string>
        {
            "비밀번호 설정으로 내 노트를 안전하게 보호하세요.",
            "캡처 후 즉시 내 노트에 저장하여 소중한 아이디어를 기록하세요.",
            "태그를 활용하면 수많은 노트 중에서도 원하는 내용을 빠르게 찾을 수 있습니다.",
            "노트 수정창에서 드래그 앤 드롭으로 이미지를 간편하게 추가할 수 있습니다.",
            "검색 기능을 통해 제목뿐만 아니라 노트 내용 속 텍스트도 검색이 가능합니다.",
            "포스트중 이미지를 더블클릭하시면 이미지 편집이 가능합니다.",
            "캡처 시 '노트 저장' 버튼을 누르면 단 한 번의 클릭으로 노트가 생성됩니다."
        };

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

        public NoteExplorerWindow()
        {
            try
            {
                InitializeComponent();
                this.DataContext = this;
                this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
                LoadNotes(filter: "Recent");
                LoadTags();
                InitializeTipTimer();
                HighlightSidebarButton(BtnFilterRecent);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"탐색기 초기화 중 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateSidebarCounts()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    
                    // All
                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0", connection))
                        TxtCountAll.Text = $"({cmd.ExecuteScalar()})";
                        
                    // Default
                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND CategoryId = 1", connection))
                        TxtCountDefault.Text = $"({cmd.ExecuteScalar()})";
                        
                    // Today
                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND date(CreatedAt, 'localtime') = date('now', 'localtime')", connection))
                        TxtCountToday.Text = $"({cmd.ExecuteScalar()})";
                        
                    // Recent
                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND CreatedAt >= date('now', 'localtime', '-7 days')", connection))
                        TxtCountRecent.Text = $"({cmd.ExecuteScalar()})";
                        
                    // Trash
                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 1", connection))
                        TxtCountTrash.Text = $"({cmd.ExecuteScalar()})";

                    // Dynamic Categories
                    var categories = DatabaseManager.Instance.GetAllCategories().Where(c => c.Id != 1);
                    var categoryItems = new List<CategorySidebarItem>();
                    foreach (var cat in categories)
                    {
                        using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Notes WHERE Status = 0 AND CategoryId = $categoryId", connection))
                        {
                            cmd.Parameters.AddWithValue("$categoryId", cat.Id);
                            int count = Convert.ToInt32(cmd.ExecuteScalar());
                            categoryItems.Add(new CategorySidebarItem { Id = cat.Id, Name = cat.Name, Count = count });
                        }
                    }
                    ItemsCategories.ItemsSource = categoryItems;
                }
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
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                PathMaximize.Data = Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,12V18H14V12H6Z");
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

        private void InitializeTipTimer()
        {
            _tipTimer = new System.Windows.Threading.DispatcherTimer();
            _tipTimer.Interval = TimeSpan.FromSeconds(5);
            _tipTimer.Tick += (s, e) => {
                _currentTipIndex = (_currentTipIndex + 1) % _tips.Count;
                TxtRollingTip.Opacity = 0;
                TxtRollingTip.Text = _tips[_currentTipIndex];
                
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                TxtRollingTip.BeginAnimation(TextBlock.OpacityProperty, anim);
            };
            _tipTimer.Start();
        }

        private void LoadNotes(string filter = "Recent", string? tag = null, string? search = null, int page = 1)
        {
            try
            {
                _currentFilter = filter;
                _currentTag = tag;
                _currentSearch = search;
                _currentPage = page;

                // Update Status Text
                if (!string.IsNullOrEmpty(search)) TxtStatusInfo.Text = $"검색 결과: '{search}'";
                else if (!string.IsNullOrEmpty(tag)) TxtStatusInfo.Text = $"태그 필터: #{tag}";
                else if (filter == "Today") TxtStatusInfo.Text = "오늘의 기록 내용";
                else if (filter == "Recent") TxtStatusInfo.Text = "최근 1주일 기록 내용";
                else if (filter == "Trash") TxtStatusInfo.Text = "휴지통 기록 내용";
                else if (filter.StartsWith("Category:"))
                {
                    long catId = long.Parse(filter.Split(':')[1]);
                    var cat = DatabaseManager.Instance.GetCategory(catId);
                    TxtStatusInfo.Text = $"그룹: {cat?.Name ?? "알 수 없음"}";
                }
                else TxtStatusInfo.Text = "전체 기록 내용";

                var notes = new List<NoteViewModel>();
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                int totalCount = 0;

                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    
                    // 1. Build Base WHERE clause for reuse
                    List<string> wheres = new List<string>();
                    if (filter == "Trash") wheres.Add("n.Status = 1");
                    else
                    {
                        wheres.Add("n.Status = 0");
                        if (filter == "Today") wheres.Add("date(n.CreatedAt, 'localtime') = date('now', 'localtime')");
                        else if (filter == "Recent") wheres.Add("n.CreatedAt >= date('now', 'localtime', '-7 days')");
                        else if (filter.StartsWith("Category:"))
                        {
                            wheres.Add("n.CategoryId = " + filter.Split(':')[1]);
                        }
                        else if (filter == "Default") wheres.Add("n.CategoryId = 1");
                    }

                    if (!string.IsNullOrEmpty(search))
                    {
                        wheres.Add("(n.Title LIKE $search OR n.Content LIKE $search)");
                    }

                    string joinSql = "";
                    if (!string.IsNullOrEmpty(tag))
                    {
                        joinSql = " JOIN NoteTags nt ON n.Id = nt.NoteId JOIN Tags t ON nt.TagId = t.Id";
                        wheres.Add("t.Name = $tag");
                    }

                    string whereClause = wheres.Count > 0 ? " WHERE " + string.Join(" AND ", wheres) : "";

                    // 2. Count Total for Pagination
                    string countSql = $"SELECT COUNT(DISTINCT n.Id) FROM Notes n {joinSql} {whereClause}";
                    using (var countCmd = new SqliteCommand(countSql, connection))
                    {
                        if (!string.IsNullOrEmpty(tag)) countCmd.Parameters.AddWithValue("$tag", tag);
                        if (!string.IsNullOrEmpty(search)) countCmd.Parameters.AddWithValue("$search", $"%{search}%");
                        totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                    }

                    string sql = $@"
                        SELECT n.Id, n.Title, n.Content, datetime(n.CreatedAt, 'localtime'), n.SourceApp, n.ContentXaml,
                               c.Name as CategoryName, c.Color as CategoryColor, datetime(n.UpdatedAt, 'localtime'), n.Status
                        FROM Notes n
                        LEFT JOIN Categories c ON n.CategoryId = c.Id
                        {joinSql}
                        {whereClause}
                        ORDER BY {_currentSortOrder}
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
                                var note = new NoteViewModel
                                {
                                    Id = noteId,
                                    Title = reader.IsDBNull(1) ? "제목 없음" : reader.GetString(1),
                                    PreviewContent = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CreatedAt = reader.GetDateTime(3),
                                    SourceApp = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    ContentXaml = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    CategoryName = reader.IsDBNull(6) ? "기본" : reader.GetString(6),
                                    CategoryColor = reader.IsDBNull(7) ? "#8E2DE2" : reader.GetString(7),
                                    UpdatedAt = reader.GetDateTime(8),
                                    Status = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
                                };

                                note.Images = GetNoteImages(noteId, imgDir);
                                note.Thumbnail = note.Images.FirstOrDefault();
                                note.Tags = GetNoteTags(noteId);
                                note.Attachments = GetNoteAttachments(noteId);
                                notes.Add(note);
                            }
                        }
                    }
                }

                LstNotes.ItemsSource = notes;
                UpdatePaginationButtons(totalCount);
                
                // Auto-select first item if exists
                if (notes.Count > 0)
                {
                    LstNotes.SelectedIndex = 0;
                }

                UpdateSidebarCounts();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"노트를 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePaginationButtons(int totalCount)
        {
            PanelPagination.Children.Clear();
            int totalPages = (int)Math.Ceiling((double)totalCount / PAGE_SIZE);
            if (totalPages <= 1) return;

            // Simple pagination: 1 2 3 ...
            for (int i = 1; i <= totalPages; i++)
            {
                var btn = new Button
                {
                    Content = i.ToString(),
                    Style = (Style)FindResource("PaginationButtonStyle"),
                    Tag = (i == _currentPage) ? "Active" : ""
                };
                int pageNum = i;
                btn.Click += (s, e) => LoadNotes(_currentFilter, _currentTag, _currentSearch, pageNum);
                PanelPagination.Children.Add(btn);
            }
        }

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

        private List<BitmapSource> GetNoteImages(long noteId, string imgDir)
        {
            var images = new List<BitmapSource>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string sql = "SELECT FilePath FROM NoteImages WHERE NoteId = $noteId ORDER BY OrderIndex ASC";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$noteId", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string fileName = reader.GetString(0);
                                string fullPath = Path.Combine(imgDir, fileName);
                                if (File.Exists(fullPath))
                                {
                                    try
                                    {
                                        using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.StreamSource = stream;
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.DecodePixelWidth = 400; // Optimize for preview
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                            images.Add(bitmap);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return images;
        }

        private List<string> GetNoteTags(long noteId)
        {
            var tags = new List<string>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string sql = @"
                        SELECT t.Name 
                        FROM Tags t
                        JOIN NoteTags nt ON t.Id = nt.TagId
                        WHERE nt.NoteId = $noteId";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$noteId", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tags.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch { }
            return tags;
        }

        private List<NoteAttachment> GetNoteAttachments(long noteId)
        {
            var attachments = new List<NoteAttachment>();
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string sql = "SELECT Id, FilePath, OriginalName, FileType FROM NoteAttachments WHERE NoteId = $noteId";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$noteId", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                attachments.Add(new NoteAttachment
                                {
                                    Id = reader.GetInt64(0),
                                    NoteId = noteId,
                                    FilePath = reader.GetString(1),
                                    OriginalName = reader.GetString(2),
                                    FileType = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return attachments;
        }

        private void LstNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstNotes.SelectedItem is NoteViewModel note)
            {
                PreviewGrid.Visibility = Visibility.Visible;
                TxtPreviewTitle.Text = note.Title;
                TxtPreviewApp.Text = string.IsNullOrEmpty(note.SourceApp) ? "직접 작성" : note.SourceApp;
                TxtPreviewDate.Text = note.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                PreviewTags.ItemsSource = note.Tags;
                PreviewAttachments.ItemsSource = note.Attachments;
                
                // Use FlowDocument to show EXACT interleaved order (matches Viewer)
                if (!string.IsNullOrEmpty(note.ContentXaml))
                {
                    try
                    {
                        var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Parse(note.ContentXaml);
                        flowDocument.PagePadding = new Thickness(0, 0, 10, 0); // Add slight right padding for scrollbar
                        flowDocument.FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
                        flowDocument.FontSize = 15;
                        
                        // Compact preview layout
                        foreach (var block in flowDocument.Blocks)
                        {
                            block.Margin = new Thickness(0, 2, 0, 2);
                            
                            if (block is BlockUIContainer container && container.Child is Grid g)
                            {
                                var img = g.Children.OfType<Image>().FirstOrDefault();
                                if (img != null)
                                {
                                    img.MaxWidth = 340; // Preview width limit
                                    img.Cursor = Cursors.Hand;
                                    img.PreviewMouseLeftButtonDown += (s, ev) =>
                                   {
                                       string? path = null;
                                       if (img.Source is BitmapImage bi && bi.UriSource != null) path = bi.UriSource.LocalPath;
 
                                       if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                       {
                                           try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
                                       }
                                   };
                                }
                            }
                        }

                        PreviewViewer.Document = flowDocument;
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
                        CatchCapture.CustomMessageBox.Show("파일을 찾을 수 없습니다.");
                    }
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show("파일을 여는 중 오류 발생: " + ex.Message);
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
                var viewWin = new NoteViewWindow(note.Id);
                viewWin.Owner = this;
                viewWin.ShowDialog();
                // Refresh in case edit happened inside View->Edit flow
                LoadNotes();
                LoadTags();
            }
        }

        private void BtnNewNote_Click(object sender, RoutedEventArgs e)
        {
            // Open NoteInputWindow in "New Note" mode (empty)
            // Passing null image will need handling in NoteInputWindow
            var noteInput = new NoteInputWindow(null); 
            noteInput.Owner = this;
            if (noteInput.ShowDialog() == true)
            {
                LoadNotes(); // Refresh list after saving
                LoadTags();
            }
        }

        private void BtnEditNote_Click(object sender, RoutedEventArgs e)
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

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long noteId)
            {
                if (CatchCapture.CustomMessageBox.Show("정말로 이 노트를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                                if (File.Exists(filePath))
                                {
                                    // Optimization: Clear file attributes if necessary (rarely needed but good for reliability)
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
                        string softDeleteSql = "UPDATE Notes SET Status = 1 WHERE Id = $id";
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
                CatchCapture.CustomMessageBox.Show("삭제 중 오류 발생: " + ex.Message);
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            IsSelectionMode = true;
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                foreach (var note in notes) note.IsSelected = true;
            }
        }

        private void BtnUnselectAll_Click(object sender, RoutedEventArgs e)
        {
            IsSelectionMode = false;
            if (ChkSelectAllHeader != null) ChkSelectAllHeader.IsChecked = false;
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                foreach (var note in notes) note.IsSelected = false;
            }
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (LstNotes.ItemsSource is IEnumerable<NoteViewModel> notes)
            {
                var selectedNotes = notes.Where(n => n.IsSelected).ToList();
                if (selectedNotes.Count == 0)
                {
                    CatchCapture.CustomMessageBox.Show("삭제할 노트를 선택해주세요.");
                    return;
                }

                if (CatchCapture.CustomMessageBox.Show($"{selectedNotes.Count}개의 노트를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    // Release potential file locks by clearing preview
                    PreviewGrid.Visibility = Visibility.Collapsed;
                    PreviewViewer.Document = null;

                    foreach (var note in selectedNotes)
                    {
                        DeleteNote(note.Id);
                    }
                    LoadNotes(); // Refresh
                    LoadTags();  // Refresh tags after cleanup
                }
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filter)
            {
                TxtSearch.Text = "";
                LoadNotes(filter: filter, page: 1);
                HighlightSidebarButton(btn);
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

        private void BtnToggleCategories_Click(object sender, RoutedEventArgs e)
        {
            if (BrdCategories.Visibility == Visibility.Visible)
            {
                BrdCategories.Visibility = Visibility.Collapsed;
                TxtGroupIcon.Text = "📁";
            }
            else
            {
                BrdCategories.Visibility = Visibility.Visible;
                TxtGroupIcon.Text = "📂";
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
                if (CatchCapture.CustomMessageBox.Show("이 노트를 복구하시겠습니까?", "알림", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
                        CatchCapture.CustomMessageBox.Show("복구 중 오류 발생: " + ex.Message);
                    }
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

        private List<string> _tags = new List<string>();
        public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(nameof(Tags)); } }

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
                // Skip empty lines and find first non-empty line
                var lines = PreviewContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        return trimmed.Length > 25 ? trimmed.Substring(0, 25) + "..." : trimmed;
                    }
                }
                return "";
            }
        }

        public string FormattedDate => UpdatedAt.ToString("yyyy-MM-dd HH:mm");
    }

    public class CategorySidebarItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string CountText => $"({Count})";
    }
}
