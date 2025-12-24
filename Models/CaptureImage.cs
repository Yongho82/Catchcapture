using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CatchCapture.Models
{
    public class CaptureImage : INotifyPropertyChanged, IDisposable
    {
        private BitmapSource? _image = null;
        private BitmapSource? _thumbnail = null;
        private DateTime _captureTime;
        private bool _isSaved;
        private string _savedPath = string.Empty;
        
        // ★ 메모리 최적화: 썸네일 기반 저장 여부
        private bool _useThumbnailMode = false;

        private int _originalWidth;
        private int _originalHeight;

        /// <summary>
        /// 표시용 이미지 (썸네일 모드면 썸네일, 아니면 원본)
        /// </summary>
        public BitmapSource Image
        {
            get => (_useThumbnailMode && _image == null) ? (_thumbnail ?? _image!) : (_image ?? _thumbnail!);
            set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged(nameof(Image));
                }
            }
        }

        /// <summary>
        /// ★ 메모리 최적화: 원본 이미지 가져오기 (필요 시 파일에서 로드, 캐싱하지 않음)
        /// </summary>
        public BitmapSource GetOriginalImage()
        {
            // 썸네일 모드가 아니거나 이미 로드되어 있으면 반환
            // (썸네일 모드인 경우 로드된 원본은 _image에 캐시됨 - 기존 호환성)
            if (!_useThumbnailMode || (_image != null && _image != _thumbnail))
            {
                return _image ?? _thumbnail!;
            }
            
            // 파일에서 원본 로드
            if (!string.IsNullOrEmpty(_savedPath) && File.Exists(_savedPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_savedPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 파일 잠금 방지
                    bitmap.EndInit();
                    bitmap.Freeze();
                    
                    // [수정] 로드된 원본을 _image에 저장하지 않고 반환만 함
                    // 이렇게 해야 편집창이 닫혔을 때 GC가 원본 이미지를 수거할 수 있음
                    return bitmap;
                }
                catch
                {
                    // 로드 실패 시 썸네일이라도 반환
                    return (_thumbnail ?? _image)!;
                }
            }
            
            return (_thumbnail ?? _image)!;
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

        public int OriginalWidth => _originalWidth;
        public int OriginalHeight => _originalHeight;

        /// <summary>
        /// 기존 생성자 (자동저장 OFF 시 사용 - 원본 메모리 유지)
        /// </summary>
        public CaptureImage(BitmapSource image)
        {
            _image = image;
            _originalWidth = image.PixelWidth;
            _originalHeight = image.PixelHeight;
            _useThumbnailMode = false;
            CaptureTime = DateTime.Now;
            IsSaved = false;
            SavedPath = string.Empty;
        }

        /// <summary>
        /// ★ 메모리 최적화: 썸네일 모드 생성자 (자동저장 ON 시 사용)
        /// </summary>
        public CaptureImage(BitmapSource thumbnail, string savedFilePath, int originalWidth, int originalHeight)
        {
            _thumbnail = thumbnail;
            _image = null; // 원본 이미지는 필요할 때 파일에서 로드
            _originalWidth = originalWidth;
            _originalHeight = originalHeight;
            _useThumbnailMode = true;
            _savedPath = savedFilePath;
            CaptureTime = DateTime.Now;
            IsSaved = true;
        }

        /// <summary>
        /// ★ 메모리 최적화: 썸네일 생성 (200x150 크기)
        /// </summary>
        public static BitmapSource CreateThumbnail(BitmapSource source, int maxWidth = 200, int maxHeight = 150)
        {
            if (source == null) return null!;
            
            double scaleX = (double)maxWidth / source.PixelWidth;
            double scaleY = (double)maxHeight / source.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);
            
            // 이미 작으면 그대로 반환
            if (scale >= 1.0)
            {
                source.Freeze();
                return source;
            }
            
            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);
            
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(source, new Rect(0, 0, newWidth, newHeight));
            }
            
            var renderBitmap = new RenderTargetBitmap(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();
            
            return renderBitmap;
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
                    _thumbnail = null;
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
