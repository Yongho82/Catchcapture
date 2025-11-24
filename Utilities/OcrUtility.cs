using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace CatchCapture.Utilities
{
    public static class OcrUtility
    {
        public static async Task<string> ExtractTextFromImageAsync(BitmapSource bitmapSource)
        {
            try
            {
                // 1. 언어 설정: 한국어 우선 시도, 실패 시 사용자 언어, 그 다음 영어
                OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ko-KR"));
                
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                }
                
                if (ocrEngine == null)
                {
                    ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
                }

                if (ocrEngine == null)
                {
                    return "OCR 엔진을 초기화할 수 없습니다.";
                }

                // 2. 이미지 전처리: 흑백 변환 + 확대
                // 흑백으로 변환하면 색상 노이즈가 줄어들어 글자 인식이 더 잘 됨
                var grayBitmap = new FormatConvertedBitmap();
                grayBitmap.BeginInit();
                grayBitmap.Source = bitmapSource;
                grayBitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray8;
                grayBitmap.EndInit();

                // 확대 (2.5배로 상향) - 특수문자 인식률 향상
                var scale = 2.5;
                var transformedBitmap = new TransformedBitmap(grayBitmap, new System.Windows.Media.ScaleTransform(scale, scale));

                // BitmapSource를 SoftwareBitmap으로 변환
                using (var stream = new MemoryStream())
                {
                    // WPF Encoder 사용
                    System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(transformedBitmap)); // 처리된 이미지 사용
                    encoder.Save(stream);
                    stream.Position = 0;

                    // UWP Decoder 사용
                    Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    // OCR 결과 추출
                    OcrResult result = await ocrEngine.RecognizeAsync(softwareBitmap);
                    
                    // 결과 텍스트 조합 (줄바꿈 유지)
                    StringBuilder sb = new StringBuilder();
                    foreach (var line in result.Lines)
                    {
                        sb.AppendLine(line.Text);
                    }
                    
                    return sb.ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                return $"OCR 오류: {ex.Message}";
            }
        }
    }
}
