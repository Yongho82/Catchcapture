using System.Drawing;

namespace CatchCapture.Models
{
    public class MonitorInfo
    {
        /// <summary>
        /// 모니터 인덱스
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// 주 모니터 여부
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// 모니터 전체 영역
        /// </summary>
        public Rectangle Bounds { get; set; }
        
        /// <summary>
        /// 모니터 작업 영역 (작업표시줄 제외)
        /// </summary>
        public Rectangle WorkingArea { get; set; }
        
        /// <summary>
        /// 모니터 장치 이름
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;
        
        /// <summary>
        /// 모니터 해상도 문자열
        /// </summary>
        public string Resolution => $"{Bounds.Width}x{Bounds.Height}";
        
        /// <summary>
        /// 모니터 표시 이름
        /// </summary>
        public string DisplayName => $"모니터 {Index + 1}" + (IsPrimary ? " (주)" : "") + $" - {Resolution}";
        
        /// <summary>
        /// 모니터 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}