using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CatchCapture.Recording
{
    /// <summary>
    /// Media Foundation을 사용한 MP4 인코더 (순수 P/Invoke)
    /// </summary>
    public class MediaFoundationEncoder : IDisposable
    {
        private IntPtr _sinkWriter = IntPtr.Zero;
        private int _streamIndex = 0;
        private long _frameCount = 0;
        private int _frameRate;
        private int _width;
        private int _height;
        private long _frameDuration;
        private bool _isInitialized = false;
        
        public MediaFoundationEncoder(int width, int height, int frameRate)
        {
            _width = width;
            _height = height;
            _frameRate = frameRate;
            _frameDuration = 10_000_000 / frameRate; // 100-nanosecond units
        }
        
        /// <summary>
        /// MP4 파일 초기화
        /// </summary>
        public void Initialize(string outputPath)
        {
            if (_isInitialized) return;
            
            // Media Foundation 시작
            int hr = MFStartup(MF_VERSION, 0);
            Marshal.ThrowExceptionForHR(hr);
            
            // SinkWriter 생성
            hr = MFCreateSinkWriterFromURL(outputPath, IntPtr.Zero, IntPtr.Zero, out _sinkWriter);
            Marshal.ThrowExceptionForHR(hr);
            
            // 비디오 스트림 설정
            ConfigureVideoStream();
            
            // 녹화 시작
            hr = IMFSinkWriter_BeginWriting(_sinkWriter);
            Marshal.ThrowExceptionForHR(hr);
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// 비디오 스트림 구성
        /// </summary>
        private void ConfigureVideoStream()
        {
            IntPtr mediaTypeOut = IntPtr.Zero;
            IntPtr mediaTypeIn = IntPtr.Zero;
            
            try
            {
                // 출력 미디어 타입 생성 (H.264)
                int hr = MFCreateMediaType(out mediaTypeOut);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetGUID(mediaTypeOut, MF_MT_MAJOR_TYPE, MFMediaType_Video);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetGUID(mediaTypeOut, MF_MT_SUBTYPE, MFVideoFormat_H264);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetUINT32(mediaTypeOut, MF_MT_AVG_BITRATE, 4_000_000); // 4 Mbps
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetUINT32(mediaTypeOut, MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeSize(mediaTypeOut, MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeRatio(mediaTypeOut, MF_MT_FRAME_RATE, (uint)_frameRate, 1);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeRatio(mediaTypeOut, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFSinkWriter_AddStream(_sinkWriter, mediaTypeOut, out _streamIndex);
                Marshal.ThrowExceptionForHR(hr);
                
                // 입력 미디어 타입 생성 (RGB32)
                hr = MFCreateMediaType(out mediaTypeIn);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetGUID(mediaTypeIn, MF_MT_MAJOR_TYPE, MFMediaType_Video);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetGUID(mediaTypeIn, MF_MT_SUBTYPE, MFVideoFormat_RGB32);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFMediaType_SetUINT32(mediaTypeIn, MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeSize(mediaTypeIn, MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeRatio(mediaTypeIn, MF_MT_FRAME_RATE, (uint)_frameRate, 1);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = MFSetAttributeRatio(mediaTypeIn, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
                Marshal.ThrowExceptionForHR(hr);
                
                hr = IMFSinkWriter_SetInputMediaType(_sinkWriter, _streamIndex, mediaTypeIn, IntPtr.Zero);
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                if (mediaTypeOut != IntPtr.Zero) Marshal.Release(mediaTypeOut);
                if (mediaTypeIn != IntPtr.Zero) Marshal.Release(mediaTypeIn);
            }
        }
        
        /// <summary>
        /// 프레임 추가
        /// </summary>
        public void AddFrame(byte[] pngData)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Encoder not initialized");
            
            using var ms = new MemoryStream(pngData);
            using var bitmap = new Bitmap(ms);
            
            // Bitmap을 RGB32로 변환
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppRgb);
            
            try
            {
                // IMFMediaBuffer 생성
                int bufferSize = bmpData.Stride * bmpData.Height;
                IntPtr buffer = IntPtr.Zero;
                
                int hr = MFCreateMemoryBuffer(bufferSize, out buffer);
                Marshal.ThrowExceptionForHR(hr);
                
                try
                {
                    // 데이터 복사
                    IntPtr bufferPtr = IntPtr.Zero;
                    hr = IMFMediaBuffer_Lock(buffer, out bufferPtr, IntPtr.Zero, IntPtr.Zero);
                    Marshal.ThrowExceptionForHR(hr);
                    
                    // RGB 데이터를 버퍼로 복사 (상하 반전 필요)
                    for (int y = 0; y < bmpData.Height; y++)
                    {
                        IntPtr srcRow = bmpData.Scan0 + (bmpData.Height - 1 - y) * bmpData.Stride;
                        IntPtr dstRow = bufferPtr + y * bmpData.Stride;
                        CopyMemory(dstRow, srcRow, (uint)bmpData.Stride);
                    }
                    
                    hr = IMFMediaBuffer_Unlock(buffer);
                    Marshal.ThrowExceptionForHR(hr);
                    
                    hr = IMFMediaBuffer_SetCurrentLength(buffer, bufferSize);
                    Marshal.ThrowExceptionForHR(hr);
                    
                    // IMFSample 생성
                    IntPtr sample = IntPtr.Zero;
                    hr = MFCreateSample(out sample);
                    Marshal.ThrowExceptionForHR(hr);
                    
                    try
                    {
                        hr = IMFSample_AddBuffer(sample, buffer);
                        Marshal.ThrowExceptionForHR(hr);
                        
                        long timestamp = _frameCount * _frameDuration;
                        hr = IMFSample_SetSampleTime(sample, timestamp);
                        Marshal.ThrowExceptionForHR(hr);
                        
                        hr = IMFSample_SetSampleDuration(sample, _frameDuration);
                        Marshal.ThrowExceptionForHR(hr);
                        
                        // 샘플 쓰기
                        hr = IMFSinkWriter_WriteSample(_sinkWriter, _streamIndex, sample);
                        Marshal.ThrowExceptionForHR(hr);
                        
                        _frameCount++;
                    }
                    finally
                    {
                        if (sample != IntPtr.Zero) Marshal.Release(sample);
                    }
                }
                finally
                {
                    if (buffer != IntPtr.Zero) Marshal.Release(buffer);
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }
        
        /// <summary>
        /// 인코딩 완료 및 파일 닫기
        /// </summary>
        public void Complete()
        {
            if (!_isInitialized) return;
            
            if (_sinkWriter != IntPtr.Zero)
            {
                IMFSinkWriter_Finalize(_sinkWriter);
                Marshal.Release(_sinkWriter);
                _sinkWriter = IntPtr.Zero;
            }
            
            MFShutdown();
            _isInitialized = false;
        }
        
        public void Dispose()
        {
            Complete();
        }
        
        #region P/Invoke Declarations
        
        private const int MF_VERSION = 0x00020070;
        private const int MFVideoInterlace_Progressive = 2;
        
        private static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFVideoFormat_H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
        private static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
        
        private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_AVG_BITRATE = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
        private static readonly Guid MF_MT_INTERLACE_MODE = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        private static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        private static readonly Guid MF_MT_FRAME_RATE = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        private static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
        
        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int flags);
        
        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFShutdown();
        
        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int MFCreateSinkWriterFromURL(
            string pwszOutputURL,
            IntPtr pByteStream,
            IntPtr pAttributes,
            out IntPtr ppSinkWriter);
        
        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMediaType(out IntPtr ppMFType);
        
        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateSample(out IntPtr ppIMFSample);
        
        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMemoryBuffer(int cbMaxLength, out IntPtr ppBuffer);
        
        private static int IMFMediaType_SetGUID(IntPtr pThis, Guid guidKey, Guid guidValue)
        {
            var vtbl = Marshal.PtrToStructure<IMFAttributesVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetGUID(pThis, ref guidKey, ref guidValue);
        }
        
        private static int IMFMediaType_SetUINT32(IntPtr pThis, Guid guidKey, uint value)
        {
            var vtbl = Marshal.PtrToStructure<IMFAttributesVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetUINT32(pThis, ref guidKey, value);
        }
        
        private static int MFSetAttributeSize(IntPtr pThis, Guid guidKey, uint width, uint height)
        {
            ulong value = ((ulong)width << 32) | height;
            var vtbl = Marshal.PtrToStructure<IMFAttributesVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetUINT64(pThis, ref guidKey, value);
        }
        
        private static int MFSetAttributeRatio(IntPtr pThis, Guid guidKey, uint numerator, uint denominator)
        {
            ulong value = ((ulong)numerator << 32) | denominator;
            var vtbl = Marshal.PtrToStructure<IMFAttributesVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetUINT64(pThis, ref guidKey, value);
        }
        
        private static int IMFSinkWriter_AddStream(IntPtr pThis, IntPtr pMediaType, out int pdwStreamIndex)
        {
            var vtbl = Marshal.PtrToStructure<IMFSinkWriterVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.AddStream(pThis, pMediaType, out pdwStreamIndex);
        }
        
        private static int IMFSinkWriter_SetInputMediaType(IntPtr pThis, int dwStreamIndex, IntPtr pInputMediaType, IntPtr pEncodingParameters)
        {
            var vtbl = Marshal.PtrToStructure<IMFSinkWriterVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetInputMediaType(pThis, dwStreamIndex, pInputMediaType, pEncodingParameters);
        }
        
        private static int IMFSinkWriter_BeginWriting(IntPtr pThis)
        {
            var vtbl = Marshal.PtrToStructure<IMFSinkWriterVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.BeginWriting(pThis);
        }
        
        private static int IMFSinkWriter_WriteSample(IntPtr pThis, int dwStreamIndex, IntPtr pSample)
        {
            var vtbl = Marshal.PtrToStructure<IMFSinkWriterVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.WriteSample(pThis, dwStreamIndex, pSample);
        }
        
        private static int IMFSinkWriter_Finalize(IntPtr pThis)
        {
            var vtbl = Marshal.PtrToStructure<IMFSinkWriterVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.Finalize(pThis);
        }
        
        private static int IMFMediaBuffer_Lock(IntPtr pThis, out IntPtr ppbBuffer, IntPtr pcbMaxLength, IntPtr pcbCurrentLength)
        {
            var vtbl = Marshal.PtrToStructure<IMFMediaBufferVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.Lock(pThis, out ppbBuffer, pcbMaxLength, pcbCurrentLength);
        }
        
        private static int IMFMediaBuffer_Unlock(IntPtr pThis)
        {
            var vtbl = Marshal.PtrToStructure<IMFMediaBufferVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.Unlock(pThis);
        }
        
        private static int IMFMediaBuffer_SetCurrentLength(IntPtr pThis, int cbCurrentLength)
        {
            var vtbl = Marshal.PtrToStructure<IMFMediaBufferVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetCurrentLength(pThis, cbCurrentLength);
        }
        
        private static int IMFSample_AddBuffer(IntPtr pThis, IntPtr pBuffer)
        {
            var vtbl = Marshal.PtrToStructure<IMFSampleVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.AddBuffer(pThis, pBuffer);
        }
        
        private static int IMFSample_SetSampleTime(IntPtr pThis, long hnsSampleTime)
        {
            var vtbl = Marshal.PtrToStructure<IMFSampleVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetSampleTime(pThis, hnsSampleTime);
        }
        
        private static int IMFSample_SetSampleDuration(IntPtr pThis, long hnsDuration)
        {
            var vtbl = Marshal.PtrToStructure<IMFSampleVtbl>(Marshal.ReadIntPtr(pThis));
            return vtbl.SetSampleDuration(pThis, hnsDuration);
        }
        
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
        
        #region COM Vtbl Structures
        
        [StructLayout(LayoutKind.Sequential)]
        private struct IMFAttributesVtbl
        {
            public IntPtr QueryInterface;
            public IntPtr AddRef;
            public IntPtr Release;
            public IntPtr GetItem;
            public IntPtr GetItemType;
            public IntPtr CompareItem;
            public IntPtr Compare;
            public IntPtr GetUINT32;
            public IntPtr GetUINT64;
            public IntPtr GetDouble;
            public IntPtr GetGUID;
            public IntPtr GetStringLength;
            public IntPtr GetString;
            public IntPtr GetAllocatedString;
            public IntPtr GetBlobSize;
            public IntPtr GetBlob;
            public IntPtr GetAllocatedBlob;
            public IntPtr GetUnknown;
            public SetItemDelegate SetItem;
            public IntPtr DeleteItem;
            public IntPtr DeleteAllItems;
            public SetUINT32Delegate SetUINT32;
            public SetUINT64Delegate SetUINT64;
            public IntPtr SetDouble;
            public SetGUIDDelegate SetGUID;
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetItemDelegate(IntPtr pThis, ref Guid guidKey, IntPtr value);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetUINT32Delegate(IntPtr pThis, ref Guid guidKey, uint value);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetUINT64Delegate(IntPtr pThis, ref Guid guidKey, ulong value);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetGUIDDelegate(IntPtr pThis, ref Guid guidKey, ref Guid guidValue);
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct IMFSinkWriterVtbl
        {
            public IntPtr QueryInterface;
            public IntPtr AddRef;
            public IntPtr Release;
            public AddStreamDelegate AddStream;
            public SetInputMediaTypeDelegate SetInputMediaType;
            public BeginWritingDelegate BeginWriting;
            public WriteSampleDelegate WriteSample;
            public IntPtr SendStreamTick;
            public IntPtr PlaceMarker;
            public IntPtr NotifyEndOfSegment;
            public IntPtr Flush;
            public FinalizeDelegate Finalize;
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int AddStreamDelegate(IntPtr pThis, IntPtr pMediaType, out int pdwStreamIndex);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetInputMediaTypeDelegate(IntPtr pThis, int dwStreamIndex, IntPtr pInputMediaType, IntPtr pEncodingParameters);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int BeginWritingDelegate(IntPtr pThis);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int WriteSampleDelegate(IntPtr pThis, int dwStreamIndex, IntPtr pSample);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int FinalizeDelegate(IntPtr pThis);
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct IMFMediaBufferVtbl
        {
            public IntPtr QueryInterface;
            public IntPtr AddRef;
            public IntPtr Release;
            public LockDelegate Lock;
            public UnlockDelegate Unlock;
            public IntPtr GetCurrentLength;
            public SetCurrentLengthDelegate SetCurrentLength;
            public IntPtr GetMaxLength;
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int LockDelegate(IntPtr pThis, out IntPtr ppbBuffer, IntPtr pcbMaxLength, IntPtr pcbCurrentLength);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int UnlockDelegate(IntPtr pThis);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetCurrentLengthDelegate(IntPtr pThis, int cbCurrentLength);
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct IMFSampleVtbl
        {
            public IntPtr QueryInterface;
            public IntPtr AddRef;
            public IntPtr Release;
            public IntPtr GetItem;
            public IntPtr GetItemType;
            public IntPtr CompareItem;
            public IntPtr Compare;
            public IntPtr GetUINT32;
            public IntPtr GetUINT64;
            public IntPtr GetDouble;
            public IntPtr GetGUID;
            public IntPtr GetStringLength;
            public IntPtr GetString;
            public IntPtr GetAllocatedString;
            public IntPtr GetBlobSize;
            public IntPtr GetBlob;
            public IntPtr GetAllocatedBlob;
            public IntPtr GetUnknown;
            public IntPtr SetItem;
            public IntPtr DeleteItem;
            public IntPtr DeleteAllItems;
            public IntPtr SetUINT32;
            public IntPtr SetUINT64;
            public IntPtr SetDouble;
            public IntPtr SetGUID;
            public IntPtr GetSampleFlags;
            public IntPtr SetSampleFlags;
            public IntPtr GetSampleTime;
            public SetSampleTimeDelegate SetSampleTime;
            public IntPtr GetSampleDuration;
            public SetSampleDurationDelegate SetSampleDuration;
            public IntPtr GetBufferCount;
            public IntPtr GetBufferByIndex;
            public IntPtr ConvertToContiguousBuffer;
            public AddBufferDelegate AddBuffer;
            public IntPtr RemoveBufferByIndex;
            public IntPtr RemoveAllBuffers;
            public IntPtr GetTotalLength;
            public IntPtr CopyToBuffer;
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int AddBufferDelegate(IntPtr pThis, IntPtr pBuffer);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetSampleTimeDelegate(IntPtr pThis, long hnsSampleTime);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int SetSampleDurationDelegate(IntPtr pThis, long hnsDuration);
        }
        
        #endregion
        
        #endregion
    }
}
