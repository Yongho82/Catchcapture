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
using System.Windows.Threading;
using System.Windows.Media;

namespace CatchCapture
{
    public partial class NoteViewWindow : Window
    {
        private long _noteId;
        private DispatcherTimer _hideHeaderTimer;

        public NoteViewWindow(long noteId)
        {
            InitializeComponent();
            WindowHelper.FixMaximizedWindow(this);
            _noteId = noteId;

            // Timer for auto-hiding header and footer
            _hideHeaderTimer = new DispatcherTimer();
            _hideHeaderTimer.Interval = TimeSpan.FromSeconds(3);
            _hideHeaderTimer.Tick += (s, e) => {
                if (this.Topmost && !HeaderGrid.IsMouseOver && !FooterBorder.IsMouseOver)
                {
                    HeaderGrid.Visibility = Visibility.Collapsed;
                    FooterBorder.Visibility = Visibility.Collapsed;
                }
                _hideHeaderTimer.Stop();
            };

            this.MouseDown += (s, e) => { 
                if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1) DragMove(); 
                else if (e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 2) ToggleMaximize();
            };
            
            // Re-show header and footer on any click inside window
            this.PreviewMouseDown += (s, e) => {
                if (HeaderGrid.Visibility != Visibility.Visible)
                {
                    HeaderGrid.Visibility = Visibility.Visible;
                    FooterBorder.Visibility = Visibility.Visible;
                    ResetHideTimer();
                }
                else if (this.Topmost)
                {
                    ResetHideTimer();
                }
            };

            this.StateChanged += NoteViewWindow_StateChanged;
            LoadWindowState();
            UpdateUIText(); // Initialize localized text

            // Centralized Hyperlink Click Handler
            ContentViewer.PreviewMouseLeftButtonDown += ContentViewer_PreviewMouseLeftButtonDown;

            LoadNoteData();
        }

        private void UpdateUIText()
        {
            if (BtnPin != null) BtnPin.ToolTip = this.Topmost ? CatchCapture.Resources.LocalizationManager.GetString("UnpinAlwaysOnTop") : CatchCapture.Resources.LocalizationManager.GetString("AlwaysOnTop");
            if (BtnMinimize != null) BtnMinimize.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Minimize");
            if (BtnMaximize != null) BtnMaximize.ToolTip = this.WindowState == WindowState.Maximized ? CatchCapture.Resources.LocalizationManager.GetString("Restore") : CatchCapture.Resources.LocalizationManager.GetString("Maximize");
            if (BtnClose != null) BtnClose.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("Close");
            if (BtnPrint != null) BtnPrint.ToolTip = CatchCapture.Resources.LocalizationManager.GetString("PrintOrSavePDF");
            if (BtnEdit != null) BtnEdit.Content = CatchCapture.Resources.LocalizationManager.GetString("EditNote");
        }

        private void ResetHideTimer()
        {
            _hideHeaderTimer.Stop();
            if (this.Topmost)
            {
                _hideHeaderTimer.Start();
            }
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
                if (settings.NoteViewerWidth > 0)
                    this.Width = settings.NoteViewerWidth;
                if (settings.NoteViewerHeight > 0)
                    this.Height = settings.NoteViewerHeight;
                
                // Restore window position
                /* [Fix] Disabled to always center on primary screen
                if (settings.NoteViewerLeft > -9999 && settings.NoteViewerTop > -9999)
                {
                    this.Left = settings.NoteViewerLeft;
                    this.Top = settings.NoteViewerTop;
                }
                */
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
                    settings.NoteViewerWidth = this.ActualWidth;
                    settings.NoteViewerHeight = this.ActualHeight;
                    // [Fix] Disabled to always center on primary screen
                    // settings.NoteViewerLeft = this.Left;
                    // settings.NoteViewerTop = this.Top;
                }
                
