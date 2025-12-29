using System;
using System.Collections.Generic; 
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CatchCapture.Utilities;

namespace CatchCapture.Models
{
    public class Settings
    {
        private static Settings? _currentLoadingSettings;
        // General
        public bool ShowSavePrompt { get; set; } = true;
        public bool ShowPreviewAfterCapture { get; set; } = false;
        public bool OpenEditorAfterCapture { get; set; } = false; // 캡처 후 편집창 자동 열기
        public bool AutoCopyToClipboard { get; set; } = true;
        public bool ShowMagnifier { get; set; } = true; // 영역 캡처 시 돋보기 표시

        // Theme Settings
        public string ThemeMode { get; set; } = "General"; // "General", "Dark", "Light", "Blue"
        public string ThemeBackgroundColor { get; set; } = "#FFFFFF";
        public string ThemeTextColor { get; set; } = "#333333";
        // Stored custom theme colors (persists even when switching to other themes)
        public string CustomThemeBackgroundColor { get; set; } = "#FFFFFF";
        public string CustomThemeTextColor { get; set; } = "#333333";

        // Capture Line & Overlay Settings
        public string OverlayBackgroundColor { get; set; } = "#8C000000"; // Default: 140/255 opacity black
        public string CaptureLineColor { get; set; } = "#FF0000";       // Default: Red
        public double CaptureLineThickness { get; set; } = 1.0;
        public string CaptureLineStyle { get; set; } = "Dash";          // "Solid", "Dash", "Dot", "DashDot"
        
        // Recording Settings
        public RecordingSettings Recording { get; set; } = new RecordingSettings();

        public void Save()
        {
            Save(this);
        }

        // Print Screen key
        public bool UsePrintScreenKey { get; set; } = false;
        public string PrintScreenAction { get; set; } = "영역 캡처";

        // Capture save options
        public string FileSaveFormat { get; set; } = "PNG"; // PNG, JPG, BMP, GIF, WEBP
        public int ImageQuality { get; set; } = 100;
        public string DefaultSaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture");
        public bool AutoSaveCapture { get; set; } = true; // ★ 메모리 최적화: 기본값 true
        
        // Auto Save File Name & Folder Settings
        public string FileNameTemplate { get; set; } = "Catch_$yyyy-MM-dd_HH-mm-ss$";
        public string FolderGroupingMode { get; set; } = "Monthly"; // "None", "Monthly", "Quarterly", "Yearly"
        
        // Note Settings
        public string NoteStoragePath { get; set; } = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture"), "notedata");
        public string NoteFileNameTemplate { get; set; } = "Catch_$yyyy-MM-dd_HH-mm-ss$";
        public string NoteFolderGroupingMode { get; set; } = "None";
        public string? NotePassword { get; set; }
        public string? NotePasswordHint { get; set; }
        public bool IsNoteLockEnabled { get; set; } = false;
        public bool OptimizeNoteImages { get; set; } = true;
        public string NoteSaveFormat { get; set; } = "WEBP"; // PNG, JPG, BMP, GIF, WEBP
        public int NoteImageQuality { get; set; } = 100;
        public int TrashRetentionDays { get; set; } = 30;

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
        
        // Note Explorer Window State
        public double NoteExplorerWidth { get; set; } = 1450;
        public double NoteExplorerHeight { get; set; } = 995;
        public double NoteExplorerLeft { get; set; } = -9999;
        public double NoteExplorerTop { get; set; } = -9999;
        public double NoteExplorerSplitterPosition { get; set; } = -9999; // GridSplitter column width
        public int NoteExplorerViewMode { get; set; } = 0; // 0: List, 1: Card
        
        // Note Viewer Window State
        public double NoteViewerWidth { get; set; } = 1200;
        public double NoteViewerHeight { get; set; } = 920;
        public double NoteViewerLeft { get; set; } = -9999;
        public double NoteViewerTop { get; set; } = -9999;
        
        // Note Input Window State
        public double NoteInputWidth { get; set; } = 1200;
        public double NoteInputHeight { get; set; } = 920;
        public double NoteInputLeft { get; set; } = -9999;
        public double NoteInputTop { get; set; } = -9999;
        
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
        public bool SimpleModeInstantEdit { get; set; } = true;
        
        // Simple mode UI scale level (0: default, 1: large icons only, 2: large icons + text)
        public int SimpleModeUIScaleLevel { get; set; } = 0;

