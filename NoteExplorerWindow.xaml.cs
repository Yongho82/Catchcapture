using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using CatchCapture.Utilities;
using Microsoft.Data.Sqlite;
using System.Windows.Controls;

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

                using (var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "Database", "catch_notes.db")}"))
                {
                    connection.Open();
                    string sql = @"
                        SELECT n.Id, n.Title, n.Content, n.CreatedAt, n.SourceApp, i.FilePath 
                        FROM Notes n
                        LEFT JOIN NoteImages i ON n.Id = i.NoteId";
                    
                    List<string> wheres = new List<string>();
                    wheres.Add("n.Status = 0");

                    if (filter == "Today") wheres.Add("date(n.CreatedAt) = date('now')");
                    else if (filter == "Recent") wheres.Add("n.CreatedAt >= date('now', '-7 days')");
                    else if (filter == "Trash") { wheres.Clear(); wheres.Add("n.Status = 1"); }
                    
                    if (!string.IsNullOrEmpty(tag))
                    {
                        sql += " JOIN NoteTags nt ON n.Id = nt.NoteId JOIN Tags t ON nt.TagId = t.Id";
                        wheres.Add("t.Name = $tag");
                    }

                    if (wheres.Count > 0)
                    {
                        sql += " WHERE " + string.Join(" AND ", wheres);
                    }

                    sql += " GROUP BY n.Id ORDER BY n.CreatedAt DESC";

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(tag)) command.Parameters.AddWithValue("$tag", tag);
                        using (var reader = command.ExecuteReader())
                        {
                        while (reader.Read())
                        {
                            string? fileName = reader.IsDBNull(5) ? null : reader.GetString(5);
                            BitmapSource? thumb = null;

                            if (!string.IsNullOrEmpty(fileName))
                            {
                                string fullPath = Path.Combine(imgDir, fileName);
                                if (File.Exists(fullPath))
                                {
                                    try
                                    {
                                        var bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.UriSource = new Uri(fullPath);
                                        // Increase decode width for higher quality thumbnails
                                        bitmap.DecodePixelWidth = 400; 
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.EndInit();
                                        bitmap.Freeze();
                                        thumb = bitmap;
                                    }
                                    catch { }
                                }
                            }

                            notes.Add(new NoteViewModel
                            {
                                Id = reader.GetInt64(0),
                                Title = reader.IsDBNull(1) ? "제목 없음" : reader.GetString(1),
                                PreviewContent = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                CreatedAt = reader.GetDateTime(3),
                                SourceApp = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Thumbnail = thumb
                            });
                        }
                    }
                }

                LstNotes.ItemsSource = notes;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"노트를 불러오는 중 오류가 발생했습니다: {ex.Message}\n{ex.StackTrace}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LstNotes_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LstNotes.SelectedItem is NoteViewModel note)
            {
                TxtPreviewTitle.Text = note.Title;
                TxtPreviewContent.Text = note.PreviewContent;
                ImgPreview.Source = note.Thumbnail;
            }
            else
            {
                TxtPreviewTitle.Text = "선택된 노트 없음";
                TxtPreviewContent.Text = "";
                ImgPreview.Source = null;
            }
        }

        private void BtnListMode_Click(object sender, RoutedEventArgs e)
        {
            ListHeader.Visibility = Visibility.Visible;
            LstNotes.Margin = new Thickness(0, 30, 0, 0);
            LstNotes.ItemTemplate = (DataTemplate)this.Resources["ListTemplate"];
            
            // Set ItemsPanel to StackPanel
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            LstNotes.ItemsPanel = new ItemsPanelTemplate(factory);
            
            ScrollViewer.SetHorizontalScrollBarVisibility(LstNotes, System.Windows.Controls.ScrollBarVisibility.Disabled);
        }

        private void BtnCardMode_Click(object sender, RoutedEventArgs e)
        {
            ListHeader.Visibility = Visibility.Collapsed;
            LstNotes.Margin = new Thickness(0, 10, 0, 0);
            LstNotes.ItemTemplate = (DataTemplate)this.Resources["CardTemplate"];
            
            // Set ItemsPanel to WrapPanel
            var factory = new FrameworkElementFactory(typeof(System.Windows.Controls.WrapPanel));
            LstNotes.ItemsPanel = new ItemsPanelTemplate(factory);

            ScrollViewer.SetHorizontalScrollBarVisibility(LstNotes, System.Windows.Controls.ScrollBarVisibility.Auto);
        }

        private void BtnNewNote_Click(object sender, RoutedEventArgs e)
        {
            // Open NoteInputWindow with a default/empty state or just guidance
            MessageBox.Show("새 노트는 캡처 후 자동으로 생성됩니다.", "안내");
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
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
        public BitmapSource? Thumbnail { get; set; }
    }
}
