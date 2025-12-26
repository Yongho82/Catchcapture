using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using CatchCapture.Utilities;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Linq;
using System.Collections.Generic;
using CatchCapture.Models;
using Microsoft.Data.Sqlite;

namespace CatchCapture
{
    public partial class NoteInputWindow : Window
    {
        private BitmapSource? _capturedImage;
        private string? _sourceApp;
        private string? _sourceUrl;
        private List<string> _attachmentPaths = new List<string>();
        
        // For Edit Mode
        private long? _editingNoteId = null;
        private bool _isEditMode => _editingNoteId.HasValue;

        // Constructor for New Note (Capture or Empty)
        public NoteInputWindow(BitmapSource? image, string? sourceApp = null, string? sourceUrl = null)
        {
            InitializeComponent();
            _capturedImage = image;
            _sourceApp = sourceApp;
            _sourceUrl = sourceUrl;
            
            LoadCategories();
            UpdateUIForMode();

            // Display source metadata
            string sourceDisplayName = _sourceApp ?? "알 수 없음";
            if (!string.IsNullOrEmpty(_sourceUrl)) sourceDisplayName += $" - {_sourceUrl}";
            TxtSourceInfo.Text = sourceDisplayName;
            
            this.Loaded += NoteInputWindow_Loaded;
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }

        // Constructor for Edit Note
        public NoteInputWindow(long noteId)
        {
            InitializeComponent();
            _editingNoteId = noteId;
            
            LoadCategories();
            UpdateUIForMode();
            
            this.Loaded += (s, e) => LoadNoteData(noteId);
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }

        private void UpdateUIForMode()
        {
            if (_isEditMode)
            {
                TxtHeaderTitle.Text = "노트 수정";
                BtnSave.Content = "수정 완료";
            }
            else
            {
                TxtHeaderTitle.Text = "노트 쓰기";
                BtnSave.Content = "작성 완료";
            }
        }

        private void LoadCategories()
        {
            var categories = DatabaseManager.Instance.GetAllCategories();
            CboCategory.ItemsSource = categories;
            if (CboCategory.Items.Count > 0)
            {
                CboCategory.SelectedIndex = 0;
            }
        }

        private void LoadNoteData(long noteId)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    
                    // 1. Load Note Info
                    // 1. Load Note Info
                    string sql = "SELECT Title, Content, SourceApp, SourceUrl, CategoryId, ContentXaml FROM Notes WHERE Id = $id";
                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("$id", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TxtTitle.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                string content = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string contentXaml = reader.IsDBNull(5) ? "" : reader.GetString(5);

                                // Try XAML first to preserve image/text order
                                if (!string.IsNullOrEmpty(contentXaml))
                                {
                                    // XAML preserves the exact order (image1 → text → image2)
                                    Editor.SetXaml(contentXaml);
                                }
                                else
                                {
                                    // Legacy mode: Load first image, then text, then remaining images
                                    Editor.Document.Blocks.Clear();
                                    
                                    // Get all images from DB
                                    var imageFiles = new List<string>();
                                    string imgSql = "SELECT FilePath FROM NoteImages WHERE NoteId = $imgNoteId ORDER BY OrderIndex ASC";
                                    string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                                    using (var imgCmd = new SqliteCommand(imgSql, connection))
                                    {
                                        imgCmd.Parameters.AddWithValue("$imgNoteId", noteId);
                                        using (var imgReader = imgCmd.ExecuteReader())
                                        {
                                            while (imgReader.Read())
                                            {
                                                string fileName = imgReader.GetString(0);
                                                string fullPath = Path.Combine(imgDir, fileName);
                                                if (File.Exists(fullPath))
                                                    imageFiles.Add(fullPath);
                                            }
                                        }
                                    }
                                    
                                    // Insert first image
                                    if (imageFiles.Count > 0)
                                    {
                                        try
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(imageFiles[0]);
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                            Editor.InsertImage(bitmap);
                                        }
                                        catch { }
                                    }
                                    
                                    // Add text content
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        Editor.Document.Blocks.Add(new Paragraph(new Run(content)));
                                    }
                                    
                                    // Add remaining images after text
                                    for (int i = 1; i < imageFiles.Count; i++)
                                    {
                                        try
                                        {
                                            var bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(imageFiles[i]);
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                            Editor.InsertImage(bitmap);
                                        }
                                        catch { }
                                    }
                                }

                                _sourceApp = reader.IsDBNull(2) ? null : reader.GetString(2);
                                _sourceUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
                                long catId = reader.GetInt64(4);

                                // Select Category
                                foreach (Category cat in CboCategory.Items)
                                {
                                    if (cat.Id == catId)
                                    {
                                        CboCategory.SelectedItem = cat;
                                        break;
                                    }
                                }
                                
