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
using System.Linq;
using System.ComponentModel;

namespace CatchCapture
{
    public partial class NoteExplorerWindow : Window, INotifyPropertyChanged
    {
        private string _currentFilter = "Recent";
        private string? _currentTag = null;
        private string? _currentSearch = "";
        private int _currentPage = 1;
        private const int PAGE_SIZE = 15;

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"탐색기 초기화 중 오류: {ex.Message}\n{ex.StackTrace}");
            }
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                        SELECT n.Id, n.Title, n.Content, n.CreatedAt, n.SourceApp, n.ContentXaml,
                               c.Name as CategoryName, c.Color as CategoryColor
                        FROM Notes n
                        LEFT JOIN Categories c ON n.CategoryId = c.Id
                        {joinSql}
                        {whereClause}
                        ORDER BY n.CreatedAt DESC
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
                                    ContentXaml = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    CategoryName = reader.IsDBNull(6) ? "기본" : reader.GetString(6),
                                    CategoryColor = reader.IsDBNull(7) ? "#8E2DE2" : reader.GetString(7)
                                };

                                note.Images = GetNoteImages(noteId, imgDir);
                                note.Thumbnail = note.Images.FirstOrDefault();
                                note.Tags = GetNoteTags(noteId);
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

        private void LstNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstNotes.SelectedItem is NoteViewModel note)
            {
                PreviewGrid.Visibility = Visibility.Visible;
                TxtPreviewTitle.Text = note.Title;
                TxtPreviewApp.Text = string.IsNullOrEmpty(note.SourceApp) ? "직접 작성" : note.SourceApp;
                TxtPreviewDate.Text = note.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                PreviewTags.ItemsSource = note.Tags;
                
                // Use FlowDocument to show EXACT interleaved order (matches Viewer)
                if (!string.IsNullOrEmpty(note.ContentXaml))
                {
                    try
                    {
                        var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Parse(note.ContentXaml);
                        flowDocument.PagePadding = new Thickness(0);
                        
                        // Small optimization: Images in preview should be uniformly stretched
                        foreach (var block in flowDocument.Blocks)
                        {
                            if (block is BlockUIContainer container && container.Child is Grid g)
                            {
                                var img = g.Children.OfType<Image>().FirstOrDefault();
                                if (img != null) img.MaxWidth = 340; // Preview width limit
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

        private void SetPlainTextPreview(string content)
        {
            var p = new Paragraph(new Run(content));
            var doc = new FlowDocument(p);
            doc.PagePadding = new Thickness(0);
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
                if (MessageBox.Show("정말로 이 노트를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteNote(noteId);
                    LoadNotes(); // Refresh
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
                    // If already in Trash, hard delete? User logic usually is Move to Trash -> Delete from Trash
                    string checkStatusSql = "SELECT Status FROM Notes WHERE Id = $id";
                    long status = 0;
                    using (var cmd = new SqliteCommand(checkStatusSql, connection))
                    {
                        cmd.Parameters.AddWithValue("$id", noteId);
                        status = (long)(cmd.ExecuteScalar() ?? 0L);
                    }

                    if (status == 1)
                    {
                        // Already in trash, hard delete
                        string deleteSql = "DELETE FROM Notes WHERE Id = $id";
                        using (var cmd = new SqliteCommand(deleteSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
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
                    foreach (var note in selectedNotes)
                    {
                        DeleteNote(note.Id);
                    }
                    LoadNotes(); // Refresh
                }
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            // Reset Sidebar selection visually (simple loop)
            // Since we use styles and triggers, we'd need tracking logic or MVVM. 
            // For now just reload data.
            if (sender is Button btn && btn.Tag is string filter)
            {
                TxtSearch.Text = "";
                LoadNotes(filter: filter, page: 1);
            }
        }

        private void BtnTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagName)
            {
                TxtSearch.Text = "";
                LoadNotes(tag: tagName, page: 1);
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
        public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); OnPropertyChanged(nameof(FormattedDate)); } }

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

        public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm");
    }
}
