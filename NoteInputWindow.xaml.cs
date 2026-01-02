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
        private List<AttachmentItem> _deletedAttachments = new List<AttachmentItem>();
        
        // For Edit Mode
        private long? _editingNoteId = null;
        public bool IsEditMode => _isEditMode;
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
            WindowHelper.FixMaximizedWindow(this);
            _capturedImage = image;
            _sourceApp = sourceApp;
            _sourceUrl = sourceUrl;
            
            LoadCategories();
            LoadCategories();
            // UpdateUIForMode call removed, handled in UpdateUIText

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
            Editor.OcrRequested += OnEditorOcrRequested;
            LoadWindowState();
            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        // Constructor for Edit Note
        public NoteInputWindow(long noteId)
        {
            InitializeComponent();
            WindowHelper.FixMaximizedWindow(this);
            _editingNoteId = noteId;
            
            LoadCategories();
            LoadCategories();
            // UpdateUIForMode call removed, handled in UpdateUIText
            
            this.Loaded += (s, e) => LoadNoteData(noteId);
            this.MouseDown += (s, e) => { 
                if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1) DragMove(); 
                else if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2) ToggleMaximize();
            };
            
            // Subscribe to capture request from editor
            Editor.CaptureRequested += OnEditorCaptureRequested;
            Editor.OcrRequested += OnEditorOcrRequested;
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
                BtnMaximize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Restore");
            }
            else
            {
                PathMaximize.Data = System.Windows.Media.Geometry.Parse("M4,4H20V20H4V4M6,8V18H18V8H6Z");
                BtnMaximize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Maximize");
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
            this.Title = CatchCapture.Resources.LocalizationManager.GetString("NoteInputTitle") ?? "";
            
            // Common
            if (BtnClose != null) BtnClose.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Close");
            if (BtnMinimize != null) BtnMinimize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Minimize");
            if (BtnMaximize != null) 
            {
               BtnMaximize.ToolTip = this.WindowState == WindowState.Maximized 
                   ? CatchCapture.Resources.LocalizationManager.GetString("Restore") 
                   : CatchCapture.Resources.LocalizationManager.GetString("Maximize");
            }
            if (BtnManageCategories != null) BtnManageCategories.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("ManageCategories");
            
            if (TxtTitlePlaceholder != null) TxtTitlePlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("TitlePlaceholder");
            if (TxtTagsLabel != null) TxtTagsLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Tags");
            if (TxtTagsPlaceholder != null) TxtTagsPlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("TagsPlaceholder");
            
            if (TxtLinkLabel != null) TxtLinkLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("Link");
            if (TxtLinkPlaceholder != null) TxtLinkPlaceholder.Text = CatchCapture.Resources.LocalizationManager.GetString("LinkPlaceholder");
            
            if (TxtFilesLabel != null) TxtFilesLabel.Text = CatchCapture.Resources.LocalizationManager.GetString("FileAttachment");
            if (TxtFileAttachGuide != null) TxtFileAttachGuide.Text = CatchCapture.Resources.LocalizationManager.GetString("FileAttachmentGuide");
            
            if (BtnCancel != null) BtnCancel.Content = CatchCapture.Resources.LocalizationManager.GetString("BtnCancel");

            // Mode specific
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

            LoadCategories();
        }

        private void LoadCategories()
        {
            var selectedId = (CboCategory.SelectedItem as Category)?.Id;
            var categories = DatabaseManager.Instance.GetAllCategories();
            
            // Localize Default Category Name (ID=1)
            foreach (var cat in categories)
            {
                if (cat.Id == 1)
                {
                    cat.Name = CatchCapture.Resources.LocalizationManager.GetString("DefaultCategory") ?? "기본";
                }
            }

            CboCategory.ItemsSource = categories;

            // Restore selection
            if (selectedId.HasValue)
            {
                foreach (Category cat in CboCategory.Items)
                {
                    if (cat.Id == selectedId.Value)
                    {
                        CboCategory.SelectedItem = cat;
                        break;
                    }
                }
            }
            
            if (CboCategory.SelectedIndex == -1 && CboCategory.Items.Count > 0)
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
                if (item.IsExisting) _deletedAttachments.Add(item);
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
                    AddAttachment(fullPath, false);
                }
                RefreshAttachmentList();
            }
        }

        public void AddAttachment(string fullPath, bool refresh = true)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return;

            // Check if already added
            if (_attachments.Any(a => a.FullPath == fullPath)) return;

            var item = new AttachmentItem
            {
                FullPath = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                IsExisting = false
            };
            _attachments.Add(item);
            if (refresh) RefreshAttachmentList();
        }

        private void BtnAddFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    bool added = false;
                    foreach (string fullPath in files)
                    {
                        if (File.Exists(fullPath))
                        {
                            AddAttachment(fullPath, false);
                            added = true;
                        }
                    }
                    if (added) RefreshAttachmentList();
                }
            }
        }

        public void AppendImage(BitmapSource image)
        {
            if (image == null) return;
            Editor.InsertCapturedImageAtEnd(image);
            Editor.Focus();
        }

        public void AppendMediaFile(string filePath, BitmapSource? thumbnail = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            
            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".mp4" || ext == ".mp3" || ext == ".gif")
            {
                Editor.MoveCaretToEnd();
                Editor.InsertMediaFile(filePath, thumbnail);
            }
            else
            {
                AddAttachment(filePath);
            }
            Editor.Focus();
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

        private void OnEditorOcrRequested()
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
            // Hide immediately
            this.Hide(); 

            // Trigger OCR via MainWindow (bypasses instant edit mode, shows result window)
            // Add slight delay to ensure window is fully hidden (preventing ghosting in capture)
            Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => 
            {
                mainWindow.TriggerOcrForNote(() =>
                {
                    // This callback runs after OCR result window closes (or is cancelled)
                    Dispatcher.Invoke(() =>
                    {
                        // Restore NoteExplorerWindow if it was visible
                        if (wasExplorerVisible && explorer != null)
                        {
                            explorer.Show();
                            explorer.WindowState = previousExplorerState;
                        }

                        // Restore self
                        this.Show();
                        this.WindowState = previousState;
                        this.Activate();
                        this.Focus();
                    });
                });
            }));
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ImgPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Preview logic 
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
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
                string title = TxtTitle.Text ?? "";
                
                // If title is empty, try to generate from content
                if (string.IsNullOrWhiteSpace(title))
                {
                    string fullText = Editor.GetPlainText();
                    if (!string.IsNullOrEmpty(fullText))
                    {
                        using (var reader = new StringReader(fullText))
                        {
                            string? line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                bool hasMedia = line.Contains("\uFFFC");
                                string cleaned = line.Replace("\uFFFC", "").Replace("\u200B", "").Trim();
                                
                                if (!string.IsNullOrEmpty(cleaned))
                                {
                                    title = cleaned;
                                    if (title.Length > 50) title = title.Substring(0, 50).Trim() + "...";
                                    break;
                                }
                                else if (!hasMedia) break;
                            }
                        }
                    }
                }
                
                if (string.IsNullOrWhiteSpace(title)) title = CatchCapture.Resources.LocalizationManager.GetString("Untitled");

                GridLoading.Visibility = Visibility.Visible;
                BtnSave.IsEnabled = false;

                // [Fix] Hide all image sliders before serializing XAML
                Editor.HideAllSliders();

                // [Fix] Save all in-memory images to files and replace Source with file Uri
                // This ensures XAML contains valid file paths instead of unloadable RenderTargetBitmap
                try
                {
                    string imgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                    Editor.PrepareImagesForSave(imgDir);
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"PrepareImagesForSave Error: {ex.Message}");
                }

                var request = new SaveNoteRequest
                {
                    Id = _editingNoteId,
                    Title = title,
                    Content = Editor.GetPlainText() ?? "",
                    ContentXaml = Editor.GetXaml() ?? "",
                    Tags = TxtTags.Text ?? "",
                    CategoryId = (CboCategory.SelectedItem is Category cat) ? cat.Id : 1,
                    SourceApp = _sourceApp,
                    SourceUrl = _sourceUrl,
                    Images = imagesInEditor.Select(img => { if (img.IsFrozen == false) img.Freeze(); return img; }).ToList()
                };

                // Add existing, new, and deleted attachments
                foreach (var att in _attachments)
                {
                    request.Attachments.Add(new NoteAttachmentRequest
                    {
                        Id = att.AttachmentId,
                        FullPath = att.FullPath,
                        DisplayName = att.DisplayName,
                        IsExisting = att.IsExisting,
                        IsDeleted = false
                    });
                }
                foreach (var att in _deletedAttachments)
                {
                    request.Attachments.Add(new NoteAttachmentRequest
                    {
                        Id = att.AttachmentId,
                        DisplayName = att.DisplayName,
                        IsExisting = true,
                        IsDeleted = true
                    });
                }

                long targetNoteId = await DatabaseManager.Instance.SaveNoteAsync(request);

                // UI Cleanup after save
                GridLoading.Visibility = Visibility.Collapsed;

                if (_isEditMode)
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoteEditSuccess"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("NoteSaveSuccess"), CatchCapture.Resources.LocalizationManager.GetString("Notice"), MessageBoxButton.OK, MessageBoxImage.Information);

                // Navigation Logic
                if (!_isEditMode && !(this.Owner is NoteExplorerWindow))
                {
                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (mainWindow != null) mainWindow.WindowState = WindowState.Minimized;
                }

                if (CatchCapture.NoteExplorerWindow.Instance != null && CatchCapture.NoteExplorerWindow.Instance.IsLoaded)
                {
                    CatchCapture.NoteExplorerWindow.Instance.WindowState = WindowState.Normal;
                    CatchCapture.NoteExplorerWindow.Instance.Activate();
                    CatchCapture.NoteExplorerWindow.Instance.RefreshNotes();
                }
                else
                {
                    new CatchCapture.NoteExplorerWindow().Show();
                }

                try { this.DialogResult = true; } catch { }
                this.Close();
            }
            catch (Exception ex)
            {
                GridLoading.Visibility = Visibility.Collapsed;
                BtnSave.IsEnabled = true;
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrSaveNote") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
