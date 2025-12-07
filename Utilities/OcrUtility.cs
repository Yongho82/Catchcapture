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

                // 2. 이미지 전처리: 대비 향상 + 선명화 + 확대 (EnhanceImageForOcr에서 처리)
                // UI 스레드 차단을 방지하기 위해 백그라운드에서 실행
                if (bitmapSource.CanFreeze) bitmapSource.Freeze();
                
                var processedBitmap = await Task.Run(() => EnhanceImageForOcr(bitmapSource));

                // BitmapSource를 SoftwareBitmap으로 변환
                using (var stream = new MemoryStream())
                {
                    // WPF Encoder 사용
                    System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(processedBitmap)); // 처리된 이미지 사용
                    encoder.Save(stream);
                    stream.Position = 0;

                    // UWP Decoder 사용
                    Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    // OCR 결과 추출
                    OcrResult result = await ocrEngine.RecognizeAsync(softwareBitmap);
                    
                    // 결과 텍스트 조합 (같은 줄의 단어들을 병합)
                    StringBuilder sb = new StringBuilder();
                    
                    // 각 라인의 단어들을 Y 좌표 기준으로 그룹화
                    var allWords = new List<(double Y, double X, string Text)>();
                    
                    foreach (var line in result.Lines)
                    {
                        foreach (var word in line.Words)
                        {
                            // 단어의 Y 좌표 (상단 기준)
                            double y = word.BoundingRect.Y;
                            double x = word.BoundingRect.X;
                            allWords.Add((y, x, word.Text));
                        }
                    }
                    
                    // Y 좌표로 정렬 후 같은 줄로 그룹화 (±15 픽셀 오차 허용 - 코드 에디터 줄 높이 고려)
                    var sortedByY = allWords.OrderBy(w => w.Y).ToList();
                    var lines = new List<List<(double Y, double X, string Text)>>();

                    if (sortedByY.Count > 0)
                    {
                        var currentLine = new List<(double Y, double X, string Text)>();
                        currentLine.Add(sortedByY[0]);
                        double currentLineY = sortedByY[0].Y;
                        
                        for (int i = 1; i < sortedByY.Count; i++)
                        {
                            // 같은 줄인지 확인 (Y 좌표 차이가 40 이하 - 확대된 이미지 기준)
                            if (Math.Abs(sortedByY[i].Y - currentLineY) <= 40)
                            {
                                currentLine.Add(sortedByY[i]);
                            }
                            else
                            {
                                // 줄 바뀜 -> 현재 줄 저장
                                lines.Add(currentLine);
                                
                                // 새로운 줄 시작
                                currentLine = new List<(double Y, double X, string Text)>();
                                currentLine.Add(sortedByY[i]);
                                currentLineY = sortedByY[i].Y;
                            }
                        }
                        // 마지막 줄 추가
                        lines.Add(currentLine);
                    }

                    // 각 줄 내부에서 X 좌표(좌->우) 순으로 정렬하여 텍스트 병합
                    foreach (var line in lines)
                    {
                        // X 좌표로 정렬
                        var sortedLine = line.OrderBy(w => w.X).Select(w => w.Text);
                        sb.AppendLine(string.Join(" ", sortedLine));
                    }
                    
                    string extractedText = sb.ToString().Trim();
                    
                    return extractedText;
                }
            }
            catch (Exception ex)
            {
                return $"OCR 오류: {ex.Message}";
            }
        }
        private static BitmapSource EnhanceImageForOcr(BitmapSource source)
        {
            try
            {
                // 1. System.Drawing.Bitmap으로 변환
                Bitmap bitmap;
                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                    encoder.Save(stream);
                    stream.Position = 0;
                    bitmap = new Bitmap(stream);
                }

                // 2. 확대 (3배)
                float scaleFactor = 3.0f;
                int newWidth = (int)(bitmap.Width * scaleFactor);
                int newHeight = (int)(bitmap.Height * scaleFactor);
                var enlarged = new Bitmap(newWidth, newHeight);
                
                using (var g = Graphics.FromImage(enlarged))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    
                    // 배경을 흰색으로 채움 (투명 배경 대응)
                    g.Clear(Color.White);
                    g.DrawImage(bitmap, 0, 0, newWidth, newHeight);
                }
                bitmap.Dispose();

                // 3. 이미지 처리 (그레이스케일 -> 대비 -> 이진화)
                // LockBits를 사용하여 고속 처리
                BitmapData data = enlarged.LockBits(new Rectangle(0, 0, newWidth, newHeight), 
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

                int bytes = Math.Abs(data.Stride) * newHeight;
                byte[] rgbValues = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rgbValues, 0, bytes);

                for (int i = 0; i < rgbValues.Length; i += 4)
                {
                    // BGRA 순서
                    byte b = rgbValues[i];
                    byte g = rgbValues[i + 1];
                    byte r = rgbValues[i + 2];

                    // 그레이스케일
                    int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);

                    // 대비 증가 (Contrast Stretching)
                    // 1.5 -> 1.3으로 완화하여 자연스럽게 처리
                    double contrast = 1.3; 
                    double cGray = ((((gray / 255.0) - 0.5) * contrast) + 0.5) * 255.0;
                    gray = (int)Math.Max(0, Math.Min(255, cGray));

                    // 이진화 제거: 그레이스케일 값을 그대로 사용 (정보 손실 방지)
                    rgbValues[i] = (byte)gray;     // B
                    rgbValues[i + 1] = (byte)gray; // G
                    rgbValues[i + 2] = (byte)gray; // R
                    rgbValues[i + 3] = 255;        // A (완전 불투명)
                }

                System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, data.Scan0, bytes);
                enlarged.UnlockBits(data);

                // 4. BitmapSource로 변환
                using (var stream = new MemoryStream())
                {
                    enlarged.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    enlarged.Dispose();
                    return bitmapImage;
                }
            }
            catch
            {
                // 전처리 실패 시 원본 반환
                return source;
            }
        }
    }
}
