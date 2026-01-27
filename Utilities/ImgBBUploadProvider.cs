using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
                throw new Exception("ImgBB API 키가 설정되지 않았습니다. 설정에서 키를 입력해주세요.");

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("이미지 파일을 찾을 수 없습니다.");

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
                    throw new Exception($"ImgBB Upload Failed: {response.StatusCode}\n{responseString}");
                }

                var json = JObject.Parse(responseString);
                string? url = json["data"]?["url"]?.ToString();

                if (string.IsNullOrEmpty(url))
                    throw new Exception("ImgBB로부터 링크를 받아오지 못했습니다.");

                return url;
            }
        }
    }
}