                settings.Save();
            }
            catch { /* Ignore save errors */ }
        }

        private void NoteViewWindow_StateChanged(object? sender, EventArgs e)
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

        private void LoadNoteData()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseManager.Instance.DbFilePath}"))
                {
                    connection.Open();
                    
                    // 1. Load basic note data
                    string sql = "SELECT Title, Content, ContentXaml, datetime(UpdatedAt, 'localtime'), SourceApp, SourceUrl, CategoryId FROM Notes WHERE Id = $id";
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
                                DateTime modifiedAt = reader.GetDateTime(3);
                                TxtDate.Text = modifiedAt.ToString("yyyy-MM-dd HH:mm");
                                
                                string sourceApp = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                string sourceUrl = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                TxtSource.Text = !string.IsNullOrEmpty(sourceUrl) ? sourceUrl : sourceApp;

                                long catId = reader.GetInt64(6);
                                var category = DatabaseManager.Instance.GetCategory(catId);
                                if (category != null)
                                {
                                    if (category.Id == 1) TxtCategory.Text = CatchCapture.Resources.LocalizationManager.GetString("DefaultCategory");
                                    else TxtCategory.Text = category.Name;
                                    var brush = new System.Windows.Media.BrushConverter().ConvertFromString(category.Color) as System.Windows.Media.Brush;
                                    CategoryCircle.Fill = brush ?? System.Windows.Media.Brushes.Gray;
                                }

                                bool xamlLoaded = false;
                                 if (!string.IsNullOrEmpty(contentXaml))
                                {
                                    try 
                                    {
                                        // [Fix] Multi-computer Cloud Sync: Redirect image paths
                                        string correctedXaml = contentXaml;
                                        string currentImgFolder = DatabaseManager.Instance.GetImageFolderPath();
                                        var regex = new System.Text.RegularExpressions.Regex(@"Source=""([^""]*[\\/]img[\\/]([^""]+))""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                        correctedXaml = regex.Replace(contentXaml, match =>
                                        {
                                            string relativePath = match.Groups[2].Value;
                                            string fullPath = System.IO.Path.Combine(currentImgFolder, relativePath);
                                            return $@"Source=""{fullPath}""";
                                        });

                                        var flowDocument = (FlowDocument)System.Windows.Markup.XamlReader.Parse(correctedXaml);
                                        flowDocument.PagePadding = new Thickness(0);
                                        // Compact layout
                                        foreach (var block in flowDocument.Blocks)
                                        {
                                            block.Margin = new Thickness(0, 2, 0, 2);
                                        }
                                        ContentViewer.Document = flowDocument;
                                        HookMediaClicks(); // Re-hook for XAML
                                        HookHyperlinks(ContentViewer.Document); // Set Hand cursors and attach events
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
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrLoadNote") + ex.Message);
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
                        CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("FileNotFound"));
                    }
                }
                catch (Exception ex)
                {
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error"));
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
                var image = new Image { Source = bitmap, MaxWidth = 800, Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Tag = path };
                image.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
                };
                var container = new BlockUIContainer(image);
                ContentViewer.Document.Blocks.Add(container);
            }
            catch { }
        }

        private void HookMediaClicks()
        {
            foreach (var block in ContentViewer.Document.Blocks)
            {
                if (block is BlockUIContainer container)
                {
                    Grid? grid = null;
                    if (container.Child is Border border && border.Child is Grid g1) grid = g1;
                    else if (container.Child is Grid g2) grid = g2;

                    bool handled = false;

                    if (grid != null)
                    {
                        string? filePath = grid.Tag?.ToString();

                        // Tag가 없거나 유튜브 주소가 아니면 FilePathHolder에서 찾기
                        var pathHolder = grid.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag?.ToString() == "FilePathHolder" || t.Name == "FilePathHolder" || t.Text.StartsWith("http") || t.Text.Contains("\\") || t.Text.Contains("/"));
                        if (pathHolder != null) filePath = pathHolder.Text;

                        // 1. 유튜브/웹 링크 처리 - 브라우저로 열기
                        if (!string.IsNullOrEmpty(filePath) && (filePath.StartsWith("http://") || filePath.StartsWith("https://")))
                        {
                            grid.Cursor = Cursors.Hand;
                            grid.PreviewMouseLeftButtonDown += (s, ev) =>
                            {
                                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
                                ev.Handled = true;
                            };
                            handled = true;
                        }
                        // 2. 로컬 미디어 파일 처리 - 더블클릭으로 기본 프로그램 열기
                        else if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            grid.Cursor = Cursors.Hand;
                            grid.MouseLeftButtonDown += (s, ev) =>
                            {
                                if (ev.ClickCount == 2)
                                {
                                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true }); }
                                    catch (Exception ex) { CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message, CatchCapture.Resources.LocalizationManager.GetString("Error")); }
                                    ev.Handled = true;
                                }
                            };
                            handled = true;
                        }
                    }

                    // 3. 일반 이미지 처리 - PreviewWindow 팝업
                    if (!handled)
                    {
                        Image? img = null;
                        if (container.Child is Image i) img = i;
                        else if (grid != null) img = grid.Children.OfType<Image>().FirstOrDefault();

                        if (img != null && img.Source is BitmapSource bitmap)
                        {
                            img.Cursor = Cursors.Hand;
                            img.PreviewMouseLeftButtonDown += (s, ev) =>
                            {
                                string? path = null;
                                if (img.Tag != null) path = img.Tag.ToString();
                                else if (img.Source is BitmapImage bi && bi.UriSource != null) path = bi.UriSource.LocalPath;

                                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                {
                                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
                                }
                            };
                        }
                    }
                }
            }
        }

        private void ContentViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is RichTextBox rtb)
            {
                var position = e.GetPosition(rtb);
                // 정확한 클릭 지점의 TextPointer 가져오기
                var textPointer = rtb.GetPositionFromPoint(position, true);

                if (textPointer != null)
                {
                    // 클릭된 텍스트 요소가 Hyperlink인지 또는 부모 중에 Hyperlink가 있는지 확인
                    DependencyObject element = textPointer.Parent;
                    
                    // LogicalTree를 타고 올라가며 Hyperlink 찾기
                    while (element != null && !(element is RichTextBox))
                    {
                        if (element is Hyperlink link)
                        {
                            if (link.NavigateUri != null)
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.NavigateUri.AbsoluteUri) { UseShellExecute = true });
                                    e.Handled = true; // 이벤트 소비
                                }
                                catch (Exception ex)
                                {
                                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message);
                                }
                            }
                            return;
                        }
                        element = LogicalTreeHelper.GetParent(element);
                    }
                }
            }
        }

        // Keep HookHyperlinks to set Hand cursor (Visual feedback)
        private void HookHyperlinks(FlowDocument doc)
        {
            // Recursive search for all hyperlinks to set cursor
            var enumerator = doc.Blocks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                SetCursorOnHyperlinks(enumerator.Current);
            }
        }

        private void SetCursorOnHyperlinks(DependencyObject obj)
        {
            if (obj is Hyperlink link)
            {
                link.Cursor = Cursors.Hand;
                // Force a recognizable color if not set
                if (link.Foreground == null || (link.Foreground is SolidColorBrush b && b.Color == Colors.Black))
                {
                    link.Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204));
                }
                // Remove existing handler first (prevents duplicate subscriptions)
                link.RequestNavigate -= Hyperlink_RequestNavigate;
                // Attach RequestNavigate event to make hyperlink clickable
                link.RequestNavigate += Hyperlink_RequestNavigate;
            }
            else if (obj is InlineUIContainer container)
            {
                if (container.Child is FrameworkElement fe && fe.Tag is string url && url.StartsWith("http"))
                {
                    fe.Cursor = Cursors.Hand;
                    fe.MouseLeftButtonDown += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                            e.Handled = true;
                        }
                        catch { }
                    };

                    // Context Menu
                    var contextMenu = new ContextMenu();
                    var copyMenuItem = new MenuItem { Header = "URL 복사" };
                    copyMenuItem.Click += (s, e) => { try { Clipboard.SetText(url); } catch { } };
                    contextMenu.Items.Add(copyMenuItem);
                    fe.ContextMenu = contextMenu;
                }
            }
            
            foreach (var child in LogicalTreeHelper.GetChildren(obj))
            {
                if (child is DependencyObject d) SetCursorOnHyperlinks(d);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrOpenFile") + " " + ex.Message);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clone the document to handle paging without affecting the view
                string xaml = XamlWriter.Save(ContentViewer.Document);
                FlowDocument copy = (FlowDocument)XamlReader.Parse(xaml);
                
                // Open Print Preview Window
                var previewWin = new PrintPreviewWindow(copy);
                previewWin.Owner = this;
                previewWin.ShowDialog();
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("ErrPreviewFailed") + ex.Message);
            }
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

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            PathPin.Fill = this.Topmost ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 115, 232)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(149, 165, 166));
            BtnPin.ToolTip = this.Topmost ? CatchCapture.Resources.LocalizationManager.GetString("UnpinAlwaysOnTop") : CatchCapture.Resources.LocalizationManager.GetString("AlwaysOnTop");

            if (this.Topmost)
            {
                ResetHideTimer();
            }
            else
            {
                _hideHeaderTimer.Stop();
                HeaderGrid.Visibility = Visibility.Visible;
                FooterBorder.Visibility = Visibility.Visible;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
