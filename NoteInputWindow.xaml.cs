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
        private List<AttachmentItem> _attachments = new List<AttachmentItem>();
        
        // For Edit Mode
        private long? _editingNoteId = null;
        private bool _isEditMode => _editingNoteId.HasValue;

        public class AttachmentItem
        {
            public string? FullPath { get; set; } // Local path for new files
            public string DisplayName { get; set; } = "";
            public bool IsExisting { get; set; }
            public long? AttachmentId { get; set; }
            public string? FilePath { get; set; } // DB filename for existing files
        }

        // Constructor for New Note (Capture or Empty)
        public NoteInputWindow(BitmapSource? image, string? sourceApp = null, string? sourceUrl = null, string? attachFilePath = null)
        {
            InitializeComponent();
            _capturedImage = image;
            _sourceApp = sourceApp;
            _sourceUrl = sourceUrl;
            
            LoadCategories();
            UpdateUIForMode();

            // Display source metadata
            string sourceDisplayName = _sourceApp ?? CatchCapture.Resources.LocalizationManager.GetString("Unknown");
            if (!string.IsNullOrEmpty(_sourceUrl)) sourceDisplayName += $" - {_sourceUrl}";
            TxtSourceInfo.Text = sourceDisplayName;

            // If file path is provided, check if it's a media file
            if (!string.IsNullOrEmpty(attachFilePath) && File.Exists(attachFilePath))
            {
                string ext = Path.GetExtension(attachFilePath).ToLower();
                if (ext == ".mp4" || ext == ".mp3" || ext == ".gif")
                {
                    // 미디어 파일은 에디터에 직접 삽입 (Loaded 이벤트에서 처리)
                    this.Loaded += (s, e) =>
                    {
                        Editor.InsertMediaFile(attachFilePath, _capturedImage);
                        Editor.Focus();
                    };
                }
                else
                {
                    // 일반 파일은 첨부파일로 추가
                    AddAttachment(attachFilePath);
                }
            }
            else
            {
                this.Loaded += NoteInputWindow_Loaded;
            }
            
            this.MouseDown += (s, e) => { 
                if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1) DragMove(); 
                else if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2) ToggleMaximize();
            };
            
            // Subscribe to capture request from editor
            Editor.CaptureRequested += OnEditorCaptureRequested;
            LoadWindowState();
            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        // Constructor for Edit Note
        public NoteInputWindow(long noteId)
        {
            InitializeComponent();
            _editingNoteId = noteId;
            
            LoadCategories();
            UpdateUIForMode();
            
            this.Loaded += (s, e) => LoadNoteData(noteId);
            this.MouseDown += (s, e) => { 
                if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1) DragMove(); 
                else if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2) ToggleMaximize();
            };
            
            // Subscribe to capture request from editor
            Editor.CaptureRequested += OnEditorCaptureRequested;
            LoadWindowState();
            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SaveWindowState();
        }

        private void LoadWindowState()
        {
            try
            {
                var settings = Settings.Load();
                
                // Restore window size
                if (settings.NoteInputWidth > 0)
                    this.Width = settings.NoteInputWidth;
                if (settings.NoteInputHeight > 0)
                    this.Height = settings.NoteInputHeight;
                
                // Restore window position
                if (settings.NoteInputLeft > -9999 && settings.NoteInputTop > -9999)
                {
                    this.Left = settings.NoteInputLeft;
                    this.Top = settings.NoteInputTop;
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
                    settings.NoteInputWidth = this.ActualWidth;
                    settings.NoteInputHeight = this.ActualHeight;
                    settings.NoteInputLeft = this.Left;
                    settings.NoteInputTop = this.Top;
                }
                
                settings.Save();
            }
            catch { /* Ignore save errors */ }
        }

        private void NoteInputWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16M6,10V18H14V10H6Z");
                BtnMaximize.ToolTip = "이전 크기로 복원";
            }
            else
            {
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
                BtnMaximize.ToolTip = "최대화";
            }
        }

        private void ToggleMaximize()
        {
            this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }



        private void UpdateUIText()
        {
            this.Title = CatchCapture.Resources.LocalizationManager.GetString("NoteInputTitle") ?? "캐치캡처-글쓰기";
            if (TxtHeaderTitle != null) TxtHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("Write");
            if (BtnClose != null) BtnClose.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Close");
            if (BtnManageCategories != null) BtnManageCategories.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ManageCategories");
            if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("TitlePlaceholder");
            if (TxtTagsLabel != null) TxtTagsLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Tags");
            if (TxtTagsPlaceholder != null) TxtTagsPlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("TagsPlaceholder");
            if (TxtLinkLabel != null) TxtLinkLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Link");
            if (TxtLinkPlaceholder != null) TxtLinkPlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("LinkPlaceholder");
            if (TxtFilesLabel != null) TxtFilesLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("FileAttachment");
            if (TxtFileAttachGuide != null) TxtFileAttachGuide.Text = CatchCapture.Resources.LocalizationManager.GetString("FileAttachmentGuide");
            
            if (BtnCancel != null) BtnCancel.Content = CatchCapture.Resources.LocalizationManager.GetString("Cancel");
            if (BtnSave != null) BtnSave.Content = CatchCapture.Resources.LocalizationManager.GetString("NoteSave");
        }

        private void UpdateUIForMode()
        {
            if (_isEditMode)
            {
                if (TxtHeaderTitle != null) TxtHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("EditNote");
                if (BtnSave != null) BtnSave.Content = CatchCapture.Resources.LocalizationManager.GetString("FinishEdit");
            }
            else
            {
                if (TxtHeaderTitle != null) TxtHeaderTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("WriteNote");
                if (BtnSave != null) BtnSave.Content = CatchCapture.Resources.LocalizationManager.GetString("FinishWrite");
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

                                // Load all images for this note from DB (for manual fallback and image list)
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

                                // Use XAML if available to preserve EXACT interleaved order
                                if (!string.IsNullOrEmpty(contentXaml))
                                {
                                    Editor.SetXaml(contentXaml);
                                }
                                else
                                {
                                    // Manual Fallback (Legacy)
                                    Editor.Document.Blocks.Clear();
                                    
                                    // Grouping Logic (as before, since we don't know the exact order for legacy notes)
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
                                    
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        Editor.Document.Blocks.Add(new Paragraph(new Run(content)));
                                    }
                                    
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
                                string sourceDisplayName = _sourceApp ?? CatchCapture.Resources.LocalizationManager.GetString("Unknown");
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

                    // 3. Load Attachments
                    string attachSql = "SELECT Id, FilePath, OriginalName FROM NoteAttachments WHERE NoteId = $id";
                    using (var command = new SqliteCommand(attachSql, connection))
                    {
                        command.Parameters.AddWithValue("$id", noteId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new AttachmentItem
                                {
                                    AttachmentId = reader.GetInt64(0),
                                    FilePath = reader.GetString(1),
                                    DisplayName = reader.GetString(2),
                                    IsExisting = true
                                };
                                _attachments.Add(item);
                            }
                            RefreshAttachmentList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrReadNote") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
            }
        }

        private void RefreshAttachmentList()
        {
            LstAttachments.ItemsSource = null;
            LstAttachments.ItemsSource = _attachments;
        }

        private void BtnRemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AttachmentItem item)
            {
                _attachments.Remove(item);
                RefreshAttachmentList();
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
                Title = CatchCapture.Resources.LocalizationManager.GetString("SelectAttachFiles"),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (string fullPath in dialog.FileNames)
                {
                    AddAttachment(fullPath);
                }
            }
        }

        public void AddAttachment(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return;

            var item = new AttachmentItem
            {
                FullPath = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                IsExisting = false
            };
            _attachments.Add(item);
            RefreshAttachmentList();
        }

        private void BtnAddFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    foreach (string fullPath in files)
                    {
                        if (File.Exists(fullPath))
                        {
                            var item = new AttachmentItem
                            {
                                FullPath = fullPath,
                                DisplayName = Path.GetFileName(fullPath),
                                IsExisting = false
                            };
                            _attachments.Add(item);
                        }
                    }
                    RefreshAttachmentList();
                }
            }
        }

        private void OnEditorCaptureRequested()
        {
            // Get MainWindow reference
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow == null)
            {
                CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrMainWinNotFound"), CatchCapture.Resources.LocalizationManager.GetString("Error"));
                return;
            }

            // Check if NoteExplorerWindow is open and visible
            var explorer = NoteExplorerWindow.Instance;
            bool wasExplorerVisible = explorer != null && explorer.IsVisible;
            var previousExplorerState = explorer?.WindowState ?? WindowState.Normal;

            if (wasExplorerVisible && explorer != null)
            {
                explorer.Hide();
            }

            // Minimize instead of Hide to preserve dialog context
            var previousState = this.WindowState;
            this.WindowState = WindowState.Minimized;

            // Trigger capture via MainWindow (bypasses instant edit mode)
            mainWindow.TriggerCaptureForNote((capturedImage) =>
            {
                // This callback runs after capture completes (or is cancelled)
                Dispatcher.Invoke(() =>
                {
                    // Restore NoteExplorerWindow if it was visible
                    if (wasExplorerVisible && explorer != null)
                    {
                        explorer.Show();
                        explorer.WindowState = previousExplorerState;
                    }

                    // Restore window state
                    this.WindowState = previousState;
                    this.Activate();
                    this.Focus();

                    if (capturedImage != null)
                    {
                        // Insert captured image into editor
                        Editor.InsertCapturedImage(capturedImage);
                        Editor.Focus();
                    }
                });
            });
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
            Editor.HideAllSliders();
            var imagesInEditor = Editor.GetAllImages();
            
            if (imagesInEditor.Count == 0 && _capturedImage == null && string.IsNullOrWhiteSpace(TxtTitle.Text) && string.IsNullOrWhiteSpace(Editor.GetPlainText()))
            {
                 CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoContentToSave"), CatchCapture.Resources.LocalizationManager.GetString("Notice"));
                 return;
            }

            try
            {
                string title = TxtTitle.Text;
                if (string.IsNullOrWhiteSpace(title)) title = CatchCapture.Resources.LocalizationManager.GetString("Untitled");
                
                string content = Editor.GetPlainText();
                string tags = TxtTags.Text;

                long categoryId = 1;
                if (CboCategory.SelectedItem is Category cat) categoryId = cat.Id;

                // Prepare Save Folders
                var settings = Settings.Load();
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

                List<(string FileName, string Hash)> savedImages = new List<(string FileName, string Hash)>();
                
                foreach (var imgSource in imagesInEditor)
                {
                    if (imgSource is BitmapImage bi && bi.UriSource != null && bi.UriSource.IsFile)
                    {
                        // Existing file check (already in our storage - subfolders included)
                        string filePath = bi.UriSource.LocalPath;
                        if (filePath.StartsWith(imgDir, StringComparison.OrdinalIgnoreCase))
                        {
                            string relativePath = filePath.Substring(imgDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            string h = DatabaseManager.Instance.ComputeHash(filePath);
                            savedImages.Add((relativePath, h));
                            continue;
                        }
                    }

                    // Save to memory first to compute hash and check for duplicates
                    bool isRemote = (imgSource is BitmapImage biRemote && biRemote.UriSource != null && !biRemote.UriSource.IsFile);
                    string saveFormat = isRemote ? "JPG" : (settings.NoteSaveFormat ?? "PNG");
                    int saveQuality = isRemote ? 80 : settings.NoteImageQuality;

                    using (var ms = new MemoryStream())
                    {
                        BitmapEncoder encoder;
                        string fmt = saveFormat.ToUpper();

                        // ... (same encoder selection logic) ...
                        switch (fmt)
                        {
                            case "JPG": case "JPEG":
                                var jpgEncoder = new JpegBitmapEncoder(); jpgEncoder.QualityLevel = saveQuality; encoder = jpgEncoder; break;
                            case "BMP": encoder = new BmpBitmapEncoder(); break;
                            case "GIF": encoder = new GifBitmapEncoder(); break;
                            case "PNG": default: encoder = new PngBitmapEncoder(); break;
                        }

                        encoder.Frames.Add(BitmapFrame.Create(imgSource));
                        encoder.Save(ms);
                        
                        ms.Position = 0;
                        string hash;
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        {
                            var hashBytes = md5.ComputeHash(ms);
                            hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        }

                        string? existingFileName = DatabaseManager.Instance.GetExistingImageByHash(hash);
                        if (existingFileName != null)
                        {
                            savedImages.Add((existingFileName, hash));
                            continue; 
                        }

                        string ext = "." + saveFormat.ToLower();
                        string fileNameOnly = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
                        
                        string yearSub = DatabaseManager.Instance.EnsureYearFolderExists(imgDir);
                        string fileName = Path.Combine(yearSub, fileNameOnly);
                        string fullPath = Path.Combine(imgDir, fileName);
                        
                        ms.Position = 0;
                        using (var fileStream = new FileStream(fullPath, FileMode.Create)) { ms.CopyTo(fileStream); }
                        savedImages.Add((fileName, hash));
                    }
                }

                var savedFileNames = savedImages.Select(x => x.FileName).ToList();
                Editor.UpdateImageSources(savedFileNames);
                string contentXaml = Editor.GetXaml();

                long targetNoteId;

                if (_isEditMode)
                {
                    targetNoteId = _editingNoteId!.Value;
                    DatabaseManager.Instance.UpdateNote(targetNoteId, title, content, contentXaml, tags, categoryId);
                    
                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        using (var cmd = new SqliteCommand("DELETE FROM NoteImages WHERE NoteId = $id", connection))
                        {
                            cmd.Parameters.AddWithValue("$id", targetNoteId);
                            cmd.ExecuteNonQuery();
                        }
                        for (int i = 0; i < savedImages.Count; i++)
                        {
                            using (var cmd = new SqliteCommand("INSERT INTO NoteImages (NoteId, FilePath, OrderIndex, FileHash) VALUES ($nid, $path, $idx, $hash)", connection))
                            {
                                cmd.Parameters.AddWithValue("$nid", targetNoteId);
                                cmd.Parameters.AddWithValue("$path", savedImages[i].FileName);
                                cmd.Parameters.AddWithValue("$idx", i);
                                cmd.Parameters.AddWithValue("$hash", savedImages[i].Hash);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoteEditSuccess"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    targetNoteId = DatabaseManager.Instance.InsertNote(title, content, contentXaml, tags, "", _sourceApp, _sourceUrl, categoryId);

                    for (int i = 0; i < savedImages.Count; i++)
                    {
                        DatabaseManager.Instance.AddNoteImage(targetNoteId, savedImages[i].FileName, i, savedImages[i].Hash);
                    }
                    
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoteSaveSuccess"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Attachments
                string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                
                // If editing, we might need to delete old ones from DB that were removed from list
                if (_isEditMode)
                {
                    using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                    {
                        connection.Open();
                        // Get current attachment IDs for this note from DB
                        var dbIds = new List<long>();
                        string getIdsSql = "SELECT Id FROM NoteAttachments WHERE NoteId = $id";
                        using(var cmd = new SqliteCommand(getIdsSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$id", targetNoteId);
                            using(var reader = cmd.ExecuteReader())
                            {
                                while(reader.Read()) dbIds.Add(reader.GetInt64(0));
                            }
                        }

                        // IDs that are in DB but NOT in our current _attachments list should be deleted
                        var remainingIds = _attachments.Where(a => a.IsExisting).Select(a => a.AttachmentId!.Value).ToList();
                        foreach(var idToDelete in dbIds.Except(remainingIds))
                        {
                            // Optional: Delete physical file too? 
                            // For simplicity, let's just delete from DB for now, 
                            // or fetch path and delete if we want to be thorough.
                            string getFilePathSql = "SELECT FilePath FROM NoteAttachments WHERE Id = $id";
                            string? filePathToDelete = null;
                            using(var cmd = new SqliteCommand(getFilePathSql, connection))
                            {
                                cmd.Parameters.AddWithValue("$id", idToDelete);
                                filePathToDelete = cmd.ExecuteScalar()?.ToString();
                            }

                            string deleteAttachSql = "DELETE FROM NoteAttachments WHERE Id = $id";
                            using(var cmd = new SqliteCommand(deleteAttachSql, connection))
                            {
                                cmd.Parameters.AddWithValue("$id", idToDelete);
                                cmd.ExecuteNonQuery();
                            }

                            if(!string.IsNullOrEmpty(filePathToDelete))
                            {
                                try
                                {
                                    string fullPath = Path.Combine(attachDir, filePathToDelete);
                                    if(File.Exists(fullPath)) File.Delete(fullPath);
                                }
                                catch { }
                            }
                        }
                    }
                }

                foreach (var item in _attachments.Where(a => !a.IsExisting))
                {
                    if (File.Exists(item.FullPath))
                    {
                        string originalName = item.DisplayName;
                        string yearSub = DatabaseManager.Instance.EnsureYearFolderExists(attachDir);
                        string newFileNameOnly = $"{Guid.NewGuid()}_{originalName}";
                        string newFileName = Path.Combine(yearSub, newFileNameOnly);
                        string newFullPath = Path.Combine(attachDir, newFileName);
                        
                        File.Copy(item.FullPath, newFullPath);
                        DatabaseManager.Instance.InsertAttachment(targetNoteId, newFileName, originalName);
                    }
                }

                // 저장 후 내 노트 탐색기 열기 (또는 활성화)
                // 저장 후 내 노트 탐색기 열기 (또는 활성화) 및 메인 윈도우 최소화
                // [Fix] 기존 노트 수정 시 또는 탐색기에서 새 노트를 만들 때는 메인 윈도우를 최소화하지 않음
                if (!_isEditMode && !(this.Owner is NoteExplorerWindow))
                {
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (mainWindow != null && mainWindow.WindowState != WindowState.Minimized)
                    {
                        mainWindow.WindowState = WindowState.Minimized;
                    }
                }

                if (CatchCapture.NoteExplorerWindow.Instance != null && CatchCapture.NoteExplorerWindow.Instance.IsLoaded)
                {
                    CatchCapture.NoteExplorerWindow.Instance.WindowState = WindowState.Normal;
                    CatchCapture.NoteExplorerWindow.Instance.Activate();
                    CatchCapture.NoteExplorerWindow.Instance.RefreshNotes(); // Force refresh if method available or needed
                }
                else
                {
                    var explorer = new CatchCapture.NoteExplorerWindow();
                    // Since specific owner might be minimized, we might just show it independent or keep owner relationship but minimize owner.
                    // If owner is minimized, owned window might be minimized too if they are strictly linked? 
                    // No, usually owned windows minimize with owner.
                    // If we minimize MainWindow (Owner), NoteExplorer (Owned) might hide.
                    // So we should NOT set Owner if we intend to minimize the MainWindow but keep Explorer visible.
                    // Or we just minimize MainWindow and ensure Explorer is separate.
                    // Let's NOT set owner if we minimize Main.
                    explorer.Show();
                }

                // Set DialogResult only if opened as dialog (try-catch for safety)
                try
                {
                    this.DialogResult = true;
                }
                catch (InvalidOperationException)
                {
                    // Window was not opened as dialog (e.g., after Hide/Show)
                }
                this.Close();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrSaveNote") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
