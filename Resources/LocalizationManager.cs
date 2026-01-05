using System;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace CatchCapture.Resources
{
    /// <summary>
    /// 다국어 리소스 관리 헬퍼 클래스
    /// </summary>
    public static class LocalizationManager
    {
        private static ResourceManager? _resourceManager;
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        static LocalizationManager()
        {
            // Resources 폴더의 Strings 리소스 파일들을 로드
            _resourceManager = new ResourceManager(
                "CatchCapture.Resources.Strings",
                Assembly.GetExecutingAssembly()
            );
        }

        /// <summary>
        /// 현재 언어 설정
        /// </summary>
        public static CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value;
                    CultureInfo.CurrentUICulture = value;
                    CultureInfo.CurrentCulture = value;
                    UpdateResources();
                    LanguageChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        private static void UpdateResources()
        {
            if (System.Windows.Application.Current == null) return;

            // 모든 리소스 키를 반복하여 Application resources에 등록
            // 이렇게 하면 XAML에서 {DynamicResource Key} 형식을 사용할 수 있습니다.
            var resourceSet = _resourceManager?.GetResourceSet(_currentCulture, true, true);
            if (resourceSet != null)
            {
                foreach (System.Collections.DictionaryEntry entry in resourceSet)
                {
                    if (entry.Key == null) continue;
                    
                    string? key = entry.Key.ToString();
                    if (key == null) continue;

                    string value = entry.Value?.ToString() ?? key;
                    value = value.Replace("\\n", "\n").Replace("\\r", "\r");
                    System.Windows.Application.Current.Resources[key] = value;
                }
            }
        }

        /// <summary>
        /// 언어 변경 이벤트
        /// </summary>
        public static event EventHandler? LanguageChanged;

        /// <summary>
        /// 언어 코드로 문화권 설정
        /// </summary>
        /// <param name="languageCode">언어 코드 (ko, en, ja, zh, es, de, fr)</param>
        public static void SetLanguage(string languageCode)
        {
            try
            {
                CurrentCulture = new CultureInfo(languageCode);
            }
            catch
            {
                // 잘못된 언어 코드인 경우 기본값(한국어) 사용
                CurrentCulture = new CultureInfo("ko");
            }
        }

        /// <summary>
        /// 리소스 문자열 가져오기
        /// </summary>
        /// <param name="key">리소스 키</param>
        /// <returns>현재 언어의 문자열</returns>
        public static string GetString(string key)
        {
            if (_resourceManager == null)
                return key;

            try
            {
                var value = _resourceManager.GetString(key, _currentCulture);
                if (value == null) return key;
                return value.Replace("\\n", "\n").Replace("\\r", "\r");
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// 지원되는 언어 목록
        /// </summary>
        public static readonly (string Code, string NativeName)[] SupportedLanguages = new[]
        {
            ("ko", "한국어"),
            ("en", "English"),
            ("ja", "日本語"),
            ("zh", "简体中文"),
            ("zh-TW", "繁體中文"),
            ("es", "Español"),
            ("de", "Deutsch"),
            ("fr", "Français"),
            ("pt", "Português"),
            ("ru", "Русский"),
            ("it", "Italiano"),
            ("vi", "Tiếng Việt"),
            ("id", "Bahasa Indonesia"),
            ("th", "ไทย"),
            ("tr", "Türkçe"),
            ("ar", "العربية")
        };

        /// <summary>
        /// 언어 코드에서 표시 이름 가져오기
        /// </summary>
        public static string GetLanguageDisplayName(string languageCode)
        {
            foreach (var (code, nativeName) in SupportedLanguages)
            {
                if (code == languageCode)
                    return nativeName;
            }
            return languageCode;
        }
    }
}
