using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace CatchCapture.Models
{
    public class CaptureImage : INotifyPropertyChanged, IDisposable
    {
        private BitmapSource _image = null!;
        private DateTime _captureTime;
        private bool _isSaved;
        private string _savedPath = string.Empty;

        public BitmapSource Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged(nameof(Image));
                }
            }
        }

        public DateTime CaptureTime
        {
            get => _captureTime;
            set
            {
                if (_captureTime != value)
                {
                    _captureTime = value;
                    OnPropertyChanged(nameof(CaptureTime));
                }
            }
        }

        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                if (_isSaved != value)
                {
                    _isSaved = value;
                    OnPropertyChanged(nameof(IsSaved));
                }
            }
        }

        public string SavedPath
        {
            get => _savedPath;
            set
            {
                if (_savedPath != value)
                {
                    _savedPath = value;
                    OnPropertyChanged(nameof(SavedPath));
                }
            }
        }

        public CaptureImage(BitmapSource image)
        {
            Image = image;
            CaptureTime = DateTime.Now;
            IsSaved = false;
            SavedPath = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 이미지 참조 해제
                    _image = null!;
                    PropertyChanged = null;
                }
                disposed = true;
            }
        }

        ~CaptureImage()
        {
            Dispose(false);
        }
    }
}