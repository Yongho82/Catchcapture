using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CatchCapture.Utilities
{
    public static class ThumbnailManager
    {
        // Limit concurrent image decodes to 4 to prevent CPU spikes
        private static readonly SemaphoreSlim _decodeSemaphore = new SemaphoreSlim(4, 4);

        public static async Task<BitmapSource?> LoadThumbnailAsync(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return null;

            await _decodeSemaphore.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        if (decodeWidth > 0)
                        {
                            bitmap.DecodePixelWidth = decodeWidth;
                        }
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                    catch
                    {
                        return null;
                    }
                });
            }
            finally
            {
                _decodeSemaphore.Release();
            }
        }
    }
}
