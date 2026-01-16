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
        private static Settings? _instance;
        private static Settings? _currentLoadingSettings;
        // General
        public bool ShowSavePrompt { get; set; } = true;
        public bool ShowPreviewAfterCapture { get; set; } = false;
        public bool OpenEditorAfterCapture { get; set; } = false; // 캡처 후 편집창 자동 열기
        public bool AutoCopyToClipboard { get; set; } = true;
        public bool ShowMagnifier { get; set; } = true; // 영역 캡처 시 돋보기 표시
        public bool ShowColorPalette { get; set; } = true; // 돋보기 하단 색상 팔레트 표시

        // Theme Settings
        public string ThemeMode { get; set; } = "General"; // "General", "Dark", "Light", "Blue"
        public string ThemeBackgroundColor { get; set; } = "#FFFFFF";
        public string ThemeTextColor { get; set; } = "#333333";
        // Stored custom theme colors (persists even when switching to other themes)
        public string CustomThemeBackgroundColor { get; set; } = "#FFFFFF";
        public string CustomThemeTextColor { get; set; } = "#333333";

        // Custom Palette Colors
        public List<string> CustomPaletteColors { get; set; } = new List<string>();

        // Capture Line & Overlay Settings
        public string OverlayBackgroundColor { get; set; } = "#8C000000"; // Default: 140/255 opacity black
        public string CaptureLineColor { get; set; } = "#FF0000";       // Default: Red
        public double CaptureLineThickness { get; set; } = 1.0;
        public string CaptureLineStyle { get; set; } = "Dash";          // "Solid", "Dash", "Dot", "DashDot"
        public bool UseOverlayCaptureMode { get; set; } = false;         // ★ true: 오버레이 캡처 (동영상 안멈춤), false: 정지 캡처
        public int EdgeCaptureRadius { get; set; } = 50;                 // 엣지 캡처 둥글기 강도 (기본: 50px)
        public int DelayCaptureSeconds { get; set; } = 3;                // 지연 캡처 대기 시간 (기본: 3초)

        // [추가] 엣지라인 및 그림자 영구 설정
        public double EdgeBorderThickness { get; set; } = 0;
        public double EdgeCornerRadius { get; set; } = 0;
        public bool HasEdgeShadow { get; set; } = false;
        public double EdgeShadowBlur { get; set; } = 15;
        public double EdgeShadowDepth { get; set; } = 5;
        public double EdgeShadowOpacity { get; set; } = 0.4;
        public string EdgeLineColor { get; set; } = "#FF0000"; // [추가] 엣지 라인 색상
        public int EdgeCapturePresetLevel { get; set; } = 3; // [추가] 엣지 프리셋 레벨 (1~5)
        
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
        public int TrashRetentionDays { get; set; } = 30; // Note Trash
        public int HistoryRetentionDays { get; set; } = 0;
        public int HistoryTrashRetentionDays { get; set; } = 30;
        public string BackupStoragePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture", "db_backups");

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
        
        // History Window State
        public double HistoryWindowWidth { get; set; } = 1430;
        public double HistoryWindowHeight { get; set; } = 820;
        public double HistoryWindowLeft { get; set; } = -9999;
        public double HistoryWindowTop { get; set; } = -9999;
        public double HistoryPreviewPaneWidth { get; set; } = 331;
        public double HistoryListPaneWidth { get; set; } = 875;
        public int HistoryViewMode { get; set; } = 0; // 0: List, 1: Card
        public int MainCaptureViewMode { get; set; } = 0; // 0: List, 1: Card

        // Preview Window State (Added)
        public double PreviewWindowWidth { get; set; } = 0;
        public double PreviewWindowHeight { get; set; } = 0;
        public double PreviewWindowLeft { get; set; } = -9999;
        public double PreviewWindowTop { get; set; } = -9999;
        public string PreviewWindowState { get; set; } = "Normal"; // "Normal", "Maximized"

        // History Column Widths
        public double HistoryColDate { get; set; } = 132;
        public double HistoryColFileName { get; set; } = 205;
        public double HistoryColMeta { get; set; } = 205;
        public double HistoryColPin { get; set; } = 53;
        public double HistoryColFavorite { get; set; } = 53;
        public double HistoryColActions { get; set; } = 53;
        public double HistoryColMemo { get; set; } = 150;

        public static event EventHandler? SettingsChanged;

        // Tray mode icon customization  
        public List<string> TrayModeIcons { get; set; } = new List<string>
        {
            "AreaCapture", "DelayCapture", "FullScreen", 
            "DesignatedCapture", "WindowCapture", "UnitCapture", "History", "MyNote"
        };
        
        // Simple mode icon customization (built-in tools)
        public List<string> SimpleModeIcons { get; set; } = new List<string>
        {
            "AreaCapture", "DelayCapture", "FullScreen", "DesignatedCapture", "History", "MyNote"
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
            "AreaCapture", "EdgeCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
            "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord"
        };              

        public bool StartWithWindows { get; set; } = true;
        public bool RunAsAdmin { get; set; } = false;
        public string StartupMode { get; set; } = "Normal"; // "Normal", "Simple", "Tray"
        public string Language { get; set; } = "ko"; // "ko", "en", "ja", "zh", "es", "de", "fr", "pt", "ru", "it"
        
        // Hotkeys
        public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefaults();

        /// <summary>
        /// Ensures all required settings exist without overwriting user customizations.
        /// Returns true if any missing items were added or broken defaults were fixed.
        /// </summary>
        public bool SyncDefaults()
        {
            bool changed = false;
            var defaults = HotkeySettings.CreateDefaults();

            // 1. Safe Hotkey Sync
            if (Hotkeys == null) { Hotkeys = defaults; changed = true; }
            else 
            {
                if (Hotkeys.SyncDefaults()) changed = true;

                // SPECIAL FIX: If OpenNote is set to exactly "N" with no modifiers 
                // (likely the broken default from early versions), update it to the intended default.
                if (Hotkeys.OpenNote != null && Hotkeys.OpenNote.Key == "N" && 
                    !Hotkeys.OpenNote.Ctrl && !Hotkeys.OpenNote.Shift && !Hotkeys.OpenNote.Alt)
                {
                    Hotkeys.OpenNote = defaults.OpenNote; // Set to Ctrl+Shift+N
                    changed = true;
                }

                // [사용자 요청] 전체삭제 단축키 변경 (Ctrl+Shift+D -> Ctrl+D)
                // 지연캡처(Ctrl+Shift+D)와 충돌 해결 및 편의성 증대
                if (Hotkeys.DeleteAll != null && Hotkeys.DeleteAll.Ctrl && Hotkeys.DeleteAll.Shift && Hotkeys.DeleteAll.Key == "D")
                {
                    Hotkeys.DeleteAll = defaults.DeleteAll; // Ctrl+D
                    changed = true;
                }

                // [사용자 요청] 엣지캡처 단축키 기본값 설정 (Ctrl+Shift+E)
                // 기존 단위캡처(E)와 충돌 방지를 위해 단위캡처는 U로 이동
                if (Hotkeys.EdgeCapture != null && (string.IsNullOrEmpty(Hotkeys.EdgeCapture.Key) || Hotkeys.EdgeCapture.Key == "X"))
                {
                    Hotkeys.EdgeCapture = defaults.EdgeCapture; // Ctrl+Shift+E
                    changed = true;
                }
                
                if (Hotkeys.ElementCapture != null && Hotkeys.ElementCapture.Key == "E")
                {
                    Hotkeys.ElementCapture = defaults.ElementCapture; // Ctrl+Shift+U
                    changed = true;
                }
            }

            // 2. Sync Main Menu Items (Add missing, don't reorder)
            var defaultMainMenu = new List<string>
            {
                "AreaCapture", "EdgeCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord"
            };
            if (MainMenuItems == null) { MainMenuItems = new List<string>(defaultMainMenu); changed = true; }

            // 3. Sync Tray Mode Icons
            var defaultTrayIcons = new List<string>
            {
                "AreaCapture", "DelayCapture", "FullScreen", 
                "DesignatedCapture", "WindowCapture", "UnitCapture", "History", "MyNote"
            };
            if (TrayModeIcons == null) { TrayModeIcons = new List<string>(defaultTrayIcons); changed = true; }

            // 4. Sync Simple Mode Icons
            var defaultSimpleIcons = new List<string>
            {
                "AreaCapture", "DelayCapture", "FullScreen", "DesignatedCapture", "History", "MyNote"
            };
            if (SimpleModeIcons == null) { SimpleModeIcons = new List<string>(defaultSimpleIcons); changed = true; }

            // 4. Ensure internal objects are not null
            if (Recording == null) { Recording = new RecordingSettings(); changed = true; }

            // [추가] 엣지 색상 기본값 동기화
            if (string.IsNullOrEmpty(EdgeLineColor))
            {
                EdgeLineColor = "#FF0000";
                changed = true;
            }

            return changed;
        }

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
            if (_instance != null) return _instance;
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
                // Safe Sync: only add missing parts, don't overwrite user choices
                if (settings.SyncDefaults())
                {
                    try { Save(settings); } catch { } 
                }
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
                _instance = settings;
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
                _instance = settings;
                // ★ 저장 성공 시 모든 창에 알림
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public Settings Clone()
        {
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
            string json = JsonSerializer.Serialize(this, options);
            return JsonSerializer.Deserialize<Settings>(json, options) ?? new Settings();
        }

        public static bool TrySave(Settings settings, out string? error)
        {
            error = null;
            try
            {
                string settingsPath = GetSettingsPath();
                var dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Sanitize values before saving (NaN, Infinity check)
                settings.Sanitize();

                // Serialize
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
                });

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

        /// <summary>
        /// Ensures all double values are finite (not NaN or Infinity) to prevent JSON serialization errors.
        /// </summary>
        public void Sanitize()
        {
            LastMainLeft = EnsureFinite(LastMainLeft, 0);
            LastMainTop = EnsureFinite(LastMainTop, 0);
            LastSimpleLeft = EnsureFinite(LastSimpleLeft, 0);
            LastSimpleTop = EnsureFinite(LastSimpleTop, 0);
            LastTrayLeft = EnsureFinite(LastTrayLeft, 0);
            LastTrayTop = EnsureFinite(LastTrayTop, 0);
            
            NoteExplorerWidth = EnsureFinite(NoteExplorerWidth, 1450);
            NoteExplorerHeight = EnsureFinite(NoteExplorerHeight, 995);
            NoteExplorerLeft = EnsureFinite(NoteExplorerLeft, -9999);
            NoteExplorerTop = EnsureFinite(NoteExplorerTop, -9999);
            NoteExplorerSplitterPosition = EnsureFinite(NoteExplorerSplitterPosition, -9999);

            NoteViewerWidth = EnsureFinite(NoteViewerWidth, 1200);
            NoteViewerHeight = EnsureFinite(NoteViewerHeight, 920);
            NoteViewerLeft = EnsureFinite(NoteViewerLeft, -9999);
            NoteViewerTop = EnsureFinite(NoteViewerTop, -9999);

            NoteInputWidth = EnsureFinite(NoteInputWidth, 1200);
            NoteInputHeight = EnsureFinite(NoteInputHeight, 920);
            NoteInputLeft = EnsureFinite(NoteInputLeft, -9999);
            NoteInputTop = EnsureFinite(NoteInputTop, -9999);

            HistoryWindowWidth = EnsureFinite(HistoryWindowWidth, 1430);
            HistoryWindowHeight = EnsureFinite(HistoryWindowHeight, 820);
            HistoryWindowLeft = EnsureFinite(HistoryWindowLeft, -9999);
            HistoryWindowTop = EnsureFinite(HistoryWindowTop, -9999);
            HistoryPreviewPaneWidth = EnsureFinite(HistoryPreviewPaneWidth, 331);
            HistoryListPaneWidth = EnsureFinite(HistoryListPaneWidth, 875);

            PreviewWindowWidth = EnsureFinite(PreviewWindowWidth, 0);
            PreviewWindowHeight = EnsureFinite(PreviewWindowHeight, 0);
            PreviewWindowLeft = EnsureFinite(PreviewWindowLeft, -9999);
            PreviewWindowTop = EnsureFinite(PreviewWindowTop, -9999);

            HistoryColDate = EnsureFinite(HistoryColDate, 132);
            HistoryColFileName = EnsureFinite(HistoryColFileName, 205);
            HistoryColMeta = EnsureFinite(HistoryColMeta, 205);
            HistoryColPin = EnsureFinite(HistoryColPin, 53);
            HistoryColFavorite = EnsureFinite(HistoryColFavorite, 53);
            HistoryColActions = EnsureFinite(HistoryColActions, 53);
            HistoryColMemo = EnsureFinite(HistoryColMemo, 150);

            CaptureLineThickness = EnsureFinite(CaptureLineThickness, 1.0);

            // [추가] 엣지 설정 소독
            EdgeBorderThickness = EnsureFinite(EdgeBorderThickness, 0);
            EdgeCornerRadius = EnsureFinite(EdgeCornerRadius, 0);
            EdgeShadowBlur = EnsureFinite(EdgeShadowBlur, 15);
            EdgeShadowDepth = EnsureFinite(EdgeShadowDepth, 5);
            EdgeShadowOpacity = EnsureFinite(EdgeShadowOpacity, 0.4);

            // [추가] 엣지 프리셋 레벨 (1~5) 범위 제한
            if (EdgeCapturePresetLevel < 1) EdgeCapturePresetLevel = 3; // Default to Standard if invalid
            if (EdgeCapturePresetLevel > 5) EdgeCapturePresetLevel = 5;

            if (Recording != null)
            {
                Recording.LastAreaLeft = EnsureFinite(Recording.LastAreaLeft, 100);
                Recording.LastAreaTop = EnsureFinite(Recording.LastAreaTop, 100);
                Recording.LastAreaWidth = EnsureFinite(Recording.LastAreaWidth, 800);
                Recording.LastAreaHeight = EnsureFinite(Recording.LastAreaHeight, 600);
            }
        }

        private double EnsureFinite(double value, double defaultValue)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return defaultValue;
            return value;
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
        public ToggleHotkey EdgeCapture { get; set; } = new ToggleHotkey();          // 엣지 캡처
        
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
                ElementCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "U" }, // E -> U (Edge와 충돌 방지)
                ScrollCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "S" },

                OcrCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "O" },
                ScreenRecord = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "V" },
                EdgeCapture = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "E" }, // X -> E

                // 편집/기타 기능: 활성화
                SaveAll = new ToggleHotkey { Enabled = true, Ctrl = true, Shift = true, Key = "A" },
                DeleteAll = new ToggleHotkey { Enabled = true, Ctrl = true, Key = "D" }, // Ctrl+Shift+D -> Ctrl+D
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

        /// <summary>
        /// Populates missing hotkeys without overwriting existing user settings.
        /// Returns true if any changes were made.
        /// </summary>
        public bool SyncDefaults()
        {
            var defaults = CreateDefaults();
            bool changed = false;

            // Helper to sync only if the key is empty or property is null
            ToggleHotkey SafeSync(ToggleHotkey current, ToggleHotkey @default)
            {
                if (current == null || (string.IsNullOrEmpty(current.Key) && !current.Enabled))
                {
                    changed = true;
                    return @default;
                }
                return current;
            }

            RegionCapture = SafeSync(RegionCapture, defaults.RegionCapture);
            DelayCapture = SafeSync(DelayCapture, defaults.DelayCapture);
            RealTimeCapture = SafeSync(RealTimeCapture, defaults.RealTimeCapture);
            MultiCapture = SafeSync(MultiCapture, defaults.MultiCapture);
            FullScreen = SafeSync(FullScreen, defaults.FullScreen);
            DesignatedCapture = SafeSync(DesignatedCapture, defaults.DesignatedCapture);
            WindowCapture = SafeSync(WindowCapture, defaults.WindowCapture);
            ElementCapture = SafeSync(ElementCapture, defaults.ElementCapture);
            ScrollCapture = SafeSync(ScrollCapture, defaults.ScrollCapture);
            OcrCapture = SafeSync(OcrCapture, defaults.OcrCapture);
            ScreenRecord = SafeSync(ScreenRecord, defaults.ScreenRecord);
            EdgeCapture = SafeSync(EdgeCapture, defaults.EdgeCapture);
            SaveAll = SafeSync(SaveAll, defaults.SaveAll);
            DeleteAll = SafeSync(DeleteAll, defaults.DeleteAll);
            SimpleMode = SafeSync(SimpleMode, defaults.SimpleMode);
            TrayMode = SafeSync(TrayMode, defaults.TrayMode);
            OpenSettings = SafeSync(OpenSettings, defaults.OpenSettings);
            OpenEditor = SafeSync(OpenEditor, defaults.OpenEditor);
            OpenNote = SafeSync(OpenNote, defaults.OpenNote);
            RecordingStartStop = SafeSync(RecordingStartStop, defaults.RecordingStartStop);

            return changed;
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