using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using CatchCapture.Models;
using CatchCapture.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using CatchCapture.Resources;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;
using CatchCapture.Recording;
using System.Diagnostics;
using IOPath = System.IO.Path;

namespace CatchCapture;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<CaptureImage> captures = new List<CaptureImage>();
    private int selectedIndex = -1;
    private Border? selectedBorder = null;
    public Settings settings;
    internal SimpleModeWindow? simpleModeWindow = null;
    private Point lastPosition;
    private int captureDelaySeconds = 0;
    private Recording.RecordingWindow? activeRecordingWindow = null; // Track active recording window
    private Utilities.SnippingWindow? _activeSnippingWindow = null; // Track active snipping window
    
    public enum CaptureViewMode
    {
        List,
        Card
    }
    private CaptureViewMode currentViewMode = CaptureViewMode.List;


    // ★ 메모리 최적화: 스크린샷 캐시를 WeakReference로 변경 (메모리 압박 시 자동 해제)
    private WeakReference<BitmapSource>? cachedScreenshotRef = null;
    private DateTime lastScreenshotTime = DateTime.MinValue;
    private readonly TimeSpan screenshotCacheTimeout = TimeSpan.FromSeconds(3); // 3초로 늘림

    private System.Windows.Threading.DispatcherTimer? tipTimer;
    private List<string> tips = new List<string>();
    private int currentTipIndex = 0;

    // 트레이 아이콘
    public System.Windows.Forms.NotifyIcon? notifyIcon;
    internal bool isExit = false;
    internal TrayModeWindow? trayModeWindow;
    internal bool _wasSimpleModeVisibleBeforeRecapture = false;
    // 캡처 직후 자동으로 열린 미리보기 창 수 (메인창 숨김/복원 관리)
    private int _autoPreviewOpenCount = 0;
    private Point _dragStartPoint;
    private bool _isReadyToDrag = false;
    // 트레이 컨텍스트 메뉴 및 항목 참조 (언어 변경 시 즉시 갱신용)
    private System.Windows.Forms.ContextMenuStrip? trayContextMenu;
    private System.Windows.Forms.ToolStripMenuItem? trayOpenItem;
    private System.Windows.Forms.ToolStripMenuItem? trayAreaItem;
    private System.Windows.Forms.ToolStripMenuItem? trayEdgeItem;
    private System.Windows.Forms.ToolStripMenuItem? traySettingsItem;
    private System.Windows.Forms.ToolStripMenuItem? trayExitItem;

        // ★ 메모리 최적화: 프리로딩을 WeakReference로 변경
    private WeakReference<BitmapSource>? preloadedScreenshotRef;
    private DateTime preloadedTime;
    private bool isPreloading = false;

    // 글로벌 단축키 관련
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_AREA = 9000;
    private const int HOTKEY_ID_FULLSCREEN = 9001;
    private const int HOTKEY_ID_WINDOW = 9002;
    private const int HOTKEY_ID_OCR = 9010;
    private const int HOTKEY_ID_SCREENRECORD = 9011;
    private const int HOTKEY_ID_SIMPLE = 9012;
    private const int HOTKEY_ID_TRAY = 9013;
    private const int HOTKEY_ID_OPENEDITOR = 9014;
    private const int HOTKEY_ID_OPENNOTE = 9015;
    private const int HOTKEY_ID_EDGECAPTURE = 9016;
    private const int HOTKEY_ID_DELAY = 9017;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? hwndSource;
    // Ensures any pending composition updates are presented (so hidden window is actually off-screen)
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern short GetKeyState(int keyCode);

    // ★ Low-Level Keyboard Hook (Print Screen 키 전역 후킹용)
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_SNAPSHOT = 0x2C; // Print Screen 키

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _keyboardHookId = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private void FlushUIAfterHide()
    {
        try
        {
            // 최소한의 UI 처리만 수행 (ApplicationIdle 대신 Normal 우선순위 사용)
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Normal);
            // DwmFlush는 선택적으로만 수행
            // DwmFlush(); // 제거하여 딜레이 최소화
        }
        catch
        {
            // Ignore flush errors; proceed to capture
        }
    }
    public MainWindow(bool isAutoStart = false)
    {
        InitializeComponent();
        LocalizationManager.LanguageChanged += MainWindow_LanguageChanged;
        settings = Settings.Load();
        captureDelaySeconds = settings.DelayCaptureSeconds;

        Settings.SettingsChanged += OnSettingsChanged;

        // Print Screen 키 감지
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;

        // 트레이 아이콘 초기화
        InitializeNotifyIcon();

        // 글로벌 단축키 등록
        RegisterGlobalHotkeys();

        // ★ Print Screen 키 전역 후킹 설치
        InstallKeyboardHook();

        // 로컬 단축키 등록
        AddKeyboardShortcuts();

        // 타이틀바 드래그 이벤트 설정
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;

        // 간편모드 활성 중에는 작업표시줄 클릭으로 본체가 튀어나오지 않도록 제어
        this.StateChanged += MainWindow_StateChanged;
        this.Activated += MainWindow_Activated;

        // 백그라운드 스크린샷 캐시 시스템 초기화
        InitializeScreenshotCache();
        UpdateOpenEditorToggleUI();

        // [Cross-computer Registry] Ownership Loss Handler - 제거됨 (ReadOnly 모드로 대체)
        // DatabaseManager.Instance.OwnershipLost += OnDatabaseOwnershipLost;

        this.Loaded += (s, e) =>
        {
            var helper = new WindowInteropHelper(this);
            hwndSource = HwndSource.FromHwnd(helper.Handle);
            hwndSource?.AddHook(HwndHook);
            RegisterGlobalHotkeys();
            UpdateInstantEditToggleUI();
            UpdateOpenEditorToggleUI(); // 편집열기 토글 UI 초기화
            UpdateMenuButtonOrder();
            InitializeTips();

            // 다국어 UI 텍스트 적용
            UpdateUIText();

            // 엣지 캡처 반경 이모지 초기화
            UpdateEdgeRadiusEmoji();

            // 지연 캡처 UI 초기화
            UpdateDelayCaptureUI();

            // 언어 변경 즉시 반영
            LocalizationManager.LanguageChanged += MainWindow_LanguageChanged;

            // ★★★ 자동시작인 경우 트레이 아이콘으로만 시작 ★★★
            if (isAutoStart)
            {
                // 자동시작: 창을 표시하지 않고 트레이 아이콘만 활성화
                this.Hide();
                this.ShowInTaskbar = false;
            }
            else
            {
                // 일반 실행: 시작 모드에 따라 초기 모드 설정
                if (settings.StartupMode == "Tray")
                {
                    SwitchToTrayMode();
                }
                else if (settings.StartupMode == "Simple")
                {
                    SwitchToSimpleMode();
                }
                else
                {
                    // Normal 모드: 상태 초기화 및 창 활성화
                    SwitchToNormalMode();
                    
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    this.Topmost = true;
                    this.Topmost = false;
                }
            }
            UpdateEmptyStateLogo();
            // 저장된 보기 모드 복원
            if (settings.MainCaptureViewMode == 1)
            {
                currentViewMode = CaptureViewMode.Card;
                this.Width = 850;
                CaptureListPanel.HorizontalAlignment = HorizontalAlignment.Left;
                if (EmptyStateActionPanel != null) EmptyStateActionPanel.Orientation = Orientation.Horizontal;
            }
            else
            {
                currentViewMode = CaptureViewMode.List;
                this.Width = 430;
                CaptureListPanel.HorizontalAlignment = HorizontalAlignment.Center;
                if (EmptyStateActionPanel != null) EmptyStateActionPanel.Orientation = Orientation.Vertical;
            }
            CaptureListPanel.ItemWidth = 210;
            UpdateViewModeUI();

            // 초기 UI 텍스트 설정
            ScreenRecordButtonText.Text = LocalizationManager.GetString("ScreenRecording");

            AdjustMainWindowHeightForMenuCount();

            // History & Trash Cleanup
            try
            {
                if (settings.TrashRetentionDays > 0)
                {
                    DatabaseManager.Instance.CleanupTrash(settings.TrashRetentionDays);
                }
                DatabaseManager.Instance.CleanupHistory(settings.HistoryRetentionDays, settings.HistoryTrashRetentionDays);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error: {ex.Message}");
            }



            // [Cross-computer Lock Warning] Removed for faster startup
            // 조용히 시작 (ReadOnly 모드 자동 적용)
        };
    }

    private async void BtnTakeOwnershipBack_Click(object sender, RoutedEventArgs e)
    {
        await PerformSafeTakeoverAsync();
    }

    private async Task PerformSafeTakeoverAsync()
    {
        await Task.Yield(); // 비동기 컨텍스트 유지
        try
        {
            // 단순 권한 획득 시도 (로컬 리로드 동반)
            if (DatabaseManager.Instance.TakeOwnership(forceReload: true))
            {
                LockOverlay.Visibility = Visibility.Collapsed;
                TakeoverProgressOverlay.Visibility = Visibility.Collapsed;
                // 성공 알림
                CustomMessageBox.Show(LocalizationManager.GetString("OwnershipTakenMessage") ?? "편집 권한을 가져왔습니다.\n(최신 데이터를 불러왔습니다.)", LocalizationManager.GetString("Success") ?? "성공", MessageBoxButton.OK, MessageBoxImage.Information, width: 400);
            }
            else
            {
                CustomMessageBox.Show(LocalizationManager.GetString("ErrOwnershipTakeover") ?? "권한 획득에 실패했습니다. 다른 컴퓨터가 사용 중일 수 있습니다.", LocalizationManager.GetString("Failure") ?? "실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Takeover Error: {ex.Message}");
        }
    }

    private void BtnLockSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsPage("Note");
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            settings = Settings.Load();
            // 필요한 UI 업데이트
            UpdateInstantEditToggleUI();
            UpdateOpenEditorToggleUI();
            UpdateMenuButtonOrder();
            AdjustMainWindowHeightForMenuCount();

            // [추가] 단축키 및 트레이 메뉴 갱신 (언어 등 변경 대응)
            RegisterGlobalHotkeys();
            RebuildTrayMenu();
            UpdateUIText();
        });
    }

    private void UpdateMenuButtonOrder()
    {
        if (CaptureButtonsPanel == null || settings.MainMenuItems == null) return;

        // 캡처 버튼들의 맵핑 (UIElement로 변경하여 Grid도 처리 가능)
        var buttonMap = new Dictionary<string, UIElement>
        {
            { "AreaCapture", AreaCaptureButton },
            { "DelayCapture", DelayCaptureGrid },
            { "RealTimeCapture", RealTimeCaptureButton },
            { "MultiCapture", MultiCaptureButton },
            { "FullScreen", FullScreenCaptureButton },
            { "DesignatedCapture", DesignatedCaptureButton },
            { "WindowCapture", WindowCaptureButton },
            { "ElementCapture", ElementCaptureButton },
            { "ScrollCapture", ScrollCaptureButton },
            { "OcrCapture", OcrCaptureButton },
            { "ScreenRecord", ScreenRecordButton },
            { "EdgeCapture", EdgeCaptureGrid }  // Grid로 변경
        };

        // Separator와 하단 버튼들 저장
        var bottomElements = new List<UIElement>();
        bool foundSeparator = false;
        foreach (UIElement child in CaptureButtonsPanel.Children)
        {
            if (child is Separator)
            {
                foundSeparator = true;
            }
            if (foundSeparator)
            {
                // Skip SimpleModeButton and TrayModeButton as they are now managed by menu items
                if (child == SimpleModeButton || child == TrayModeButton) continue;
                bottomElements.Add(child);
            }
        }

        // 패널 초기화
        CaptureButtonsPanel.Children.Clear();

        // 설정에 따라 버튼 순서대로 추가
        foreach (var key in settings.MainMenuItems)
        {
            if (buttonMap.TryGetValue(key, out var element))
            {
                CaptureButtonsPanel.Children.Add(element);
            }
        }

        // Separator와 하단 버튼들 다시 추가
        foreach (var element in bottomElements)
        {
            CaptureButtonsPanel.Children.Add(element);
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Print Screen 키 감지
        if (e.Key == Key.PrintScreen || e.Key == Key.Snapshot)
        {
            if (settings.UsePrintScreenKey)
            {
                e.Handled = true; // 기본 동작 방지

                // 설정된 액션 실행
                switch (settings.PrintScreenAction)
                {
                    case "영역 캡처":
                        AreaCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "지연 캡처":
                        StartDelayedAreaCaptureSeconds(3);
                        break;
                    case "실시간 캡처":
                        StartRealTimeCaptureMode();
                        break;
                    case "다중 캡처":
                        MultiCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "전체화면":
                        FullScreenCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "지정 캡처":
                        DesignatedCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "창 캡처":
                        WindowCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "단위 캡처":
                        ElementCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "스크롤 캡처":
                        ScrollCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "OCR 캡처":
                        OcrCaptureButton_Click(this, new RoutedEventArgs());
                        break;
                    case "동영상 녹화":
                        ScreenRecordButton_Click(this, new RoutedEventArgs());
                        break;
                }
            }
        }
    }

    private void OpenSettingsPage(string page)
    {
        var sw = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (sw == null)
        {
            sw = new SettingsWindow();
            sw.Show();
        }
        else
        {
            sw.Activate();
            if (sw.WindowState == WindowState.Minimized) sw.WindowState = WindowState.Normal;
        }
        
        sw.SelectPage(page);
    }

    private void InitializeNotifyIcon()
    {
        notifyIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        notifyIcon.Visible = true;
        notifyIcon.Text = LocalizationManager.GetString("AppName");

        notifyIcon.Click += (s, e) =>
        {
            if (e is System.Windows.Forms.MouseEventArgs me && me.Button == System.Windows.Forms.MouseButtons.Left)
            {
                RestoreLastMode();
            }
        };

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        trayContextMenu = new System.Windows.Forms.ContextMenuStrip();
        trayContextMenu.ShowImageMargin = true;
        trayContextMenu.ShowCheckMargin = false; 
        trayContextMenu.DropShadowEnabled = false; // 시스템 그림자로 인한 간격 벌어짐 방지
        trayContextMenu.Renderer = new DarkToolStripRenderer();
        trayContextMenu.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        trayContextMenu.ForeColor = System.Drawing.Color.White;
        trayContextMenu.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular);
        trayContextMenu.ImageScalingSize = new System.Drawing.Size(16, 16);
        
        // 2. 컨텍스트박스 자동 크기 조절 (최소 크기 제거)
        // trayContextMenu.MinimumSize = new System.Drawing.Size(180, 0);

        // [1] Open
        trayOpenItem = new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("Open"),
            LoadMenuImage("catcha.png"),
            (s, e) => ShowMainWindow());
        trayContextMenu.Items.Add(trayOpenItem);

        // [2] Open Note
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("OpenMyNote"), 
            LoadMenuImage("my_note.png"), 
            (s, e) => OpenNoteExplorer()));

        // [3] Open History (아이콘 변경: histroy_note.png)
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("OpenHistory") ?? "히스토리 열기", 
            LoadMenuImage("histroy_note.png"), 
            (s, e) => {
                var hw = Application.Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
                if (hw == null) { hw = new HistoryWindow(); hw.Show(); }
                else { hw.Activate(); if (hw.WindowState == WindowState.Minimized) hw.WindowState = WindowState.Normal; }
            }));

        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // [4] Area Capture
        trayAreaItem = new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("AreaCapture"),
            LoadMenuImage("area_capture.png"),
            (s, e) => StartAreaCapture(false));
        trayContextMenu.Items.Add(trayAreaItem);

        // [5] Edge Capture
        trayEdgeItem = new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("EdgeCapture") ?? "엣지 캡처",
            LoadMenuImage("edge_capture.png"));
        var edgeItems = new (string Key, int Radius, string Emoji)[]
        {
            ("EdgeSoft", 12, "🫧"),
            ("EdgeSmooth", 25, "📱"),
            ("EdgeClassic", 50, "🍪"),
            ("EdgeCapsule", 100, "💊"),
            ("EdgeCircle", 999, "🌕")
        };
        foreach (var t in edgeItems)
        {
            var subItem = new System.Windows.Forms.ToolStripMenuItem($"{t.Emoji} {LocalizationManager.GetString(t.Key)}");
            subItem.Click += async (s, e) => {
                settings.EdgeCaptureRadius = t.Radius;
                Settings.Save(settings);
                await StartAreaCaptureAsync(t.Radius, false);
            };
            trayEdgeItem.DropDownItems.Add(subItem);
        }
        if (trayEdgeItem.DropDown is System.Windows.Forms.ToolStripDropDownMenu menu)
        {
            // ApplyRecursiveStyle에서 처리되지만, EdgeCapture만 예외적으로 ImageMargin 제거
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.DropShadowEnabled = false;
        }
        trayContextMenu.Items.Add(trayEdgeItem);

        // [6] Capture Submenu "캡처 >"
        var captureSub = new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("CaptureAnd") ?? "캡처", LoadMenuImage("camera.png"));
        
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("DelayCapture"), LoadMenuImage("clock.png"), (s, e) => StartDelayedAreaCaptureSeconds(3, false)));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("RealTimeCapture"), LoadMenuImage("real-time.png"), (s, e) => StartRealTimeCaptureMode(false)));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("MultiCapture"), LoadMenuImage("multi_capture.png"), (s, e) => MultiCaptureButton_Click(this, new RoutedEventArgs()))); 
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("FullScreen"), LoadMenuImage("full_screen.png"), (s, e) => CaptureFullScreen(false)));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("DesignatedCapture"), LoadMenuImage("designated.png"), (s, e) => DesignatedCaptureButton_Click(this, new RoutedEventArgs())));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("WindowCapture"), LoadMenuImage("window_cap.png"), (s, e) => WindowCaptureButton_Click(this, new RoutedEventArgs())));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("UnitCapture"), LoadMenuImage("unit_capture.png"), (s, e) => ElementCaptureButton_Click(this, new RoutedEventArgs())));
        captureSub.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("ScrollCapture"), LoadMenuImage("scroll_capture.png"), (s, e) => ScrollCaptureButton_Click(this, new RoutedEventArgs())));
        
        trayContextMenu.Items.Add(captureSub);

        // [7] OCR Capture
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("OcrCapture"), 
            LoadMenuImage("ocr_capture.png"), 
            (s, e) => OcrCaptureButton_Click(this, new RoutedEventArgs())));

        // [8] Screen Record
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(
            LocalizationManager.GetString("ScreenRecording"), 
            LoadMenuImage("videocamera.png"), 
            (s, e) => ScreenRecordButton_Click(this, new RoutedEventArgs())));

        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // [9] Capture Folder
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("OpenCaptureFolder") ?? "캡처 폴더", LoadMenuImage("openfolder.png"), (s, e) => {
             try { 
                 var path = GetAutoSaveFilePath(settings.FileSaveFormat.ToLower(), null, null);
                 var dir = System.IO.Path.GetDirectoryName(path);
                 if(dir != null) {
                     if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                     System.Diagnostics.Process.Start("explorer.exe", dir);
                 }
             } catch {}
        }));
        
        // [10] Note Folder
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("OpenNoteFolder") ?? "노트 폴더", LoadMenuImage("openfolder.png"), (s, e) => {
             try {
                 string notePath = settings.NoteStoragePath;
                 if (string.IsNullOrEmpty(notePath))
                 {
                     notePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "CatchCapture", "notedata");
                 }
                 else
                 {
                     notePath = Utilities.DatabaseManager.EnsureCatchCaptureSubFolder(notePath);
                     notePath = Utilities.DatabaseManager.ResolveStoragePath(notePath);
                 }
                 if (!System.IO.Directory.Exists(notePath)) System.IO.Directory.CreateDirectory(notePath);
                 System.Diagnostics.Process.Start("explorer.exe", notePath);
             } catch {}
        }));

        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // [11] Settings Submenu
        traySettingsItem = new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("Settings"), LoadMenuImage("setting.png"));
        traySettingsItem.Click += (s, e) => OpenSettingsPage("System");

        void AddSetting(string nameKey, string pageTag, string defaultName) {
            traySettingsItem.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem(
                LocalizationManager.GetString(nameKey) ?? defaultName, 
                LoadMenuImage("setting.png"), 
                (s, e) => OpenSettingsPage(pageTag)));
        }

        AddSetting("SystemSettings", "System", "프로그램 설정");
        AddSetting("CaptureSettings", "Capture", "작업 설정");
        AddSetting("HotkeySettings", "Hotkey", "단축키 설정");
        AddSetting("ScreenRecording", "Recording", "녹화 설정");
        AddSetting("NoteSettings", "Note", "노트 설정");
        AddSetting("HistorySettings", "History", "히스토리 설정");
        AddSetting("ThemeSettings", "Theme", "테마 설정");
        AddSetting("MenuEdit", "MenuEdit", "메뉴 편집");

        trayContextMenu.Items.Add(traySettingsItem);
        
        // [12] Exit
        trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        trayExitItem = new System.Windows.Forms.ToolStripMenuItem(LocalizationManager.GetString("Exit"), LoadMenuImage("exit_tray.png"), (s, e) =>
        {
            isExit = true;
            Close();
        });
        trayContextMenu.Items.Add(trayExitItem);

        if (notifyIcon != null) notifyIcon.ContextMenuStrip = trayContextMenu;
        
        // [레이아웃 보정] 모든 메뉴 항목의 너비를 180px로 강제 설정
        // 재귀적으로 스타일 적용
        ApplyRecursiveStyle(trayContextMenu.Items);
    }

    private void ApplyRecursiveStyle(System.Windows.Forms.ToolStripItemCollection items)
    {
        foreach (System.Windows.Forms.ToolStripItem item in items)
        {
            if (item is System.Windows.Forms.ToolStripMenuItem mi)
            {
                mi.AutoSize = true;
                mi.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                
                // 하위 메뉴가 있는 경우 화살표 공간 확보
                if (mi.DropDownItems.Count > 0)
                {
                    mi.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2); 
                    ApplyRecursiveStyle(mi.DropDownItems);
                    
                    if (mi.DropDown is System.Windows.Forms.ToolStripDropDownMenu dropdown)
                    {
                        dropdown.ShowCheckMargin = false;
                        dropdown.ImageScalingSize = new System.Drawing.Size(16, 16);
                        dropdown.DropShadowEnabled = false; 
                    }
                }
                else
                {
                    mi.Padding = new System.Windows.Forms.Padding(0, 1, 6, 1); // Leaf
                }
            }
        }
    }

    // 다크 테마 렌더러/컬러 테이블로 컨텍스트 메뉴 스타일링
    private sealed class DarkToolStripRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }
        protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item == null) return;
            
            // 시각적 균형을 위해 텍스트 영역을 아이콘 쪽으로 6픽셀 당김 (상하 오프셋은 0으로 조정)
            var textRect = e.TextRectangle;
            textRect.Offset(-6, 0);
            
            System.Windows.Forms.TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, textRect, System.Drawing.Color.White, 
                System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.NoPrefix);
        }
        protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            var item = e.Item;
            if (item == null) return;

            // Use the system-calculated arrow rectangle for correct positioning with AutoSize
            int arrowSize = 4;
            var rect = e.ArrowRectangle;
            
            // Center the arrow within the arrow rectangle
            int x = rect.Left + (rect.Width - arrowSize) / 2;
            int y = rect.Top + (rect.Height - arrowSize * 2) / 2;

            using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                System.Drawing.Point[] pts = {
                    new System.Drawing.Point(x, y),
                    new System.Drawing.Point(x + arrowSize, y + arrowSize),
                    new System.Drawing.Point(x, y + arrowSize * 2)
                };
                g.FillPolygon(brush, pts);
            }
        }
    }

    private sealed class DarkColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(45, 45, 48);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(104, 104, 104);
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(63, 63, 70);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(63, 63, 70);
        public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(63, 63, 70);
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(63, 63, 70);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(63, 63, 70);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(80, 80, 80);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(80, 80, 80);
    }

    // 아이콘 로더 (icons 폴더에서 PNG 로드, 없으면 null)
    private System.Drawing.Image? LoadMenuImage(string fileName)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = System.IO.Path.Combine(baseDir, "icons", fileName);
            if (!System.IO.File.Exists(path)) return null;
            // load without locking the file
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            using (var original = System.Drawing.Image.FromStream(fs))
            {
                return ResizeImage(original, 16, 16);
            }
        }
        catch { return null; }
    }

    private static System.Drawing.Image ResizeImage(System.Drawing.Image img, int width, int height)
    {
        var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.DrawImage(img, new System.Drawing.Rectangle(0, 0, width, height));
        }
        return bmp;
    }

    public void ActivateWindow()
    {
        // 최신 설정 로드
        settings = Settings.Load();
        var lastMode = settings.LastActiveMode ?? "Normal";

        switch (lastMode)
        {
            case "Simple":
                if (simpleModeWindow == null || !simpleModeWindow.IsVisible)
                {
                    SwitchToSimpleMode();
                }
                else
                {
                    simpleModeWindow.Show();
                    simpleModeWindow.Activate();
                    simpleModeWindow.Topmost = true;
                }
                break;

            case "Tray":
                if (trayModeWindow == null)
                {
                    ShowTrayModeWindow();
                }
                else
                {
                    trayModeWindow.Show();
                    trayModeWindow.Activate();
                    trayModeWindow.Topmost = true;
                }
                break;

            case "Normal":
            default:
                if (!this.IsVisible)
                {
                    this.Show();
                }
                this.WindowState = WindowState.Normal;
                this.Activate();
                this.Topmost = true;
                this.Topmost = false;
                break;
        }
    }

    private void ShowMainWindow()
    {
        // 마지막 활성 모드 복원
        var lastMode = settings.LastActiveMode ?? "Normal";

        switch (lastMode)
        {
            case "Simple":
                // 간편모드 복원
                if (simpleModeWindow != null && !simpleModeWindow.IsVisible)
                {
                    simpleModeWindow.Show();
                    simpleModeWindow.Activate();
                    simpleModeWindow.Topmost = true;
                }
                else if (simpleModeWindow == null)
                {
                    SwitchToSimpleMode();
                }
                break;

            case "Tray":
                // 트레이모드에서는 트레이모드 창을 토글하거나 표시
                if (trayModeWindow == null)
                {
                    ShowTrayModeWindow();
                }
                else
                {
                    trayModeWindow.Show();
                    trayModeWindow.Activate();
                }
                break;

            case "Normal":
            default:
                // 일반모드 복원
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                break;
        }
    }

    private void ToggleTrayModeWindow()
    {
        if (trayModeWindow == null || !trayModeWindow.IsVisible)
        {
            ShowTrayModeWindow();
        }
        else if (!trayModeWindow.IsVisible)
        {
            // 창이 존재하지만 숨겨진 경우: 설정을 다시 로드하고 Show 호출
            trayModeWindow.ReloadSettings();
            trayModeWindow.Show();
        }
        else
        {
            trayModeWindow.Hide();
        }
    }


    private void RestoreLastMode()
    {
        var lastMode = settings.LastActiveMode ?? "Normal";

        switch (lastMode)
        {
            case "Simple":
                // ★ 간편모드가 숨겨져 있으면 다시 표시
                if (simpleModeWindow != null && !simpleModeWindow.IsVisible)
                {
                    simpleModeWindow.Show();
                    simpleModeWindow.Activate();
                    simpleModeWindow.Topmost = true;
                }
                else if (simpleModeWindow != null && simpleModeWindow.IsVisible)
                {
                    // 이미 보이면 숨기기
                    simpleModeWindow.Hide();
                }
                else
                {
                    // 간편모드 창이 없으면 새로 생성
                    SwitchToSimpleMode();
                }
                break;

            case "Tray":
                // ★ 간편모드가 열려있으면 트레이모드 열지 않음
                if (simpleModeWindow != null && simpleModeWindow.IsVisible)
                {
                    return;
                }
                ToggleTrayModeWindow();
                break;

            case "Normal":
            default:
                if (this.IsVisible)
                {
                    this.Hide();
                }
                else
                {
                    SwitchToNormalMode();
                }
                break;
        }
    }

    public void SwitchToTrayMode()
    {
        // ★ 트레이모드 상태 저장
        settings.LastActiveMode = "Tray";
        Settings.Save(settings);

        // SimpleModeWindow가 열려 있다면 닫기
        if (simpleModeWindow != null)
        {
            // 중요: SimpleModeWindow를 닫기 전에 MainWindow를 Application.MainWindow로 복원
            Application.Current.MainWindow = this;
            simpleModeWindow.Close();
            simpleModeWindow = null;
        }

        // MainWindow를 먼저 숨기기
        this.Hide();

        ShowTrayModeWindow();
    }

    private void ShowTrayModeWindow()
    {
        try
        {
            // 트레이 모드 설정
            settings.IsTrayMode = true;

            // 트레이 모드 창 생성 또는 표시
            if (trayModeWindow == null)
            {
                trayModeWindow = new TrayModeWindow(this);
                trayModeWindow.Show();
            }
            else
            {
                // 기존 창이 있으면 설정을 다시 로드하고 표시
                trayModeWindow.ReloadSettings();
                if (!trayModeWindow.IsVisible)
                {
                    trayModeWindow.Show();
                }
            }

            // 현재 캡처 개수 업데이트
            trayModeWindow.UpdateCaptureCount(captures.Count);

            trayModeWindow.Activate();
            trayModeWindow.Topmost = true;

            // 설정 저장
            Settings.Save(settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"TrayModeWindow 오류:\n{ex.Message}\n\n임시로 MainWindow를 표시합니다.");

            // 오류 발생 시 MainWindow 다시 표시
            this.Show();
            this.Activate();
        }
    }

    private void PositionTrayModeWindow()
    {
        // 화면 작업 영역 가져오기 (태스크바 제외)
        var workArea = SystemParameters.WorkArea;

        // 우측 하단에 위치 (트레이 근처)
        this.Left = workArea.Right - this.Width - 10;
        this.Top = workArea.Bottom - this.Height - 10;

        // 위치 저장
        settings.LastTrayLeft = this.Left;
        settings.LastTrayTop = this.Top;
    }

    public void SwitchToNormalMode()
    {
        // ★ 일반모드 상태 저장
        settings.LastActiveMode = "Normal";

        // 간편 모드가 켜져 있다면 종료
        Application.Current.MainWindow = this;
        if (simpleModeWindow != null)
        {
            simpleModeWindow.Close();
            simpleModeWindow = null;
        }

        // 트레이 모드 창 닫기 추가
        if (trayModeWindow != null)
        {
            trayModeWindow.Close();
            trayModeWindow = null;
        }

        // 트레이 모드 해제
        settings.IsTrayMode = false;

        // 작업표시줄 표시
        this.ShowInTaskbar = true;

        // 창 스타일 복원
        this.WindowStyle = WindowStyle.None;
        this.ResizeMode = ResizeMode.CanResize;
        this.Topmost = false;

        // 기본 크기로 복원
        this.Width = currentViewMode == CaptureViewMode.Card ? 850 : 430;
        this.Height = 692;

        // 사용자 요청: 모드 전환 시 또는 시작 시 항상 화면 정중앙에 배치
        var workArea = SystemParameters.WorkArea;
        this.Left = (workArea.Width - this.Width) / 2 + workArea.Left;
        this.Top = (workArea.Height - this.Height) / 2 + workArea.Top;
        this.WindowStartupLocation = WindowStartupLocation.Manual;

        // 창 표시
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        UpdateViewModeUI();
        Settings.Save(settings);

        // 설정을 다시 로드하여 메모리와 파일 동기화
        settings = Settings.Load();
        UpdateInstantEditToggleUI();
        AdjustMainWindowHeightForMenuCount();
    }

    private void TrayModeButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchToTrayMode();
    }

    private void UpdateInstantEditToggleUI()
    {
        settings = Settings.Load(); // 최신 설정 로드
        if (settings.SimpleModeInstantEdit)
        {
            // 형광 초록색
            InstantEditToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676"));
            InstantEditToggleCircle.Margin = new Thickness(16, 0, 0, 0); // 스위치 크기가 줄어서 마진도 조정
        }
        else
        {
            InstantEditToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            InstantEditToggleCircle.Margin = new Thickness(2, 0, 0, 0);
        }
    }

    private void InstantEditToggle_Click(object sender, MouseButtonEventArgs e)
    {
        settings.SimpleModeInstantEdit = !settings.SimpleModeInstantEdit;
        Settings.Save(settings);
        UpdateInstantEditToggleUI();
    }

    public void SwitchToSimpleMode()
    {
        // ★ 간편모드 상태 저장
        settings.LastActiveMode = "Simple";
        Settings.Save(settings);

        // 트레이 모드 창 닫기
        if (trayModeWindow != null)
        {
            trayModeWindow.Close();
            trayModeWindow = null;
        }

        // 트레이 모드 해제
        settings.IsTrayMode = false;

        // 기존 간편 모드 로직 호출
        SimpleModeButton_Click(this, new RoutedEventArgs());
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!isExit)
        {
            e.Cancel = true; // 종료 취소
            this.Hide(); // 창 숨기기 (트레이로 이동 효과)
        }
        else
        {
            // 모든 창 닫기
            if (simpleModeWindow != null)
            {
                simpleModeWindow.Close();
                simpleModeWindow = null;
            }

            if (trayModeWindow != null)
            {
                trayModeWindow.Close();
                trayModeWindow = null;
            }

            // 노트 탐색기 닫기
            if (NoteExplorerWindow.Instance != null)
            {
                NoteExplorerWindow.Instance.Close();
            }

            // 타이머 정지
            if (tipTimer != null)
            {
                tipTimer.Stop();
                tipTimer = null;
            }

            // 종료 시 트레이 아이콘 정리
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }

            // 애플리케이션 완전 종료
            Application.Current.Shutdown();

            base.OnClosing(e);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 타이틀바 영역에서만 드래그 가능하도록 설정
        if (e.GetPosition(this).Y <= 24)
        {
            lastPosition = e.GetPosition(this);
            this.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // 창 최소화
        this.WindowState = WindowState.Minimized;
    }

    private void BtnViewMode_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn == null) return;

        string mode = btn.Tag?.ToString() ?? "";
        if (mode == "Card")
        {
            if (currentViewMode == CaptureViewMode.Card) return;
            currentViewMode = CaptureViewMode.Card;
            
            this.Width = 850;
            CaptureListPanel.ItemWidth = 210;
            CaptureListPanel.HorizontalAlignment = HorizontalAlignment.Left;
            if (EmptyStateActionPanel != null) EmptyStateActionPanel.Orientation = Orientation.Horizontal;
            
            settings.MainCaptureViewMode = 1;
            Settings.Save(settings);
        }
        else
        {
            if (currentViewMode == CaptureViewMode.List) return;
            currentViewMode = CaptureViewMode.List;
            
            this.Width = 430;
            CaptureListPanel.ItemWidth = 210; 
            CaptureListPanel.HorizontalAlignment = HorizontalAlignment.Center;
            if (EmptyStateActionPanel != null) EmptyStateActionPanel.Orientation = Orientation.Vertical;
            
            settings.MainCaptureViewMode = 0;
            Settings.Save(settings);
        }
        


        RebuildCaptureList();
        UpdateViewModeUI();
    }

    public void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = Application.Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
        if (historyWindow == null)
        {
            historyWindow = new HistoryWindow();
            historyWindow.Show();
        }
        else
        {
            historyWindow.Activate();
            if (historyWindow.WindowState == WindowState.Minimized)
                historyWindow.WindowState = WindowState.Normal;
        }
    }

    private void UpdateViewModeUI()
    {
        if (PathViewList == null || PathViewCard == null) return;

        if (currentViewMode == CaptureViewMode.Card)
        {
            PathViewList.Opacity = 0.4;
            PathViewCard.Opacity = 1.0;
        }
        else
        {
            PathViewList.Opacity = 1.0;
            PathViewCard.Opacity = 0.4;
        }
    }

    private void RebuildCaptureList()
    {
        CaptureListPanel.Children.Clear();
        selectedBorder = null;
        selectedIndex = -1;

        // 최신 캡처가 위(앞)에 오도록 역순으로 추가 (List.Children.Insert(0,...) 방식과 동일하게)
        for (int i = 0; i < captures.Count; i++)
        {
            var border = CreateCaptureItem(captures[i], i);
            CaptureListPanel.Children.Insert(0, border);
        }
        
        UpdateButtonStates();
        UpdateEmptyStateLogo();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 닫기 버튼 클릭 시 트레이로 숨기기 (종료 아님)
        this.Hide();
    }

    private void AddKeyboardShortcuts()
    {
        // Ctrl+C 단축키 처리
        KeyDown += MainWindow_KeyDown;
    }

    private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;

        // Ctrl+C: 선택 복사
        if (e.Key == Key.C && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0)
            {
                CopySelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+C: 모두 복사
        if (e.Key == Key.C && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                CopyAllImages();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+S: 선택 저장
        if (e.Key == Key.S && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0)
            {
                SaveSelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+S: 모두 저장
        if (e.Key == Key.S && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                SaveAllImages();
                e.Handled = true;
            }
            return;
        }

        // Delete: 선택 삭제
        if (e.Key == Key.Delete && mods == ModifierKeys.None)
        {
            if (selectedIndex >= 0)
            {
                DeleteSelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+Delete: 모두 삭제
        if (e.Key == Key.Delete && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                DeleteAllImages();
                e.Handled = true;
            }
            return;
        }

        // 1) Settings 기반 핫키를 후순위로 처리 (리스트 상호작용 우선)
        try
        {
            if (HandleSettingsHotkeys(e))
            {
                e.Handled = true;
                return;
            }
        }
        catch { /* ignore hotkey errors to avoid blocking */ }

        // Ctrl+A: 영역 캡처 시작
        if (e.Key == Key.A && mods == ModifierKeys.Control)
        {
            StartAreaCapture();
            e.Handled = true;
            return;
        }

        // Ctrl+F: 전체 화면 캡처
        if (e.Key == Key.F && mods == ModifierKeys.Control)
        {
            CaptureFullScreen();
            e.Handled = true;
            return;
        }

        // Ctrl+M: 간편 모드 토글
        if (e.Key == Key.M && mods == ModifierKeys.Control)
        {
            ToggleSimpleMode();
            e.Handled = true;
            return;
        }

        // Ctrl+P: 선택 미리보기 열기
        if (e.Key == Key.P && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0 && selectedIndex < captures.Count)
            {
                // ★ 메모리 최적화: 원본 이미지 비동기 로드 (썸네일 모드에서도 파일에서 원본 로드)
                var original = await captures[selectedIndex].GetOriginalImageAsync();
                ShowPreviewWindow(original, selectedIndex);
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Z: 실행 취소 (미리보기 창에서 편집된 경우 현재 선택된 이미지를 다시 로드)
        if (e.Key == Key.Z && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0 && selectedIndex < captures.Count)
            {
                // 현재 선택된 이미지를 원본으로 되돌림
                captures[selectedIndex] = new CaptureImage(captures[selectedIndex].Image);
                UpdateCaptureItemIndexes();
                UpdateButtonStates();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+R: 스크롤 캡처
        if (e.Key == Key.R && mods == ModifierKeys.Control)
        {
            CaptureScrollableWindow();
            e.Handled = true;
            return;
        }

        // Ctrl+D: 지정 캡처
        if (e.Key == Key.D && mods == ModifierKeys.Control)
        {
            DesignatedCaptureButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl+O: 열기 (불러오기)
        if (e.Key == Key.O && mods == ModifierKeys.Control)
        {
            OpenFileDialog_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // Ctrl+N: 새 캡처 (영역 캡처)
        if (e.Key == Key.N && mods == ModifierKeys.Control)
        {
            StartAreaCapture();
            e.Handled = true;
            return;
        }

        // Ctrl+T: 상단most 토글
        if (e.Key == Key.T && mods == ModifierKeys.Control)
        {
            ToggleTopmost();
            e.Handled = true;
            return;
        }
    }

    // 상단most 토글 기능
    private void ToggleTopmost()
    {
        this.Topmost = !this.Topmost;
        ShowGuideMessage(LocalizationManager.GetString(this.Topmost ? "TopmostOnMsg" : "TopmostOffMsg"), TimeSpan.FromSeconds(1));
    }

    // 파일 열기 다이얼로그
    private async void OpenFileDialog_Click(object? sender, RoutedEventArgs? e)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.GetString("OpenImageTitle"),
            Filter = $"{LocalizationManager.GetString("ImageFilesFilter")}|*.png;*.jpg;*.jpeg;*.bmp;*.gif|{LocalizationManager.GetString("AllFiles")}|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    // Use ThumbnailManager to load images asynchronously and throttled
                    var bitmap = await ThumbnailManager.LoadThumbnailAsync(fileName, 0);
                    if (bitmap != null)
                    {
                        AddCaptureToList(bitmap);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{LocalizationManager.GetString("FileOpenErrorPrefix")}: {fileName}\n{LocalizationManager.GetString("Error")}: {ex.Message}", LocalizationManager.GetString("FileOpenErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    #region 캡처 기능

    private async void AreaCaptureButton_MouseEnter(object sender, MouseEventArgs e)
    {
        // 이미 로딩 중이거나, 최근 1초 이내에 프리로딩한 게 있으면 스킵
        // ★ 속도 최적화: 캐시 유효 시간 3초로 연장
        if (isPreloading || (DateTime.Now - preloadedTime).TotalSeconds < 3.0) return;

        isPreloading = true;
        try
        {
            // 백그라운드에서 조용히 캡처
            var shot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

            // UI 스레드에서 결과 저장 (Freeze 필수)
            shot.Freeze();
            // ★ 메모리 최적화: WeakReference로 저장
            preloadedScreenshotRef = new WeakReference<BitmapSource>(shot);
            preloadedTime = DateTime.Now;
        }
        catch
        {
            // 프리로딩 실패는 무시
        }
        finally
        {
            isPreloading = false;
        }
    }

    private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartAreaCapture();
    }

    private void DelayCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // 저장된 지연 시간으로 바로 캡처 시작 (컨텍스트 메뉴 없음)
        int delay = settings.DelayCaptureSeconds;
        
        if (delay <= 0)
        {
            StartAreaCapture();
            return;
        }

        var countdown = new GuideWindow("", null)
        {
            Owner = this
        };
        countdown.Show();
        countdown.StartCountdown(delay, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(StartAreaCapture);
        });
    }

    private void DelaySelector_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            fe.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void UpdateDelayCaptureUI()
    {
        var delayText = this.FindName("DelayValueText") as TextBlock;
        var delayBox = this.FindName("DelayValueBox") as Border;
        var stop1 = this.FindName("DelayStop1") as System.Windows.Media.GradientStop;
        var stop2 = this.FindName("DelayStop2") as System.Windows.Media.GradientStop;

        if (delayText != null)
        {
            delayText.Text = settings.DelayCaptureSeconds.ToString();
            delayText.ToolTip = settings.DelayCaptureSeconds == 0 ? "현재: 지연 없음\n(클릭하여 지연 시간 변경)" : $"현재: {settings.DelayCaptureSeconds}초\n(클릭하여 지연 시간 변경)";
            
            if (stop1 != null && stop2 != null)
            {
                Color color1, color2;
                // 지연 시간에 따른 그라데이션 색상 변경
                switch (settings.DelayCaptureSeconds)
                {
                    case 3:
                        color1 = (Color)ColorConverter.ConvertFromString("#007AFF");
                        color2 = (Color)ColorConverter.ConvertFromString("#5856D6");
                        break;
                    case 5:
                        color1 = (Color)ColorConverter.ConvertFromString("#FF9500");
                        color2 = (Color)ColorConverter.ConvertFromString("#FF2D55");
                        break;
                    case 10:
                        color1 = (Color)ColorConverter.ConvertFromString("#AF52DE");
                        color2 = (Color)ColorConverter.ConvertFromString("#FF2D55");
                        break;
                    default:
                        color1 = (Color)ColorConverter.ConvertFromString("#007AFF");
                        color2 = (Color)ColorConverter.ConvertFromString("#5856D6");
                        break;
                }
                stop1.Color = color1;
                stop2.Color = color2;

                // 박스 테두리 색상도 숫자 색상에 맞춰 반투명하게 변경
                if (delayBox != null)
                {
                    var borderBrush = new SolidColorBrush(color1);
                    borderBrush.Opacity = 0.5;
                    delayBox.BorderBrush = borderBrush;
                }
            }
        }
    }

    private void DelayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int seconds = 0;
        if (sender is MenuItem mi && mi.Tag is string tagStr && int.TryParse(tagStr, out int s))
        {
            seconds = s;
        }
        else if (sender is MenuItem mi2 && mi2.Tag is int tagInt)
        {
            seconds = tagInt;
        }

        settings.DelayCaptureSeconds = seconds;
        captureDelaySeconds = seconds;
        Settings.Save(settings);
        
        // UI 업데이트
        UpdateDelayCaptureUI();
    }

    private void EdgeCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // [Modified] Use Preset Settings for Edge Capture
        var (radius, _, _, _) = CatchCapture.Utilities.EdgeCaptureHelper.GetPresetSettings(settings.EdgeCapturePresetLevel);
        _ = StartAreaCaptureAsync(radius);
    }

    // 엣지 반경 옵션 배열 (반경, 아이콘 경로)
    private static readonly (int Radius, string IconPath)[] EdgeRadiusOptions = new[]
    {
        (12, "/icons/edge_1.png"),
        (25, "/icons/edge_2.png"),
        (50, "/icons/edge_3.png"),
        (100, "/icons/edge_4.png"),
        (999, "/icons/edge_5.png")
    };

    private void EdgeRadiusSelector_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            fe.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }


    private void UpdateEdgeRadiusEmoji()
    {
        // FindName으로 요소 찾기
        var iconImage = this.FindName("EdgeRadiusImage") as System.Windows.Controls.Image;
        if (iconImage == null) return;

        int level = settings.EdgeCapturePresetLevel;
        if (level < 1 || level > 5) level = 3; // Fallback

        // Level is 1-based, array is 0-based
        int index = level - 1;
        if (index >= 0 && index < EdgeRadiusOptions.Length)
        {
            var option = EdgeRadiusOptions[index];
            try
            {
                iconImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(option.IconPath, UriKind.Relative));
            }
            catch { }
            
            // 툴팁 업데이트
            string[] names = { "소프트 엣지", "매끄러운 둥근 모서리", "클래식 라운드", "알약 스타일", "퍼펙트 서클" };
            if (index < names.Length)
            {
                iconImage.ToolTip = $"현재: {names[index]}\n(우클릭/좌클릭하여 변경)";
            }
        }
    }

    private void EdgeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tagStr && int.TryParse(tagStr, out int radius))
        {
            // Map legacy radius values to Preset Levels
            int level = 3; // Default
            if (radius == 12) level = 1;
            else if (radius == 25) level = 2;
            else if (radius == 50) level = 3;
            else if (radius == 100) level = 4;
            else if (radius == 999) level = 5;

            settings.EdgeCapturePresetLevel = level;
            // Also update legacy radius for compatibility if needed, though we rely on PresetLevel now
            settings.EdgeCaptureRadius = radius; 
            Settings.Save(settings);
            
            // UI 업데이트
            UpdateEdgeRadiusEmoji();
        }
    }


    private BitmapSource GetCachedOrFreshScreenshot()
    {
        var now = DateTime.Now;

        // ★ 메모리 최적화: WeakReference에서 캐시된 스크린샷 가져오기
        BitmapSource? cachedScreenshot = null;
        cachedScreenshotRef?.TryGetTarget(out cachedScreenshot);

        // 캐시가 유효한지 확인 (3초 이내)
        if (cachedScreenshot != null && (now - lastScreenshotTime) < screenshotCacheTimeout)
        {
            return cachedScreenshot;
        }

        // 새로운 스크린샷 캡처 및 캐시 업데이트
        var newScreenshot = ScreenCaptureUtility.CaptureScreen();
        cachedScreenshotRef = new WeakReference<BitmapSource>(newScreenshot);
        lastScreenshotTime = now;

        return newScreenshot;
    }

    /// <summary>
    /// Special capture mode for NoteInputWindow - bypasses instant edit mode
    /// and returns captured image via callback. Does NOT add to capture list.
    /// </summary>
    public async void TriggerCaptureForNote(Action<BitmapSource?> onCaptureComplete)
    {
        try
        {
            // Hide all windows
            this.Hide();
            if (simpleModeWindow != null) simpleModeWindow.Hide();
            if (trayModeWindow != null) trayModeWindow.Hide();

            // [Fix] NoteExplorerWindow might be open, hide it too
            if (NoteExplorerWindow.Instance != null && NoteExplorerWindow.Instance.IsVisible)
            {
                NoteExplorerWindow.Instance.Hide();
            }

            // [Fix] Add a small delay for Note Editor capture to prevent "ghost" images.
            // This is only called via TriggerCaptureForNote, so regular captures are unaffected.
            await Task.Delay(200);

            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            FlushUIAfterHide();

            // Capture screenshot
            var screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

            // Open SnippingWindow WITHOUT instant edit mode (always simple selection)
            using (var snippingWindow = new SnippingWindow(false, screenshot))
            {
                // NOTE: Do NOT enable instant edit mode here - pure selection only
                // snippingWindow.EnableInstantEditMode(); // INTENTIONALLY DISABLED

                if (snippingWindow.ShowDialog() == true)
                {
                    var selectedArea = snippingWindow.SelectedArea;
                    var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                    // Return image via callback (do not add to main capture list)
                    onCaptureComplete?.Invoke(capturedImage);
                }
                else
                {
                    // Cancelled
                    onCaptureComplete?.Invoke(null);
                }
            }

            // Restore opacity but don't show main window (NoteInputWindow will handle visibility)
            this.Opacity = 1;
            
            // Clean up
            screenshot = null;
            GC.Collect(0, GCCollectionMode.Forced);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TriggerCaptureForNote error: {ex.Message}");
            onCaptureComplete?.Invoke(null);
        }
    }

    public async void TriggerOcrForNote(Action onComplete)
    {
        try
        {
            // 1. Hide Main Window and others
            if (this.IsVisible) this.Hide();
            if (simpleModeWindow != null && simpleModeWindow.IsVisible) simpleModeWindow.Hide();
            if (trayModeWindow != null && trayModeWindow.IsVisible) trayModeWindow.Hide();

            // 2. Clear UI
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            FlushUIAfterHide();

            // 3. Capture Screen
            await Task.Delay(50); // Ensure UI fade matches
            var screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

            // 4. Show Snipping Window (false = No Instant Edit Toolbar)
            using (var snippingWindow = new SnippingWindow(false, screenshot))
            {
                if (snippingWindow.ShowDialog() == true)
                {
                     var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(snippingWindow.SelectedArea);
                     
                     // Add to list but skip preview, AND do not show main window
                     AddCaptureToList(capturedImage, skipPreview: true, showMainWindow: false);

                     // OCR Execution
                     try
                     {
                         var ocrResult = await OcrUtility.ExtractTextFromImageAsync(capturedImage);
                         var resultWindow = new OcrResultWindow(ocrResult.Text, ocrResult.ShowWarning);
                         resultWindow.Owner = this; 
                         resultWindow.ShowDialog(); // Wait for user to copy and close
                     }
                     catch (Exception ex)
                     {
                         MessageBox.Show($"OCR 처리 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                     }
                }
            }

            // 5. Cleanup
            this.Opacity = 1;
            screenshot = null;
            GC.Collect(0, GCCollectionMode.Forced);
            
            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TriggerOcrForNote error: {ex.Message}");
            onComplete?.Invoke();
        }
    }


    public async Task StartAreaCaptureAsync(int cornerRadius = 0, bool showMainWindowAfter = true)
    {
        // ★ 기존 캡처창이 열려있다면 닫기 (재캡처 연타 시 중복 실행 방지)
        if (_activeSnippingWindow != null)
        {
            var oldWindow = _activeSnippingWindow;
            _activeSnippingWindow = null; // 새 세션을 위해 필드를 미리 비움 (기존 세션이 창을 복원하지 않도록 신호)
            try { oldWindow.Close(); } catch { }
        }

        // 간편모드 체크 추가 (재캡처 플래그 포함)
        bool isSimpleMode = (simpleModeWindow != null && simpleModeWindow.IsVisible) || _wasSimpleModeVisibleBeforeRecapture;
        _wasSimpleModeVisibleBeforeRecapture = false; // 플래그 사용 후 리셋

        // ★ 속도 최적화: settings는 이미 로드되어 있으므로 캐시 사용
        bool instantEdit = settings.SimpleModeInstantEdit;

        // 1단계: 즉시 숨기기 (Opacity 조정보다 Hide가 더 빠르고 확실함)
        this.Hide();
        if (simpleModeWindow != null) simpleModeWindow.Hide();
        if (trayModeWindow != null) trayModeWindow.Hide();

        // ★ 속도 최적화: DwmFlush 제거하여 즉시 실행 (창 숨김 애니메이션보다 반응성 우선)
        // try { DwmFlush(); } catch { }

        // ★ 속도 최적화: 프리로딩된 스크린샷 확인
        BitmapSource? screenshot = null;
        BitmapSource? preloadedShot = null;
        preloadedScreenshotRef?.TryGetTarget(out preloadedShot);
        
        (string AppName, string Title) metadata;

        // 프리로딩된 이미지가 있고, 3초 이내의 것이라면 즉시 사용
        // ★ 수정: 오버레이 모드(CaptureOverlay)일 때는 프리로딩 무시 (동영상이 멈추면 안되므로)
        if (!settings.UseOverlayCaptureMode && preloadedShot != null && (DateTime.Now - preloadedTime).TotalSeconds < 3.0)
        {
            screenshot = preloadedShot;
            preloadedScreenshotRef = null;
            metadata = ScreenCaptureUtility.GetActiveWindowMetadata();
        }
        else
        {
            // 설정 확인: 빠른 캡처(오버레이) vs 정지 캡처
            if (settings.UseOverlayCaptureMode)
            {
                // ★ 빠른 캡처: 처음에 캡처하지 않음 (로딩 시간 0초, 동영상 계속 재생)
                screenshot = null;
                // 메타데이터 비동기 로드
                metadata = await Task.Run(() => ScreenCaptureUtility.GetActiveWindowMetadata());
            }
            else
            {
                // ★ 정지 캡처: 스크린샷 먼저 찍음 (화면 정지)
                var screenshotTask = Task.Run(() => ScreenCaptureUtility.CaptureScreen());
                var metadataTask = Task.Run(() => ScreenCaptureUtility.GetActiveWindowMetadata());
                
                screenshot = await screenshotTask;
                metadata = await metadataTask;
            }
        }

        // SnippingWindow 생성 및 표시 (cornerRadius 전달)
        var snippingWindow = new SnippingWindow(false, screenshot, metadata.AppName, metadata.Title, cornerRadius);
        _activeSnippingWindow = snippingWindow;
        
        try
        {
            if (instantEdit)
            {
                snippingWindow.EnableInstantEditMode();
            }

            if (snippingWindow.ShowDialog() == true)
            {
                var selectedArea = snippingWindow.SelectedArea;
                var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                // Opacity 복원
                this.Opacity = 1;

                // 노트 저장 요청 확인
                if (snippingWindow.RequestSaveToNote)
                {
                    if (!settings.IsTrayMode)
                    {
                        this.WindowState = WindowState.Minimized;
                        this.Show();
                    }
                    
                    // [Fix] 기존에 열린 노트 입력창이 있는지 확인 (새 노트 작성 중인 창 우선)
                    var openWin = Application.Current.Windows.OfType<NoteInputWindow>().FirstOrDefault(w => !w.IsEditMode);
                    if (openWin != null)
                    {
                        openWin.AppendImage(capturedImage);
                        openWin.Activate();
                        openWin.Focus();
                    }
                    else
                    {
                        var noteWin = new NoteInputWindow(capturedImage, snippingWindow.SourceApp, snippingWindow.SourceTitle);
                        // noteWin.Owner = this; // Owner 설정을 제거하여 메인창이 앞으로 올 수 있게 함
                        noteWin.Show();
                    }
                    return;
                }
                
                bool requestMinimize = snippingWindow.RequestMainWindowMinimize;
                
                if (requestMinimize && !settings.IsTrayMode)
                {
                    this.WindowState = WindowState.Minimized;
                    this.Show();
                }

                // 캡처 영역의 중심 좌표 계산
                int centerX = (int)(selectedArea.X + selectedArea.Width / 2);
                int centerY = (int)(selectedArea.Y + selectedArea.Height / 2);

                // 초기 메타데이터 (활성 창 기준) - Null 안전 처리
                string finalApp = snippingWindow.SourceApp ?? "Unknown";
                string finalTitle = snippingWindow.SourceTitle ?? "Unknown";

                // 만약 초기 메타데이터가 불확실하거나(Unknown), 시스템 창(Taskbar 등)인 경우
                // 또는 사용자가 드래그한 영역이 초기 활성 창과 다를 가능성이 높으므로 좌표 기반으로 재확인
                if (finalApp == "Unknown" || finalTitle == "Unknown" || 
                    finalTitle == "Program Manager" || finalTitle == "Taskbar" ||
                    !string.IsNullOrEmpty(finalApp)) // 항상 좌표 기반을 우선 확인해볼 가치가 있음 (드래그 캡처 특성상)
                {
                    // 좌표 기반 메타데이터 추출 시도
                    var pointMetadata = ScreenCaptureUtility.GetWindowAtPoint(centerX, centerY);
                    if (pointMetadata.AppName != "Unknown" && pointMetadata.Title != "Unknown")
                    {
                        finalApp = pointMetadata.AppName;
                        finalTitle = pointMetadata.Title;
                    }
                }

                AddCaptureToList(capturedImage, 
                                 skipPreview: requestMinimize, 
                                 showMainWindow: showMainWindowAfter && !requestMinimize, 
                                 sourceApp: finalApp, 
                                 sourceTitle: finalTitle);
            }
            else
            {
                // 취소 시 창 복원
                if (_activeSnippingWindow == snippingWindow)
                {
                    this.Opacity = 1;

                    if (isSimpleMode)
                    {
                        if (simpleModeWindow != null)
                        {
                            simpleModeWindow._suppressActivatedExpand = true;
                            simpleModeWindow.Show();
                            simpleModeWindow.Activate();
                        }
                    }
                    else if (!settings.IsTrayMode && showMainWindowAfter)
                    {
                        this.Show();
                        this.Activate();
                    }
                    else
                    {
                        if (trayModeWindow != null && showMainWindowAfter)
                        {
                            trayModeWindow.Show();
                            trayModeWindow.Activate();
                        }
                    }
                }
            }
        }
        finally
        {
            if (_activeSnippingWindow == snippingWindow)
            {
                _activeSnippingWindow = null;
            }
            snippingWindow.Dispose();

            // 메모리 정리
            screenshot = null;
            GC.Collect(0, GCCollectionMode.Forced);
        }
    }

    public void StartAreaCapture(bool showMainWindowAfter = true)
    {
        _ = StartAreaCaptureAsync(0, showMainWindowAfter);
    }

    private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureFullScreen();
    }

    private void CaptureFullScreen(bool showMainWindowAfter = true)
    {
        // 전체 화면 캡처
        this.Hide();
        FlushUIAfterHide();

        var capturedImage = ScreenCaptureUtility.CaptureScreen();
        AddCaptureToList(capturedImage, showMainWindow: showMainWindowAfter);
    }


    private void ScrollCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Hide();
            System.Threading.Thread.Sleep(10);

            var scrollCaptureWindow = new ScrollCaptureWindow();

            if (scrollCaptureWindow.ShowDialog() == true && scrollCaptureWindow.CapturedImage != null)
            {
                AddCaptureToList(scrollCaptureWindow.CapturedImage);
            }
            else if (scrollCaptureWindow.EscCancelled)
            {
                // ESC로 취소 (캡처 시작 전) - 메인창 복원
                if (!settings.IsTrayMode && this.Visibility != Visibility.Visible)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                }
            }
            else
            {
                // 기타 취소 - 메인창 복원
                if (!settings.IsTrayMode && this.Visibility != Visibility.Visible)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"스크롤 캡처 오류: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            if (!settings.IsTrayMode && this.Visibility != Visibility.Visible)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }
    }

    private async void OcrCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // 현재 모드 상태 저장
        bool wasTrayMode = settings.IsTrayMode;
        // 간편모드 체크: IsVisible이 아닌 존재 여부로 확인 (SimpleModeWindow.PerformCustomCapture에서 이미 숨김)
        bool wasSimpleMode = simpleModeWindow != null;

        // 간편모드가 아닐 때만 간편모드 창 숨기기 처리 (이미 PerformCustomCapture에서 처리됨)
        // SimpleModeWindow.PerformCustomCapture에서 이미 Hide()를 호출하므로 여기서는 불필요

        // 메인 창이 표시되어 있다면 숨기기
        if (this.IsVisible)
        {
            this.Hide();
        }

        // UI 업데이트 강제 대기
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        FlushUIAfterHide();

        // 스크린샷 캡처
        BitmapSource screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

        // SnippingWindow 열기 (즉시편집 없이)
        using var snippingWindow = new SnippingWindow(false, screenshot);

        if (snippingWindow.ShowDialog() == true)
        {
            var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(snippingWindow.SelectedArea);

            // 캡처 리스트에 추가
            // AddCaptureToList에서 창 복원 로직을 일원화하여 처리함
            AddCaptureToList(capturedImage, skipPreview: true);

            // OCR 실행
        System.Windows.Window? loadingWindow = null;
        try
        {
            // 로딩 창 동적 생성 (심플하고 깔끔한 디자인)
            loadingWindow = new System.Windows.Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Width = 250,
                Height = 80,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ShowInTaskbar = false,
                Content = new System.Windows.Controls.Border 
                { 
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)), 
                    CornerRadius = new System.Windows.CornerRadius(12),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100)),
                    BorderThickness = new System.Windows.Thickness(1),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.5 },
                    Child = new System.Windows.Controls.StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Orientation = Orientation.Horizontal,
                        Children = 
                        {
                            // 텍스트
                            new System.Windows.Controls.TextBlock 
                            { 
                                Text = "OCR 추출 중입니다...", 
                                Foreground = System.Windows.Media.Brushes.White,
                                FontSize = 14,
                                FontWeight = FontWeights.SemiBold,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new System.Windows.Thickness(10, 0, 0, 0)
                            }
                        }
                    }
                }
            };
            loadingWindow.Show();

            // UI 렌더링을 위해 잠시 대기
            await Task.Delay(100);

            var ocrResult = await OcrUtility.ExtractTextFromImageAsync(capturedImage);
            
            loadingWindow.Close();
            loadingWindow = null;

            var resultWindow = new OcrResultWindow(ocrResult.Text, ocrResult.ShowWarning);
            resultWindow.Owner = this;
            resultWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            loadingWindow?.Close();
            MessageBox.Show($"OCR 처리 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        }
        else
        {
            // 취소 시에만 명시적으로 복원
            this.Opacity = 1;

            if (wasSimpleMode)
            {
                // SimpleModeWindow.PerformCustomCapture에서 처리됨
            }
            else if (wasTrayMode)
            {
                if (trayModeWindow != null)
                {
                    trayModeWindow.Show();
                    trayModeWindow.Activate();
                }
            }
            else
            {
                this.Show();
                this.Activate();
            }
        }
    }

    /// <summary>
    /// 화면 녹화 버튼 클릭 - 녹화 도구 창 열기
    /// </summary>
    private void ScreenRecordButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // [Fix] Check if already open
            if (activeRecordingWindow != null && activeRecordingWindow.IsVisible)
            {
                activeRecordingWindow.Activate();
                activeRecordingWindow.Topmost = true;
                return;
            }

            // 녹화 시작 전 현재 모드 저장
            // 간편모드에서는 SimpleModeWindow가 먼저 Hide()를 호출하므로 IsVisible 대신 존재 여부만 확인
            bool wasSimpleMode = simpleModeWindow != null;
            bool wasTrayMode = settings.IsTrayMode;

            this.Hide();
            FlushUIAfterHide();

            activeRecordingWindow = new Recording.RecordingWindow();
            activeRecordingWindow.Closed += (s, args) =>
            {
                activeRecordingWindow = null; // Clear reference

                // 녹화 창이 닫힐 때 이전 모드로 복원
                if (wasSimpleMode && simpleModeWindow != null)
                {
                    // 간편 모드로 복원
                    simpleModeWindow.Show();
                    simpleModeWindow.Activate();
                    simpleModeWindow.Topmost = true;
                }
                else if (wasTrayMode && trayModeWindow != null)
                {
                    // 트레이 모드로 복원
                    trayModeWindow.Show();
                    trayModeWindow.Activate();
                    trayModeWindow.Topmost = true;
                }
                else
                {
                    // 일반 모드로 복원
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                }
            };
            activeRecordingWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"녹화 도구 열기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            if (!settings.IsTrayMode) this.Show(); // 오류 시 복원
        }
    }

    private void CaptureScrollableWindow()
    {
        try
        {
            // 안내 메시지 표시
            var guideWindow = new GuideWindow(LocalizationManager.GetString("ClickWindowThenEnter"), TimeSpan.FromSeconds(2.5));
            guideWindow.Owner = this;
            guideWindow.Show();

            // 사용자가 다른 창을 선택할 수 있도록 기다림
            this.Hide();

            // 스크롤 캡처 수행
            var capturedImage = ScreenCaptureUtility.CaptureScrollableWindow();

            if (capturedImage != null)
            {
                AddCaptureToList(capturedImage);
                ShowGuideMessage(LocalizationManager.GetString("ScrollCaptureComplete"), TimeSpan.FromSeconds(1.5));

                // 캡처된 이미지 클립보드에 복사
                ScreenCaptureUtility.CopyImageToClipboard(capturedImage);
                // ShowGuideMessage(LocalizationManager.GetString("CopiedToClipboard"), TimeSpan.FromSeconds(1.5));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"스크롤 캡처 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // 창이 닫히지 않도록 항상 메인 창을 다시 표시
        }
    }

    private void DesignatedCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // 지정 캡처 오버레이 표시
        try
        {
            this.Hide();
            FlushUIAfterHide();

            var designatedWindow = new CatchCapture.Utilities.DesignatedCaptureWindow();
            designatedWindow.Owner = this;

            bool captureOccurred = false;

            // Subscribe to continuous capture event
            designatedWindow.CaptureCompleted += (img) =>
            {
                captureOccurred = true;
                // Ensure UI thread
                Dispatcher.Invoke(() =>
                {
                    // showMainWindow: false prevents main window pop-up so user can capture continuously
                    AddCaptureToList(img, skipPreview: true, showMainWindow: false);
                    // Optionally also copy to clipboard
                    CatchCapture.Utilities.ScreenCaptureUtility.CopyImageToClipboard(img);
                });
            };

            // Block until user closes overlay via ✕ (DialogResult false)
            designatedWindow.ShowDialog();

            // ✕ 버튼으로 닫았는데 캡처가 없었으면 메인 창 다시 표시
            if (!captureOccurred && !settings.IsTrayMode)
            {
                this.Show();
                this.Activate();
            }
        }
        finally
        {

        }
    }

    private async void WindowCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 간편모드 체크
            bool isSimpleMode = simpleModeWindow != null && simpleModeWindow.IsVisible;

            // 즉시편집 설정 확인
            var currentSettings = Settings.Load();
            bool instantEdit = currentSettings.SimpleModeInstantEdit;

            this.Hide();
            if (simpleModeWindow != null) simpleModeWindow.Hide();
            if (trayModeWindow != null) trayModeWindow.Hide();

            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            FlushUIAfterHide();
            System.Threading.Thread.Sleep(10);

            var windowCaptureOverlay = new CatchCapture.Utilities.WindowCaptureOverlay();

            if (windowCaptureOverlay.ShowDialog() == true && windowCaptureOverlay.CapturedImage != null)
            {
                // 즉시편집 모드가 활성화되어 있으면 SnippingWindow로 전환
                if (instantEdit && !windowCaptureOverlay.CapturedRect.IsEmpty)
                {
                    // 전체 화면 스크린샷 캡처
                    var screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

                    // SnippingWindow를 즉시편집 모드로 열기
                    using var snippingWindow = new SnippingWindow(false, screenshot);
                    snippingWindow.EnableInstantEditMode();

                    // 캡처된 창의 위치를 미리 선택된 영역으로 설정
                    snippingWindow.SetPreselectedArea(windowCaptureOverlay.CapturedRect);

                    if (snippingWindow.ShowDialog() == true)
                    {
                        var selectedArea = snippingWindow.SelectedArea;
                        var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                        bool requestMinimize = snippingWindow.RequestMainWindowMinimize;
                        this.Opacity = 1;
                        AddCaptureToList(capturedImage, skipPreview: requestMinimize, showMainWindow: !requestMinimize);

                        // 캡처 완료 후 모드별 창 복원
                        if (isSimpleMode)
                        {
                            if (simpleModeWindow != null)
                            {
                                simpleModeWindow._suppressActivatedExpand = true;
                                simpleModeWindow.Show();
                                simpleModeWindow.Activate();
                            }
                        }
                        else if (!settings.IsTrayMode)
                        {
                            if (requestMinimize)
                            {
                                this.WindowState = WindowState.Minimized;
                                this.Show();
                            }
                            else
                            {
                                this.Show();
                                this.Activate();
                            }
                        }
                        else
                        {
                            if (trayModeWindow != null)
                            {
                                trayModeWindow.Show();
                                trayModeWindow.Activate();
                            }
                        }
                    }
                    else
                    {
                        // 취소 시
                        this.Opacity = 1;
                        if (isSimpleMode)
                        {
                            if (simpleModeWindow != null)
                            {
                                simpleModeWindow._suppressActivatedExpand = true;
                                simpleModeWindow.Show();
                                simpleModeWindow.Activate();
                            }
                        }
                        else if (!settings.IsTrayMode)
                        {
                            this.Show();
                            this.Activate();
                        }
                        else
                        {
                            if (trayModeWindow != null)
                            {
                                trayModeWindow.Show();
                                trayModeWindow.Activate();
                            }
                        }
                    }
                }
                else
                {
                    // 즉시편집 모드가 아니면 기존 방식대로
                    AddCaptureToList(windowCaptureOverlay.CapturedImage);
                }
            }
            else
            {
                // 취소된 경우 (ESC 또는 우클릭) 메인 창 다시 표시
                if (!settings.IsTrayMode)
                {
                    this.Show();
                    this.Activate();
                }
            }
        }
        finally
        {
            // 창 표시는 AddCaptureToList 또는 else 블록에서 처리
        }
    }

    private async void ElementCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 간편모드 체크
            bool isSimpleMode = simpleModeWindow != null && simpleModeWindow.IsVisible;

            // 즉시편집 설정 확인
            var currentSettings = Settings.Load();
            bool instantEdit = currentSettings.SimpleModeInstantEdit;

            this.Hide();
            if (simpleModeWindow != null) simpleModeWindow.Hide();
            if (trayModeWindow != null) trayModeWindow.Hide();

            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            FlushUIAfterHide();
            System.Threading.Thread.Sleep(10);

            var elementCaptureWindow = new ElementCaptureWindow();

            if (elementCaptureWindow.ShowDialog() == true && elementCaptureWindow.CapturedImage != null)
            {
                // 즉시편집 모드가 활성화되어 있으면 SnippingWindow로 전환
                if (instantEdit && !elementCaptureWindow.CapturedRect.IsEmpty)
                {
                    // 전체 화면 스크린샷 캡처
                    var screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

                    // SnippingWindow를 즉시편집 모드로 열기
                    using var snippingWindow = new SnippingWindow(false, screenshot);
                    snippingWindow.EnableInstantEditMode();

                    // 캡처된 요소의 위치를 미리 선택된 영역으로 설정
                    snippingWindow.SetPreselectedArea(elementCaptureWindow.CapturedRect);

                    if (snippingWindow.ShowDialog() == true)
                    {
                        var selectedArea = snippingWindow.SelectedArea;
                        var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                        bool requestMinimize = snippingWindow.RequestMainWindowMinimize;
                        this.Opacity = 1;
                        AddCaptureToList(capturedImage, skipPreview: requestMinimize, showMainWindow: !requestMinimize);

                        // 캡처 완료 후 모드별 창 복원
                        if (isSimpleMode)
                        {
                            if (simpleModeWindow != null)
                            {
                                simpleModeWindow._suppressActivatedExpand = true;
                                simpleModeWindow.Show();
                                simpleModeWindow.Activate();
                            }
                        }
                        else if (!settings.IsTrayMode)
                        {
                            if (requestMinimize)
                            {
                                this.WindowState = WindowState.Minimized;
                                this.Show();
                            }
                            else
                            {
                                this.Show();
                                this.Activate();
                            }
                        }
                        else
                        {
                            if (trayModeWindow != null)
                            {
                                trayModeWindow.Show();
                                trayModeWindow.Activate();
                            }
                        }
                    }
                    else
                    {
                        // 취소 시
                        this.Opacity = 1;
                        if (isSimpleMode)
                        {
                            if (simpleModeWindow != null)
                            {
                                simpleModeWindow._suppressActivatedExpand = true;
                                simpleModeWindow.Show();
                                simpleModeWindow.Activate();
                            }
                        }
                        else if (!settings.IsTrayMode)
                        {
                            this.Show();
                            this.Activate();
                        }
                        else
                        {
                            if (trayModeWindow != null)
                            {
                                trayModeWindow.Show();
                                trayModeWindow.Activate();
                            }
                        }
                    }
                }
                else
                {
                    // 즉시편집 모드가 아니면 기존 방식대로
                    AddCaptureToList(elementCaptureWindow.CapturedImage);
                }
            }
            else
            {
                // 취소된 경우 (ESC 또는 우클릭) 메인 창 다시 표시
                if (!settings.IsTrayMode)
                {
                    this.Show();
                    this.Activate();
                }
            }
        }
        finally
        {
            // 창 표시는 AddCaptureToList 또는 else 블록에서 처리
        }
    }

    private void MultiCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // 캡처 시작 전 메인 창이 보인다면 일반 모드로 확실히 설정
        if (this.Visibility == Visibility.Visible)
        {
            settings.IsTrayMode = false;
            Settings.Save(settings);
        }

        // 창 숨기기
        this.Hide();

        // 짧은 대기로 창이 완전히 숨겨지도록
        System.Threading.Thread.Sleep(10);

        using (var multiCaptureWindow = new CatchCapture.Utilities.MultiCaptureWindow())
        {
            if (multiCaptureWindow.ShowDialog() == true)
            {
                // 1. 개별 이미지 모드 (F1)
                if (multiCaptureWindow.IndividualImages != null && multiCaptureWindow.IndividualImages.Count > 0)
                {
                    foreach (var img in multiCaptureWindow.IndividualImages)
                    {
                        AddCaptureToList(img);
                    }
                }
                // 2. 합치기 모드 (Enter)
                else if (multiCaptureWindow.FinalCompositeImage != null)
                {
                    AddCaptureToList(multiCaptureWindow.FinalCompositeImage);
                }
            }
            else
            {
                // 캡처 취소 시
                if (!settings.IsTrayMode)
                {
                    // 일반 모드: 메인 창 표시
                    this.Show();
                    this.Activate();
                }
                else
                {
                    // 트레이 모드: 트레이 모드 창 다시 표시
                    if (trayModeWindow != null)
                    {
                        trayModeWindow.Show();
                        trayModeWindow.Activate();
                    }
                }
            }
        }
    }
    // 간편모드 전용 지정캡처 메서드 (메인창을 표시하지 않음)
    private void PerformDesignatedCaptureForSimpleMode()
    {
        try
        {
            var designatedWindow = new CatchCapture.Utilities.DesignatedCaptureWindow();
            designatedWindow.Owner = simpleModeWindow;

            // Subscribe to continuous capture event
            designatedWindow.CaptureCompleted += (img) =>
            {
                // Ensure UI thread
                Dispatcher.Invoke(() =>
                {
                    AddCaptureToList(img);
                    // 클립보드에 복사
                    CatchCapture.Utilities.ScreenCaptureUtility.CopyImageToClipboard(img);
                });
            };

            // Block until user closes overlay via ✕
            designatedWindow.ShowDialog();
        }
        finally
        {
            // 간편모드 창만 다시 표시 (메인창은 표시하지 않음)
            simpleModeWindow?.Show();
        }
    }

    private string GetAutoSaveFilePath(string extension, string? sourceApp = null, string? sourceTitle = null)
    {
        var currentSettings = Models.Settings.Load();
        string saveFolder = currentSettings.DefaultSaveFolder;
        if (string.IsNullOrWhiteSpace(saveFolder))
        {
            saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
        }
        else
        {
            saveFolder = Utilities.DatabaseManager.EnsureCatchCaptureSubFolder(saveFolder);
        }
        
        // 폴더 분류 적용
        string groupMode = currentSettings.FolderGroupingMode ?? "None";
        DateTime now = DateTime.Now;
        string subFolder = "";
        
        if (groupMode == "Monthly") subFolder = now.ToString("yyyy-MM");
        else if (groupMode == "Quarterly") 
        {
            int q = (now.Month + 2) / 3;
            subFolder = $"{now.Year}_{q}Q";
        }
        else if (groupMode == "Yearly") subFolder = now.ToString("yyyy");
        
        if (!string.IsNullOrEmpty(subFolder) && groupMode != "None")
        {
            saveFolder = System.IO.Path.Combine(saveFolder, subFolder);
        }

        if (!System.IO.Directory.Exists(saveFolder))
        {
            System.IO.Directory.CreateDirectory(saveFolder);
        }

        // 파일명 템플릿 적용
        string template = currentSettings.FileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";
        string filenameTemplate = template;
        
        // Sanitize helper
        string Sanitize(string s) {
            if (string.IsNullOrEmpty(s)) return "Unknown";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        try 
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\$(.*?)\$");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string fmt = match.Groups[1].Value;
                
                if (fmt.Equals("App", StringComparison.OrdinalIgnoreCase)) {
                     filenameTemplate = filenameTemplate.Replace(match.Value, Sanitize(sourceApp ?? ""));
                     continue;
                }
                if (fmt.Equals("Title", StringComparison.OrdinalIgnoreCase)) {
                     string t = sourceTitle ?? "";
                     if (t.Length > 50) t = t.Substring(0, 50); // 길이는 적당히 제한
                     filenameTemplate = filenameTemplate.Replace(match.Value, Sanitize(t));
                     continue;
                }

                try { filenameTemplate = filenameTemplate.Replace(match.Value, now.ToString(fmt)); } catch { }
            }
        } 
        catch { }

        string ext = extension.StartsWith(".") ? extension : "." + extension;
        string fullPath = System.IO.Path.Combine(saveFolder, filenameTemplate + ext);
        
        // 중복 방지
        int dupIndex = 1;
        while (System.IO.File.Exists(fullPath))
        {
             fullPath = System.IO.Path.Combine(saveFolder, $"{filenameTemplate} ({dupIndex}){ext}");
             dupIndex++;
        }
        return fullPath;
    }

    private void SaveToHistory(CaptureImage captureInfo, BitmapSource image)
    {
        string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_history_log.txt");
        try
        {
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] SaveToHistory Started. App={captureInfo.SourceApp}, Title={captureInfo.SourceTitle}, Image={image.PixelWidth}x{image.PixelHeight}\n");

            var settings = Settings.Load();
            string? fullPath = null;

            // 1. 이미 자동 저장된 파일이 있는지 확인
            if (captureInfo.IsSaved && !string.IsNullOrEmpty(captureInfo.SavedPath) && System.IO.File.Exists(captureInfo.SavedPath))
            {
                // 사용자 지정 캡처 폴더에 이미 저장되어 있다면 해당 경로 사용
                fullPath = captureInfo.SavedPath;
            }
            else
            {
                // 2. 저장되지 않은 경우 (자동저장 OFF 등)라도 사용자가 설정한 표준 폴더 구조(년/월 등)에 저장
                // GetAutoSaveFilePath가 폴더 생성 및 그룹화(월별/년별 등)를 모두 처리함
                string ext = settings.FileSaveFormat.ToLower();
                if (!ext.StartsWith(".")) ext = "." + ext;
                
                fullPath = GetAutoSaveFilePath(ext, captureInfo.SourceApp, captureInfo.SourceTitle);

                // 파일 저장
                ScreenCaptureUtility.SaveImageToFile(image, fullPath, settings.ImageQuality);
                
                // 정보 업데이트
                captureInfo.SavedPath = fullPath;
                captureInfo.IsSaved = true;
            }

            if (string.IsNullOrEmpty(fullPath)) 
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] FullPath is empty. Aborting.\n");
                return;
            }

            // DB 저장
            var historyItem = new HistoryItem
            {
                FileName = IOPath.GetFileName(fullPath),
                FilePath = fullPath,
                OriginalFilePath = fullPath, // 2중 저장을 안 하므로 FilePath와 동일하게 설정
                SourceApp = captureInfo.SourceApp ?? "Unknown",
                SourceTitle = captureInfo.SourceTitle ?? "Unknown",
                FileSize = System.IO.File.Exists(fullPath) ? new System.IO.FileInfo(fullPath).Length : 0,
                Resolution = $"{image.PixelWidth}x{image.PixelHeight}",
                IsFavorite = false,
                Status = 0,
                CreatedAt = DateTime.Now
            };

            long newId = DatabaseManager.Instance.InsertCapture(historyItem);
            captureInfo.HistoryId = newId;

            // [Hit & Run] 실시간 동기화 (히스토리 데이터 포함) + 락 해제
            _ = Task.Run(() =>
            {
                DatabaseManager.Instance.SyncToCloud(true);
                DatabaseManager.Instance.RemoveLock();
            });

            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] DB Insert Success. NewId={newId}\n");

            // 열려있는 히스토리 창이 있으면 갱신
            Application.Current.Dispatcher.Invoke(() =>
            {
                var historyWindow = Application.Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
                if (historyWindow != null)
                {
                    historyWindow.LoadHistory();
                }
            });
        }
        catch (Exception ex)
        {
            // 저장 경로 접근 불가 오류 감지
            bool isStorageError = ex.Message.Contains("database disk image is malformed") ||
                                  ex.Message.Contains("히스토리 데이터베이스가 아직 초기화되지 않았습니다") ||
                                  ex.Message.Contains("클라우드 드라이브가 로드될 때까지") ||
                                  ex is DirectoryNotFoundException ||
                                  ex is IOException;
            
            string errorMsg;
            string errorTitle;
            
            if (isStorageError)
            {
                errorTitle = Models.LocalizationManager.CurrentLanguage == "ko" ? "저장 경로 접근 불가" : "Storage Not Ready";
                
                if (Models.LocalizationManager.CurrentLanguage == "ko")
                {
                    errorMsg = "저장 경로에 접근할 수 없습니다.\n잠시 후 다시 시도해주세요.\n\n" +
                               "※ 외장 하드/USB가 연결되어 있는지 확인하세요.\n" +
                               "※ 클라우드 드라이브(OneDrive, Google Drive 등)가 로드될 때까지 기다려주세요.";
                }
                else
                {
                    errorMsg = "Cannot access storage path.\nPlease try again later.\n\n" +
                               "※ Check if external HDD/USB is connected.\n" +
                               "※ Wait for cloud drive (OneDrive, Google Drive, etc.) to load.";
                }
            }
            else
            {
                errorTitle = "SaveToHistory Error";
                errorMsg = $"{LocalizationManager.GetString("ErrHistorySave") ?? "히스토리 저장 실패"}: {ex.Message}\nStack: {ex.StackTrace}";
            }
            
            try { System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR: {errorMsg}\n원본 오류: {ex.Message}\n"); } catch { }
            
            System.Diagnostics.Debug.WriteLine(errorMsg);

            // CustomMessageBox 사용
            Application.Current.Dispatcher.Invoke(() => 
                CatchCapture.CustomMessageBox.Show(errorMsg, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    private void AddCaptureToList(BitmapSource image, bool skipPreview = false, bool showMainWindow = true, string? sourceApp = null, string? sourceTitle = null)
    {
        // [Fix] 백그라운드 스레드 처리를 위해 이미지 Freeze (Safe threading)
        if (image.CanFreeze) image.Freeze();

        try
        {
            var currentSettings = CatchCapture.Models.Settings.Load();
            CaptureImage captureImage;

            // ★ 메모리 최적화: 자동저장 시 썸네일만 메모리에 저장
            if (currentSettings.AutoSaveCapture)
            {
                string fullPath = GetAutoSaveFilePath(currentSettings.FileSaveFormat.ToLower(), sourceApp, sourceTitle);

                try
                {
                    CatchCapture.Utilities.ScreenCaptureUtility.SaveImageToFile(image, fullPath, currentSettings.ImageQuality);
                    var thumbnail = CaptureImage.CreateThumbnail(image, 200, 150);
                    
                    // 메타데이터 전달하여 생성
                    captureImage = new CaptureImage(thumbnail, fullPath, image.PixelWidth, image.PixelHeight, sourceApp, sourceTitle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"자동 저장 실패: {ex.Message}");
                    captureImage = new CaptureImage(image, sourceApp, sourceTitle);
                }
            }
            else
            {
                // 자동저장 OFF → 메타데이터 전달하여 생성
                captureImage = new CaptureImage(image, sourceApp, sourceTitle);
            }

            captures.Add(captureImage);

            // UI에 이미지 추가 - 최신 캡처를 위에 표시하기 위해 인덱스 0에 추가
            var border = CreateCaptureItem(captureImage, captures.Count - 1);
            CaptureListPanel.Children.Insert(0, border);

            // [추가] 클립보드 자동 복사 설정 확인
            if (currentSettings.AutoCopyToClipboard)
            {
                try
                {
                    // 원본 이미지(image)를 클립보드에 복사
                    CatchCapture.Utilities.ScreenCaptureUtility.CopyImageToClipboard(image);
                    
                    // [수정] 클립보드 복사 알림 표시
                    CatchCapture.Utilities.StickerWindow.Show(LocalizationManager.GetString("CopiedToClipboard"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"클립보드 자동 복사 실패: {ex.Message}");
                }
            }

            // 추가된 이미지 선택
            SelectCapture(border, captures.Count - 1);

            // 버튼 상태 업데이트
            UpdateButtonStates();


            // 간편모드가 활성화된 경우 (최우선 체크)
            if (simpleModeWindow != null)
            {
                // 간편모드에서는 캡처 개수만 업데이트하고 창을 표시하지 않음
                UpdateCaptureCount();

                // [Fix] 다이얼로그 종료 후 포커스 전환 시 깜빡임 방지를 위해 비동기 실행
                Dispatcher.BeginInvoke(new Action(() => {
                    // 캡처 도중 숨겨졌을 수 있으므로 다시 표시
                    if (simpleModeWindow != null && simpleModeWindow.Visibility != Visibility.Visible)
                    {
                        simpleModeWindow._suppressActivatedExpand = true;
                        simpleModeWindow.Show();
                        simpleModeWindow.Activate();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);

                // 캡처 후 편집창 자동 열기 (또는 미리보기)
                if (!skipPreview && (settings.OpenEditorAfterCapture || settings.ShowPreviewAfterCapture))
                {
                    ShowPreviewWindow(image, captures.Count - 1);
                }
            }
            // 트레이 모드일 때 처리
            else if (settings.IsTrayMode)
            {
                // 트레이 창이 없거나 닫혔으면 다시 생성/표시
                if (trayModeWindow == null || !trayModeWindow.IsLoaded)
                {
                    trayModeWindow = new TrayModeWindow(this);
                }
                
                // [Fix] 비동기 실행으로 깜빡임 방지
                Dispatcher.BeginInvoke(new Action(() => {
                    if (trayModeWindow != null)
                    {
                        trayModeWindow.Show();
                        trayModeWindow.Activate();
                        trayModeWindow.UpdateCaptureCount(captures.Count);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                // 트레이모드에서는 자동으로 클립보드에 복사
                try
                {
                    ScreenCaptureUtility.CopyImageToClipboard(image);
                    CatchCapture.Utilities.StickerWindow.Show(LocalizationManager.GetString("CopiedToClipboard"));
                }
                catch
                {
                    ShowGuideMessage(LocalizationManager.GetString("ClipboardCopyFailed"), TimeSpan.FromSeconds(3));
                }

                // 캡처 후 편집창 자동 열기 (또는 미리보기)
                if (!skipPreview && (settings.OpenEditorAfterCapture || settings.ShowPreviewAfterCapture))
                {
                    ShowPreviewWindow(image, captures.Count - 1);
                }
            }
            else
            {
                // 일반 모드: 창 표시
                UpdateCaptureCount();
                
                // [Fix] 비동기 실행으로 깜빡임 방지
                if (showMainWindow && !settings.ShowPreviewAfterCapture && !settings.OpenEditorAfterCapture)
                {
                    Dispatcher.BeginInvoke(new Action(() => {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                
                // 캡처 후 미리보기 표시 설정 확인
                if (!skipPreview && (settings.ShowPreviewAfterCapture || settings.OpenEditorAfterCapture))
                {
                    var preview = ShowPreviewWindow(image, captures.Count - 1);
                    preview.Closed += (s, e2) =>
                    {
                        if (_autoPreviewOpenCount > 0) _autoPreviewOpenCount--;
                        if (_autoPreviewOpenCount == 0)
                        {
                            // Check if preview requested minimize (e.g. saved to Note)
                            if (s is PreviewWindow pw && pw.RequestMainWindowMinimize) 
                            {
                                if (!settings.IsTrayMode)
                                {
                                    this.Show();
                                    this.WindowState = WindowState.Minimized;
                                }
                                return;
                            }

                            try
                            {
                                this.Show();
                                this.WindowState = WindowState.Normal;
                                this.Activate();
                            }
                            catch { }
                        }
                    };
                    _autoPreviewOpenCount++;
                    try { this.Hide(); } catch { }
                }
            }

            // 히스토리 DB 저장 (비동기) - 모든 모드에서 공통 실행
            Task.Run(() => SaveToHistory(captureImage, image));

            UpdateEmptyStateLogo();
        }
        catch (Exception ex)
        {
            // 저장 경로 접근 불가 오류 감지
            bool isStorageError = ex.Message.Contains("Could not find a part of the path") ||
                                  ex.Message.Contains("경로의 일부를 찾을 수 없습니다") ||
                                  ex is DirectoryNotFoundException ||
                                  ex is IOException;
            
            string errorMsg;
            string errorTitle;
            
            if (isStorageError)
            {
                errorTitle = Models.LocalizationManager.CurrentLanguage == "ko" ? "저장 경로 접근 불가" : "Storage Not Ready";
                
                if (Models.LocalizationManager.CurrentLanguage == "ko")
                {
                    errorMsg = "저장 경로에 접근할 수 없습니다.\n잠시 후 다시 시도해주세요.\n\n" +
                               "※ 외장 하드/USB가 연결되어 있는지 확인하세요.\n" +
                               "※ 클라우드 드라이브(OneDrive, Google Drive 등)가 로드될 때까지 기다려주세요.";
                }
                else
                {
                    errorMsg = "Cannot access storage path.\nPlease try again later.\n\n" +
                               "※ Check if external HDD/USB is connected.\n" +
                               "※ Wait for cloud drive (OneDrive, Google Drive, etc.) to load.";
                }
            }
            else
            {
                errorTitle = Models.LocalizationManager.Get("Error") ?? "오류";
                errorMsg = $"{Models.LocalizationManager.Get("ErrCapture") ?? "캡처 중 오류가 발생했습니다."}\n\n{ex.Message}";
            }
            
            CatchCapture.CustomMessageBox.Show(errorMsg, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        UpdateEmptyStateLogo();
    }
    private Border CreateCaptureItem(CaptureImage captureImage, int index)
{
    // [추가] 비디오/오디오 파일인 경우 전용 생성 로직으로 위임
    if (captureImage.IsVideo)
    {
        // AddVideoToList에서 이미 UI에 추가되었거나 RebuildCaptureList에서 호출된 경우
        // CreateVideoThumbnailItem은 Border 뿐만 아니라 인딩 오버레이도 반환하므로 주의
        // 여기서는 이미 인코딩이 완료된 상태거나 히스토리성 로드인 경우가 많음
        var (vItem, _) = CreateVideoThumbnailItem(captureImage.Image, captureImage.SavedPath);
        // 인코딩 오버레이는 로드 시점에는 숨김 (인코딩 상태 추적은 AddVideoToList 내 Task에서 수행)
        if (vItem.Child is Grid vGrid)
        {
             foreach (var child in vGrid.Children)
             {
                 if (child is Border b && b.Visibility == Visibility.Visible && b.Height == 24) // encodingOverlay 힌트
                 {
                     b.Visibility = Visibility.Collapsed;
                 }
             }
        }
        return vItem;
    }
    // 썸네일 크기 고정 (사용자 요청: 200x150)
        double thumbWidth = 200;
        double thumbHeight = 150;
        double imgHeight = currentViewMode == CaptureViewMode.Card ? 115 : 140;

        // 그리드 생성 (카드형의 경우 텍스트를 위해 행 분리)
        Grid grid = new Grid();
        if (currentViewMode == CaptureViewMode.Card)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // 이미지 컨트롤 생성
        Image image = new Image
        {
            Source = captureImage.Image,
            Width = thumbWidth - 10,
            Height = imgHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 5, 5, 2),
            SnapsToDevicePixels = true
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        // 인덱스를 태그로 저장
        image.Tag = index;

        // 더블 클릭 이벤트 (미리보기)
        image.MouseDown += async (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                int actualIndex = (int)((Image)s).Tag;
                // ★ 메모리 최적화: 원본 이미지 비동기 로드
                var original = await captureImage.GetOriginalImageAsync();
                ShowPreviewWindow(original, actualIndex);
                e.Handled = true;
            }
        };

        grid.Children.Add(image);
        if (currentViewMode == CaptureViewMode.Card) Grid.SetRow(image, 0);

        // 카드형인 경우 파일명 추가
        if (currentViewMode == CaptureViewMode.Card)
        {
            string fileName = "Unknown";
            if (!string.IsNullOrEmpty(captureImage.SavedPath))
            {
                fileName = IOPath.GetFileName(captureImage.SavedPath);
            }
            else if (!string.IsNullOrEmpty(captureImage.SourceTitle))
            {
                fileName = captureImage.SourceTitle;
            }

            Viewbox vb = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 4),
                MaxWidth = thumbWidth - 20,
                MaxHeight = 24
            };

            TextBlock fileNameText = new TextBlock
            {
                Text = fileName,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.None, 
                Opacity = 1.0
            };
            fileNameText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            RenderOptions.SetClearTypeHint(fileNameText, ClearTypeHint.Enabled);
            vb.Child = fileNameText;
            grid.Children.Add(vb);
            Grid.SetRow(vb, 1);
        }

        // 하단 정보 패널 (크기 텍스트 및 노트 저장 아이콘 연계)
        StackPanel bottomPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6)
        };

        // 이미지 크기 텍스트 보더
        Border sizeBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock sizeText = new TextBlock
        {
            Text = $"{captureImage.OriginalWidth} x {captureImage.OriginalHeight}",
            Foreground = Brushes.White,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        sizeBorder.Child = sizeText;
        bottomPanel.Children.Add(sizeBorder);
        grid.Children.Add(bottomPanel);
        if (currentViewMode == CaptureViewMode.Card) Grid.SetRow(bottomPanel, 0);

        // --- 호버 오버레이 버튼 패널 추가 ---
        StackPanel buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 5, 0),
            Visibility = Visibility.Collapsed // 평소엔 숨김
        };

        // 구글 버튼 생성
        Button googleBtn = new Button
        {
            Width = 28, Height = 28, Margin = new Thickness(0, 0, 5, 0),
            Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            Cursor = Cursors.Hand, ToolTip = LocalizationManager.GetString("GoogleSearch")
        };

        // 둥근 버튼 스타일 적용 (템플릿)
        var gTemplate = new ControlTemplate(typeof(Button));
        var gBorderFactory = new FrameworkElementFactory(typeof(Border));
        gBorderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        gBorderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        gBorderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        gBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        var gContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        gContentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        gContentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        gBorderFactory.AppendChild(gContentPresenter);
        gTemplate.VisualTree = gBorderFactory;
        googleBtn.Template = gTemplate;

        // 구글 아이콘 이미지 설정
        var googleIcon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Icons/google_img.png")),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(googleIcon, BitmapScalingMode.HighQuality);
        googleBtn.Content = googleIcon;

        // 클릭 이벤트 연결
        googleBtn.Click += async (s, e) =>
        {
            e.Handled = true;
            var original = await captureImage.GetOriginalImageAsync();
            SearchImageOnGoogle(original);
        };

        // 버튼 생성 헬퍼 함수
        Button CreateHoverButton(string iconPath, string toolTip)
        {
            var btn = new Button
            {
                Width = 28, Height = 28, Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                Cursor = Cursors.Hand, ToolTip = toolTip
            };

            // 둥근 버튼 스타일 적용
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;
            btn.Template = template;

            // 아이콘 설정
            var icon = new Image
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/icons/{iconPath}")),
                Width = 14, Height = 14, Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            btn.Content = icon;
            return btn;
        }

        // 저장 버튼 추가
        Button saveBtn = CreateHoverButton("save_selected.png", LocalizationManager.GetString("Save"));
        saveBtn.Click += (s, e) => { e.Handled = true; SaveImageToFile(captureImage); };

        // 삭제 버튼 추가
        Button deleteBtn = CreateHoverButton("delete_selected.png", LocalizationManager.GetString("Delete"));
        deleteBtn.Click += (s, e) =>
        {
            e.Handled = true;
            if (settings.ShowSavePrompt && !captureImage.IsSaved)
            {
                if (MessageBox.Show(LocalizationManager.GetString("UnsavedImageDeleteConfirm"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            // 리스트에서 제거 로직
           Border? currentBorder = null;
            foreach (var child in CaptureListPanel.Children) {
                if (child is Border b &&
                    b.Child == grid) { currentBorder = b; break; }
            }
            if (currentBorder != null) {
                // 히스토리 연동 삭제 (휴지통으로 이동)
                if (captureImage.HistoryId.HasValue)
                {
                    DatabaseManager.Instance.DeleteCapture(captureImage.HistoryId.Value, false);
                }

                CaptureListPanel.Children.Remove(currentBorder);
                captures.Remove(captureImage);
                UpdateCaptureItemIndexes();
                UpdateCaptureCount();
                UpdateButtonStates();
                selectedBorder = null; selectedIndex = -1;
            }
        };

        // 공유 버튼 추가
        Button shareBtn = CreateHoverButton("share_img.png", LocalizationManager.GetString("Share"));
        shareBtn.Click += async (s, e) => { 
            e.Handled = true; 
            var original = await captureImage.GetOriginalImageAsync();
            ShareImage(original); 
        };

        // 패널에 버튼 추가 (구글 -> 공유 -> 저장 -> 삭제)
        buttonPanel.Children.Add(googleBtn);
        buttonPanel.Children.Add(shareBtn);
        buttonPanel.Children.Add(saveBtn);
        buttonPanel.Children.Add(deleteBtn);
        grid.Children.Add(buttonPanel);
        if (currentViewMode == CaptureViewMode.Card) Grid.SetRow(buttonPanel, 0);

        // 내 노트 저장 버튼 추가 (하단 픽셀 정보 왼쪽에 위치하며 호버 시 표시)
        Button noteBtn = CreateHoverButton("my_note.png", LocalizationManager.GetString("SaveToMyNote"));
        noteBtn.Click += (s, e) => { e.Handled = true; SaveImageToNote(captureImage); };
        noteBtn.Visibility = Visibility.Collapsed;
        noteBtn.Margin = new Thickness(0, 0, 5, 0);
        bottomPanel.Children.Insert(0, noteBtn);

        // 메인 테두리 생성
        Border border = new Border
        {
            Child = grid,
            Margin = new Thickness(0, 8, 0, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 5, Opacity = 0.2, Direction = 270 },
            Tag = index,
            Width = thumbWidth, Height = thumbHeight
        };
        
        // 테마에 맞는 색상 적용
        border.SetResourceReference(Border.BackgroundProperty, "ThemeBackground");
        border.SetResourceReference(Border.BorderBrushProperty, "ThemeBorder");

        // 호버 이벤트 연결 (마우스 올리면 버튼 보임)
        border.MouseEnter += (s, e) => {
            buttonPanel.Visibility = Visibility.Visible;
            noteBtn.Visibility = Visibility.Visible;
        };
        border.MouseLeave += (s, e) => {
            buttonPanel.Visibility = Visibility.Collapsed;
            noteBtn.Visibility = Visibility.Collapsed;
        };

        // 드래그 시작점 추적을 위해 Preview이벤트 사용
        border.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _dragStartPoint = e.GetPosition(null);
            _isReadyToDrag = true;

            // 버튼 위에서 눌린 것이 아니면 드래그 준비
            DependencyObject? parent = e.OriginalSource as DependencyObject;
            while (parent != null && parent != border)
            {
                if (parent is Button)
                {
                    _isReadyToDrag = false;
                    break;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
        };

        // 클릭 이벤트 (선택)
        border.MouseLeftButtonDown += async (s, e) =>
        {

            // 더블클릭 시 미리보기 창 열기
            if (e.ClickCount == 2)
            {
                int actualIndex = (int)((Border)s).Tag;
                // ★ 메모리 최적화: 원본 이미지 비동기 로드
                var original = await captureImage.GetOriginalImageAsync();
                ShowPreviewWindow(original, actualIndex);
                e.Handled = true;
            }
            // 싱글클릭 시 선택
            else if (s is Border clickedBorder)
            {
                int clickedIndex = (int)clickedBorder.Tag;
                SelectCapture(clickedBorder, clickedIndex);
            }
        };

        // 드래그 앤 드롭 이벤트 추가
        border.MouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _isReadyToDrag)
            {
                Point currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isReadyToDrag = false; // 드래그 시작 후 플래그 해제
                    StartCaptureItemDrag(border, captureImage);
                }
            }
        };

        return border;
    }

    private async void StartCaptureItemDrag(Border border, CaptureImage captureImage)
    {
        try
        {
            // 1. 파일 경로 준비
            string filePath = captureImage.SavedPath;

            // 아직 저장되지 않았거나 파일이 없는 경우 임시 파일로 저장
            if (!captureImage.IsSaved || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                string tempFolder = IOPath.Combine(IOPath.GetTempPath(), "CatchCapture", "DragTemp");
                if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                filePath = IOPath.Combine(tempFolder, $"Capture_{timestamp}.png");

                // PNG로 저장 (비동기로 원본 로드)
                var originalImage = await captureImage.GetOriginalImageAsync();
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(originalImage));
                    encoder.Save(stream);
                }
            }

            // 2. DataObject 생성 (FileDrop 형식)
            var data = new DataObject(DataFormats.FileDrop, new string[] { filePath });

            // 3. 드래그 시작 (DoDragDrop은 블로킹 작업임)
            DragDrop.DoDragDrop(border, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Drag and drop start failed: {ex.Message}");
        }
    }


        public void ShareImage(BitmapSource image)
        {
            try
            {
                // 1. 임시 파일로 저장
                string tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CatchCapture");
                if (!System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.CreateDirectory(tempFolder);
                }

                string tempPath = System.IO.Path.Combine(tempFolder, $"share_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // PNG로 저장
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                {
                    encoder.Save(fs);
                }

                // 2. Windows Share 대화상자 호출 (Windows 10 이상)
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

                // DataTransferManager 가져오기
                var dataTransferManager = Windows.ApplicationModel.DataTransfer.DataTransferManagerInterop.GetForWindow(hwnd);

                dataTransferManager.DataRequested += async (s, args) =>
                {
                    var request = args.Request;
                    request.Data.Properties.Title = LocalizationManager.GetString("ShareImageTitle");
                    request.Data.Properties.Description = LocalizationManager.GetString("ShareImageDesc");

                    var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempPath);
                    request.Data.SetStorageItems(new[] { storageFile });
                    request.Data.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(storageFile));
                };

                Windows.ApplicationModel.DataTransfer.DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
            }
            catch (Exception ex)
            {
                // Windows Share가 지원되지 않는 경우 클립보드 복사로 대체
                try
                {
                    if (ScreenCaptureUtility.CopyImageToClipboard(image))
                    {
                        string msg = LocalizationManager.GetString("CopiedToClipboard");
                        if (msg == "CopiedToClipboard") msg = "클립보드에 복사 되었습니다.";
                        StickerWindow.Show(msg);
                    }
                }
                catch
                {
                    CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ShareError")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }



    private void SelectCapture(Border border, int index)
    {
        // 이전 선택 해제
        if (selectedBorder != null)
        {
            selectedBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeBorder");
        }

        // 새 선택 적용
        selectedBorder = border;
        selectedIndex = index;
        border.BorderBrush = Brushes.DodgerBlue;

        // 버튼 상태 업데이트
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        // 캡처 목록(이미지) 또는 UI 목록(동영상 포함)이 있는지 확인
        bool hasItems = CaptureListPanel.Children.Count > 0;
        bool hasSelection = selectedIndex >= 0;

        // 복사/저장 버튼은 "선택된 이미지" 기준이므로 기존 로직 유지 (동영상은 선택 개념이 다름)
        bool hasCaptures = captures.Count > 0;

        // 복사 버튼 상태 업데이트
        CopySelectedButton.IsEnabled = hasSelection;
        CopyAllButton.IsEnabled = hasCaptures;

        // 저장 버튼 상태 업데이트
        SaveAllButton.IsEnabled = hasCaptures;

        // 삭제 버튼 상태 업데이트 - 전체 삭제는 동영상 포함하여 지울 수 있어야 함
        DeleteAllButton.IsEnabled = hasItems;

        // 로고 및 안내 버튼 가시성 업데이트
        UpdateEmptyStateLogo();
    }

    private void UpdateCaptureCount()
    {
        Title = $"캐치 - 캡처 {captures.Count}개";
    }

    #endregion

    #region 복사 기능

    private void OpenMyNoteButton_Click(object sender, RoutedEventArgs e)
    {
        OpenNoteExplorer();
    }

    public void OpenNoteExplorer()
    {
        // 이미 열려있는지 확인
        if (NoteExplorerWindow.Instance != null && NoteExplorerWindow.Instance.IsLoaded)
        {
            NoteExplorerWindow.Instance.WindowState = WindowState.Normal;
            NoteExplorerWindow.Instance.Activate();
            return;
        }

        // 비밀번호 잠금 확인
    if (settings.IsNoteLockEnabled && !App.IsNoteAuthenticated && !string.IsNullOrEmpty(settings.NotePassword))
    {
        var lockWin = new NoteLockCheckWindow(settings.NotePassword, settings.NotePasswordHint);
            
            if (this.IsVisible)
            {
                lockWin.Owner = this;
                lockWin.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                lockWin.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                lockWin.Topmost = true;
            }

            if (lockWin.ShowDialog() != true)
            {
                return;
            }
        }

        // 내 노트 탐색기 열기 (독립 창으로 생성)
        var explorer = new NoteExplorerWindow();
        explorer.Show();
    }

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedImage();
    }

    private void OpenSaveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folderPath = Utilities.DatabaseManager.EnsureCatchCaptureSubFolder(settings.DefaultSaveFolder);
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            // 폴더 열기
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            CatchCapture.CustomMessageBox.Show($"저장 폴더를 열 수 없습니다: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void CopySelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            // ★ 메모리 최적화: 원본 이미지 로드 (썸네일 모드에서도 파일에서 원본 로드)
            var image = captures[selectedIndex].GetOriginalImage();
            if (ScreenCaptureUtility.CopyImageToClipboard(image))
            {
                // 스티커 알림 표시
                StickerWindow.Show(LocalizationManager.GetString("CopiedToClipboard"));
            }
        }
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        CopyAllImages();
    }

    public void CopyAllImages()
    {
        if (captures.Count == 0) return;

        // 모든 이미지를 세로로 결합
        int totalWidth = 0;
        int totalHeight = 0;

        foreach (var capture in captures)
        {
            var img = capture.GetOriginalImage();
            totalWidth = Math.Max(totalWidth, img.PixelWidth);
            totalHeight += img.PixelHeight;
        }

        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            int currentY = 0;
            foreach (var capture in captures)
            {
                var img = capture.GetOriginalImage();
                drawingContext.DrawImage(img, new Rect(0, currentY, img.PixelWidth, img.PixelHeight));
                currentY += img.PixelHeight;
            }
        }

        RenderTargetBitmap combinedImage = new RenderTargetBitmap(
            totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
        combinedImage.Render(drawingVisual);

        if (ScreenCaptureUtility.CopyImageToClipboard(combinedImage))
        {
            // 스티커 알림 표시
            StickerWindow.Show(LocalizationManager.GetString("CopiedToClipboard"));
        }
    }

    #endregion

    #region 저장 기능

    private void SaveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSelectedImage();
    }

    public void SaveSelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            SaveImageToFile(captures[selectedIndex]);
        }
    }

    private void SaveAllButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAllImages();
    }

    private void SaveImageToFile(CaptureImage captureImage)
    {
        // 파일명 템플릿 적용 (기본 이름 제안)
        string template = settings.FileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";
        string defaultFileName = template;
        DateTime now = DateTime.Now;

        // Sanitize helper
        string Sanitize(string s) {
            if (string.IsNullOrEmpty(s)) return "Unknown";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        try 
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\$(.*?)\$");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string fmt = match.Groups[1].Value;
                
                if (fmt.Equals("App", StringComparison.OrdinalIgnoreCase)) {
                        defaultFileName = defaultFileName.Replace(match.Value, Sanitize(captureImage.SourceApp ?? ""));
                        continue;
                }
                if (fmt.Equals("Title", StringComparison.OrdinalIgnoreCase)) {
                        string t = captureImage.SourceTitle ?? "";
                        if (t.Length > 50) t = t.Substring(0, 50);
                        defaultFileName = defaultFileName.Replace(match.Value, Sanitize(t));
                        continue;
                }

                try { defaultFileName = defaultFileName.Replace(match.Value, now.ToString(fmt)); } catch { }
            }
        } 
        catch { }

        // 설정에서 포맷 가져오기
        string format = settings.FileSaveFormat; // PNG, JPG, BMP, GIF, WEBP
        string ext = $".{format}";

        defaultFileName += ext;

        // 필터 생성 (설정된 포맷을 최우선으로)
        string filter = "모든 파일|*.*";
        if (format.Equals("PNG", StringComparison.OrdinalIgnoreCase))
            filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp|GIF 이미지|*.gif|WEBP 이미지|*.webp|" + filter;
        else if (format.Equals("JPG", StringComparison.OrdinalIgnoreCase))
            filter = "JPEG 이미지|*.jpg|PNG 이미지|*.png|BMP 이미지|*.bmp|GIF 이미지|*.gif|WEBP 이미지|*.webp|" + filter;
        else if (format.Equals("BMP", StringComparison.OrdinalIgnoreCase))
            filter = "BMP 이미지|*.bmp|PNG 이미지|*.png|JPEG 이미지|*.jpg|GIF 이미지|*.gif|WEBP 이미지|*.webp|" + filter;
        else if (format.Equals("GIF", StringComparison.OrdinalIgnoreCase))
            filter = "GIF 이미지|*.gif|PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp|WEBP 이미지|*.webp|" + filter;
        else if (format.Equals("WEBP", StringComparison.OrdinalIgnoreCase))
            filter = "WEBP 이미지|*.webp|PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp|GIF 이미지|*.gif|" + filter;

        var dialog = new SaveFileDialog
        {
            Title = LocalizationManager.GetString("SaveImage"),
            Filter = filter,
            DefaultExt = ext,
            FileName = defaultFileName,
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // 설정된 품질 사용
                // ★ 메모리 최적화: 원본 이미지 로드 (썸네일 모드에서도 파일에서 원본 로드)
                ScreenCaptureUtility.SaveImageToFile(captureImage.GetOriginalImage(), dialog.FileName, settings.ImageQuality);
                captureImage.IsSaved = true;
                captureImage.SavedPath = dialog.FileName;
                ShowGuideMessage(LocalizationManager.GetString("ImageSaved"), TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveImageToNote(CaptureImage captureImage)
    {
        try
        {
            // ★ 메모리 최적화: 원본 이미지 로드
            var image = captureImage.GetOriginalImage();
            if (image != null)
            {
                // 비밀번호 잠금 확인
                if (!string.IsNullOrEmpty(settings.NotePassword) && !App.IsNoteAuthenticated)
                {
                    var lockWin = new NoteLockCheckWindow(settings.NotePassword, settings.NotePasswordHint);
                    lockWin.Owner = this;
                    if (lockWin.ShowDialog() != true)
                    {
                        return;
                    }
                }

                // [Fix] 기존에 열린 노트 입력창이 있는지 확인 (새 노트 작성 중인 창 우선)
                var openNoteWin = Application.Current.Windows.OfType<NoteInputWindow>().FirstOrDefault(w => !w.IsEditMode);
                if (openNoteWin != null)
                {
                    openNoteWin.AppendImage(image);
                    openNoteWin.Activate();
                    openNoteWin.Focus();
                }
                else
                {
                    var noteWin = new NoteInputWindow(image, captureImage.SourceApp, captureImage.SourceTitle);
                    // noteWin.Owner = this; // Owner 제거
                    noteWin.Show();
                }
            }
        }
        catch (Exception ex)
        {
            CatchCapture.CustomMessageBox.Show($"노트 저장 중 오류가 발생했습니다: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SaveAllImages()
    {
        if (captures.Count == 0) return;

        // 저장 폴더 선택 (폴더 선택 다이얼로그 대용으로 SaveFileDialog 사용)
        // 실제로는 사용자가 폴더를 지정하기 위한 용도
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = LocalizationManager.GetString("SelectSaveFolder")
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string folderPath = dialog.SelectedPath;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 설정에서 포맷과 품질 가져오기
            string format = settings.FileSaveFormat.ToLower();
            string ext = $".{format}";
            int quality = settings.ImageQuality;

            for (int i = 0; i < captures.Count; i++)
            {
                string fileName = System.IO.Path.Combine(folderPath, $"캡처_{timestamp}_{i + 1}{ext}");
                ScreenCaptureUtility.SaveImageToFile(captures[i].GetOriginalImage(), fileName, quality);
                captures[i].IsSaved = true;
                captures[i].SavedPath = fileName;
            }

            ShowGuideMessage(LocalizationManager.GetString("AllImagesSaved"), TimeSpan.FromSeconds(1));
        }
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedImage();
    }

    public void DeleteSelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            if (!captures[selectedIndex].IsSaved && settings.ShowSavePrompt)
            {
                if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("UnsavedImageDeleteConfirm"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            int uiIndex = -1;
            for (int i = 0; i < CaptureListPanel.Children.Count; i++)
            {
                if (CaptureListPanel.Children[i] is Border border &&
                    border.Tag is int borderTag &&
                    borderTag == selectedIndex)
                {
                    uiIndex = i;
                    break;
                }
            }

            if (uiIndex >= 0)
            {
                // [메모리 최적화] UI 컨트롤에서 이미지 참조 끊기
                if (CaptureListPanel.Children[uiIndex] is Border border && border.Child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Image img)
                            img.Source = null;
                    }
                }

                CaptureListPanel.Children.RemoveAt(uiIndex);
            }

            // ★ 메모리 최적화: 삭제 전 Dispose 호출
            captures[selectedIndex].Dispose();

            // 히스토리 연동 삭제 (휴지통으로 이동)
            if (captures[selectedIndex].HistoryId is long historyId)
            {
                DatabaseManager.Instance.DeleteCapture(historyId, false);
            }

            captures.RemoveAt(selectedIndex);
            UpdateCaptureItemIndexes();
            UpdateCaptureCount();
            UpdateButtonStates();
            selectedBorder = null;
            selectedIndex = -1;

            UpdateEmptyStateLogo();

            // [메모리 최적화] 삭제 시 즉시 메모리 회수
            GC.Collect(0, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }
    }

    // 캡처 아이템 인덱스 업데이트
    private void UpdateCaptureItemIndexes()
    {
        for (int i = 0; i < CaptureListPanel.Children.Count; i++)
        {
            if (CaptureListPanel.Children[i] is Border border)
            {
                // 현재 태그를 가져와 인덱스 확인
                int tagIndex = -1;
                if (border.Tag is int index)
                {
                    tagIndex = index;
                }

                // 삭제된 아이템 이후의 인덱스는 1씩 감소
                if (tagIndex > selectedIndex)
                {
                    border.Tag = tagIndex - 1;

                    // 이미지의 태그도 업데이트
                    if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Image img)
                    {
                        img.Tag = tagIndex - 1;
                    }
                }
            }
        }
    }

    private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteAllImages();
        UpdateEmptyStateLogo();
    }

    public void DeleteAllImages()
    {
        // UI에 아이템이 하나도 없으면 리턴
        if (CaptureListPanel.Children.Count == 0) return;

        bool hasUnsavedImages = captures.Exists(c => !c.IsSaved);

        if (hasUnsavedImages && settings.ShowSavePrompt)
        {
            var result = CatchCapture.CustomMessageBox.Show(
                LocalizationManager.GetString("UnsavedImagesDeleteConfirm"),
                LocalizationManager.GetString("Confirm"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return;
            }
        }

        // [메모리 최적화] UI 컨트롤에서 이미지 참조 끊기
        foreach (var child in CaptureListPanel.Children)
        {
            if (child is Border border && border.Child is Grid grid)
            {
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is Image img)
                    {
                        img.Source = null; // 참조 해제
                    }
                }
            }
        }

        // 모델 데이터 정리 (Dispose 호출 및 히스토리 연동)
        foreach (var capture in captures)
        {
            if (capture.HistoryId.HasValue)
            {
                DatabaseManager.Instance.DeleteCapture(capture.HistoryId.Value, false);
            }
            capture.Dispose();
        }

        CaptureListPanel.Children.Clear();
        captures.Clear();
        selectedBorder = null;
        selectedIndex = -1;
        UpdateButtonStates();
        UpdateCaptureCount();

        UpdateEmptyStateLogo();

        // [메모리 최적화] 삭제 후 즉시 메모리 회수
        GC.Collect(0, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
    }

    public void OpenSettingsWindow()
    {
        var win = new SettingsWindow();
        win.Owner = this;
        var result = win.ShowDialog();
        if (result == true)
        {
            settings = Settings.Load();
            RegisterGlobalHotkeys();
            UpdateInstantEditToggleUI();
        }
    }
    #endregion

    #region 미리보기 기능

    private PreviewWindow ShowPreviewWindow(BitmapSource image, int index)
    {
        // [Fix] 이미 해당 인덱스의 창이 열려있는지 확인하여 중복 방지
        var existing = Application.Current.Windows.OfType<PreviewWindow>().FirstOrDefault(w => w.Tag is int idx && idx == index);
        if (existing != null)
        {
            existing.Show();
            existing.Activate();
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            return existing;
        }

        // 미리보기 창 생성
        PreviewWindow previewWindow = new PreviewWindow(image, index, captures);
        previewWindow.ImageUpdated += (sender, e) =>
        {
            if (e.Index >= 0 && e.Index < captures.Count)
            {
                // 이미지 업데이트
                captures[e.Index].Image = e.NewImage;

                // 썸네일 업데이트 - 데이터 인덱스에 해당하는 UI 인덱스 찾기
                for (int i = 0; i < CaptureListPanel.Children.Count; i++)
                {
                    if (CaptureListPanel.Children[i] is Border border &&
                        border.Tag is int borderTag &&
                        borderTag == e.Index)
                    {
                        if (border.Child is Grid grid &&
                            grid.Children.Count > 0 &&
                            grid.Children[0] is Image thumbnailImage)
                        {
                            thumbnailImage.Source = e.NewImage;
                        }
                        break;
                    }
                }
            }
        };

        previewWindow.Owner = this;
        previewWindow.Show();
    
        return previewWindow;
    }
    private void ShowGuideMessage(string message, TimeSpan? duration = null)
    {
        GuideWindow guideWindow = new GuideWindow(message, duration);
        guideWindow.Owner = this;
        guideWindow.Show();
    }

    // 구글 렌즈 검색 기능 (HTML 리다이렉트 방식 - 2025년 대응)
    // 구글 렌즈 검색 기능 (공용 유틸리티 사용)
    public void SearchImageOnGoogle(BitmapSource image)
    {
        try
        {
            CatchCapture.Utilities.GoogleSearchUtility.SearchImage(image);
            ShowGuideMessage(LocalizationManager.GetString("SearchingOnGoogle"), TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            // 실패 시 클립보드 폴백
            ScreenCaptureUtility.CopyImageToClipboard(image);
            CatchCapture.CustomMessageBox.Show($"검색 실행 실패: {ex.Message}\n이미지가 클립보드에 복사되었습니다.", LocalizationManager.GetString("Error"));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.google.com/imghp?hl=ko",
                UseShellExecute = true
            });
        }
    }



    // Windows 공유 기능
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
        public string lpVerb;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
        public string lpFile;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
        public string lpParameters;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private void RegisterGlobalHotkeys()
    {
        try
        {
            if (hwndSource == null) return;

            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            // 기존 단축키 해제
            UnregisterHotKey(hwnd, HOTKEY_ID_AREA);
            UnregisterHotKey(hwnd, HOTKEY_ID_FULLSCREEN);
            UnregisterHotKey(hwnd, HOTKEY_ID_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_OCR);
            UnregisterHotKey(hwnd, HOTKEY_ID_SCREENRECORD);
            UnregisterHotKey(hwnd, HOTKEY_ID_SIMPLE);
            UnregisterHotKey(hwnd, HOTKEY_ID_TRAY);
            UnregisterHotKey(hwnd, HOTKEY_ID_OPENEDITOR);
            UnregisterHotKey(hwnd, HOTKEY_ID_OPENNOTE);
            UnregisterHotKey(hwnd, HOTKEY_ID_EDGECAPTURE);
            UnregisterHotKey(hwnd, HOTKEY_ID_DELAY);

            // 설정에서 단축키 가져오기
            if (settings.Hotkeys.RegionCapture.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.RegionCapture);
                RegisterHotKey(hwnd, HOTKEY_ID_AREA, modifiers, key);
            }

            if (settings.Hotkeys.FullScreen.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.FullScreen);
                RegisterHotKey(hwnd, HOTKEY_ID_FULLSCREEN, modifiers, key);
            }

            if (settings.Hotkeys.DesignatedCapture.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.DesignatedCapture);
                RegisterHotKey(hwnd, HOTKEY_ID_WINDOW, modifiers, key);
            }

            if (settings.Hotkeys.OcrCapture.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.OcrCapture);
                RegisterHotKey(hwnd, HOTKEY_ID_OCR, modifiers, key);
            }

            if (settings.Hotkeys.ScreenRecord.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.ScreenRecord);
                RegisterHotKey(hwnd, HOTKEY_ID_SCREENRECORD, modifiers, key);
            }

            if (settings.Hotkeys.SimpleMode.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.SimpleMode);
                RegisterHotKey(hwnd, HOTKEY_ID_SIMPLE, modifiers, key);
            }

            if (settings.Hotkeys.TrayMode.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.TrayMode);
                RegisterHotKey(hwnd, HOTKEY_ID_TRAY, modifiers, key);
            }

            if (settings.Hotkeys.OpenEditor.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.OpenEditor);
                RegisterHotKey(hwnd, HOTKEY_ID_OPENEDITOR, modifiers, key);
            }

            if (settings.Hotkeys.OpenNote.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.OpenNote);
                RegisterHotKey(hwnd, HOTKEY_ID_OPENNOTE, modifiers, key);
            }

            if (settings.Hotkeys.EdgeCapture.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.EdgeCapture);
                RegisterHotKey(hwnd, HOTKEY_ID_EDGECAPTURE, modifiers, key);
            }

            if (settings.Hotkeys.DelayCapture.Enabled)
            {
                var (modifiers, key) = ConvertToggleHotkey(settings.Hotkeys.DelayCapture);
                RegisterHotKey(hwnd, HOTKEY_ID_DELAY, modifiers, key);
            }
        }
        catch (Exception ex)
        {
            CatchCapture.CustomMessageBox.Show($"단축키 등록 실패: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UnregisterGlobalHotkeys()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            UnregisterHotKey(hwnd, HOTKEY_ID_AREA);
            UnregisterHotKey(hwnd, HOTKEY_ID_FULLSCREEN);
            UnregisterHotKey(hwnd, HOTKEY_ID_WINDOW);
            UnregisterHotKey(hwnd, HOTKEY_ID_OCR);
            UnregisterHotKey(hwnd, HOTKEY_ID_SCREENRECORD);
            UnregisterHotKey(hwnd, HOTKEY_ID_SIMPLE);
            UnregisterHotKey(hwnd, HOTKEY_ID_TRAY);
            UnregisterHotKey(hwnd, HOTKEY_ID_OPENEDITOR);
            UnregisterHotKey(hwnd, HOTKEY_ID_OPENNOTE);

            hwndSource?.RemoveHook(HwndHook);
        }
        catch { /* 해제 중 오류 무시 */ }
    }

    private (uint modifiers, uint key) ConvertToggleHotkey(ToggleHotkey hotkey)
    {
        uint modifiers = 0;
        uint key = 0;

        if (hotkey.Ctrl) modifiers |= 0x0002; // MOD_CONTROL
        if (hotkey.Alt) modifiers |= 0x0001; // MOD_ALT
        if (hotkey.Shift) modifiers |= 0x0004; // MOD_SHIFT
        if (hotkey.Win) modifiers |= 0x0008; // MOD_WIN

        // 키 변환
        if (!string.IsNullOrEmpty(hotkey.Key))
        {
            if (hotkey.Key.Length == 1)
            {
                key = (uint)char.ToUpper(hotkey.Key[0]);
            }
            else if (hotkey.Key.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(hotkey.Key.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
            {
                key = (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
            }
        }

        return (modifiers, key);
    }

    private void RealTimeCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartRealTimeCaptureMode();
    }

    private const int HOTKEY_ID_REALTIME = 9003;
    private const int HOTKEY_ID_REALTIME_CANCEL = 9004;
    private bool wasInTrayModeBeforeRealTimeCapture = false;
// --- Low Level Hook for F1 ---

    private const int VK_F1 = 0x70;
    private const int VK_ESCAPE = 0x1B;

    private LowLevelKeyboardProc? _f1HookProc;
    private IntPtr _f1HookID = IntPtr.Zero;

    private bool _showMainWindowAfterRealTimeCapture = true;
    private void StartRealTimeCaptureMode(bool showMainWindowAfter = true)
    {
        // 원래 모드 기억
        _showMainWindowAfterRealTimeCapture = showMainWindowAfter;
        wasInTrayModeBeforeRealTimeCapture = settings.IsTrayMode;

        // 캡처 시작 전 메인 창이 보인다면 일반 모드로 확실히 설정
        if (this.Visibility == Visibility.Visible)
        {
            settings.IsTrayMode = false;
            Settings.Save(settings);
        }

        // 메인 창 숨기기
        this.Hide();

        // 안내 메시지 표시
        var guide = new GuideWindow(LocalizationManager.GetString("RealTimeF1Guide"), null);
        guide.Show();

        // 훅 설치
        _f1HookProc = F1HookCallback;

        // GetModuleHandle(null) returns the handle to the file used to create the calling process (.exe)
        IntPtr hModule = GetModuleHandle(null);
        _f1HookID = SetWindowsHookEx(WH_KEYBOARD_LL, _f1HookProc, hModule, 0);

        if (_f1HookID == IntPtr.Zero)
        {
            // 훅 설치 실패 시 에러 표시 및 복구
            MessageBox.Show("Failed to install keyboard hook.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            guide.Close();
            this.Show();
        }
    }

    private IntPtr F1HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
            if (vkCode == VK_F1 || vkCode == VK_ESCAPE)
            {
                // 훅 제거 (즉시 수행)
                if (_f1HookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_f1HookID);
                    _f1HookID = IntPtr.Zero;
                }

                bool isF1 = (vkCode == VK_F1);

                // UI 작업은 비동기로 처리하여 훅 체인을 차단하지 않음
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    // 안내창 닫기
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is GuideWindow) win.Close();
                    }

                    if (isF1)
                    {
                        // 영역 캡처 시작
                        try
                        {
                            await StartAreaCaptureAsync(0, _showMainWindowAfterRealTimeCapture);
                        }
                        finally
                        {
                            if (wasInTrayModeBeforeRealTimeCapture)
                            {
                                settings.IsTrayMode = true;
                                Settings.Save(settings);
                                wasInTrayModeBeforeRealTimeCapture = false;
                            }
                        }
                    }
                    else // Escape
                    {
                        // 취소 시 메인 창 복원
                        this.Show();
                    }
                }));

                return (IntPtr)1; // 키 입력 무시
            }
        }
        return CallNextHookEx(_f1HookID, nCode, wParam, lParam);
    }
    // Windows 메시지 처리 (단축키 이벤트 수신)
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();

            switch (id)
            {
                case HOTKEY_ID_AREA:
                    Dispatcher.Invoke(() => StartAreaCapture());
                    handled = true;
                    break;

                case HOTKEY_ID_FULLSCREEN:
                    Dispatcher.Invoke(() => CaptureFullScreen());
                    handled = true;
                    break;

                case HOTKEY_ID_WINDOW:
                    Dispatcher.Invoke(() => DesignatedCaptureButton_Click(this, new RoutedEventArgs()));
                    handled = true;
                    break;

                case HOTKEY_ID_OCR:
                    Dispatcher.Invoke(() => OcrCaptureButton_Click(this, new RoutedEventArgs()));
                    handled = true;
                    break;

                case HOTKEY_ID_SCREENRECORD:
                    Dispatcher.Invoke(() => ScreenRecordButton_Click(this, new RoutedEventArgs()));
                    handled = true;
                    break;

                case HOTKEY_ID_SIMPLE:
                    Dispatcher.Invoke(() => ToggleSimpleMode());
                    handled = true;
                    break;

                case HOTKEY_ID_TRAY:
                    Dispatcher.Invoke(() => SwitchToTrayMode());
                    handled = true;
                    break;

                case HOTKEY_ID_OPENEDITOR:
                    Dispatcher.Invoke(() => SwitchToNormalMode());
                    handled = true;
                    break;

                case HOTKEY_ID_OPENNOTE:
                    Dispatcher.Invoke(() => OpenNoteExplorer());
                    handled = true;
                    break;

                case HOTKEY_ID_EDGECAPTURE:
                    Dispatcher.Invoke(() => {
                         var (pRadius, _, _, _) = CatchCapture.Utilities.EdgeCaptureHelper.GetPresetSettings(settings.EdgeCapturePresetLevel);
                         StartAreaCaptureAsync(pRadius);
                    });
                    handled = true;
                    break;

                case HOTKEY_ID_DELAY:
                    Dispatcher.Invoke(() => DelayCaptureButton_Click(this, new RoutedEventArgs()));
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    // 변경된 로직: 무조건적인 타이머 대신, 사용자가 윈도우 위에서 활동할 때만 프리로딩 수행
    private void InitializeScreenshotCache()
    {
        // 윈도우(또는 메인 컨테이너)에 마우스가 들어오거나 움직일 때 프리로딩 트리거
        this.MouseMove += (s, e) => CheckAndPreloadScreenshot();
        this.MouseEnter += (s, e) => CheckAndPreloadScreenshot();
    }

    private void CheckAndPreloadScreenshot()
    {
        var now = DateTime.Now;

        // ★ 성능 최적화: 프리로딩 간격 대폭 확대 (1초 -> 10초) 및 비활성 상태 시 스킵
        if ((now - lastScreenshotTime).TotalSeconds < 10) return;
        if (!this.IsActive) return;

        // 이미 진행 중인 백그라운드 작업이 있다면 스킵
        if (isPreloading) return;

        isPreloading = true;

        // 백그라운드 스레드에서 캡처 수행
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // 전체 화면 캡처
                var newScreenshot = ScreenCaptureUtility.CaptureScreen();
                newScreenshot.Freeze(); // 다른 스레드에서 접근 가능하도록 Freeze

                // UI 스레드에서 캐시 업데이트
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    cachedScreenshotRef = new WeakReference<BitmapSource>(newScreenshot);
                    lastScreenshotTime = DateTime.Now;
                    isPreloading = false;
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch
            {
                isPreloading = false;
            }
        });
    }

    #endregion

    #region 간편 모드

    private void SimpleModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSimpleMode();
    }

    private void ThemeCycleButton_Click(object sender, RoutedEventArgs e)
    {
        if (settings == null) return;

        string[] modes = { "General", "Dark", "Light", "Blue", "Custom" };
        int currentIndex = Array.IndexOf(modes, settings.ThemeMode);
        if (currentIndex == -1) currentIndex = 0;

        int nextIndex = (currentIndex + 1) % modes.Length;
        settings.ThemeMode = modes[nextIndex];

        // 테마별 기본 색상 설정
        if (settings.ThemeMode == "General")
        {
            settings.ThemeBackgroundColor = "#FFFFFF";
            settings.ThemeTextColor = "#333333";
        }
        else if (settings.ThemeMode == "Dark")
        {
            settings.ThemeBackgroundColor = "#1E1E1E";
            settings.ThemeTextColor = "#CCCCCC";
        }
        else if (settings.ThemeMode == "Light")
        {
            settings.ThemeBackgroundColor = "#F5F7FA";
            settings.ThemeTextColor = "#2C3E50";
        }
        else if (settings.ThemeMode == "Blue")
        {
            settings.ThemeBackgroundColor = "#E3F2FD";
            settings.ThemeTextColor = "#1565C0";
        }
        else if (settings.ThemeMode == "Custom")
        {
            // 사용자 지정 테마 복원
            settings.ThemeBackgroundColor = settings.CustomThemeBackgroundColor ?? "#FFFFFF";
            settings.ThemeTextColor = settings.CustomThemeTextColor ?? "#333333";
        }

        // 설정 저장
        Settings.Save(settings);

        // 테마 즉시 적용
        App.ApplyTheme(settings);
    }

    private void ToggleSimpleMode()
    {
        if (simpleModeWindow == null || !simpleModeWindow.IsVisible)
        {
            ShowSimpleMode();
        }
        else
        {
            HideSimpleMode();
        }
    }

    private void ShowSimpleMode()
    {
        // ★ 간편모드 상태 저장 (맨 처음에 추가)
        settings.LastActiveMode = "Simple";
        Settings.Save(settings);
        if (simpleModeWindow == null)
        {
            simpleModeWindow = new SimpleModeWindow(this);
            // 창이 완전히 닫히면 참조 해제
            simpleModeWindow.Closed += (s, e) => { simpleModeWindow = null; };
            // 간편모드를 작업표시줄 대표로 사용하기 위해 Owner 해제 및 Taskbar 표시

            // 이벤트 핸들러 등록
            simpleModeWindow.AreaCaptureRequested += async (s, e) =>
            {
                // 즉시편집 설정 확인
                var currentSettings = Settings.Load();
                bool instantEdit = currentSettings.SimpleModeInstantEdit;

                // 캐시된 스크린샷을 사용하여 빠른 영역 캡처
                var cachedScreen = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

                // SnippingWindow 표시 (여기서 사용자가 영역 선택)
                using var snippingWindow = new SnippingWindow(false, cachedScreen);

                // 즉시편집 모드 활성화 (ShowDialog 전에 설정)
                if (instantEdit)
                {
                    snippingWindow.EnableInstantEditMode();
                }

                if (snippingWindow.ShowDialog() == true)
                {
                    // 선택된 영역 캡처 - 동결된 프레임 우선 사용
                    var selectedArea = snippingWindow.SelectedArea;
                    var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                    // 클립보드에 복사
                    ScreenCaptureUtility.CopyImageToClipboard(capturedImage);

                    // 캡처 목록에 추가
                    AddCaptureToList(capturedImage);

                    // 간편모드 창 다시 표시 및 알림
                    simpleModeWindow?.Show();
                    simpleModeWindow?.Activate();
                    if (simpleModeWindow != null)
                    {
                        simpleModeWindow.Topmost = true;
                        // 여기서 알림 표시
                        // notification.Show(); // 알림 제거
                    }
                }
                else
                {
                    // 캡처 취소 시 간편모드 창 다시 표시
                    simpleModeWindow?.Show();
                    simpleModeWindow?.Activate();
                }
            };

            simpleModeWindow.FullScreenCaptureRequested += (s, e) =>
            {
                // 전체화면 캡처 수행
                FlushUIAfterHide();

                var capturedImage = ScreenCaptureUtility.CaptureScreen();

                // 클립보드에 복사
                ScreenCaptureUtility.CopyImageToClipboard(capturedImage);

                // 캡처 목록에 추가
                AddCaptureToList(capturedImage);

                // 간편모드 창 다시 표시
                simpleModeWindow?.Show();
                simpleModeWindow?.Activate();
            };

            simpleModeWindow.DesignatedCaptureRequested += (s, e) =>
            {
                // 간편모드 전용 지정캡처 로직 (메인창 표시하지 않음)
                PerformDesignatedCaptureForSimpleMode();
            };

            simpleModeWindow.ExitSimpleModeRequested += (s, e) =>
            {
                HideSimpleMode();
                this.Show();
                this.Activate();
            };
        }

        // 사용자 요청: 화면 정중앙에 표시
        var workArea = SystemParameters.WorkArea;
        simpleModeWindow.Left = (workArea.Width - simpleModeWindow.Width) / 2 + workArea.Left;
        simpleModeWindow.Top = (workArea.Height - simpleModeWindow.Height) / 2 + workArea.Top;

        // 작업표시줄 대표를 간편모드로 전환
        this.ShowInTaskbar = false;   // 본체는 작업표시줄에서 숨김
        this.Hide();                  // 본체 창 숨김 (복원 방지)

        simpleModeWindow.ShowInTaskbar = true; // 간편모드를 작업표시줄 대표로
        simpleModeWindow.Topmost = true;
        simpleModeWindow.Show();
        simpleModeWindow.Activate();

        // 앱의 MainWindow를 간편모드로 전환하여 작업표시줄 포커스가 간편모드로 가도록 함
        Application.Current.MainWindow = simpleModeWindow;
    }

    private void HideSimpleMode()
    {
        // ★ 간편 모드를 종료하고 일반 모드로 돌아오므로 설정을 Normal로 업데이트
        settings.LastActiveMode = "Normal";
        Settings.Save(settings);

        if (simpleModeWindow != null)
        {
            // 작업표시줄 대표를 다시 본체로 복구
            simpleModeWindow.ShowInTaskbar = false;
            // Close() 대신 Hide()를 사용하여 프로그램 종료 방지
            simpleModeWindow.Hide();
        }

        // 메인 창 복원 및 작업표시줄 아이콘 다시 표시
        this.ShowInTaskbar = true;
        this.WindowState = WindowState.Normal;
        this.Show();
        this.Activate();

        // 앱의 MainWindow를 본체로 복구
        Application.Current.MainWindow = this;
    }

    // SimpleModeWindow가 숨겨졌을 때 호출되는 메서드
    public void OnSimpleModeHidden()
    {
        // simpleModeWindow 참조는 유지하되, 상태만 업데이트
        // (다음에 트레이 아이콘 클릭 시 간편모드를 다시 표시하기 위함)
    }

    // 간편모드가 떠 있는 동안 작업표시줄 클릭으로 본체가 튀어나오지 않도록 제어
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (simpleModeWindow != null && simpleModeWindow.IsVisible)
        {
            if (this.WindowState != WindowState.Minimized)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.WindowState = WindowState.Minimized;
                    // 간편모드를 앞으로
                    if (simpleModeWindow != null)
                    {
                        simpleModeWindow.Topmost = false;
                        simpleModeWindow.Topmost = true;
                    }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
    }

    // 간편모드가 떠 있는 동안 본체가 활성화되면 다시 최소화하고 간편모드를 전면으로
    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        // 간편모드가 열려있으면 메인윈도우를 최소화하고 간편모드를 앞으로
        if (simpleModeWindow != null && simpleModeWindow.IsVisible)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                this.WindowState = WindowState.Minimized;
                if (simpleModeWindow != null)
                {
                    simpleModeWindow.Topmost = false;
                    simpleModeWindow.Topmost = true;
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    #endregion

    // 사이드바 설정 버튼 클릭
    private void SettingsSideButton_Click(object sender, RoutedEventArgs e)
    {
        // 현재 모드 저장
        var previousMode = settings.LastActiveMode;

        var win = new SettingsWindow();
        win.Owner = this;
        var result = win.ShowDialog();
        if (result == true)
        {
            // Reload updated settings so hotkeys and options apply immediately
            settings = Settings.Load();
            // 단축키 재등록
            RegisterGlobalHotkeys();
            UpdateInstantEditToggleUI();

            // ★ 모드가 변경된 경우 자동으로 전환
            if (previousMode != settings.LastActiveMode)
            {
                switch (settings.LastActiveMode)
                {
                    case "Normal":
                        SwitchToNormalMode();
                        break;
                    case "Tray":
                        SwitchToTrayMode();
                        break;
                    case "Simple":
                        SwitchToSimpleMode();
                        break;
                }
            }
        }
    }

        // 편집열기 토글 클릭 이벤트
        private void OpenEditorToggle_Click(object sender, MouseButtonEventArgs e)
        {
            // 설정값 토글
            settings.OpenEditorAfterCapture = !settings.OpenEditorAfterCapture;
            settings.Save();

            // UI 업데이트
            UpdateOpenEditorToggleUI();
        }

        // 편집열기 토글 UI 업데이트
        private void UpdateOpenEditorToggleUI()
        {
            if (settings.OpenEditorAfterCapture)
            {
                // 켜짐 상태 (파란색)
                OpenEditorToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4361EE"));
                OpenEditorToggleCircle.HorizontalAlignment = HorizontalAlignment.Right;
                OpenEditorToggleCircle.Margin = new Thickness(0, 0, 2, 0);
            }
            else
            {
                // 꺼짐 상태 (회색)
                OpenEditorToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                OpenEditorToggleCircle.HorizontalAlignment = HorizontalAlignment.Left;
                OpenEditorToggleCircle.Margin = new Thickness(2, 0, 0, 0);
            }
        }


    private static bool MatchHotkey(CatchCapture.Models.ToggleHotkey hk, KeyEventArgs e)
    {
        if (!hk.Enabled) return false;
        // Normalize key text to uppercase single token
        var keyText = (hk.Key ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(keyText)) return false;

        // Modifier check
        var mods = Keyboard.Modifiers;
        if ((hk.Ctrl && (mods & ModifierKeys.Control) == 0) || (!hk.Ctrl && (mods & ModifierKeys.Control) != 0)) return false;
        if ((hk.Shift && (mods & ModifierKeys.Shift) == 0) || (!hk.Shift && (mods & ModifierKeys.Shift) != 0)) return false;
        if ((hk.Alt && (mods & ModifierKeys.Alt) == 0) || (!hk.Alt && (mods & ModifierKeys.Alt) != 0)) return false;
        if (hk.Win && (mods & ModifierKeys.Windows) == 0) return false;
        if (!hk.Win && (mods & ModifierKeys.Windows) != 0) return false;

        // Key check: accept letters and function keys
        var pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;
        string pressedName = pressedKey.ToString().ToUpperInvariant();
        if (pressedName.Length == 1)
        {
            // Single character letter/digit
            return pressedName == keyText;
        }
        else
        {
            // F1..F24 etc.
            return pressedName == keyText;
        }
    }

    private bool HandleSettingsHotkeys(KeyEventArgs e)
    {
        var hk = settings.Hotkeys;
        // 영역 캡처
        if (MatchHotkey(hk.RegionCapture, e))
        {
            StartAreaCapture();
            return true;
        }
        // 지연 캡처: 설정값만큼 지연 후 실행
        if (MatchHotkey(hk.DelayCapture, e))
        {
            StartDelayedAreaCaptureSeconds(settings.DelayCaptureSeconds);
            return true;
        }
        // 전체화면
        if (MatchHotkey(hk.FullScreen, e))
        {
            CaptureFullScreen();
            return true;
        }
        // 지정캡처
        if (MatchHotkey(hk.DesignatedCapture, e))
        {
            DesignatedCaptureButton_Click(this, new RoutedEventArgs());
            return true;
        }
        // 전체저장
        if (MatchHotkey(hk.SaveAll, e))
        {
            SaveAllImages();
            return true;
        }
        // 전체삭제
        if (MatchHotkey(hk.DeleteAll, e))
        {
            DeleteAllImages();
            return true;
        }
        // 간편모드 토글
        if (MatchHotkey(hk.SimpleMode, e))
        {
            ToggleSimpleMode();
            return true;
        }
        // 트레이 모드 전환
        if (MatchHotkey(hk.TrayMode, e))
        {
            SwitchToTrayMode();
            return true;
        }
        // 에디터 열기
        if (MatchHotkey(hk.OpenEditor, e))
        {
            SwitchToNormalMode();
            return true;
        }
        // 설정 열기
        if (MatchHotkey(hk.OpenSettings, e))
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
            // Reload settings after potential changes
            settings = Settings.Load();
            RegisterGlobalHotkeys();
            return true;
        }
        // 노트 열기
        if (MatchHotkey(hk.OpenNote, e))
        {
            OpenNoteExplorer();
            return true;
        }
        // 녹화 시작/정지 (F3)
        if (MatchHotkey(hk.RecordingStartStop, e))
        {
            if (activeRecordingWindow != null && activeRecordingWindow.IsVisible)
            {
                // Use reflection or a public method to trigger record button click
                // For now, we assume F3 is handled globally by RecordingWindow itself
                // but if MainWindow has focus, it might need to pass it or at least not block it.
            }
            return false; // Let it bubble up or be handled by global hotkey
        }
        return false;
    }

    public void TriggerRealTimeCapture()
    {
        StartRealTimeCaptureMode();
    }
    // 설정기반 지연 캡처(초)
    private void StartDelayedAreaCaptureSeconds(int seconds, bool showMainWindowAfter = true)
    {
        // [추가된 코드 시작] -----------------------------------------
        // 지연 캡처 시작 전 메인 창이 보인다면 일반 모드로 확실히 설정
        if (this.Visibility == Visibility.Visible)
        {
            settings.IsTrayMode = false;
            Settings.Save(settings);
        }
        // [추가된 코드 끝] -------------------------------------------

        if (seconds <= 0)
        {
            StartAreaCapture(showMainWindowAfter);
            return;
        }
        var countdown = new GuideWindow("", null)
        {
            Owner = this
        };
        countdown.Show();
        countdown.StartCountdown(seconds, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(() => StartAreaCapture(showMainWindowAfter));
        });
    }

    private void CleanupMainWindowResources()
    {
        try
        {
            // 전역 핫키 해제
            UnregisterGlobalHotkeys();

            // 간편 모드 창 정리
            if (simpleModeWindow != null)
            {
                simpleModeWindow.Close();
                simpleModeWindow = null;
            }

            // 캡처 이미지들의 메모리 정리
            foreach (var capture in captures)
            {
                capture?.Dispose();
            }
            captures.Clear();

            // UI 요소들 정리
            CaptureListPanel?.Children.Clear();

            // 이벤트 핸들러 해제
            this.MouseLeftButtonDown -= Window_MouseLeftButtonDown;
            this.StateChanged -= MainWindow_StateChanged;
            this.Activated -= MainWindow_Activated;
            this.KeyDown -= MainWindow_KeyDown;

            // 언어 변경 이벤트 해제
            LocalizationManager.LanguageChanged -= MainWindow_LanguageChanged;

            // 강제 가비지 컬렉션으로 메모리 누수 방지
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch { /* 정리 중 오류 무시 */ }
    }

    #region Trigger Methods for TrayModeWindow

    public void TriggerDelayCapture(int seconds = -1)
    {
        int delay = (seconds >= 0) ? seconds : settings.DelayCaptureSeconds;

        // 실시간 카운트다운 표시 후 캡처 시작
        if (delay <= 0)
        {
            StartAreaCapture();
            return;
        }

        var countdown = new GuideWindow("", null!)
        {
            Owner = null, // 트레이 모드에서는 Owner를 null로 설정
            Topmost = true
        };
        countdown.Show();
        countdown.StartCountdown(delay, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(StartAreaCapture);
        });
    }
    private void UpdateEmptyStateLogo()
    {
        if (EmptyStatePanel != null)
        {
            // 캡처 목록(이미지, 동영상, MP3 등)이 비어있으면 로고와 버튼 표시, 아니면 숨김
            EmptyStatePanel.Visibility = CaptureListPanel.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // 카드 모드에서는 가로 배치, 리스트 모드에서는 세로 배치
            if (EmptyStateActionPanel != null)
            {
                if (currentViewMode == CaptureViewMode.Card)
                {
                    EmptyStateActionPanel.Orientation = Orientation.Horizontal;
                    BigOpenNoteButton.Margin = new Thickness(10, 0, 10, 0);
                    BigHistoryButton.Margin = new Thickness(10, 0, 10, 0);
                }
                else
                {
                    EmptyStateActionPanel.Orientation = Orientation.Vertical;
                    BigOpenNoteButton.Margin = new Thickness(0, 5, 0, 5);
                    BigHistoryButton.Margin = new Thickness(0, 5, 0, 5);
                }
            }
        }
    }
    public void TriggerAreaCapture()
    {
        AreaCaptureButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerFullScreenCapture()
    {
        FullScreenCaptureButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerDesignatedCapture()
    {
        DesignatedCaptureButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerWindowCapture()
    {
        WindowCaptureButton_Click(this, new RoutedEventArgs());
    }

    public void TriggerUnitCapture()
    {
        ElementCaptureButton_Click(this, new RoutedEventArgs());
    }
    public void TriggerScrollCapture()
    {
        ScrollCaptureButton_Click(this, new RoutedEventArgs());
    }
    public void TriggerMultiCapture()
    {
        MultiCaptureButton_Click(this, new RoutedEventArgs());
    }

    public void TriggerOcrCapture()
    {
        OcrCaptureButton_Click(this, new RoutedEventArgs());
    }
    public void TriggerCopySelected()
    {
        CopySelectedButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerCopyAll()
    {
        CopyAllButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerSaveSelected()
    {
        SaveSelectedButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerSaveAll()
    {
        SaveAllButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerDeleteSelected()
    {
        DeleteSelectedButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerDeleteAll()
    {
        DeleteAllButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerSimpleMode()
    {
        SimpleModeButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerSettings()
    {
        SettingsSideButton_Click(null!, new RoutedEventArgs());
    }

    public void TriggerScreenRecord()
    {
        ScreenRecordButton_Click(null!, new RoutedEventArgs());
    }

    private string GetLocalizedTooltip(string key)
    {
        string tooltipKey = key + "Tooltip";
        string tt = LocalizationManager.GetString(tooltipKey);
        if (tt == tooltipKey) // Not found
        {
            return LocalizationManager.GetString(key);
        }
        return tt;
    }

    #endregion

    private void SetButtonText(System.Windows.Controls.Button? button, string text)
    {
        if (button == null) return;
        if (button.Content is System.Windows.Controls.StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is System.Windows.Controls.TextBlock tb)
                {
                    tb.Text = text;
                    break;
                }
            }
        }
    }

    private void MainWindow_LanguageChanged(object? sender, EventArgs e)
    {
        // UI 스레드에서 안전하게 텍스트 업데이트
        Dispatcher.Invoke(() =>
        {
            UpdateUIText();
            UpdateTrayMenuTexts();
        });
    }
    private void UpdateUIText()
    {
        // 1. 윈도우 제목
        this.Title = LocalizationManager.GetString("AppTitle");
        if (TitleBarAppNameText != null) TitleBarAppNameText.Text = LocalizationManager.GetString("AppTitle");

        if (InstantEditLabel != null) InstantEditLabel.Text = LocalizationManager.GetString("InstantEdit");
        if (InstantEditPanel != null) InstantEditPanel.ToolTip = GetLocalizedTooltip("InstantEdit");
        if (InstantEditLabel != null && InstantEditPanel == null) InstantEditLabel.ToolTip = GetLocalizedTooltip("InstantEdit");

        if (OpenEditorLabel != null) OpenEditorLabel.Text = LocalizationManager.GetString("OpenEditor");
        if (OpenEditorPanel != null) OpenEditorPanel.ToolTip = GetLocalizedTooltip("OpenEditor");
        if (OpenEditorLabel != null && OpenEditorPanel == null) OpenEditorLabel.ToolTip = GetLocalizedTooltip("OpenEditor");

        // 2. 메인 버튼들
        if (AreaCaptureButtonText != null) AreaCaptureButtonText.Text = LocalizationManager.GetString("AreaCapture");
        if (AreaCaptureButton != null) AreaCaptureButton.ToolTip = GetLocalizedTooltip("AreaCapture");

        if (DelayCaptureButtonText != null) DelayCaptureButtonText.Text = LocalizationManager.GetString("DelayCapture");
        if (DelayCaptureButton != null) DelayCaptureButton.ToolTip = GetLocalizedTooltip("DelayCapture");

        if (EdgeCaptureButtonText != null) EdgeCaptureButtonText.Text = LocalizationManager.GetString("EdgeCapture");
        if (EdgeCaptureButton != null) EdgeCaptureButton.ToolTip = GetLocalizedTooltip("EdgeCapture");

        if (Delay3Menu != null) Delay3Menu.Header = LocalizationManager.GetString("Delay3Sec");
        if (Delay5Menu != null) Delay5Menu.Header = LocalizationManager.GetString("Delay5Sec");
        if (Delay10Menu != null) Delay10Menu.Header = LocalizationManager.GetString("Delay10Sec");

        if (RealTimeCaptureButtonText != null) RealTimeCaptureButtonText.Text = LocalizationManager.GetString("RealTimeCapture");
        if (RealTimeCaptureButton != null) RealTimeCaptureButton.ToolTip = GetLocalizedTooltip("RealTimeCapture");

        if (MultiCaptureButtonText != null) MultiCaptureButtonText.Text = LocalizationManager.GetString("MultiCapture");
        if (MultiCaptureButton != null) MultiCaptureButton.ToolTip = GetLocalizedTooltip("MultiCapture");

        if (FullScreenButtonText != null) FullScreenButtonText.Text = LocalizationManager.GetString("FullScreen");
        if (FullScreenCaptureButton != null) FullScreenCaptureButton.ToolTip = GetLocalizedTooltip("FullScreen");

        if (DesignatedCaptureButtonText != null) DesignatedCaptureButtonText.Text = LocalizationManager.GetString("DesignatedCapture");
        if (DesignatedCaptureButton != null) DesignatedCaptureButton.ToolTip = GetLocalizedTooltip("DesignatedCapture");

        if (WindowCaptureButtonText != null) WindowCaptureButtonText.Text = LocalizationManager.GetString("WindowCapture");
        if (WindowCaptureButton != null) WindowCaptureButton.ToolTip = GetLocalizedTooltip("WindowCapture");

        if (ElementCaptureButtonText != null) ElementCaptureButtonText.Text = LocalizationManager.GetString("ElementCapture");
        if (ElementCaptureButton != null) ElementCaptureButton.ToolTip = GetLocalizedTooltip("ElementCapture");

        if (ScrollCaptureButtonText != null) ScrollCaptureButtonText.Text = LocalizationManager.GetString("ScrollCapture");
        if (ScrollCaptureButton != null) ScrollCaptureButton.ToolTip = GetLocalizedTooltip("ScrollCapture");

        if (OcrCaptureButtonText != null) OcrCaptureButtonText.Text = LocalizationManager.GetString("OcrCapture");
        if (OcrCaptureButton != null) OcrCaptureButton.ToolTip = GetLocalizedTooltip("OcrCapture");

        if (ScreenRecordButtonText != null) ScreenRecordButtonText.Text = LocalizationManager.GetString("ScreenRecording");
        if (ScreenRecordButton != null) ScreenRecordButton.ToolTip = GetLocalizedTooltip("ScreenRecording");

        if (SimpleModeButtonText != null) SimpleModeButtonText.Text = LocalizationManager.GetString("SimpleMode");
        if (SimpleModeButton != null) SimpleModeButton.ToolTip = GetLocalizedTooltip("SimpleMode");

        if (TrayModeButtonText != null) TrayModeButtonText.Text = LocalizationManager.GetString("TrayMode");
        if (TrayModeButton != null) TrayModeButton.ToolTip = GetLocalizedTooltip("TrayMode");

        if (ModeSelectText != null) ModeSelectText.Text = LocalizationManager.GetString("ModeSelect");
        // 3. 하단 아이콘 버튼들
        if (SettingsBottomText != null) SettingsBottomText.Text = LocalizationManager.GetString("Settings");
        if (CopySelectedBottomText != null) CopySelectedBottomText.Text = LocalizationManager.GetString("Copy");
        if (CopyAllBottomText != null) CopyAllBottomText.Text = LocalizationManager.GetString("CopyAll");
        if (SaveAllBottomText != null) SaveAllBottomText.Text = LocalizationManager.GetString("SaveAll");
        if (DeleteAllBottomText != null) DeleteAllBottomText.Text = LocalizationManager.GetString("DeleteAll");
        
        // 내 노트 열기 및 저장 폴더 열기 버튼
        if (OpenMyNoteButtonText != null) OpenMyNoteButtonText.Text = LocalizationManager.GetString("OpenMyNote");
        if (OpenSaveFolderButtonText != null) OpenSaveFolderButtonText.Text = LocalizationManager.GetString("OpenSaveFolder");


        // 4. 리스트 아이템 툴팁 갱신
        if (CaptureListPanel != null)
        {
            foreach (UIElement child in CaptureListPanel.Children)
            {
                if (child is Border border && border.Child is Grid grid)
                {
                    // 버튼 패널 찾기 (Grid의 자식 중 StackPanel)
                    foreach (UIElement gridChild in grid.Children)
                    {
                        if (gridChild is StackPanel panel && panel.Orientation == Orientation.Horizontal)
                        {
                            if (panel.VerticalAlignment == VerticalAlignment.Top)
                            {
                                // 버튼 순서: 구글, 공유, 저장, 삭제
                                int btnIndex = 0;
                                foreach (UIElement panelChild in panel.Children)
                                {
                                    if (panelChild is Button btn)
                                    {
                                        switch (btnIndex)
                                        {
                                            case 0: // Google
                                                btn.ToolTip = LocalizationManager.GetString("GoogleSearch");
                                                break;
                                            case 1: // Share (Step 2에서 추가됨)
                                                btn.ToolTip = LocalizationManager.GetString("Share");
                                                break;
                                            case 2: // Save
                                                btn.ToolTip = LocalizationManager.GetString("Save");
                                                break;
                                            case 3: // Delete
                                                btn.ToolTip = LocalizationManager.GetString("Delete");
                                                break;
                                        }
                                        btnIndex++;
                                    }
                                }
                            }
                            else if (panel.VerticalAlignment == VerticalAlignment.Bottom)
                            {
                                foreach (UIElement panelChild in panel.Children)
                                {
                                    if (panelChild is Button btn)
                                    {
                                        btn.ToolTip = LocalizationManager.GetString("SaveToMyNote");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (TitleSimpleModeButton != null) TitleSimpleModeButton.ToolTip = LocalizationManager.GetString("SimpleMode");
        if (TitleTrayModeButton != null) TitleTrayModeButton.ToolTip = LocalizationManager.GetString("TrayMode");
        if (OpenSaveFolderButtonText != null) OpenSaveFolderButtonText.Text = LocalizationManager.GetString("OpenSaveFolder");
        if (OpenMyNoteButtonText != null) OpenMyNoteButtonText.Text = LocalizationManager.GetString("OpenMyNote");

        if (TitleThemeButton != null) TitleThemeButton.ToolTip = LocalizationManager.GetString("ChangeTheme");
        if (MinimizeButton != null) MinimizeButton.ToolTip = LocalizationManager.GetString("Minimize");
        if (CloseButton != null) CloseButton.ToolTip = LocalizationManager.GetString("Close");

        // Edge Context Menu
        if (EdgeRadiusContextMenu != null)
        {
            foreach (var item in EdgeRadiusContextMenu.Items)
            {
                if (item is MenuItem mi && mi.Tag != null)
                {
                    string tagStr = mi.Tag.ToString() ?? "";
                    string key = tagStr switch {
                        "12" => "EdgeSoft",
                        "25" => "EdgeSmooth",
                        "50" => "EdgeClassic",
                        "100" => "EdgeCapsule",
                        "999" => "EdgeCircle",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(key)) mi.Header = LocalizationManager.GetString(key);
                }
            }
        }
        if (EdgeRadiusImage != null) EdgeRadiusImage.ToolTip = LocalizationManager.GetString("ChangeEdgeStyleTooltip");

        // View Mode Tooltips
        if (BtnViewList != null) BtnViewList.ToolTip = LocalizationManager.GetString("ListViewTooltip") ?? "리스트형 보기";
        if (BtnViewCard != null) BtnViewCard.ToolTip = LocalizationManager.GetString("CardViewTooltip") ?? "카드형 보기";
        if (BtnHistory != null) BtnHistory.ToolTip = LocalizationManager.GetString("HistoryWindowTitle");

        // Tip Area
        if (TipTextBlock != null) TipTextBlock.Text = LocalizationManager.GetString("MainTip1") ?? "캡처 목록 이미지를 더블클릭해 빠르게 편집할 수 있어요.";

        // Lock Overlays
        if (LockOverlayMessage != null) LockOverlayMessage.Text = LocalizationManager.GetString("RepositoryLockedMessage") ?? "다른 컴퓨터에서 이 저장소를 사용 중입니다.\n데이터 충돌 방지를 위해 기능이 잠겼습니다.";
        if (BtnTakeOwnershipBack != null) BtnTakeOwnershipBack.Content = LocalizationManager.GetString("TakeOwnership") ?? "이 컴퓨터에서 점유권 가져오기";
        if (BtnLockSettings != null) BtnLockSettings.Content = LocalizationManager.GetString("ChangeStorageSettings") ?? "공유 폴더 설정 변경";
        
        if (TakeoverStatusText != null) TakeoverStatusText.Text = LocalizationManager.GetString("TakeoverProgress") ?? "저장소 권한을 안전하게 가져오고 있습니다";

        // Big Empty State Buttons Localization
        if (BigOpenNoteButton != null)
        {
            SetButtonText(BigOpenNoteButton, LocalizationManager.GetString("MyNote"));
            BigOpenNoteButton.ToolTip = LocalizationManager.GetString("OpenMyNote");
        }
        if (BigHistoryButton != null)
        {
            SetButtonText(BigHistoryButton, LocalizationManager.GetString("History"));
            BigHistoryButton.ToolTip = LocalizationManager.GetString("OpenHistory");
        }

        // Delay Capture UI Update
        UpdateDelayCaptureUI();
    }
    private void UpdateTrayMenuTexts()
    {
        // 트레이 아이콘 툴팁
        if (notifyIcon != null)
        {
            notifyIcon.Text = LocalizationManager.GetString("AppTitle");
        }
        
        // 메뉴 전체 재생성
        RebuildTrayMenu();
    }
    // ★ Low-Level Keyboard Hook 설치
    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule!)
        {
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName!), 0);
        }
    }

    // ★ Low-Level Keyboard Hook 해제
    private void UninstallKeyboardHook()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
    }

    // ★ 키보드 Hook 콜백
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            // Print Screen 키 감지
            if (vkCode == VK_SNAPSHOT && settings.UsePrintScreenKey)
            {
                // UI 스레드에서 캡처 실행
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    switch (settings.PrintScreenAction)
                    {
                        case "영역 캡처":
                            StartAreaCapture();
                            break;
                        case "지연 캡처":
                            StartDelayedAreaCaptureSeconds(settings.DelayCaptureSeconds);
                            break;
                        case "실시간 캡처":
                            StartRealTimeCaptureMode();
                            break;
                        case "다중 캡처":
                            MultiCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "전체화면":
                            CaptureFullScreen();
                            break;
                        case "지정 캡처":
                            DesignatedCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "창 캡처":
                            WindowCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "단위 캡처":
                            ElementCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "스크롤 캡처":
                            ScrollCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "OCR 캡처":
                            OcrCaptureButton_Click(this, new RoutedEventArgs());
                            break;
                        case "동영상 녹화":
                            ScreenRecordButton_Click(this, new RoutedEventArgs());
                            break;
                    }
                }));

                // ★ 이벤트를 삼켜서 윈도우 기본 캡처 방지
                return (IntPtr)1;
            }

            // ★ F1 키 감지 (글로벌 핫키가 다른 창에서 안먹는 문제 해결)
            if (vkCode == VK_F1)
            {
                 // 현재 조합키 상태 확인 (비동기 상태 체크)
                 bool isCtrl = (GetKeyState(0x11) & 0x8000) != 0;  // VK_CONTROL
                 bool isShift = (GetKeyState(0x10) & 0x8000) != 0; // VK_SHIFT
                 bool isAlt = (GetKeyState(0x12) & 0x8000) != 0;   // VK_MENU
                 bool isWin = (GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0; // VK_LWIN, VK_RWIN
                 
                 // 영역 캡처 핫키가 F1이고 조합키가 없는 경우
                 if (settings.Hotkeys.RegionCapture.Enabled && 
                     settings.Hotkeys.RegionCapture.Key == "F1" && 
                     !isCtrl && !isShift && !isAlt && !isWin)
                 {
                     Dispatcher.BeginInvoke(new Action(() => StartAreaCapture()));
                     return (IntPtr)1; // 키 가로채기 (다른 앱에 전달 안 함)
                 }
            }
        }

        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }
    private List<string> GetDefaultTips(string lang)
    {
        if (lang == "en")
        {
            return new List<string>
            {
                "Double-click captured images to edit them quickly.",
                "Click the Google icon to search for the image on Google.",
                "You can customize menu positions and visibility in Settings.",
                "Use OCR to extract text and instantly translate with Google.",
                "Use Ctrl+Shift+A for quick area capture."
            };
        }
        else if (lang == "ja")
        {
            return new List<string>
            {
                "キャプチャした画像をダブルクリックすると、すぐに編集できます。",
                "Googleアイコンをクリックすると、画像をGoogleで検索できます。",
                "設定でメニューの位置や表示をカスタマイズできます。",
                "OCRを使用してテキストを抽出し、Google翻訳ですぐに翻訳できます。",
                "Ctrl+Shift+Aで素早くエリアキャプチャができます。"
            };
        }
        // Default to Korean
        return new List<string>
        {
            "캡처 목록 이미지를 더블클릭해 빠르게 편집할 수 있어요.",
            "구글 아이콘을 누르면 구글 이미지 검색이 됩니다.",
            "설정에서 메뉴 위치 및 삭제가 가능하오니 편리하게 사용하세요.",
            "OCR 문자 추출 후 구글 즉시 번역을 활용할 수 있어요.",
            "Ctrl+Shift+A 단축키로 빠르게 영역 캡처할 수 있어요."
        };
    }

    private async void InitializeTips()
    {
        // 최신 설정 로드 (언어 확인용)
        var currentSettings = Settings.Load();

        // 초기 팁 설정
        tips = GetDefaultTips(currentSettings.Language);

        // 웹에서 팁 로드 시도
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            string url = "https://ezupsoft.com/catchcapture/tooltip.html";
            string json = await client.GetStringAsync(url);

            var tipData = System.Text.Json.JsonSerializer.Deserialize<TipData>(json);
            if (tipData?.tips != null)
            {
                string lang = settings.Language ?? "ko";
                if (tipData.tips.TryGetValue(lang, out var langTips) && langTips.Count > 0)
                {
                    tips = langTips;
                }
                else if (tipData.tips.TryGetValue("ko", out var koTips) && koTips.Count > 0)
                {
                    tips = koTips;
                }
            }
        }
        catch
        {
            // 로드 실패 시 기본 팁 사용
        }

        // 즉시 첫 번째 팁 표시
        if (tips.Count > 0)
        {
            Dispatcher.Invoke(() => TipTextBlock.Text = tips[0]);
        }

        // 타이머 시작 (4초마다 팁 변경)
        tipTimer = new System.Windows.Threading.DispatcherTimer();
        tipTimer.Interval = TimeSpan.FromSeconds(4);
        tipTimer.Tick += (s, e) =>
        {
            if (tips.Count > 0)
            {
                currentTipIndex = (currentTipIndex + 1) % tips.Count;
                TipTextBlock.Text = tips[currentTipIndex];
            }
        };
        tipTimer.Start();

        // 설정 변경 시 팁 다시 로드 (언어 변경 포함)
        CatchCapture.Models.Settings.SettingsChanged += async (s, e) =>
        {
            await ReloadTips();
        };
    }

    private async Task ReloadTips()
    {
        try
        {
            // 언어 변경 확인을 위해 설정 다시 로드
            var currentSettings = Settings.Load();

            // ★ 중요: 먼저 로컬 기본 팁으로 언어 변경 적용 (웹 실패 대비)
            tips = GetDefaultTips(currentSettings.Language);

            // UI 즉시 업데이트
            Dispatcher.Invoke(() =>
            {
               currentTipIndex = 0;
               if (tips.Count > 0) TipTextBlock.Text = tips[0];
            });

            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            string url = "https://ezupsoft.com/catchcapture/tooltip.html";
            string json = await client.GetStringAsync(url);

            var tipData = System.Text.Json.JsonSerializer.Deserialize<TipData>(json);
            if (tipData?.tips != null)
            {
                string lang = currentSettings.Language ?? "ko";
                if (tipData.tips.TryGetValue(lang, out var langTips) && langTips.Count > 0)
                {
                    tips = langTips;
                    // 웹 데이터가 유효하면 업데이트
                    Dispatcher.Invoke(() =>
                    {
                       if (tips.Count > 0) TipTextBlock.Text = tips[0];
                    });
                }
            }
        }
        catch { }
    }

    // 팁 데이터 클래스
    private class TipData
    {
        public Dictionary<string, List<string>>? tips { get; set; }
    }

    // Height auto-adjustment for menu count
    private double _baseMainWindowHeight = 692.0; // Increased to 692 to ensure visible area is 670px (10px margins + 1px borders)
    private const double ButtonVerticalStep = 41.0;
    private int _baselineMenuCount = 12; // Fixed baseline count (12 default menu items)

    private void AdjustMainWindowHeightForMenuCount()
    {
        try
        {
            if (settings.IsTrayMode) return;

            int currentCount = 0;
            try
            {
                if (CaptureButtonsPanel != null)
                {
                    foreach (UIElement child in CaptureButtonsPanel.Children)
                    {
                        if (child is Separator) break;
                        // Button 또는 EdgeCaptureGrid(Grid)를 포함하여 카운트
                        if (child is Button || child is Grid) currentCount++;
                    }
                }
            }
            catch { }

            if (currentCount <= 0) currentCount = _baselineMenuCount; // Fallback to baseline

            // Calculate diff from baseline (13)
            int diff = _baselineMenuCount - currentCount;

            // Calculate target height
            double targetHeight = _baseMainWindowHeight - (diff * ButtonVerticalStep);

            // Safety clamp
            if (targetHeight < 400) targetHeight = 400;

            // Apply
            if (!double.IsNaN(targetHeight) && targetHeight > 0)
            {
                this.MinHeight = targetHeight;
                this.Height = targetHeight;
            }
        }
        catch { }
    }
    /// <summary>
    /// 비디오 녹화 데이터를 캡처 리스트에 추가 (동영상 썸네일 + 재생버튼)
    /// </summary>
    public void AddVideoToList(CatchCapture.Recording.ScreenRecorder recorder, CatchCapture.Models.RecordingSettings settings)
    {
        // MP3인 경우 프레임이 0이어도 허용
        bool isMp3 = settings.Format == CatchCapture.Models.RecordingFormat.MP3;
        if (recorder == null || (recorder.FrameCount == 0 && !isMp3)) return;

        Dispatcher.Invoke(() =>
        {
            try
            {
                // 첫 프레임을 썸네일로 가져오기 (MP3는 null 사용)
                BitmapSource? thumbnail = null;
                if (!isMp3)
                {
                    thumbnail = recorder.GetThumbnail();
                    if (thumbnail == null) return;
                }

                // 저장 경로 미리 계산 (이미지 저장 로직과 동일하게 적용)
                string ext;
                switch (settings.Format)
                {
                    case CatchCapture.Models.RecordingFormat.GIF: ext = ".gif"; break;
                    case CatchCapture.Models.RecordingFormat.MP3: ext = ".mp3"; break;
                    default: ext = ".mp4"; break;
                }

                string fullPath = GetAutoSaveFilePath(ext);
                string filename = System.IO.Path.GetFileName(fullPath);

                // 동영상 썸네일 아이템 생성 (재생 버튼 포함)
                // thumbnail이 null이어도 MP3면 내부에서 처리됨 (스피커 아이콘)
                var (videoItem, encodingOverlay) = CreateVideoThumbnailItem(thumbnail!, fullPath);
                CaptureListPanel.Children.Insert(0, videoItem);

                // [추가] captures 리스트에도 추가하여 관리 (모드 전환 시 유지 등)
                var captureImage = new CaptureImage(thumbnail!, fullPath,
                                    thumbnail?.PixelWidth ?? 0, thumbnail?.PixelHeight ?? 0,
                                    isMp3 ? "오디오 녹음" : "동영상 녹화", filename);
                captureImage.IsVideo = true;
                captures.Add(captureImage);

                // 간편모드/트레이모드 카운트 갱신
                UpdateCaptureCount();
                if (trayModeWindow != null) trayModeWindow.UpdateCaptureCount(captures.Count);

                // 추가된 아이템 선택
                SelectCapture(videoItem, captures.Count - 1);

                // 버튼 상태 업데이트 (전체 삭제 활성화 등)
                UpdateButtonStates();
                UpdateEmptyStateLogo();

                // 백그라운드에서 저장 시작
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await recorder.SaveRecordingAsync(fullPath);

                        // [추가] 프리뷰용 썸네일 이미지 파일로 저장
                        if (thumbnail != null)
                        {
                            try
                            {
                                string thumbPath = fullPath + ".preview.png";
                                CatchCapture.Utilities.ScreenCaptureUtility.SaveImageToFile(thumbnail, thumbPath);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"썸네일 저장 실패: {ex.Message}");
                            }
                        }

                        // 히스토리에 추가
                        var historyItem = new HistoryItem
                        {
                            FileName = filename,
                            FilePath = fullPath,
                            OriginalFilePath = fullPath,
                            SourceApp = "화면 녹화",
                            SourceTitle = isMp3 ? "오디오 녹음" : "동영상 녹화",
                            FileSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0,
                            Resolution = isMp3 ? "Audio Only" : (thumbnail != null ? $"{thumbnail.PixelWidth}x{thumbnail.PixelHeight}" : "N/A"),
                            IsFavorite = false,
                            Status = 0,
                            CreatedAt = DateTime.Now
                        };

                        DatabaseManager.Instance.InsertCapture(historyItem);

                        // [Hit & Run] 실시간 동기화 + 락 해제
                        _ = Task.Run(() =>
                        {
                            DatabaseManager.Instance.SyncToCloud(true);
                            DatabaseManager.Instance.RemoveLock();
                        });

                        // 저장 완료 시 인코딩 표시 제거 및 알림
                        Dispatcher.Invoke(() =>
                        {
                            // 인코딩 오버레이 숨기기 (또는 애니메이션 종료)
                            encodingOverlay.Visibility = Visibility.Collapsed;

                            // 저장 완료 토스트
                            ShowGuideMessage($"녹화 저장 완료: {filename}", TimeSpan.FromSeconds(2));

                            // 열려있는 히스토리 창이 있으면 갱신
                            var historyWindow = Application.Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
                            if (historyWindow != null)
                            {
                                historyWindow.LoadHistory();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 인코딩 실패 표시
                            if (encodingOverlay.Child is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb)
                            {
                                tb.Text = "❌ 오류";
                                tb.Foreground = Brushes.Red;
                            }

                            MessageBox.Show($"녹화 저장 실패:\n{ex.Message}", "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        recorder?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"비디오 리스트 추가 실패:\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    /// <summary>
    /// 동영상 썸네일 아이템 생성 (재생 버튼 오버레이 + 인코딩 표시 포함)
    /// </summary>
    /// <returns>썸네일 Border와 인코딩 오버레이 Border</returns>
    private (Border item, Border overlay) CreateVideoThumbnailItem(BitmapSource thumbnail, string filePath)
    {
        bool isAudio = System.IO.Path.GetExtension(filePath).ToLower() == ".mp3";

        var border = new Border
        {
            Width = 200,
            Height = 120,
            Margin = new Thickness(4),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(67, 97, 238)), // 파란색 테두리
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            Tag = filePath // 파일 경로 저장
        };

        var grid = new Grid();

        if (isAudio)
        {
            // MP3용 배경 및 아이콘
            grid.Background = Brushes.Black;

            // 중앙 스피커 아이콘
            var speakerIcon = new TextBlock
            {
                Text = "🔊",
                FontSize = 40,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(speakerIcon);
        }
        else
        {
            // 동영상 썸네일 이미지
            var image = new Image
            {
                Source = thumbnail,
                Stretch = Stretch.UniformToFill
            };
            grid.Children.Add(image);

            // 재생 버튼 오버레이 (반투명 원 + ▶)
            var playButtonBg = new Ellipse
            {
                Width = 36,
                Height = 36,
                Fill = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(playButtonBg);

            var playIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };
            grid.Children.Add(playIcon);
        }

        // 포맷 레이블 (우측 상단)
        var formatLabel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 67, 97, 238)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1, 4, 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0)
        };
        var formatText = new TextBlock
        {
            Text = System.IO.Path.GetExtension(filePath).ToUpper().Replace(".", ""),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(4, 2, 4, 2)
        };
        formatLabel.Child = formatText;

        grid.Children.Add(formatLabel);

        border.Child = grid;

        // 인코딩 중 오버레이 (기존 로직 유지)
        var encodingOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 255, 140, 0)), // 짙은 오렌지색
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 24,
            CornerRadius = new CornerRadius(0, 0, 2, 2),
            Visibility = Visibility.Visible // 기본적으로 보임
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 회전하는 애니메이션 흉내낼 텍스트 또는 아이콘
        var encodingText = new TextBlock
        {
            Text = "⏳ 저장 중...",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 1, 0, 1)
        };
        stack.Children.Add(encodingText);
        encodingOverlay.Child = stack;

        grid.Children.Add(encodingOverlay);

        border.Child = grid;

        // 1. 더블 클릭 시 플레이어로 열기 (사용자 요청)
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                var filePath = border.Tag as string;

                // 인코딩 중이라면 경고
                if (encodingOverlay.Visibility == Visibility.Visible)
                {
                     ShowGuideMessage("아직 저장(인코딩) 중입니다. 잠시만 기다려주세요.", TimeSpan.FromSeconds(1.5));
                     e.Handled = true;
                     return;
                }

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"동영상 열기 실패:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                e.Handled = true;
            }
        };

        // 2. 우클릭 컨텍스트 메뉴 (저장 폴더 열기, 삭제)
        var contextMenu = new ContextMenu();

        var openFolderItem = new MenuItem { Header = "저장 폴더 열기" };
        openFolderItem.Click += (s, e) =>
        {
            var filePath = border.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                if (File.Exists(filePath))
                {
                    // 파일 선택된 상태로 폴더 열기
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else
                {
                    // 파일 없으면 폴더만 열기
                    string folder = System.IO.Path.GetDirectoryName(filePath) ?? "";
                    if (Directory.Exists(folder))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", folder);
                    }
                }
            }
        };

        var deleteItem = new MenuItem { Header = "삭제" };
        deleteItem.Click += (s, e) =>
        {
            var filePath = border.Tag as string;
            var confirm = MessageBox.Show("이 동영상을 삭제하시겠습니까?", "삭제 확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);

                    // UI에서 제거
                    CaptureListPanel.Children.Remove(border);

                    // 버튼 상태 업데이트 (동영상만 남았을 때도 고려)
                    UpdateButtonStates();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        contextMenu.Items.Add(openFolderItem);
        contextMenu.Items.Add(deleteItem);
        border.ContextMenu = contextMenu;

        // 노트 저장 버튼 추가 (우측 하단에 호버 시 표시)
        Button noteBtn = new Button
        {
            Width = 28, Height = 28,
            Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            Cursor = Cursors.Hand,
            ToolTip = "내노트저장",
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6)
        };

        // 둥근 버튼 스타일 적용
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentPresenter);
        template.VisualTree = borderFactory;
        noteBtn.Template = template;

        // 아이콘 설정
        var icon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/icons/my_note.png")),
            Width = 14, Height = 14, Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        noteBtn.Content = icon;

        // 클릭 이벤트 연결
        noteBtn.Click += (s, e) =>
        {
            e.Handled = true;
            SaveVideoToNote(filePath, thumbnail);
        };

        grid.Children.Add(noteBtn);

        // 마우스 호버 효과 (비디오만)
        if (!isAudio)
        {
            border.MouseEnter += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 87, 87)); // 빨간색으로 변경
                noteBtn.Visibility = Visibility.Visible;
            };

            border.MouseLeave += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(67, 97, 238)); // 원래 색상
                noteBtn.Visibility = Visibility.Collapsed;
            };
        }
        else
        {
            // 오디오 아이템 호버 효과
            border.MouseEnter += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 150, 255));
                noteBtn.Visibility = Visibility.Visible;
            };

            border.MouseLeave += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(67, 97, 238));
                noteBtn.Visibility = Visibility.Collapsed;
            };
        }

        return (border, encodingOverlay);
    }

    private void SaveVideoToNote(string filePath, BitmapSource? thumbnail)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // 비밀번호 잠금 확인
            if (!string.IsNullOrEmpty(settings.NotePassword) && !App.IsNoteAuthenticated)
            {
                var lockWin = new NoteLockCheckWindow(settings.NotePassword, settings.NotePasswordHint);
                lockWin.Owner = this;
                if (lockWin.ShowDialog() != true) return;
            }

            // [Fix] 기존에 열린 노트 입력창이 있는지 확인 (새 노트 작성 중인 창 우선)
            var openNoteWin = Application.Current.Windows.OfType<NoteInputWindow>().FirstOrDefault(w => !w.IsEditMode);
            if (openNoteWin != null)
            {
                openNoteWin.AppendMediaFile(filePath, thumbnail);
                openNoteWin.Activate();
                openNoteWin.Focus();
            }
            else
            {
                var noteWin = new NoteInputWindow(thumbnail, "화면 녹화", System.IO.Path.GetFileName(filePath), filePath);
                // noteWin.Owner = this; // Owner 제거
                noteWin.Show();
            }
        }
        catch (Exception ex)
        {
            CatchCapture.CustomMessageBox.Show($"노트 저장 중 오류가 발생했습니다: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
