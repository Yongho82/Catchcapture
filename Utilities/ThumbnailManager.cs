using System;
using System.IO;
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
                // Read all bytes first to avoid any file locking issues while WPF decodes
                byte[] bytes = await File.ReadAllBytesAsync(path);

                return await Task.Run(() =>
                {
                    try
                    {
                        using (var ms = new System.IO.MemoryStream(bytes))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            if (decodeWidth > 0)
                            {
                                bitmap.DecodePixelWidth = decodeWidth;
                            }
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Thumbnail Decode Error ({path}): {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail Load Error ({path}): {ex.Message}");
                return null;
            }
            finally
            {
                _decodeSemaphore.Release();
            }
        }
    }
}
