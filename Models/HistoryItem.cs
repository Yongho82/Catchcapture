using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace CatchCapture.Models
{
    public class HistoryItem : INotifyPropertyChanged
    {
        public long Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string SourceApp { get; set; } = string.Empty;
        public string SourceTitle { get; set; } = string.Empty;
        public string OriginalFilePath { get; set; } = string.Empty;
        public string Memo { get; set; } = string.Empty;
        
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged(nameof(IsPinned));
                }
            }
        }

        public int Status { get; set; } // 0: Normal, 1: Trash
        public long FileSize { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public bool IsInTrash => Status == 1;
        public bool IsNormal => Status == 0;
        
        private bool _isThumbnailLoading = false;
        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null && !_isThumbnailLoading && !string.IsNullOrEmpty(FilePath))
                {
                    _isThumbnailLoading = true;
                    System.Threading.Tasks.Task.Run(() => 
                    {
                        try
                        {
                            string loadPath = FilePath;
                            
                            // Check if it's media (mp4, gif, mp3)
                            bool isMedia = loadPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                           loadPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                           loadPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                            
                            if (isMedia)
                            {
                                string thumbPath = loadPath + ".preview.png";
                                if (System.IO.File.Exists(thumbPath)) loadPath = thumbPath;
                                else if (loadPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return; // No thumb for audio
                            }

                            if (System.IO.File.Exists(loadPath))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(loadPath);
                                bitmap.DecodePixelWidth = 200; // Reduced to 200 for Card View
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                _thumbnail = bitmap;
                                OnPropertyChanged(nameof(Thumbnail));
                            }
                        }
                        catch { }
                        finally 
                        { 
                            _isThumbnailLoading = false; 
                        }
                    });
                }
                return _thumbnail;
            }
        }

        // UI Props
        public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        public string MetaInfo => $"{SourceApp} | {SourceTitle}";
        public string FavoriteEmoji => IsFavorite ? "⭐" : "☆";
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
