using System;

namespace CatchCapture.Models
{
    /// <summary>
    /// 화면 녹화 설정 모델
    /// </summary>
    public class RecordingSettings
    {
        // 파일 형식
        public RecordingFormat Format { get; set; } = RecordingFormat.MP4;
        
        // 화질 (비트레이트) -- 기본 설정 SD(중화질)로 변경
        public RecordingQuality Quality { get; set; } = RecordingQuality.Medium;
        
        // 프레임 레이트
        public int FrameRate { get; set; } = 30;
        
        // 오디오 녹음 여부 (시스템 소리)
        public bool RecordAudio { get; set; } = false;
        
        // 마이크 녹음 여부
        public bool RecordMic { get; set; } = false;
        
        // 마우스 효과 (클릭 시각화)
        public bool ShowMouseEffects { get; set; } = true;
        
        // 텍스트 오버레이
        public bool ShowTextOverlay { get; set; } = false;
        
        // 녹화 전 카운트다운 (초)
        public int CountdownSeconds { get; set; } = 0;
        
        // 마지막 녹화 영역 (복원용)
        public double LastAreaLeft { get; set; } = 100;
        public double LastAreaTop { get; set; } = 100;
        public double LastAreaWidth { get; set; } = 800;
        public double LastAreaHeight { get; set; } = 600;
        
        /// <summary>
        /// 비트레이트 값 반환 (bps)
        /// </summary>
        public int GetBitrate()
        {
            return Quality switch
            {
                RecordingQuality.High => 8_000_000,   // 8 Mbps
                RecordingQuality.Medium => 4_000_000, // 4 Mbps
                RecordingQuality.Low => 2_000_000,    // 2 Mbps
                _ => 4_000_000
            };
        }
    }
    
    /// <summary>
    /// 녹화 파일 형식
    /// </summary>
    public enum RecordingFormat
    {
        MP4,
        GIF
    }
    
    /// <summary>
    /// 녹화 화질
    /// </summary>
    public enum RecordingQuality
    {
        High,   // 고화질
        Medium, // 중화질
        Low     // 저화질
    }
}
