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
using System.Linq;

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
                    
                    // 1. Load basic note data
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
                                    var brush = new System.Windows.Media.BrushConverter().ConvertFromString(category.Color) as System.Windows.Media.Brush;
                                    CategoryCircle.Fill = brush ?? System.Windows.Media.Brushes.Gray;
                                }

                                bool xamlLoaded = false;
                                if (!string.IsNullOrEmpty(contentXaml))
                                {
                                    try 
                                    {
                                        var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Parse(contentXaml);
                                        flowDocument.PagePadding = new Thickness(0);
                                        ContentViewer.Document = flowDocument;
                                        xamlLoaded = true;
                                    }
                                    catch
                                    {
                                        SetPlainTextContent(content);
                                    }
                                }
                                else
                                {
                                    SetPlainTextContent(content);
                                }
                                
                                if (!xamlLoaded)
                                {
                                    LoadAttachedImages();
                                }
                            }
                        }
                    }

                    // 2. Load Tags
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

                    // 3. Load Attachments
                    string attachSql = "SELECT Id, FilePath, OriginalName, FileType FROM NoteAttachments WHERE NoteId = $id";
                    using (var command = new SqliteCommand(attachSql, connection))
                    {
                        command.Parameters.AddWithValue("$id", _noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            var attachments = new List<NoteAttachment>();
                            while (reader.Read())
                            {
                                attachments.Add(new NoteAttachment
                                {
                                    Id = reader.GetInt64(0),
                                    NoteId = _noteId,
                                    FilePath = reader.GetString(1),
                                    OriginalName = reader.GetString(2),
                                    FileType = reader.IsDBNull(3) ? "" : reader.GetString(3)
                                });
                            }
                            ItemsAttachments.ItemsSource = attachments;
                            PanelAttachments.Visibility = attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show("노트 로드 실패: " + ex.Message);
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

        private void SetPlainTextContent(string text)
        {
            var p = new Paragraph(new Run(text));
            var doc = new FlowDocument(p);
            doc.PagePadding = new Thickness(0);
            ContentViewer.Document = doc;
        }

        private void LoadAttachedImages()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    string sql = "SELECT FilePath FROM NoteImages WHERE NoteId = $id ORDER BY OrderIndex";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$id", _noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                            while (reader.Read())
                            {
                                string filePath = reader.GetString(0);
                                string fullPath = Path.Combine(imgDir, filePath);
                                if (File.Exists(fullPath))
                                {
                                    AddImageToContentViewer(fullPath);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void AddImageToContentViewer(string path)
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(path));
                var image = new Image { Source = bitmap, MaxWidth = 800, Margin = new Thickness(0, 10, 0, 10) };
                var container = new BlockUIContainer(image);
                ContentViewer.Document.Blocks.Add(container);
            }
            catch { }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var inputWin = new NoteInputWindow(_noteId);
            inputWin.Owner = this;
            if (inputWin.ShowDialog() == true)
            {
                LoadNoteData();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
