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
        
        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null && !string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath))
                {
                    try
                    {
                        // Check if it's media (mp4, gif, mp3)
                        bool isMedia = FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || 
                                       FilePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                       FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
                        
                        string loadPath = FilePath;
                        if (isMedia)
                        {
                            string thumbPath = FilePath + ".preview.png";
                            if (System.IO.File.Exists(thumbPath)) loadPath = thumbPath;
                            else if (FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return null; // No thumb for audio
                        }

                        // Lazy load thumbnail (optimized size)
                        _thumbnail = LoadThumbnail(loadPath);
                    }
                    catch { }
                }
                return _thumbnail;
            }
        }

        private BitmapSource? LoadThumbnail(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 300; // Limit size for performance
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
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
