using System;
using System.ComponentModel;

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
                    OnPropertyChanged(nameof(FavoriteEmoji));
                }
            }
        }
        public int Status { get; set; } // 0: Normal, 1: Trash
        public long FileSize { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public bool IsInTrash => Status == 1;
        public bool IsNormal => Status == 0;

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
