using System;

namespace CatchCapture.Models
{
    /// <summary>
    /// 레거시 지원을 위한 중계 클래스
    /// 이제 모든 다국어 처리는 CatchCapture.Resources.LocalizationManager에 위임합니다.
    /// 이렇게 하면 번역 데이터의 이중 관리를 없애고 .resx 파일만 수정하면 됩니다.
    /// </summary>
    public static class LocalizationManager
    {
        // 기존 코드와의 호환성을 위한 이벤트
        public static event EventHandler? LanguageChanged;

        static LocalizationManager()
        {
            // Resources의 언어 변경 이벤트를 구독하여 이쪽 이벤트도 발생시킴
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) =>
            {
                LanguageChanged?.Invoke(null, EventArgs.Empty);
            };
        }

        public static string CurrentLanguage
        {
            get => CatchCapture.Resources.LocalizationManager.CurrentCulture.Name;
            set => SetLanguage(value);
        }

        public static void SetLanguage(string language)
        {
            CatchCapture.Resources.LocalizationManager.SetLanguage(language);
        }

        public static string Get(string key)
        {
            // 하드코딩된 사전을 모두 제거하고
            // 중앙 리소스 매니저에게 직접 요청합니다.
            return CatchCapture.Resources.LocalizationManager.GetString(key);
        }
    }
}
