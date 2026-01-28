using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture.Utilities
{
    public class ImgBBUploadProvider
    {
        private static readonly HttpClient client = new HttpClient();
        private static ImgBBUploadProvider? _instance;
        public static ImgBBUploadProvider Instance => _instance ??= new ImgBBUploadProvider();

        private ImgBBUploadProvider() { }

        public async Task<string> UploadImageAsync(string imagePath, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception(LocalizationManager.GetString("ImgBBApiKeyRequired"));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException(LocalizationManager.GetString("FileNotFound"));

            using (var content = new MultipartFormDataContent())
            {
                byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);

                content.Add(new StringContent(apiKey), "key");
                content.Add(new StringContent(base64Image), "image");

                var response = await client.PostAsync("https://api.imgbb.com/1/upload", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"{LocalizationManager.GetString("UploadFailed")}: {response.StatusCode}");
                }

                var json = JObject.Parse(responseString);
                string? url = json["data"]?["url"]?.ToString();

                if (string.IsNullOrEmpty(url))
                    throw new Exception(LocalizationManager.GetString("ImgBBLinkFailed"));

                return url;
            }
        }
    }
}