        // Main window menu items order (for customizable main menu)
        public List<string> MainMenuItems { get; set; } = new List<string>
        {
            "AreaCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
            "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord"
        };              

        public bool StartWithWindows { get; set; } = true;
        public bool RunAsAdmin { get; set; } = false;
        public string StartupMode { get; set; } = "Normal"; // "Normal", "Simple", "Tray"
        public string Language { get; set; } = "ko"; // "ko", "en", "ja", "zh", "es", "de", "fr", "pt", "ru", "it"
        
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
            if (_currentLoadingSettings != null) return _currentLoadingSettings;

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
                if (settings != null)
                {
                _currentLoadingSettings = settings;
                try
                {
                    // Sync password from DB if missing in JSON
                    if (string.IsNullOrEmpty(settings.NotePassword))
                    {
                        try
                        {
                            var dbPwd = CatchCapture.Utilities.DatabaseManager.Instance.GetConfig("NotePassword");
                            var dbHint = CatchCapture.Utilities.DatabaseManager.Instance.GetConfig("NotePasswordHint");
                            if (!string.IsNullOrEmpty(dbPwd))
                            {
                                settings.NotePassword = dbPwd;
                                settings.NotePasswordHint = dbHint;
                                var dbLock = CatchCapture.Utilities.DatabaseManager.Instance.GetConfig("IsNoteLockEnabled");
                                settings.IsNoteLockEnabled = dbLock == "1";
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    _currentLoadingSettings = null;
                }
                return settings;
                }
                return new Settings();
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

                // Sync password to DB
                try
                {
                    CatchCapture.Utilities.DatabaseManager.Instance.SetConfig("NotePassword", settings.NotePassword);
                    CatchCapture.Utilities.DatabaseManager.Instance.SetConfig("NotePasswordHint", settings.NotePasswordHint);
                    CatchCapture.Utilities.DatabaseManager.Instance.SetConfig("IsNoteLockEnabled", settings.IsNoteLockEnabled ? "1" : "0");
                }
                catch { /* Ignore DB errors in setting save */ }

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
        public ToggleHotkey OcrCapture { get; set; } = new ToggleHotkey();           // OCR 캡처
        public ToggleHotkey ScreenRecord { get; set; } = new ToggleHotkey();         // 화면 녹화
        
        // 기타 기능들
        public ToggleHotkey SaveAll { get; set; } = new ToggleHotkey();              // 전체저장
        public ToggleHotkey DeleteAll { get; set; } = new ToggleHotkey();            // 전체삭제
        public ToggleHotkey SimpleMode { get; set; } = new ToggleHotkey();           // 간편모드
        public ToggleHotkey TrayMode { get; set; } = new ToggleHotkey();             // 트레이모드
        public ToggleHotkey OpenSettings { get; set; } = new ToggleHotkey();         // 설정
        public ToggleHotkey OpenEditor { get; set; } = new ToggleHotkey();           // 에디터
        public ToggleHotkey OpenNote { get; set; } = new ToggleHotkey();             // 내 노트

        // 녹화 시 시작/중지 단축키 (RecordingWindow 전용)
        public ToggleHotkey RecordingStartStop { get; set; } = new ToggleHotkey();

        // Legacy (kept for backward compatibility with older settings.json)
        public ToggleHotkey SimpleCapture { get; set; } = new ToggleHotkey();
        public ToggleHotkey SizeCapture { get; set; } = new ToggleHotkey();

        public static HotkeySettings CreateDefaults()
        {
            return new HotkeySettings
            {
                // 캡처 기능: Ctrl+Shift 조합, 기본값 활성화
                RegionCapture = new ToggleHotkey { Enabled = true, Key = "F1" },
                DelayCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "D" },
                RealTimeCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "R" },
                MultiCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "M" },
                FullScreen = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "F" },
                DesignatedCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "W" },
                WindowCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "C" },
                ElementCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "E" },
                ScrollCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "S" },

                OcrCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "O" },
                ScreenRecord = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "V" },

                // 편집/기타 기능: 활성화
                SaveAll = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "S" },
                DeleteAll = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "Delete" },
                SimpleMode = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "Q" },
                TrayMode = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "T" },
                OpenSettings = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "O" },
                OpenEditor = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "E" },
                OpenNote = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "N" },
                RecordingStartStop = new ToggleHotkey { Enabled = true, Key = "F2" },

                // Legacy defaults left as disabled
                SimpleCapture = new ToggleHotkey { Enabled = false },
                SizeCapture = new ToggleHotkey { Enabled = false }
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