using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using CatchCapture.Utilities;
using Microsoft.Data.Sqlite;
using System.Windows.Controls;
using System.Linq;

namespace CatchCapture
{
    public partial class NoteExplorerWindow : Window
    {
        public NoteExplorerWindow()
        {
            try
            {
                InitializeComponent();
                this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
                LoadNotes();
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

        private void LoadNotes(string filter = "All", string? tag = null)
        {
            try
            {
                var notes = new List<NoteViewModel>();
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();

                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    // Join with Categories to get name and color
                    string sql = @"
                        SELECT n.Id, n.Title, n.Content, n.CreatedAt, n.SourceApp, 
                               c.Name as CategoryName, c.Color as CategoryColor
                        FROM Notes n
                        LEFT JOIN Categories c ON n.CategoryId = c.Id";
                    
                    List<string> wheres = new List<string>();
                    
                    if (filter == "Trash") 
                    {
                        wheres.Add("n.Status = 1");
                    }
                    else
                    {
                        wheres.Add("n.Status = 0");
                        if (filter == "Today") wheres.Add("date(n.CreatedAt) = date('now')");
                        else if (filter == "Recent") wheres.Add("n.CreatedAt >= date('now', '-7 days')");
                        else if (filter == "Default") wheres.Add("n.CategoryId = 1");
                    }
                    
                    if (!string.IsNullOrEmpty(tag))
                    {
                        sql += " JOIN NoteTags nt ON n.Id = nt.NoteId JOIN Tags t ON nt.TagId = t.Id";
                        wheres.Add("t.Name = $tag");
                    }

                    if (wheres.Count > 0)
                    {
                        sql += " WHERE " + string.Join(" AND ", wheres);
                    }

                    sql += " ORDER BY n.CreatedAt DESC";

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(tag)) command.Parameters.AddWithValue("$tag", tag);
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
                                    CategoryName = reader.IsDBNull(5) ? "기본" : reader.GetString(5),
                                    CategoryColor = reader.IsDBNull(6) ? "#8E2DE2" : reader.GetString(6)
                                };

                                // Load Images for this note
                                note.Images = GetNoteImages(noteId, imgDir);
                                note.Thumbnail = note.Images.FirstOrDefault();

                                // Load Tags for this note
                                note.Tags = GetNoteTags(noteId);

                                notes.Add(note);
                            }
                        }
                    }
                }

                LstNotes.ItemsSource = notes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"노트를 불러오는 중 오류가 발생했습니다: {ex.Message}\n{ex.StackTrace}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                TxtPreviewContent.Text = note.PreviewContent;
                PreviewTags.ItemsSource = note.Tags;
                PreviewImageGallery.ItemsSource = note.Images;
            }
            else
            {
                PreviewGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void LstNotes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Open note for editing
            BtnEditNote_Click(sender, e);
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
                MessageBox.Show("삭제 중 오류 발생: " + ex.Message);
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            // Reset Sidebar selection visually (simple loop)
            // Since we use styles and triggers, we'd need tracking logic or MVVM. 
            // For now just reload data.
            if (sender is Button btn && btn.Tag is string filter)
            {
                LoadNotes(filter);
            }
        }

        private void BtnTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagName)
            {
                LoadNotes(tag: tagName);
            }
        }
    }

    public class NoteViewModel
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? PreviewContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SourceApp { get; set; }
        
        public string CategoryName { get; set; } = "기본";
        public string CategoryColor { get; set; } = "#8E2DE2";
        
        public BitmapSource? Thumbnail { get; set; }
        public List<BitmapSource> Images { get; set; } = new List<BitmapSource>();
        public List<string> Tags { get; set; } = new List<string>();

        // For truncated content display (15 chars)
        public string TruncatedContent 
        {
            get 
            {
                if (string.IsNullOrEmpty(PreviewContent)) return "";
                return PreviewContent.Length > 15 ? PreviewContent.Substring(0, 15) + "..." : PreviewContent;
            }
        }
    }
}
