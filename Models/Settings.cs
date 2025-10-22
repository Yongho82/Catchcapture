using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatchCapture.Models
{
    public class Settings
    {
        // General
        public bool ShowSavePrompt { get; set; } = true;

        // Capture save options
        public string FileSaveFormat { get; set; } = "PNG"; // PNG or JPG
        public string DefaultSaveFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        public bool AutoSaveCapture { get; set; } = false;

        // Persisted window states
        public double LastMainLeft { get; set; } = double.NaN;
        public double LastMainTop { get; set; } = double.NaN;
        public double LastSimpleLeft { get; set; } = double.NaN;
        public double LastSimpleTop { get; set; } = double.NaN;
        public bool LastModeIsSimple { get; set; } = false;
        public bool SimpleModeVertical { get; set; } = false;

        // Hotkeys
        public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefaults();

        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "CatchCapture");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "settings.json");
        }

        public static Settings Load()
        {
            string settingsPath = GetSettingsPath();

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
            string settingsPath = GetSettingsPath();

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

    public class HotkeySettings
    {
        // New set requested
        public ToggleHotkey RegionCapture { get; set; } = new ToggleHotkey();        // 영역캡처
        public ToggleHotkey DelayCapture { get; set; } = new ToggleHotkey();         // 지연캡처
        public ToggleHotkey FullScreen { get; set; } = new ToggleHotkey();           // 전체화면
        public ToggleHotkey DesignatedCapture { get; set; } = new ToggleHotkey();    // 지정캡처
        public ToggleHotkey SaveAll { get; set; } = new ToggleHotkey();              // 전체저장
        public ToggleHotkey DeleteAll { get; set; } = new ToggleHotkey();            // 전체삭제
        public ToggleHotkey SimpleMode { get; set; } = new ToggleHotkey();           // 간편모드
        public ToggleHotkey OpenSettings { get; set; } = new ToggleHotkey();         // 설정

        // Legacy (kept for backward compatibility with older settings.json)
        public ToggleHotkey SimpleCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey WindowCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey SizeCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey ScrollCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey ScreenRecord { get; set; } = new ToggleHotkey();

        public static HotkeySettings CreateDefaults()
        {
            return new HotkeySettings
            {
                // Reasonable defaults (Ctrl + letter)
                RegionCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "A" },
                DelayCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "D" },
                FullScreen = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "F" },
                DesignatedCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "W" },
                SaveAll = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "Z" },
                DeleteAll = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "X" },
                SimpleMode = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "M" },
                OpenSettings = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "O" },

                // Legacy defaults left as disabled
                SimpleCapture = new ToggleHotkey { Enabled = false },
                WindowCapture = new ToggleHotkey { Enabled = false },
                SizeCapture = new ToggleHotkey { Enabled = false },
                ScrollCapture = new ToggleHotkey { Enabled = false },
                ScreenRecord = new ToggleHotkey { Enabled = false }
            };
        }
    }

    public class ToggleHotkey
    {
        public bool Enabled { get; set; } = true;
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }
        public string Key { get; set; } = ""; // Single letter or function key name
    }
}