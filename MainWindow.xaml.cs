using System.Text;
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


namespace CatchCapture;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<CaptureImage> captures = new List<CaptureImage>();
    private int selectedIndex = -1;
    private Border? selectedBorder = null;
    private Settings settings;
    private SimpleModeWindow? simpleModeWindow = null;
    private Point lastPosition;
    private int captureDelaySeconds = 0;
    
    // 스크린샷 캐시 (성능 최적화용)
    private BitmapSource? cachedScreenshot = null;
    private DateTime lastScreenshotTime = DateTime.MinValue;
    private readonly TimeSpan screenshotCacheTimeout = TimeSpan.FromSeconds(5);
    private System.Windows.Threading.DispatcherTimer? screenshotCacheTimer;

    // 트레이 아이콘
    public System.Windows.Forms.NotifyIcon? notifyIcon;  // private를 public으로 변경
    private bool isExit = false;
    private TrayModeWindow? trayModeWindow;
    // 글로벌 단축키 관련
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_AREA = 9000;
    private const int HOTKEY_ID_FULLSCREEN = 9001;
    private const int HOTKEY_ID_WINDOW = 9002;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? hwndSource;
    // Ensures any pending composition updates are presented (so hidden window is actually off-screen)
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

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
    public MainWindow()
    {
        InitializeComponent();
        settings = Settings.Load();
        
        // Print Screen 키 감지
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        
        // 트레이 아이콘 초기화
        InitializeNotifyIcon();
        
        // 글로벌 단축키 등록
        RegisterGlobalHotkeys();
        
        // 로컬 단축키 등록
        AddKeyboardShortcuts();
        
        // 타이틀바 드래그 이벤트 설정
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
        
        // 간편모드 활성 중에는 작업표시줄 클릭으로 본체가 튀어나오지 않도록 제어
        this.StateChanged += MainWindow_StateChanged;
        this.Activated += MainWindow_Activated;
        
        // 백그라운드 스크린샷 캐시 시스템 초기화
        InitializeScreenshotCache();
        
        this.Loaded += (s, e) =>
        {
            var helper = new WindowInteropHelper(this);
            hwndSource = HwndSource.FromHwnd(helper.Handle);
            hwndSource?.AddHook(HwndHook);
            RegisterGlobalHotkeys();
            UpdateInstantEditToggleUI();
            // 시작 모드에 따라 초기 모드 설정
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
                // Normal 모드: 창을 명시적으로 표시하고 활성화
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                this.Topmost = true;
                this.Topmost = false;
            }
        };
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
                }
            }
        }
    }

    private void InitializeNotifyIcon()
    {
        notifyIcon = new System.Windows.Forms.NotifyIcon();
        try
        {
            // 실행 파일의 아이콘을 사용
            notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }
        catch
        {
            // 아이콘 로드 실패 시 기본 시스템 아이콘 사용 (fallback)
            notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        
        notifyIcon.Visible = true;
        notifyIcon.Text = "캐치캡처";
        
        // 클릭 시 트레이 모드 창 토글
        notifyIcon.Click += (s, e) =>
        {
            if (e is System.Windows.Forms.MouseEventArgs me && me.Button == System.Windows.Forms.MouseButtons.Left)
            {
                RestoreLastMode();
            }
        };

        // 컨텍스트 메뉴
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("일반 모드", null, (s, e) => SwitchToNormalMode());
        contextMenu.Items.Add("간편 모드", null, (s, e) => SwitchToSimpleMode());
        contextMenu.Items.Add("트레이 모드", null, (s, e) => SwitchToTrayMode());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("종료", null, (s, e) => 
        {
            isExit = true;
            Close();
        });
        
        notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowMainWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void ToggleTrayModeWindow()
    {
        if (trayModeWindow == null || !trayModeWindow.IsVisible)
        {
            ShowTrayModeWindow();
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
                if (simpleModeWindow == null || !simpleModeWindow.IsVisible)
                {
                    SwitchToSimpleMode();
                }
                else
                {
                    simpleModeWindow.Hide();
                }
                break;
                
            case "Tray":
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
        settings.LastActiveMode = "Tray";
        
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
            else if (!trayModeWindow.IsVisible)
            {
                // 창이 존재하지만 숨겨진 경우에만 Show 호출
                trayModeWindow.Show();
            }
            
            // [추가] 현재 캡처 개수 업데이트
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
        settings.LastActiveMode = "Normal";  // 이 줄 추가
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
        this.Width = 350;
        this.Height = 570;
        
        // 저장된 위치로 복원 (0,0은 초기값일 수 있으므로 제외하고 우측 하단으로)
        if (!double.IsNaN(settings.LastMainLeft) && !double.IsNaN(settings.LastMainTop) &&
            !(settings.LastMainLeft == 0 && settings.LastMainTop == 0))
        {
            this.Left = settings.LastMainLeft;
            this.Top = settings.LastMainTop;
        }
        else
        {
            // 기본 위치: 화면 우측 하단 (트레이 아이콘 근처)
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.Width - 10;
            this.Top = workArea.Bottom - this.Height - 10;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        
        // 창 표시
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        Settings.Save(settings);

        // 설정을 다시 로드하여 메모리와 파일 동기화
        settings = Settings.Load();
        UpdateInstantEditToggleUI();
    }

    private void TrayModeButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchToTrayMode();
    }

    private void UpdateInstantEditToggleUI()
    {
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

    private void SwitchToSimpleMode()
    {
        settings.LastActiveMode = "Simple";
        
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

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;

        // 1) Settings 기반 핫키를 최우선으로 처리
        try
        {
            if (HandleSettingsHotkeys(e))
            {
                e.Handled = true;
                return;
            }
        }
        catch { /* ignore hotkey errors to avoid blocking */ }

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
                ShowPreviewWindow(captures[selectedIndex].Image, selectedIndex);
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
        ShowGuideMessage($"상단 고정: {(this.Topmost ? "켜짐" : "꺼짐")}", TimeSpan.FromSeconds(1));
    }
    
    // 파일 열기 다이얼로그
    private void OpenFileDialog_Click(object? sender, RoutedEventArgs? e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "이미지 열기",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일|*.*",
            Multiselect = true
        };
        
        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(fileName));
                    AddCaptureToList(bitmap);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일을 열 수 없습니다: {fileName}\n오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    #region 캡처 기능

    private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartAreaCapture();
    }

    private void DelayCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                fe.ContextMenu.IsOpen = true;
            }
        }
    }

    private void DelayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tagStr && int.TryParse(tagStr, out int seconds))
        {
            captureDelaySeconds = seconds;
        }
        else if (sender is MenuItem mi2 && mi2.Tag is int tagInt)
        {
            captureDelaySeconds = tagInt;
        }
        else
        {
            captureDelaySeconds = 0;
        }

        // 실시간 카운트다운 표시 후 캡처 시작
        if (captureDelaySeconds <= 0)
        {
            StartAreaCapture();
            return;
        }

        var countdown = new GuideWindow("", null)
        {
            Owner = this
        };
        countdown.Show();
        countdown.StartCountdown(captureDelaySeconds, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(StartAreaCapture);
        });
    }

    private BitmapSource GetCachedOrFreshScreenshot()
    {
        var now = DateTime.Now;
        
        // 캐시가 유효한지 확인 (2초 이내)
        if (cachedScreenshot != null && (now - lastScreenshotTime) < screenshotCacheTimeout)
        {
            return cachedScreenshot;
        }
        
        // 새로운 스크린샷 캡처 및 캐시 업데이트
        cachedScreenshot = ScreenCaptureUtility.CaptureScreen();
        lastScreenshotTime = now;
        
        return cachedScreenshot;
    }

    private async Task StartAreaCaptureAsync()
    {
        // 간편모드 체크 추가
        bool isSimpleMode = simpleModeWindow != null && simpleModeWindow.IsVisible;
        // 즉시편집 설정 확인
        var currentSettings = Settings.Load();
        bool instantEdit = currentSettings.SimpleModeInstantEdit;
        
        // 1단계: 투명하게
        this.Opacity = 0;
        await Task.Delay(30);
        
        // 2단계: 숨기기
        this.Hide();
        
        // 3단계: UI 업데이트 강제 대기
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        await Task.Delay(30);
        
        var screenshot = await Task.Run(() => ScreenCaptureUtility.CaptureScreen());

        // 캡처된 스크린샷을 전달하여 SnippingWindow가 즉시 표시되도록
        using var snippingWindow = new SnippingWindow(false, screenshot);
        
        // 즉시편집 모드 활성화
        if (instantEdit)
        {
            snippingWindow.EnableInstantEditMode();
        }

        if (snippingWindow.ShowDialog() == true)
        {
            var selectedArea = snippingWindow.SelectedArea;
            var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);
            
            // 4단계: Opacity 복원
            this.Opacity = 1;
            
            AddCaptureToList(capturedImage);
        }
        else
        {
            // 4단계: Opacity 복원
            this.Opacity = 1;
            
            // 캡처 취소 시
            if (isSimpleMode)
            {
                // 간편 모드: 간편모드 창만 다시 표시
                if (simpleModeWindow != null)
                {
                    simpleModeWindow.Show();
                    simpleModeWindow.Activate();
                }
            }
            else if (!settings.IsTrayMode)
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
    // 동기 래퍼 메서드 (기존 호출 호환성 유지)
    private void StartAreaCapture()
    {
        _ = StartAreaCaptureAsync();  // Fire and forget
    }
    private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureFullScreen();
    }

    private void CaptureFullScreen()
    {
        // 전체 화면 캡처
        this.Hide();
        FlushUIAfterHide();

        var capturedImage = ScreenCaptureUtility.CaptureScreen();
        AddCaptureToList(capturedImage);

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
        }
        finally
        {
            // 창 표시는 AddCaptureToList에서 처리
        }
    }
    
    private void CaptureScrollableWindow()
    {
        try
        {
            // 안내 메시지 표시
            var guideWindow = new GuideWindow("캡처할 창을 클릭하고 Enter 키를 누르세요", TimeSpan.FromSeconds(2.5));
            guideWindow.Owner = this;
            guideWindow.Show();
            
            // 사용자가 다른 창을 선택할 수 있도록 기다림
            this.Hide();
            
            // 스크롤 캡처 수행
            var capturedImage = ScreenCaptureUtility.CaptureScrollableWindow();
            
            if (capturedImage != null)
            {
                AddCaptureToList(capturedImage);
                ShowGuideMessage("스크롤 캡처가 완료되었습니다.", TimeSpan.FromSeconds(1.5));
                
                // 캡처된 이미지 클립보드에 복사
                ScreenCaptureUtility.CopyImageToClipboard(capturedImage);
                ShowGuideMessage("이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1.5));
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

            // Subscribe to continuous capture event
            designatedWindow.CaptureCompleted += (img) =>
            {
                // Ensure UI thread
                Dispatcher.Invoke(() =>
                {
                    AddCaptureToList(img);
                    // Optionally also copy to clipboard
                    CatchCapture.Utilities.ScreenCaptureUtility.CopyImageToClipboard(img);
                });
            };

            // Block until user closes overlay via ✕ (DialogResult false)
            designatedWindow.ShowDialog();
        }
        finally
        {

        }
    }
    private void WindowCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Hide();
            System.Threading.Thread.Sleep(10); // 창이 숨겨질 시간

            var windowCaptureOverlay = new CatchCapture.Utilities.WindowCaptureOverlay();
            
            if (windowCaptureOverlay.ShowDialog() == true && windowCaptureOverlay.CapturedImage != null)
            {
                AddCaptureToList(windowCaptureOverlay.CapturedImage);
            }
        }
        finally
        {
            // 창 표시는 AddCaptureToList에서 처리
        }
    }
    
    private void ElementCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Hide();
            System.Threading.Thread.Sleep(10);

            var elementCaptureWindow = new ElementCaptureWindow();
            
            if (elementCaptureWindow.ShowDialog() == true && elementCaptureWindow.CapturedImage != null)
            {
                AddCaptureToList(elementCaptureWindow.CapturedImage);
            }
        }
        finally
        {
            // 창 표시는 AddCaptureToList에서 처리
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

    private void AddCaptureToList(BitmapSource image)
    {
        try
        {
            // 캡처 이미지 객체 생성
            var captureImage = new CaptureImage(image);
            captures.Add(captureImage);
            try 
            {
                var currentSettings = CatchCapture.Models.Settings.Load();
                if (currentSettings.AutoSaveCapture)
                {
                    string saveFolder = currentSettings.DefaultSaveFolder;
                    // 폴더 경로가 비어있으면 기본 경로 설정
                    if (string.IsNullOrWhiteSpace(saveFolder))
                    {
                        saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
                    }
                    
                    // 폴더가 없으면 생성
                    if (!System.IO.Directory.Exists(saveFolder))
                    {
                        System.IO.Directory.CreateDirectory(saveFolder);
                    }

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                    string ext = currentSettings.FileSaveFormat.Equals("PNG", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                    string filename = $"AutoSave_{timestamp}_{captures.Count}{ext}";
                    string fullPath = System.IO.Path.Combine(saveFolder, filename);

                    CatchCapture.Utilities.ScreenCaptureUtility.SaveImageToFile(image, fullPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"자동 저장 실패: {ex.Message}");
            }

            // UI에 이미지 추가 - 최신 캡처를 위에 표시하기 위해 인덱스 0에 추가
            var border = CreateCaptureItem(captureImage, captures.Count - 1);
            CaptureListPanel.Children.Insert(0, border);

            // 추가된 이미지 선택
            SelectCapture(border, captures.Count - 1);

            // 버튼 상태 업데이트
            UpdateButtonStates();
            
            
            // 간편모드가 활성화된 경우 (최우선 체크)
            if (simpleModeWindow != null)
            {
                // 간편모드에서는 캡처 개수만 업데이트하고 창을 표시하지 않음
                UpdateCaptureCount();
            }
            // 트레이 모드일 때 처리
            else if (settings.IsTrayMode)
            {
                // 트레이 창이 없거나 닫혔으면 다시 생성/표시
                if (trayModeWindow == null || !trayModeWindow.IsLoaded)
                {
                    trayModeWindow = new TrayModeWindow(this);
                }
                
                // 캡처 도중 숨겨졌을 수 있으므로 다시 표시
                trayModeWindow.Show();
                trayModeWindow.UpdateCaptureCount(captures.Count);
                
                // 트레이모드에서는 자동으로 클립보드에 복사
                try
                {
                    ScreenCaptureUtility.CopyImageToClipboard(image);
                    ShowGuideMessage("캡처가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // 실패 시 재시도 없이 안내 메시지 표시하고 종료 (프로그램 멈춤 방지)
                    ShowGuideMessage("클립보드 복사 실패: 다른 프로그램이 사용 중입니다.\n일반 모드에서 수동으로 복사해주세요.", TimeSpan.FromSeconds(3));
                }
            }
            else
            {
                // 일반 모드: 창 표시
                // 캡처 개수 업데이트 (일반 모드용)
                UpdateCaptureCount();
                
                // 창 표시 (트레이 모드가 아니고 간편모드가 아닐 때만)
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
            
            // 캡처 후 미리보기 표시 설정 확인
            if (settings.ShowPreviewAfterCapture)
            {
                var preview = new PreviewWindow(image, captures.Count - 1);
                preview.Owner = this;
                preview.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            // 에러를 바탕화면 파일로 저장
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var errorFilePath = System.IO.Path.Combine(desktopPath, "CatchCapture_Error.txt");
                var errorMessage = $"=== 에러 발생 ===\n시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n에러 메시지:\n{ex.Message}\n\n스택 트레이스:\n{ex.StackTrace}\n\n설정 정보:\nIsTrayMode: {settings.IsTrayMode}\ntrayModeWindow: {(trayModeWindow == null ? "null" : "exists")}\nsimpleModeWindow: {(simpleModeWindow == null ? "null" : "exists")}\n==================\n\n";
                System.IO.File.AppendAllText(errorFilePath, errorMessage);
                
                MessageBox.Show($"캡처 중 오류가 발생했습니다.\n에러 로그가 바탕화면에 저장되었습니다.\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
    private Border CreateCaptureItem(CaptureImage captureImage, int index)
    {
        // 썸네일 크기 고정
        double thumbWidth = 200;
        double thumbHeight = 120;

        // 그리드 생성
        Grid grid = new Grid();
        
        // 이미지 컨트롤 생성
        Image image = new Image
        {
            Source = captureImage.Image,
            Width = thumbWidth,
            Height = thumbHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

        // 인덱스를 태그로 저장
        image.Tag = index;

        // 더블 클릭 이벤트 (미리보기)
        image.MouseDown += (s, e) => 
        {
            if (e.ClickCount == 2)
            {
                int actualIndex = (int)((Image)s).Tag;
                ShowPreviewWindow(captureImage.Image, actualIndex);
                e.Handled = true;
            }
        };

        grid.Children.Add(image);
        
        // 이미지 크기 텍스트
        Border sizeBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6)
        };
        
        TextBlock sizeText = new TextBlock
        {
            Text = $"{captureImage.Image.PixelWidth} x {captureImage.Image.PixelHeight}",
            Foreground = Brushes.White,
            FontSize = 10
        };
        
        sizeBorder.Child = sizeText;
        grid.Children.Add(sizeBorder);

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
            Cursor = Cursors.Hand, ToolTip = "구글 이미지 검색"
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

        // 'G' 텍스트 설정 (파란색)
        googleBtn.Content = new TextBlock 
        { 
            Text = "G", 
            FontWeight = FontWeights.Bold, 
            Foreground = new SolidColorBrush(Color.FromRgb(66, 133, 244)), // Google Blue
            FontSize = 14
        };
        
        // 클릭 이벤트 연결
        googleBtn.Click += (s, e) => 
        {
            e.Handled = true;
            SearchImageOnGoogle(captureImage.Image);
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
        Button saveBtn = CreateHoverButton("save_selected.png", "저장");
        saveBtn.Click += (s, e) => { e.Handled = true; SaveImageToFile(captureImage); };

        // 삭제 버튼 추가
        Button deleteBtn = CreateHoverButton("delete_selected.png", "삭제");
        deleteBtn.Click += (s, e) => 
        {
            e.Handled = true;
            if (settings.ShowSavePrompt && !captureImage.IsSaved)
            {
                if (MessageBox.Show("저장되지 않은 이미지입니다. 삭제하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            // 리스트에서 제거 로직
           Border? currentBorder = null;
            foreach (var child in CaptureListPanel.Children) {
                if (child is Border b && b.Child == grid) { currentBorder = b; break; }
            }
            if (currentBorder != null) {
                CaptureListPanel.Children.Remove(currentBorder);
                captures.Remove(captureImage);
                UpdateCaptureItemIndexes();
                UpdateCaptureCount();
                UpdateButtonStates();
                selectedBorder = null; selectedIndex = -1;
            }
        };

        // 패널에 버튼 추가 (구글 -> 저장 -> 삭제)
        buttonPanel.Children.Add(googleBtn);
        buttonPanel.Children.Add(saveBtn);
        buttonPanel.Children.Add(deleteBtn);
        grid.Children.Add(buttonPanel);

        // 메인 테두리 생성
        Border border = new Border
        {
            Child = grid,
            Margin = new Thickness(0, 6, 0, 6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 5, Opacity = 0.2, Direction = 270 },
            Tag = index,
            Width = thumbWidth, Height = thumbHeight
        };

        // 호버 이벤트 연결 (마우스 올리면 버튼 보임)
        border.MouseEnter += (s, e) => buttonPanel.Visibility = Visibility.Visible;
        border.MouseLeave += (s, e) => buttonPanel.Visibility = Visibility.Collapsed;

        // 클릭 이벤트 (선택)
        border.MouseLeftButtonDown += (s, e) => 
        {
            // 더블클릭 시 미리보기 창 열기
            if (e.ClickCount == 2)
            {
                int actualIndex = (int)((Border)s).Tag;
                ShowPreviewWindow(captureImage.Image, actualIndex);
                e.Handled = true;
            }
            // 싱글클릭 시 선택
            else if (s is Border clickedBorder)
            {
                int clickedIndex = (int)clickedBorder.Tag;
                SelectCapture(clickedBorder, clickedIndex);
            }
        };

        return border;
    }

    private void SelectCapture(Border border, int index)
    {
        // 이전 선택 해제
        if (selectedBorder != null)
        {
            selectedBorder.BorderBrush = Brushes.Transparent;
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
        bool hasCaptures = captures.Count > 0;
        bool hasSelection = selectedIndex >= 0;

        // 복사 버튼 상태 업데이트
        CopySelectedButton.IsEnabled = hasSelection;
        CopyAllButton.IsEnabled = hasCaptures;

        // 저장 버튼 상태 업데이트
        // SaveSelectedButton.IsEnabled = hasSelection; // <-- 이 줄 삭제 또는 주석 처리
        SaveAllButton.IsEnabled = hasCaptures;

        // 삭제 버튼 상태 업데이트
        // DeleteSelectedButton.IsEnabled = hasSelection; // <-- 이 줄 삭제 또는 주석 처리
        DeleteAllButton.IsEnabled = hasCaptures;
    }

    private void UpdateCaptureCount()
    {
        Title = $"캣치 - 캡처 {captures.Count}개";
    }

    #endregion

    #region 복사 기능

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedImage();
    }

    private void CopySelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            var image = captures[selectedIndex].Image;
            ScreenCaptureUtility.CopyImageToClipboard(image);
            ShowGuideMessage("이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        CopyAllImages();
    }

    private void CopyAllImages()
    {
        if (captures.Count == 0) return;

        // 모든 이미지를 세로로 결합
        int totalWidth = 0;
        int totalHeight = 0;

        // 최대 너비와 총 높이 계산
        foreach (var capture in captures)
        {
            totalWidth = Math.Max(totalWidth, capture.Image.PixelWidth);
            totalHeight += capture.Image.PixelHeight;
        }

        // 결합된 이미지 생성
        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            int currentY = 0;
            foreach (var capture in captures)
            {
                drawingContext.DrawImage(capture.Image, new Rect(0, currentY, capture.Image.PixelWidth, capture.Image.PixelHeight));
                currentY += capture.Image.PixelHeight;
            }
        }

        RenderTargetBitmap combinedImage = new RenderTargetBitmap(
            totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
        combinedImage.Render(drawingVisual);

        // 클립보드에 복사
        ScreenCaptureUtility.CopyImageToClipboard(combinedImage);
        ShowGuideMessage("모든 이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1));
    }

    #endregion

    #region 저장 기능

    private void SaveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSelectedImage();
    }

    private void SaveSelectedImage()
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
        // 자동 파일 이름 생성
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        
        // 설정에서 포맷 가져오기
        string format = settings.FileSaveFormat; // PNG, JPG, BMP, GIF, WEBP
        string ext = $".{format.ToLower()}";
        
        string defaultFileName = $"캡처 {timestamp}{ext}";
        
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
            Title = "이미지 저장",
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
                ScreenCaptureUtility.SaveImageToFile(captureImage.Image, dialog.FileName, settings.ImageQuality);
                captureImage.IsSaved = true;
                captureImage.SavedPath = dialog.FileName;
                ShowGuideMessage("이미지가 저장되었습니다.", TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    private void SaveAllImages()
    {
        if (captures.Count == 0) return;

        // 저장 폴더 선택 (폴더 선택 다이얼로그 대용으로 SaveFileDialog 사용)
        // 실제로는 사용자가 폴더를 지정하기 위한 용도
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "저장할 폴더를 선택하세요",
            ShowNewFolderButton = true
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
                ScreenCaptureUtility.SaveImageToFile(captures[i].Image, fileName, quality);
                captures[i].IsSaved = true;
                captures[i].SavedPath = fileName;
            }

            ShowGuideMessage("모든 이미지가 저장되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedImage();
    }

    private void DeleteSelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            // 저장되지 않은 이미지인 경우 확인
            if (!captures[selectedIndex].IsSaved && settings.ShowSavePrompt)
            {
                var result = MessageBox.Show(
                    "저장되지 않은 이미지입니다. 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // UI에서 제거 - 실제 UI 인덱스 찾기
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
                CaptureListPanel.Children.RemoveAt(uiIndex);
            }

            // 데이터에서 제거
            captures.RemoveAt(selectedIndex);

            // 인덱스 업데이트
            UpdateCaptureItemIndexes();

            // 선택 초기화
            selectedBorder = null;
            selectedIndex = -1;

            // 버튼 상태 업데이트
            UpdateButtonStates();
            UpdateCaptureCount();
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
    }

    private void DeleteAllImages()
    {
        if (captures.Count == 0) return;

        // 저장되지 않은 이미지가 있는지 확인
        bool hasUnsavedImages = captures.Exists(c => !c.IsSaved);

        if (hasUnsavedImages && settings.ShowSavePrompt)
        {
            var result = MessageBox.Show(
                "저장되지 않은 이미지가 있습니다. 모두 삭제하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return;
            }
        }

        // 모든 이미지 삭제
        CaptureListPanel.Children.Clear();
        captures.Clear();
        selectedBorder = null;
        selectedIndex = -1;

        // 버튼 상태 업데이트
        UpdateButtonStates();
        UpdateCaptureCount();
    }

    #endregion

    #region 미리보기 기능

    private void ShowPreviewWindow(BitmapSource image, int index)
    {
        // 미리보기 창 생성
        PreviewWindow previewWindow = new PreviewWindow(captures[index].Image, index, captures);
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
    }
    private void ShowGuideMessage(string message, TimeSpan? duration = null)
    {
        GuideWindow guideWindow = new GuideWindow(message, duration);
        guideWindow.Owner = this;
        guideWindow.Show();
    }
    
    // 구글 렌즈 검색 기능 (HTML 리다이렉트 방식 - 2025년 대응)
    private void SearchImageOnGoogle(BitmapSource image)
    {
        try
        {
            ScreenCaptureUtility.CopyImageToClipboard(image);
            // 1. 이미지 리사이징 (Base64 길이 최적화)
            // 긴 변을 400px로 제한 (검색 정확도와 용량의 균형)
            double scale = 1.0;
            double maxSide = 400.0;
            if (image.PixelWidth > maxSide || image.PixelHeight > maxSide)
            {
                scale = Math.Min(maxSide / image.PixelWidth, maxSide / image.PixelHeight);
            }

            var transformedBitmap = new TransformedBitmap(image, new ScaleTransform(scale, scale));

            // 2. JPEG로 압축 (품질 70%)
            var encoder = new JpegBitmapEncoder { QualityLevel = 70 }; 
            encoder.Frames.Add(BitmapFrame.Create(transformedBitmap));
            
            using var ms = new MemoryStream();
            encoder.Save(ms);
            byte[] imageBytes = ms.ToArray();

            // 3. Base64 인코딩
            string base64 = Convert.ToBase64String(imageBytes);
            
            // 4. 구글 렌즈 URL 생성
            string lensUrl = $"https://lens.google.com/upload?ep=gsbubb&hl=ko&re=df&st={DateTimeOffset.Now.ToUnixTimeMilliseconds()}#base64:{base64}";

            // 5. 임시 HTML 파일 생성 (커맨드라인 길이 제한 우회)
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"google_lens_{DateTime.Now.Ticks}.html");
            
            string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Google Lens</title>
</head>
<body>
    <p>구글 렌즈로 이동 중...</p>
    <script>
        window.location.href = ""{lensUrl}"";
    </script>
</body>
</html>";

            System.IO.File.WriteAllText(tempPath, htmlContent);

            // 6. 브라우저로 HTML 파일 실행
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });
            
            // 안내 메시지
            ShowGuideMessage("구글 렌즈로 검색합니다...", TimeSpan.FromSeconds(2));
                        // 추가: 2초 후 자동으로 Ctrl+V 입력
            Task.Delay(2000).ContinueWith(_ => 
            {
                Dispatcher.Invoke(() => 
                {
                    SendCtrlV();
                });
            });
        }
        catch (Exception ex)
        {
            // 실패 시 클립보드 폴백
            ScreenCaptureUtility.CopyImageToClipboard(image);
            MessageBox.Show($"검색 실행 실패: {ex.Message}\n이미지가 클립보드에 복사되었습니다.", "오류");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.google.com/imghp?hl=ko",
                UseShellExecute = true
            });
        }
    }
    
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const int VK_CONTROL = 0x11;
    private const int VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private void SendCtrlV()
    {
        try
        {
            // Ctrl 누름
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50); // 짧은 딜레이
            // V 누름
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            // V 뗌
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(50);
            // Ctrl 뗌
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch
        {
            // 실패해도 무시
        }
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
        }
        catch (Exception ex)
        {
            MessageBox.Show($"단축키 등록 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
// 실시간 캡처 모드 시작 (F1 대기)
    public void StartRealTimeCaptureMode()
    {
        // 원래 모드 기억
        wasInTrayModeBeforeRealTimeCapture = settings.IsTrayMode;
        
        // 캡처 시작 전 메인 창이 보인다면 일반 모드로 확실히 설정
        if (this.Visibility == Visibility.Visible)
        {
            settings.IsTrayMode = false;
            Settings.Save(settings);
        }
        
        // 메인 창 숨기기
        this.Hide();
        
        // F1 키 등록 (Modifiers.None, Key.F1 = 0x70)
        var helper = new WindowInteropHelper(this);
        RegisterHotKey(helper.Handle, HOTKEY_ID_REALTIME, 0, 0x70); 
        
        // ESC 키 등록 (Modifiers.None, Key.Escape = 0x1B)
        RegisterHotKey(helper.Handle, HOTKEY_ID_REALTIME_CANCEL, 0, 0x1B);

        // 안내 메시지 표시
        var guide = new GuideWindow("원하는 화면을 띄우고 [F1] 키를 누르세요\n(취소: ESC)", null);
        guide.Show();
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

                case HOTKEY_ID_REALTIME:
                    // 1. 핫키 해제
                    UnregisterHotKey(hwnd, HOTKEY_ID_REALTIME);
                    UnregisterHotKey(hwnd, HOTKEY_ID_REALTIME_CANCEL);
                    
                    // 2. 안내창 닫기
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is GuideWindow) win.Close();
                    }

                    // 3. 원래 트레이 모드였다면 복원
                    if (wasInTrayModeBeforeRealTimeCapture)
                    {
                        settings.IsTrayMode = true;
                        Settings.Save(settings);
                    }

                    // 4. 영역 캡처 시작
                    Dispatcher.Invoke(() => StartAreaCapture());
                    handled = true;
                    break;

                case HOTKEY_ID_REALTIME_CANCEL:
                    // 1. 핫키 해제
                    UnregisterHotKey(hwnd, HOTKEY_ID_REALTIME);
                    UnregisterHotKey(hwnd, HOTKEY_ID_REALTIME_CANCEL);
                    
                    // 2. 안내창 닫기
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is GuideWindow) win.Close();
                    }

                    // 3. 취소 후 창 복원 - 원래 모드로 복원
                    if (wasInTrayModeBeforeRealTimeCapture)
                    {
                        settings.IsTrayMode = true;
                        Settings.Save(settings);
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
                    handled = true;
                    break;
            }
        }
        
        return IntPtr.Zero;
    }

    private void InitializeScreenshotCache()
    {
        // 백그라운드에서 주기적으로 스크린샷을 미리 캐시하는 타이머 설정
        screenshotCacheTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1) // 1초마다 캐시 갱신 확인
        };
        
        screenshotCacheTimer.Tick += (s, e) =>
        {
            var now = DateTime.Now;
            
            // 캐시가 만료되었거나 없으면 백그라운드에서 새로 캡처
            if (cachedScreenshot == null || (now - lastScreenshotTime) > screenshotCacheTimeout)
            {
                // 백그라운드 스레드에서 스크린샷 캡처 (UI 블로킹 방지)
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var newScreenshot = ScreenCaptureUtility.CaptureScreen();
                        
                        // UI 스레드에서 캐시 업데이트
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            cachedScreenshot = newScreenshot;
                            lastScreenshotTime = now;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch
                    {
                        // 스크린샷 캡처 실패 시 무시
                    }
                });
            }
        };
        
        screenshotCacheTimer.Start();
    }

    #endregion

    #region 간편 모드

    private void SimpleModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSimpleMode();
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
        simpleModeWindow = new SimpleModeWindow(this);
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
                    var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(0.4));
                    notification.Owner = simpleModeWindow;
                    notification.Show();
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

        // 메인 창 위치를 기준으로 간편모드 위치 지정
        // 메인창 좌표 기준 좌측 상단에 살짝 여백을 두고 표시
        simpleModeWindow.Left = this.Left + 10;
        simpleModeWindow.Top = this.Top + 10;

        // 작업표시줄 대표를 간편모드로 전환
        this.ShowInTaskbar = false;   // 본체는 작업표시줄에서 숨김
        this.Hide();                  // 본체 창 숨김 (복원 방지)

        simpleModeWindow.ShowInTaskbar = true; // 간편모드를 작업표시줄 대표로
        simpleModeWindow.Topmost = true;
        simpleModeWindow.Show();

        // 앱의 MainWindow를 간편모드로 전환하여 작업표시줄 포커스가 간편모드로 가도록 함
        Application.Current.MainWindow = simpleModeWindow;
    }

    private void HideSimpleMode()
    {
        if (simpleModeWindow != null)
        {
            // 작업표시줄 대표를 다시 본체로 복구
            simpleModeWindow.ShowInTaskbar = false;
            // Close() 대신 Hide()를 사용하여 프로그램 종료 방지
            simpleModeWindow.Hide();
            // 간편모드 창 참조 해제 (이벤트 핸들러는 자동으로 해제됨)
            simpleModeWindow = null;
        }
        
        // 메인 창 복원 및 작업표시줄 아이콘 다시 표시
        this.ShowInTaskbar = true;
        this.WindowState = WindowState.Normal;
        this.Show();
        this.Activate();

        // 앱의 MainWindow를 본체로 복구
        Application.Current.MainWindow = this;
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
        var win = new SettingsWindow();
        win.Owner = this;
        var result = win.ShowDialog();
        if (result == true)
        {
            // Reload updated settings so hotkeys and options apply immediately
            settings = Settings.Load();
            // 단축키 재등록
            RegisterGlobalHotkeys();  // ← 주석 해제하고 메서드명 수정
            ShowGuideMessage("설정이 적용되었습니다.", TimeSpan.FromSeconds(1));
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
        // 지연 캡처: 기본 3초 바로 실행
        if (MatchHotkey(hk.DelayCapture, e))
        {
            StartDelayedAreaCaptureSeconds(3);
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
        // 설정 열기
        if (MatchHotkey(hk.OpenSettings, e))
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
            // Reload settings after potential changes
            settings = Settings.Load();
            return true;
        }
        return false;
    }

    public void TriggerRealTimeCapture()
    {
        StartRealTimeCaptureMode();
    }
    // 설정기반 지연 캡처(초)
    private void StartDelayedAreaCaptureSeconds(int seconds)
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
            StartAreaCapture();
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
            Dispatcher.Invoke(StartAreaCapture);
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

            // 강제 가비지 컬렉션으로 메모리 누수 방지
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch { /* 정리 중 오류 무시 */ }
    }

    #region Trigger Methods for TrayModeWindow
    
    public void TriggerDelayCapture(int seconds = 0)
    {
        if (seconds > 0)
        {
            captureDelaySeconds = seconds;
        }
        
        // 실시간 카운트다운 표시 후 캡처 시작
        if (captureDelaySeconds <= 0)
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
        countdown.StartCountdown(captureDelaySeconds, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(StartAreaCapture);
        });
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

    #endregion
}