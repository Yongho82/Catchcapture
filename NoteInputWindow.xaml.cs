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

namespace CatchCapture
{
    public partial class NoteInputWindow : Window
    {
        private BitmapSource? _capturedImage;
        private string? _sourceApp;
        private string? _sourceUrl;
        private List<string> _attachmentPaths = new List<string>();

        public NoteInputWindow(BitmapSource image, string? sourceApp = null, string? sourceUrl = null)
        {
            InitializeComponent();
            _capturedImage = image;
            _sourceApp = sourceApp;
            _sourceUrl = sourceUrl;
            
            LoadCategories();

            // Display source metadata
            string sourceDisplayName = _sourceApp ?? "알 수 없음";
            if (!string.IsNullOrEmpty(_sourceUrl)) sourceDisplayName += $" - {_sourceUrl}";
            TxtSourceInfo.Text = sourceDisplayName;
            
            // Defer image insertion to Loaded event to ensure RichTextBox is ready for Undo/Selection
            this.Loaded += NoteInputWindow_Loaded;
            
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
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

        private void BtnManageCategories_Click(object sender, RoutedEventArgs e)
        {
            var manageWin = new CategoryManagementWindow();
            manageWin.Owner = this;
            manageWin.ShowDialog();
            LoadCategories();
        }

        private void NoteInputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Insert captured image into editor once the window is loaded
            if (_capturedImage != null)
            {
                Editor.InitializeWithImage(_capturedImage);
                // Ensure focus is on the editor so Undo works immediately if needed
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
            if (e.ClickCount == 2 && _capturedImage != null)
            {
                // Open PreviewWindow (image editor)
                var previewWin = new PreviewWindow(_capturedImage, 0);
                previewWin.Owner = this;
                if (previewWin.ShowDialog() == true)
                {
                    // Update preview with edited image if editor returned true (not implemented yet in PreviewWindow, but prepared)
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var imagesInEditor = Editor.GetAllImages();
            // If the user deleted everything, we can still fall back to the original capture if it exists
            if (imagesInEditor.Count == 0 && _capturedImage == null) return;

            try
            {
                string title = TxtTitle.Text;
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "제목없음";
                }
                
                string content = Editor.GetPlainText();
                string tags = TxtTags.Text;

                long categoryId = 1;
                if (CboCategory.SelectedItem is Category cat)
                {
                    categoryId = cat.Id;
                }

                // Prepare Save Folders
                var settings = Settings.Load();
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

                List<string> savedFileNames = new List<string>();
                
                // Identify images to save: either from editor or fallback capture
                var imagesToSave = imagesInEditor.Count > 0 ? imagesInEditor : new List<BitmapSource> { _capturedImage! };

                // 1. Save all images to disk
                foreach (var imgSource in imagesToSave)
                {
                    string ext = settings.OptimizeNoteImages ? ".jpg" : ".png";
                    string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
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

                // 2. Save Metadata to DB
                // InsertNote always inserts ONE image (the primary one)
                long noteId = DatabaseManager.Instance.InsertNote(title, content, tags, savedFileNames[0], _sourceApp, _sourceUrl, categoryId);

                // 3. Save additional images to DB if any
                for (int i = 1; i < savedFileNames.Count; i++)
                {
                    DatabaseManager.Instance.AddNoteImage(noteId, savedFileNames[i], i);
                }

                // 3. Save Attachments
                string attachDir = DatabaseManager.Instance.GetAttachmentsFolderPath();
                foreach (var oldPath in _attachmentPaths)
                {
                    if (File.Exists(oldPath))
                    {
                        string originalName = Path.GetFileName(oldPath);
                        string newFileName = $"{Guid.NewGuid()}_{originalName}";
                        string newFullPath = Path.Combine(attachDir, newFileName);
                        
                        File.Copy(oldPath, newFullPath);
                        DatabaseManager.Instance.InsertAttachment(noteId, newFileName, originalName);
                    }
                }

                CatchCapture.CustomMessageBox.Show("노트가 성공적으로 저장되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
