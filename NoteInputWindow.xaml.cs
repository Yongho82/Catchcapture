using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using CatchCapture.Utilities;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Linq;

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
            
            // Display source metadata
            string sourceDisplayName = _sourceApp ?? "알 수 없음";
            if (!string.IsNullOrEmpty(_sourceUrl)) sourceDisplayName += $" - {_sourceUrl}";
            TxtSourceInfo.Text = sourceDisplayName;
            
            // Insert captured image into editor
            if (_capturedImage != null)
            {
                Editor.Document.Blocks.Clear(); // Clear initial empty paragraph
                Editor.InsertImage(_capturedImage);
                // Add an empty paragraph after image for text input
                Editor.Document.Blocks.Add(new Paragraph());
            }
            
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
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
            if (_capturedImage == null) return;

            try
            {
                string title = TxtTitle.Text;
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "제목없음";
                }
                
                string content = Editor.GetPlainText();
                string tags = TxtTags.Text;
                // 1. Save Image to File
                string imgDir = DatabaseManager.Instance.GetImageFolderPath();
                string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                string fullPath = Path.Combine(imgDir, fileName);

                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    BitmapFrame frame = BitmapFrame.Create(_capturedImage);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(frame);
                    encoder.Save(fileStream);
                }

                // 2. Save Metadata to DB
                long noteId = DatabaseManager.Instance.InsertNote(title, content, tags, fileName, _sourceApp, _sourceUrl);

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

                MessageBox.Show("노트가 성공적으로 저장되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
