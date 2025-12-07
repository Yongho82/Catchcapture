using System;
using System.Collections.Generic; 
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatchCapture.Models
{
    public class Settings
    {
        // General
        public bool ShowSavePrompt { get; set; } = true;
        public bool ShowPreviewAfterCapture { get; set; } = false;
        public bool OpenEditorAfterCapture { get; set; } = true; // 캡처 후 편집창 자동 열기

        public void Save()
        {
            Save(this);
        }

        // Print Screen key
        public bool UsePrintScreenKey { get; set; } = false;
        public string PrintScreenAction { get; set; } = "영역 캡처";

        // Capture save options
        public string FileSaveFormat { get; set; } = "PNG"; // PNG, JPG, BMP, GIF
        public int ImageQuality { get; set; } = 100;
        public string DefaultSaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture");
        public bool AutoSaveCapture { get; set; } = false;

        // Persisted window states
        public double LastMainLeft { get; set; } = 0;
        public double LastMainTop { get; set; } = 0;
        public double LastSimpleLeft { get; set; } = 0;
        public double LastSimpleTop { get; set; } = 0;
        public double LastTrayLeft { get; set; } = 0;
        public double LastTrayTop { get; set; } = 0;
        public bool LastModeIsSimple { get; set; } = false;
        public bool SimpleModeVertical { get; set; } = false;
        public bool IsTrayMode { get; set; } = false;
        public string LastActiveMode { get; set; } = "Normal"; // "Normal", "Simple", "Tray"
        public static event EventHandler? SettingsChanged;

        // Tray mode icon customization  
        public List<string> TrayModeIcons { get; set; } = new List<string>
        {
            "AreaCapture", "DelayCapture", "FullScreen", 
            "DesignatedCapture", "WindowCapture", "UnitCapture"
        };
        
        // Simple mode icon customization (built-in tools)
        public List<string> SimpleModeIcons { get; set; } = new List<string>
        {
            "AreaCapture", "DelayCapture", "FullScreen", "DesignatedCapture"
        };

        // Simple mode external application shortcuts
        public List<ExternalAppShortcut> SimpleModeApps { get; set; } = new List<ExternalAppShortcut>();

        // Simple mode instant edit
        public bool SimpleModeInstantEdit { get; set; } = false;
        
        // Simple mode UI scale level (0: default, 1: large icons only, 2: large icons + text)
        public int SimpleModeUIScaleLevel { get; set; } = 0;
        
        // System settings        
        // System settings
        public bool StartWithWindows { get; set; } = false;
        public string StartupMode { get; set; } = "Normal"; // "Normal", "Simple", "Tray"
        public string Language { get; set; } = "ko"; // "ko", "zh", "ja", "en"
        
        // Hotkeys
        public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefaults();

        private static string GetSettingsPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(localAppData, "CatchCapture");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "settings.json");
        }

        // Legacy roaming path (for one-time import)
        private static string GetLegacySettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "CatchCapture");
            return Path.Combine(dir, "settings.json");
        }

        public static string SettingsPath => GetSettingsPath();

        public static Settings Load()
        {
            string settingsPath = GetSettingsPath();

            if (!File.Exists(settingsPath))
            {
                // One-time import from legacy path if exists
                string legacyPath = GetLegacySettingsPath();
                if (File.Exists(legacyPath))
                {
                    try
                    {
                        string legacyJson = File.ReadAllText(legacyPath);
                        var legacy = JsonSerializer.Deserialize<Settings>(legacyJson);
                        if (legacy != null)
                        {
                            Save(legacy); // persist to new local appdata path
                            return legacy;
                        }
                    }
                    catch { /* ignore and create new */ }
                }
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
            if (!TrySave(settings, out string? error))
            {
                System.Windows.MessageBox.Show($"설정 저장 실패: {error}", "오류");
            }
            else
            {
                // ★ 저장 성공 시 모든 창에 알림
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool TrySave(Settings settings, out string? error)
        {
            error = null;
            try
            {
                string settingsPath = GetSettingsPath();
                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Serialize
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

                // Atomic write: write to temp and replace
                string tmp = settingsPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(settingsPath))
                {
                    File.Replace(tmp, settingsPath, settingsPath + ".bak", ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tmp, settingsPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public class HotkeySettings
    {
        // 캡처 기능들
        public ToggleHotkey RegionCapture { get; set; } = new ToggleHotkey();        // 영역캡처
        public ToggleHotkey DelayCapture { get; set; } = new ToggleHotkey();         // 지연캡처
        public ToggleHotkey RealTimeCapture { get; set; } = new ToggleHotkey();      // 순간캡처
        public ToggleHotkey MultiCapture { get; set; } = new ToggleHotkey();         // 멀티캡처
        public ToggleHotkey FullScreen { get; set; } = new ToggleHotkey();           // 전체화면
        public ToggleHotkey DesignatedCapture { get; set; } = new ToggleHotkey();    // 지정캡처
        public ToggleHotkey WindowCapture { get; set; } = new ToggleHotkey();        // 창캡처
        public ToggleHotkey ElementCapture { get; set; } = new ToggleHotkey();       // 단위캡처
        public ToggleHotkey ScrollCapture { get; set; } = new ToggleHotkey();        // 스크롤캡처
        
        // 기타 기능들
        public ToggleHotkey SaveAll { get; set; } = new ToggleHotkey();              // 전체저장
        public ToggleHotkey DeleteAll { get; set; } = new ToggleHotkey();            // 전체삭제
        public ToggleHotkey SimpleMode { get; set; } = new ToggleHotkey();           // 간편모드
        public ToggleHotkey OpenSettings { get; set; } = new ToggleHotkey();         // 설정

        // Legacy (kept for backward compatibility with older settings.json)
        public ToggleHotkey SimpleCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey SizeCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey ScreenRecord { get; set; } = new ToggleHotkey();

        public static HotkeySettings CreateDefaults()
        {
            return new HotkeySettings
            {
                // 캡처 기능: Ctrl+Shift 조합, 비활성화
                RegionCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "A" },
                DelayCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "D" },
                RealTimeCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "R" },
                MultiCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "M" },
                FullScreen = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "F" },
                DesignatedCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "W" },
                WindowCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "C" },
                ElementCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "E" },
                ScrollCapture = new ToggleHotkey { Enabled = false, Ctrl = true, Shift = true, Key = "S" },

                // 편집/기타 기능: 활성화
                SaveAll = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "S" },
                DeleteAll = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "D" },
                SimpleMode = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "Q" },
                OpenSettings = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "O" },

                // Legacy defaults left as disabled
                SimpleCapture = new ToggleHotkey { Enabled = false },
                SizeCapture = new ToggleHotkey { Enabled = false },
                ScreenRecord = new ToggleHotkey { Enabled = false }
            };
        }
    }

    public class ToggleHotkey
    {
        public bool Enabled { get; set; } = false;
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }
        public string Key { get; set; } = ""; // Single letter or function key name
    }

    // External application shortcut info for Simple Mode
    public class ExternalAppShortcut
    {
        public string DisplayName { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;   // exe or .lnk path
        public string? Arguments { get; set; }                    // optional
        public string? WorkingDirectory { get; set; }
        public string? IconPath { get; set; }                     // optional custom icon
    }
}