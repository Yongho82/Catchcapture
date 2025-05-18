using System;
using System.IO;
using System.Text.Json;

namespace CatchCapture.Models
{
    public class Settings
    {
        public bool ShowSavePrompt { get; set; } = true;

        public static Settings Load()
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string settingsPath = Path.Combine(appPath, "settings.json");

            if (!File.Exists(settingsPath))
            {
                var defaultSettings = new Settings();
                Save(defaultSettings);
                return defaultSettings;
            }

            try
            {
                string json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                return settings ?? new Settings();
            }
            catch (Exception)
            {
                return new Settings();
            }
        }

        public static void Save(Settings settings)
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string settingsPath = Path.Combine(appPath, "settings.json");

            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception)
            {
                // 저장 실패 시 기본 설정 사용
            }
        }
    }
} 