                                // Update source info UI
                                string sourceDisplayName = _sourceApp ?? "알 수 없음";
                                if (!string.IsNullOrEmpty(_sourceUrl)) sourceDisplayName += $" - {_sourceUrl}";
                                TxtSourceInfo.Text = _sourceUrl ?? ""; 
                            }
                        }
                    }

                    // 2. Load Tags
                    string tagSql = @"
                        SELECT t.Name FROM Tags t
                        JOIN NoteTags nt ON t.Id = nt.TagId
                        WHERE nt.NoteId = $id";
                    using (var command = new SqliteCommand(tagSql, connection))
                    {
                        command.Parameters.AddWithValue("$id", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            var tags = new List<string>();
                            while (reader.Read())
                            {
                                tags.Add(reader.GetString(0));
                            }
                            TxtTags.Text = string.Join(", ", tags);
                        }
                    }

                    // Note: Image loading for legacy notes is now handled in Step 1's else block
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show("노트 정보를 불러오는 중 오류 발생: " + ex.Message);
            }
        }

        private void BtnManageCategories_Click(object sender, RoutedEventArgs e)
        {
            var manageWin = new CategoryManagementWindow();
            manageWin.Owner = this;
            manageWin.ShowDialog();
            LoadCategories();
        }

        private void NoteInputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode && _capturedImage != null)
            {
                Editor.InitializeWithImage(_capturedImage);
                Editor.Focus(); 
            }
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "첨부 파일 선택",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (string fullPath in dialog.FileNames)
                {
                    _attachmentPaths.Add(fullPath);
                    LstAttachments.Items.Add(Path.GetFileName(fullPath));
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ImgPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Preview logic 
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var imagesInEditor = Editor.GetAllImages();
            
            if (imagesInEditor.Count == 0 && _capturedImage == null && string.IsNullOrWhiteSpace(TxtTitle.Text) && string.IsNullOrWhiteSpace(Editor.GetPlainText()))
            {
                 CatchCapture.CustomMessageBox.Show("저장할 내용이 없습니다.");
                 return;
            }

            try
            {
                string title = TxtTitle.Text;
                if (string.IsNullOrWhiteSpace(title)) title = "제목없음";
                
                string content = Editor.GetPlainText();
                string tags = TxtTags.Text;

                long categoryId = 1;
                if (CboCategory.SelectedItem is Category cat) categoryId = cat.Id;

                // Prepare Save Folders
                var settings = Settings.Load();
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

                List<string> savedFileNames = new List<string>();
                
                foreach (var imgSource in imagesInEditor)
                {
                    string fileName;
                    
                    if (imgSource is BitmapImage bi && bi.UriSource != null && bi.UriSource.IsFile)
                    {
                        // Existing file check
                        string filePath = bi.UriSource.LocalPath;
                        if (Path.GetDirectoryName(filePath)?.Equals(imgDir, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            savedFileNames.Add(Path.GetFileName(filePath));
                            continue;
                        }
                    }

                    // Save new file
                    string ext = settings.OptimizeNoteImages ? ".jpg" : ".png";
                    fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
                    string fullPath = Path.Combine(imgDir, fileName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        BitmapFrame frame = BitmapFrame.Create(imgSource);
                        if (settings.OptimizeNoteImages)
                        {
                            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                            encoder.QualityLevel = settings.NoteImageQuality;
                            encoder.Frames.Add(frame);
                            encoder.Save(fileStream);
                        }
                        else
                        {
                            PngBitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(frame);
                            encoder.Save(fileStream);
                        }
                    }
                    savedFileNames.Add(fileName);
                }

                // CRITICAL: We need to update the editor's image sources to point to the saved files BEFORE getting XAML!
                Editor.UpdateImageSources(savedFileNames);
                string contentXaml = Editor.GetXaml();

                long targetNoteId;

                if (_isEditMode)
                {
                    targetNoteId = _editingNoteId!.Value;
                    // UPDATE
                    DatabaseManager.Instance.UpdateNote(targetNoteId, title, content, contentXaml, tags, categoryId);
                    
                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        using (var cmd = new SqliteCommand("DELETE FROM NoteImages WHERE NoteId = $id", connection))
                        {
                            cmd.Parameters.AddWithValue("$id", targetNoteId);
                            cmd.ExecuteNonQuery();
                        }
                        for (int i = 0; i < savedFileNames.Count; i++)
                        {
                            using (var cmd = new SqliteCommand("INSERT INTO NoteImages (NoteId, FilePath, OrderIndex) VALUES ($nid, $path, $idx)", connection))
                            {
                                cmd.Parameters.AddWithValue("$nid", targetNoteId);
                                cmd.Parameters.AddWithValue("$path", savedFileNames[i]);
                                cmd.Parameters.AddWithValue("$idx", i);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    
                    CatchCapture.CustomMessageBox.Show("노트가 성공적으로 수정되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // INSERT - Don't pass mainImage to avoid duplication, we'll add all images via AddNoteImage
                    targetNoteId = DatabaseManager.Instance.InsertNote(title, content, contentXaml, tags, "", _sourceApp, _sourceUrl, categoryId);

                    // Add all images to NoteImages table
                    for (int i = 0; i < savedFileNames.Count; i++)
                    {
                        DatabaseManager.Instance.AddNoteImage(targetNoteId, savedFileNames[i], i);
                    }
                    
                    CatchCapture.CustomMessageBox.Show("노트가 성공적으로 저장되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Attachments
                string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                foreach (var oldPath in _attachmentPaths)
                {
                    if (File.Exists(oldPath))
                    {
                        string originalName = Path.GetFileName(oldPath);
                        string newFileName = $"{Guid.NewGuid()}_{originalName}";
                        string newFullPath = Path.Combine(attachDir, newFileName);
                        
                        File.Copy(oldPath, newFullPath);
                        DatabaseManager.Instance.InsertAttachment(targetNoteId, newFileName, originalName);
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
