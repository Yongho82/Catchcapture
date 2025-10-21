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

        // Hotkeys
        public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefaults();

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

    public class HotkeySettings
    {
        public ToggleHotkey SimpleCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey FullScreen { get; set; } = new ToggleHotkey();
        public ToggleHotkey WindowCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey RegionCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey SizeCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey ScrollCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey ScreenRecord { get; set; } = new ToggleHotkey();

        public static HotkeySettings CreateDefaults()
        {
            return new HotkeySettings
            {
                SimpleCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "M" },
                FullScreen = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "F" },
                WindowCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "W" },
                RegionCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "A" },
                SizeCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "S" },
                ScrollCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "R" },
                ScreenRecord = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "L" }
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