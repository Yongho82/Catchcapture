using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CatchCapture.Recording
{
    /// <summary>
    /// Animated GIF 인코더 - 외부 라이브러리 없이 순수 C# 구현
    /// NGif 라이브러리 기반 (Public Domain)
    /// </summary>
    public class AnimatedGifEncoder
    {
        private int _width;
        private int _height;
        private Color _transparent = Color.Empty;
        private int _transIndex;
        private int _repeat = -1;
        private int _delay = 0;
        private bool _started = false;
        private Stream? _stream;
        private Image? _image;
        private byte[]? _pixels;
        private byte[]? _indexedPixels;
        private int _colorDepth;
        private byte[]? _colorTab;
        private bool[] _usedEntry = new bool[256];
        private int _palSize = 7;
        private int _dispose = -1;
        private bool _closeStream = false;
        private bool _firstFrame = true;
        private bool _sizeSet = false;
        private int _sample = 10;

        /// <summary>
        /// 프레임 간 딜레이 설정 (ms)
        /// </summary>
        public void SetDelay(int ms)
        {
            _delay = (int)Math.Round(ms / 10.0);
        }

        /// <summary>
        /// GIF 품질 설정 (1-20, 낮을수록 좋음)
        /// </summary>
        public void SetQuality(int quality)
        {
            if (quality < 1) quality = 1;
            if (quality > 20) quality = 20;
            _sample = quality;
        }

        /// <summary>
        /// 반복 횟수 설정 (0 = 무한)
        /// </summary>
        public void SetRepeat(int iter)
        {
            if (iter >= 0) _repeat = iter;
        }

        /// <summary>
        /// 투명 색상 설정
        /// </summary>
        public void SetTransparent(Color c)
        {
            _transparent = c;
        }

        /// <summary>
        /// 인코딩 시작
        /// </summary>
        public bool Start(Stream stream)
        {
            if (stream == null) return false;
            _stream = stream;
            _closeStream = false;
            return _started = true;
        }

        /// <summary>
        /// 인코딩 시작 (파일)
        /// </summary>
        public bool Start(string file)
        {
            try
            {
                _stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                _closeStream = true;
                return _started = true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 프레임 추가
        /// </summary>
        public bool AddFrame(Image im)
        {
            if (im == null || !_started) return false;

            try
            {
                if (!_sizeSet)
                {
                    SetSize(im.Width, im.Height);
                }
                _image = im;
                GetImagePixels();
                AnalyzePixels();
                if (_firstFrame)
                {
                    WriteLSD();
                    WritePalette();
                    if (_repeat >= 0)
                    {
                        WriteNetscapeExt();
                    }
                }
                WriteGraphicCtrlExt();
                WriteImageDesc();
                if (!_firstFrame)
                {
                    WritePalette();
                }
                WritePixels();
                _firstFrame = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 인코딩 완료
        /// </summary>
        public bool Finish()
        {
            if (!_started) return false;

            try
            {
                _stream?.WriteByte(0x3b); // GIF Trailer
                _stream?.Flush();
                if (_closeStream) _stream?.Close();
                _started = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 프레임 사이즈 설정
        /// </summary>
        public void SetSize(int width, int height)
        {
            _width = width;
            _height = height;
            if (_width < 1) _width = 320;
            if (_height < 1) _height = 240;
            _sizeSet = true;
        }

        /// <summary>
        /// 이미지에서 픽셀 데이터 추출
        /// </summary>
        private void GetImagePixels()
        {
            int w = _image!.Width;
            int h = _image.Height;

            if (w != _width || h != _height)
            {
                var temp = new Bitmap(_width, _height);
                using (var g = Graphics.FromImage(temp))
                {
                    g.DrawImage(_image, 0, 0, _width, _height);
                }
                _image = temp;
            }

            var bitmap = new Bitmap(_image);
            _pixels = new byte[3 * _width * _height];
            
            int idx = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    _pixels[idx++] = c.R;
                    _pixels[idx++] = c.G;
                    _pixels[idx++] = c.B;
                }
            }

            bitmap.Dispose();
        }

        /// <summary>
        /// 픽셀 분석 및 팔레트 생성
        /// </summary>
        private void AnalyzePixels()
        {
            int len = _pixels!.Length;
            int nPix = len / 3;
            _indexedPixels = new byte[nPix];

            var nq = new NeuQuant(_pixels, len, _sample);
            _colorTab = nq.Process();

            // 팔레트를 256색으로 맞춤
            int k = 0;
            for (int i = 0; i < 256; i++)
            {
                byte r = _colorTab[k++];
                byte g = _colorTab[k++];
                byte b = _colorTab[k++];
                _usedEntry[i] = false;
            }

            // 각 픽셀을 팔레트 인덱스로 변환
            k = 0;
            for (int i = 0; i < nPix; i++)
            {
                int index = nq.Map(
                    _pixels[k++] & 0xff,
                    _pixels[k++] & 0xff,
                    _pixels[k++] & 0xff);
                _usedEntry[index] = true;
                _indexedPixels[i] = (byte)index;
            }

            _pixels = null;
            _colorDepth = 8;
            _palSize = 7;

            // 투명색 처리
            if (_transparent != Color.Empty)
            {
                _transIndex = FindClosest(_transparent);
            }
        }

        /// <summary>
        /// 가장 가까운 색상 인덱스 찾기
        /// </summary>
        private int FindClosest(Color c)
        {
            if (_colorTab == null) return -1;

            int r = c.R;
            int g = c.G;
            int b = c.B;
            int minpos = 0;
            int dmin = 256 * 256 * 256;
            int len = _colorTab.Length;

            for (int i = 0; i < len;)
            {
                int dr = r - (_colorTab[i++] & 0xff);
                int dg = g - (_colorTab[i++] & 0xff);
                int db = b - (_colorTab[i++] & 0xff);
                int d = dr * dr + dg * dg + db * db;
                if (d < dmin)
                {
                    dmin = d;
                    minpos = i / 3;
                }
            }

            return minpos;
        }

        /// <summary>
        /// Logical Screen Descriptor 작성
        /// </summary>
        private void WriteLSD()
        {
            // GIF Signature
            WriteString("GIF89a");

            // Logical Screen Width
            WriteShort(_width);
            // Logical Screen Height
            WriteShort(_height);

            // Packed Field
            _stream!.WriteByte((byte)(0x80 | 0x70 | _palSize));

            // Background Color Index
            _stream.WriteByte(0);
            // Pixel Aspect Ratio
            _stream.WriteByte(0);
        }

        /// <summary>
        /// Netscape Extension 작성 (반복용)
        /// </summary>
        private void WriteNetscapeExt()
        {
            _stream!.WriteByte(0x21); // Extension
            _stream.WriteByte(0xff); // Application Extension
            _stream.WriteByte(11); // Block Size
            WriteString("NETSCAPE2.0");
            _stream.WriteByte(3); // Sub-block Size
            _stream.WriteByte(1);
            WriteShort(_repeat);
            _stream.WriteByte(0); // Block Terminator
        }

        /// <summary>
        /// 컬러 팔레트 작성
        /// </summary>
        private void WritePalette()
        {
            _stream!.Write(_colorTab!, 0, _colorTab!.Length);
            int n = 3 * 256 - _colorTab.Length;
            for (int i = 0; i < n; i++)
            {
                _stream.WriteByte(0);
            }
        }

        /// <summary>
        /// Graphic Control Extension 작성
        /// </summary>
        private void WriteGraphicCtrlExt()
        {
            _stream!.WriteByte(0x21); // Extension
            _stream.WriteByte(0xf9); // Graphic Control
            _stream.WriteByte(4); // Block Size

            int transp, disp;
            if (_transparent == Color.Empty)
            {
                transp = 0;
                disp = 0;
            }
            else
            {
                transp = 1;
                disp = 2;
            }

            if (_dispose >= 0)
            {
                disp = _dispose & 7;
            }

            disp <<= 2;

            // Packed Field
            _stream.WriteByte((byte)(disp | transp));
            WriteShort(_delay);
            _stream.WriteByte((byte)_transIndex);
            _stream.WriteByte(0); // Block Terminator
        }

        /// <summary>
        /// Image Descriptor 작성
        /// </summary>
        private void WriteImageDesc()
        {
            _stream!.WriteByte(0x2c); // Image Separator
            WriteShort(0); // Left Position
            WriteShort(0); // Top Position
            WriteShort(_width);
            WriteShort(_height);

            if (_firstFrame)
            {
                _stream.WriteByte(0);
            }
            else
            {
                _stream.WriteByte((byte)(0x80 | _palSize));
            }
        }

        /// <summary>
        /// 픽셀 데이터 작성 (LZW 압축)
        /// </summary>
        private void WritePixels()
        {
            var encoder = new LZWEncoder(_width, _height, _indexedPixels!, _colorDepth);
            encoder.Encode(_stream!);
        }

        /// <summary>
        /// 문자열 작성
        /// </summary>
        private void WriteString(string s)
        {
            foreach (char c in s)
            {
                _stream!.WriteByte((byte)c);
            }
        }

        /// <summary>
        /// Little-endian short 작성
        /// </summary>
        private void WriteShort(int value)
        {
            _stream!.WriteByte((byte)(value & 0xff));
            _stream.WriteByte((byte)((value >> 8) & 0xff));
        }
    }

    /// <summary>
    /// LZW 인코더
    /// </summary>
    internal class LZWEncoder
    {
        private static readonly int EOF = -1;
        private int imgW, imgH;
        private byte[] pixAry;
        private int initCodeSize;
        private int remaining;
        private int curPixel;
        
        // GIFCOMPR.C
        private static readonly int BITS = 12;
        private static readonly int HSIZE = 5003;
        private int n_bits;
        private int maxbits = BITS;
        private int maxcode;
        private int maxmaxcode = 1 << BITS;
        private int[] htab = new int[HSIZE];
        private int[] codetab = new int[HSIZE];
        private int free_ent = 0;
        private bool clear_flg = false;
        private int g_init_bits;
        private int ClearCode;
        private int EOFCode;
        private int cur_accum = 0;
        private int cur_bits = 0;
        private int[] masks = { 0x0000, 0x0001, 0x0003, 0x0007, 0x000F, 0x001F, 0x003F, 0x007F, 0x00FF,
                              0x01FF, 0x03FF, 0x07FF, 0x0FFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF };
        
        private int a_count;
        private byte[] accum = new byte[256];

        public LZWEncoder(int width, int height, byte[] pixels, int color_depth)
        {
            imgW = width;
            imgH = height;
            pixAry = pixels;
            initCodeSize = Math.Max(2, color_depth);
        }

        public void Encode(Stream os)
        {
            os.WriteByte((byte)initCodeSize);
            remaining = imgW * imgH;
            curPixel = 0;
            Compress(initCodeSize + 1, os);
            os.WriteByte(0);
        }

        private void Compress(int init_bits, Stream outs)
        {
            int fcode, c, i, ent, disp, hsize_reg, hshift;

            g_init_bits = init_bits;
            clear_flg = false;
            n_bits = g_init_bits;
            maxcode = MaxCode(n_bits);

            ClearCode = 1 << (init_bits - 1);
            EOFCode = ClearCode + 1;
            free_ent = ClearCode + 2;

            a_count = 0;
            ent = NextPixel();

            hshift = 0;
            for (fcode = HSIZE; fcode < 65536; fcode *= 2)
                hshift++;
            hshift = 8 - hshift;

            hsize_reg = HSIZE;
            ClearHash(hsize_reg);

            Output(ClearCode, outs);

            while ((c = NextPixel()) != EOF)
            {
                fcode = (c << maxbits) + ent;
                i = (c << hshift) ^ ent;

                if (htab[i] == fcode)
                {
                    ent = codetab[i];
                    continue;
                }
                else if (htab[i] >= 0)
                {
                    disp = hsize_reg - i;
                    if (i == 0) disp = 1;
                    do
                    {
                        if ((i -= disp) < 0) i += hsize_reg;
                        if (htab[i] == fcode)
                        {
                            ent = codetab[i];
                            goto outer_continue;
                        }
                    } while (htab[i] >= 0);
                }
                Output(ent, outs);
                ent = c;
                if (free_ent < maxmaxcode)
                {
                    codetab[i] = free_ent++;
                    htab[i] = fcode;
                }
                else
                {
                    ClearBlock(outs);
                }
            outer_continue:;
            }
            Output(ent, outs);
            Output(EOFCode, outs);
        }

        private void ClearBlock(Stream outs)
        {
            ClearHash(HSIZE);
            free_ent = ClearCode + 2;
            clear_flg = true;
            Output(ClearCode, outs);
        }

        private void ClearHash(int hsize)
        {
            for (int i = 0; i < hsize; i++)
                htab[i] = -1;
        }

        private int MaxCode(int n_bits)
        {
            return (1 << n_bits) - 1;
        }

        private int NextPixel()
        {
            if (remaining == 0) return EOF;
            remaining--;
            return pixAry[curPixel++] & 0xff;
        }

        private void Output(int code, Stream outs)
        {
            cur_accum &= masks[cur_bits];

            if (cur_bits > 0)
                cur_accum |= (code << cur_bits);
            else
                cur_accum = code;

            cur_bits += n_bits;

            while (cur_bits >= 8)
            {
                CharOut((byte)(cur_accum & 0xff), outs);
                cur_accum >>= 8;
                cur_bits -= 8;
            }

            if (free_ent > maxcode || clear_flg)
            {
                if (clear_flg)
                {
                    maxcode = MaxCode(n_bits = g_init_bits);
                    clear_flg = false;
                }
                else
                {
                    n_bits++;
                    if (n_bits == maxbits)
                        maxcode = maxmaxcode;
                    else
                        maxcode = MaxCode(n_bits);
                }
            }

            if (code == EOFCode)
            {
                while (cur_bits > 0)
                {
                    CharOut((byte)(cur_accum & 0xff), outs);
                    cur_accum >>= 8;
                    cur_bits -= 8;
                }
                FlushChar(outs);
            }
        }

        private void CharOut(byte c, Stream outs)
        {
            accum[a_count++] = c;
            if (a_count >= 254)
                FlushChar(outs);
        }

        private void FlushChar(Stream outs)
        {
            if (a_count > 0)
            {
                outs.WriteByte((byte)a_count);
                outs.Write(accum, 0, a_count);
                a_count = 0;
            }
        }
    }

    /// <summary>
    /// NeuQuant Neural-Net Quantization Algorithm
    /// </summary>
    internal class NeuQuant
    {
        private static readonly int netsize = 256;
        private static readonly int prime1 = 499;
        private static readonly int prime2 = 491;
        private static readonly int prime3 = 487;
        private static readonly int prime4 = 503;
        private static readonly int minpicturebytes = 3 * prime4;
        private static readonly int maxnetpos = netsize - 1;
        private static readonly int netbiasshift = 4;
        private static readonly int ncycles = 100;
        private static readonly int intbiasshift = 16;
        private static readonly int intbias = 1 << intbiasshift;
        private static readonly int gammashift = 10;
        private static readonly int betashift = 10;
        private static readonly int beta = intbias >> betashift;
        private static readonly int betagamma = intbias << (gammashift - betashift);
        private static readonly int initrad = netsize >> 3;
        private static readonly int radiusbiasshift = 6;
        private static readonly int radiusbias = 1 << radiusbiasshift;
        private static readonly int initradius = initrad * radiusbias;
        private static readonly int radiusdec = 30;
        private static readonly int alphabiasshift = 10;
        private static readonly int initalpha = 1 << alphabiasshift;
        private int alphadec;
        private static readonly int radbiasshift = 8;
        private static readonly int radbias = 1 << radbiasshift;
        private static readonly int alpharadbshift = alphabiasshift + radbiasshift;
        private static readonly int alpharadbias = 1 << alpharadbshift;

        private byte[] thepicture;
        private int lengthcount;
        private int samplefac;
        private int[][] network;
        private int[] netindex = new int[256];
        private int[] bias = new int[netsize];
        private int[] freq = new int[netsize];
        private int[] radpower = new int[initrad];

        public NeuQuant(byte[] thepic, int len, int sample)
        {
            thepicture = thepic;
            lengthcount = len;
            samplefac = sample;

            network = new int[netsize][];
            for (int i = 0; i < netsize; i++)
            {
                network[i] = new int[4];
                int[] p = network[i];
                p[0] = p[1] = p[2] = (i << (netbiasshift + 8)) / netsize;
                freq[i] = intbias / netsize;
                bias[i] = 0;
            }
        }

        public byte[] Process()
        {
            Learn();
            Unbiasnet();
            Inxbuild();
            return ColorMap();
        }

        private byte[] ColorMap()
        {
            byte[] map = new byte[3 * netsize];
            int[] index = new int[netsize];
            for (int i = 0; i < netsize; i++)
                index[network[i][3]] = i;
            int k = 0;
            for (int i = 0; i < netsize; i++)
            {
                int j = index[i];
                map[k++] = (byte)network[j][0];
                map[k++] = (byte)network[j][1];
                map[k++] = (byte)network[j][2];
            }
            return map;
        }

        private void Inxbuild()
        {
            int previouscol = 0;
            int startpos = 0;

            for (int i = 0; i < netsize; i++)
            {
                int[] p = network[i];
                int smallpos = i;
                int smallval = p[1];

                for (int j = i + 1; j < netsize; j++)
                {
                    int[] q = network[j];
                    if (q[1] < smallval)
                    {
                        smallpos = j;
                        smallval = q[1];
                    }
                }
                int[] q2 = network[smallpos];
                if (i != smallpos)
                {
                    int j = q2[0]; q2[0] = p[0]; p[0] = j;
                    j = q2[1]; q2[1] = p[1]; p[1] = j;
                    j = q2[2]; q2[2] = p[2]; p[2] = j;
                    j = q2[3]; q2[3] = p[3]; p[3] = j;
                }
                if (smallval != previouscol)
                {
                    netindex[previouscol] = (startpos + i) >> 1;
                    for (int j = previouscol + 1; j < smallval; j++)
                        netindex[j] = i;
                    previouscol = smallval;
                    startpos = i;
                }
            }
            netindex[previouscol] = (startpos + maxnetpos) >> 1;
            for (int j = previouscol + 1; j < 256; j++)
                netindex[j] = maxnetpos;
        }

        private void Learn()
        {
            if (lengthcount < minpicturebytes) samplefac = 1;
            alphadec = 30 + ((samplefac - 1) / 3);
            byte[] p = thepicture;
            int pix = 0;
            int lim = lengthcount;
            int samplepixels = lengthcount / (3 * samplefac);
            int delta = samplepixels / ncycles;
            int alpha = initalpha;
            int radius = initradius;

            int rad = radius >> radiusbiasshift;
            if (rad <= 1) rad = 0;
            for (int i = 0; i < rad; i++)
                radpower[i] = alpha * (((rad * rad - i * i) * radbias) / (rad * rad));

            int step;
            if (lengthcount < minpicturebytes)
                step = 3;
            else if ((lengthcount % prime1) != 0)
                step = 3 * prime1;
            else if ((lengthcount % prime2) != 0)
                step = 3 * prime2;
            else if ((lengthcount % prime3) != 0)
                step = 3 * prime3;
            else
                step = 3 * prime4;

            int i2 = 0;
            while (i2 < samplepixels)
            {
                int b = (p[pix] & 0xff) << netbiasshift;
                int g = (p[pix + 1] & 0xff) << netbiasshift;
                int r = (p[pix + 2] & 0xff) << netbiasshift;

                int j = Contest(b, g, r);

                Altersingle(alpha, j, b, g, r);
                if (rad != 0) Alterneigh(rad, j, b, g, r);

                pix += step;
                if (pix >= lim) pix -= lengthcount;

                i2++;
                if (delta == 0) delta = 1;
                if (i2 % delta == 0)
                {
                    alpha -= alpha / alphadec;
                    radius -= radius / radiusdec;
                    rad = radius >> radiusbiasshift;
                    if (rad <= 1) rad = 0;
                    for (int k = 0; k < rad; k++)
                        radpower[k] = alpha * (((rad * rad - k * k) * radbias) / (rad * rad));
                }
            }
        }

        public int Map(int b, int g, int r)
        {
            int bestd = 1000;
            int best = -1;
            int i = netindex[g];
            int j = i - 1;

            while ((i < netsize) || (j >= 0))
            {
                if (i < netsize)
                {
                    int[] p = network[i];
                    int dist = p[1] - g;
                    if (dist >= bestd) i = netsize;
                    else
                    {
                        i++;
                        if (dist < 0) dist = -dist;
                        int a = p[0] - b; if (a < 0) a = -a;
                        dist += a;
                        if (dist < bestd)
                        {
                            a = p[2] - r; if (a < 0) a = -a;
                            dist += a;
                            if (dist < bestd) { bestd = dist; best = p[3]; }
                        }
                    }
                }
                if (j >= 0)
                {
                    int[] p = network[j];
                    int dist = g - p[1];
                    if (dist >= bestd) j = -1;
                    else
                    {
                        j--;
                        if (dist < 0) dist = -dist;
                        int a = p[0] - b; if (a < 0) a = -a;
                        dist += a;
                        if (dist < bestd)
                        {
                            a = p[2] - r; if (a < 0) a = -a;
                            dist += a;
                            if (dist < bestd) { bestd = dist; best = p[3]; }
                        }
                    }
                }
            }
            return best;
        }

        private void Unbiasnet()
        {
            for (int i = 0; i < netsize; i++)
            {
                network[i][0] >>= netbiasshift;
                network[i][1] >>= netbiasshift;
                network[i][2] >>= netbiasshift;
                network[i][3] = i;
            }
        }

        private void Alterneigh(int rad, int i, int b, int g, int r)
        {
            int lo = i - rad; if (lo < -1) lo = -1;
            int hi = i + rad; if (hi > netsize) hi = netsize;

            int j = i + 1;
            int k = i - 1;
            int m = 1;
            while ((j < hi) || (k > lo))
            {
                int a = radpower[m++];
                if (j < hi)
                {
                    int[] p = network[j++];
                    try
                    {
                        p[0] -= (a * (p[0] - b)) / alpharadbias;
                        p[1] -= (a * (p[1] - g)) / alpharadbias;
                        p[2] -= (a * (p[2] - r)) / alpharadbias;
                    }
                    catch { }
                }
                if (k > lo)
                {
                    int[] p = network[k--];
                    try
                    {
                        p[0] -= (a * (p[0] - b)) / alpharadbias;
                        p[1] -= (a * (p[1] - g)) / alpharadbias;
                        p[2] -= (a * (p[2] - r)) / alpharadbias;
                    }
                    catch { }
                }
            }
        }

        private void Altersingle(int alpha, int i, int b, int g, int r)
        {
            int[] n = network[i];
            n[0] -= (alpha * (n[0] - b)) / initalpha;
            n[1] -= (alpha * (n[1] - g)) / initalpha;
            n[2] -= (alpha * (n[2] - r)) / initalpha;
        }

        private int Contest(int b, int g, int r)
        {
            int bestd = ~(1 << 31);
            int bestbiasd = bestd;
            int bestpos = -1;
            int bestbiaspos = bestpos;

            for (int i = 0; i < netsize; i++)
            {
                int[] n = network[i];
                int dist = n[0] - b; if (dist < 0) dist = -dist;
                int a = n[1] - g; if (a < 0) a = -a; dist += a;
                a = n[2] - r; if (a < 0) a = -a; dist += a;
                if (dist < bestd) { bestd = dist; bestpos = i; }
                int biasdist = dist - ((bias[i]) >> (intbiasshift - netbiasshift));
                if (biasdist < bestbiasd) { bestbiasd = biasdist; bestbiaspos = i; }
                int betafreq = (freq[i] >> betashift);
                freq[i] -= betafreq;
                bias[i] += (betafreq << gammashift);
            }
            freq[bestpos] += beta;
            bias[bestpos] -= betagamma;
            return bestbiaspos;
        }
    }
}
