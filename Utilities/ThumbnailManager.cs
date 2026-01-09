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
        private static readonly SemaphoreSlim _decodeSemaphore = new SemaphoreSlim(4, 4);

        public static async Task<BitmapSource?> LoadThumbnailAsync(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            await _decodeSemaphore.WaitAsync();
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(path);

                return await Task.Run(() =>
                {
                    try
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            // Check original size first
                            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            int originalWidth = decoder.Frames[0].PixelWidth;
                            
                            ms.Position = 0; // Reset stream

                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            
                            // Only downscale if the image is larger than the target width
                            if (decodeWidth > 0 && originalWidth > decodeWidth)
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
