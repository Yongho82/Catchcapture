using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Documents;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using CatchCapture.Utilities;
using System.Windows.Markup;
using System.Xml;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class NoteViewWindow : Window
    {
        private long _noteId;

        public NoteViewWindow(long noteId)
        {
            InitializeComponent();
            _noteId = noteId;

            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            LoadNoteData();
        }

        private void LoadNoteData()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string sql = "SELECT Title, Content, ContentXaml, CreatedAt, SourceApp, SourceUrl, CategoryId FROM Notes WHERE Id = $id";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$id", _noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TxtTitle.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                string content = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string contentXaml = reader.IsDBNull(2) ? "" : reader.GetString(2); 
                                DateTime createdAt = reader.GetDateTime(3);
                                TxtDate.Text = createdAt.ToString("yyyy-MM-dd HH:mm");
                                
                                string sourceApp = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                string sourceUrl = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                TxtSource.Text = !string.IsNullOrEmpty(sourceUrl) ? sourceUrl : sourceApp;

                                long catId = reader.GetInt64(6);
                                var category = DatabaseManager.Instance.GetCategory(catId);
                                if (category != null)
                                {
                                    TxtCategory.Text = category.Name;
                                    CategoryCircle.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(category.Color);
                                }

                                // Load Content
                                if (!string.IsNullOrEmpty(contentXaml))
                                {
                                    // Load Rich Content (XAML)
                                    try 
                                    {
                                        var flowDocument = (FlowDocument)XamlReader.Parse(contentXaml);
                                        ContentViewer.Document = flowDocument;
                                    }
                                    catch
                                    {
                                        // Fallback to plain text
                                        SetPlainTextContent(content);
                                    }
                                }
                                else
                                {
                                    // Load Plain Text + Images (Legacy)
                                    SetPlainTextContent(content);
                                    LoadLegacyImages(); 
                                }
                            }
                        }
                    }
                    
                    // Load Tags
                    string tagSql = @"SELECT t.Name FROM Tags t JOIN NoteTags nt ON t.Id = nt.TagId WHERE nt.NoteId = $id";
                    using (var command = new SqliteCommand(tagSql, connection))
                    {
                        command.Parameters.AddWithValue("$id", _noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            List<string> tags = new List<string>();
                            while(reader.Read()) tags.Add(reader.GetString(0));
                            ItemsTags.ItemsSource = tags;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("노트 로드 실패: " + ex.Message);
            }
        }

        private void SetPlainTextContent(string text)
        {
            var p = new Paragraph(new Run(text));
            var doc = new FlowDocument(p);
            ContentViewer.Document = doc;
        }

        private void LoadLegacyImages()
        {
            // Only used if XAML content is missing. Appends images to the bottom.
            try
            {
                var images = new List<string>();
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string imgSql = "SELECT FilePath FROM NoteImages WHERE NoteId = $id ORDER BY OrderIndex ASC";
                    using (var command = new SqliteCommand(imgSql, connection))
                    {
                        command.Parameters.AddWithValue("$id", _noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                            while (reader.Read())
                            {
                                string path = Path.Combine(imgDir, reader.GetString(0));
                                if(File.Exists(path)) images.Add(path);
                            }
                        }
                    }
                }

                if (images.Count > 0)
                {
                    var doc = ContentViewer.Document ?? new FlowDocument();
                    foreach (var imgPath in images)
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imgPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        var container = new BlockUIContainer(new System.Windows.Controls.Image { Source = bitmap, Stretch = System.Windows.Media.Stretch.Uniform, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 10) });
                        doc.Blocks.Add(container);
                    }
                    ContentViewer.Document = doc;
                }
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var editWin = new NoteInputWindow(_noteId);
            editWin.Owner = this.Owner; 
            this.Close(); // Close view window
            editWin.ShowDialog();
            // Note: If calling from Explorer, Explorer will refresh.
            // If we want to reopen View after edit, we'd need chaining. 
            // Standard UX: detailed view -> edit -> save -> detailed view is nice, but back to list is also fine.
            // Current flow: View -> Edit -> Close -> List.
        }
    }
}
