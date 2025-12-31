using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Windows.Media;
using CatchCapture.Models;

namespace CatchCapture;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string PipeName = "CatchCapture_ActivationPipe";
    private CancellationTokenSource? _pipeServerCts;
    public static bool IsNoteAuthenticated { get; set; } = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Global\\CatchCapture_SingleInstance", out createdNew);

        if (!createdNew)
        {
            // Already running: send activation signal via named pipe
            SendActivationSignal();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 언어 설정 로드 및 적용
        var settings = CatchCapture.Models.Settings.Load();
        CatchCapture.Resources.LocalizationManager.SetLanguage(settings.Language ?? "ko");
        
        // 테마 설정 적용
        ApplyTheme(settings);
        Settings.SettingsChanged += OnSettingsChanged;

        // 자동시작 여부 확인
        bool isAutoStart = e.Args.Length > 0 && 
                          (e.Args[0].Equals("/autostart", StringComparison.OrdinalIgnoreCase) || 
                           e.Args[0].Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        
        // MainWindow 생성
        var mainWindow = new MainWindow(isAutoStart);
        Application.Current.MainWindow = mainWindow;
        
        if (!isAutoStart)
        {
            mainWindow.Show();
        }
        
        // Start pipe server to listen for activation signals
        StartPipeServer();
    }

    private void SendActivationSignal()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000); // 1 second timeout
            using var writer = new StreamWriter(client);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to send activation signal: {ex.Message}");
        }
    }

    private void StartPipeServer()
    {
        _pipeServerCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_pipeServerCts.Token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(_pipeServerCts.Token);
                    
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync();
                    
                    if (message == "ACTIVATE")
                    {
                        // Activate main window on UI thread
                        Dispatcher.Invoke(() =>
                        {
                            // Application.Current.MainWindow may change to SimpleModeWindow
                            // Search all windows to find the MainWindow instance
                            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                            if (mainWindow != null)
                            {
                                mainWindow.ActivateWindow();
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pipe server error: {ex.Message}");
                }
            }
        }, _pipeServerCts.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            CatchCapture.Utilities.DatabaseManager.Instance.CleanupTempFiles();
            CatchCapture.Utilities.DatabaseManager.Instance.CloseConnection();
            // DB 최적화 (VACUUM) 실행
            CatchCapture.Utilities.DatabaseManager.Instance.Vacuum();

            _pipeServerCts?.Cancel();
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
            // ignore
        }
        base.OnExit(e);
    }
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var settings = Settings.Load();
            ApplyTheme(settings);
        });
    }

    public static void ApplyTheme(Settings settings)
    {
        if (settings == null) return;
        
        try
        {
            var bgUrl = settings.ThemeBackgroundColor ?? "#FFFFFF";
            var fgUrl = settings.ThemeTextColor ?? "#333333";

            Color bgColor = (Color)ColorConverter.ConvertFromString(bgUrl);
            Color fgColor = (Color)ColorConverter.ConvertFromString(fgUrl);

            if (Application.Current != null && Application.Current.Resources != null)
            {
                // Derive colors based on brightness
                double brightness = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
                
                // Calculate dynamic colors
                Color sidebarBg, sidebarBorder, sidebarHover, sidebarPressed, windowBorder;

                if (brightness < 0.5) // Dark Theme
                {
                    windowBorder = AdjustColor(bgColor, 15);
                    sidebarBg = AdjustColor(bgColor, -15); // Increased contrast for dark mode
                    sidebarBorder = AdjustColor(bgColor, 20);
                    sidebarHover = AdjustColor(bgColor, 25);
                    sidebarPressed = AdjustColor(bgColor, 35);
                    
                    Application.Current.Resources["ThemePanelBackground"] = new SolidColorBrush(sidebarBg);
                }
                else // Light Theme
                {
                    windowBorder = AdjustColor(bgColor, -20);
                    sidebarBg = AdjustColor(bgColor, -6); // #F9F9F9 for white background
                    sidebarBorder = windowBorder; 
                    sidebarHover = AdjustColor(bgColor, -15);
                    sidebarPressed = AdjustColor(bgColor, -25);

                    // User requirements overrides
                    if (settings.ThemeMode == "General")
                    {
                        bgColor = Color.FromRgb(255, 255, 255); // 전체 배경 255
                        sidebarBg = Color.FromRgb(249, 249, 249); // 버튼 249
                        windowBorder = Color.FromRgb(224, 224, 224); // #E0E0E0 라인 살림
                        sidebarBorder = windowBorder;
                    }
                    else if (settings.ThemeMode == "Light")
                    {
                        bgColor = Color.FromRgb(255, 255, 255); // 전체 흰색
                        sidebarBg = Color.FromRgb(249, 249, 249); // 버튼 249
                        windowBorder = AdjustColor(bgColor, -20); // 라인 살리고
                        sidebarBorder = windowBorder;
                    }
                    else if (settings.ThemeMode == "Blue")
                    {
                        // bgColor remains blue
                        sidebarBg = Color.FromRgb(249, 249, 249); // 버튼 249
                        windowBorder = AdjustColor(bgColor, -20); // 라인 살리고
                        sidebarBorder = windowBorder;
                    }

                    // For these modes, clarify panel background is white (behind gray buttons)
                    if (settings.ThemeMode == "General" || settings.ThemeMode == "Light" || settings.ThemeMode == "Blue")
                    {
                        Application.Current.Resources["ThemePanelBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    }
                    else
                    {
                        Application.Current.Resources["ThemePanelBackground"] = new SolidColorBrush(bgColor);
                    }
                }

                // Apply resources
                Application.Current.Resources["ThemeBackground"] = new SolidColorBrush(bgColor);
                Application.Current.Resources["ThemeColorBackground"] = bgColor;
                Application.Current.Resources["ThemeForeground"] = new SolidColorBrush(fgColor);
                Application.Current.Resources["ThemeColorForeground"] = fgColor;

                Application.Current.Resources["ThemeWindowBorder"] = new SolidColorBrush(windowBorder);
                Application.Current.Resources["ThemeBorder"] = new SolidColorBrush(windowBorder); 
                Application.Current.Resources["ThemeSidebarButtonBackground"] = new SolidColorBrush(sidebarBg);
                Application.Current.Resources["ThemeColorSidebarButtonBackground"] = sidebarBg;
                Application.Current.Resources["ThemeSidebarButtonBorder"] = new SolidColorBrush(sidebarBorder);
                Application.Current.Resources["ThemeSidebarButtonHoverBackground"] = new SolidColorBrush(sidebarHover);
                Application.Current.Resources["ThemeSidebarButtonPressedBackground"] = new SolidColorBrush(sidebarPressed);

                // Title Bar Colors (Specific colors for General mode as per user feedback)
                if (settings.ThemeMode == "General")
                {
                    // High-Glossy Blue Gradient for Main Title Bar (vibrant dark blue)
                    var mainGlossyBrush = new LinearGradientBrush();
                    mainGlossyBrush.StartPoint = new Point(0, 0);
                    mainGlossyBrush.EndPoint = new Point(0, 1);
                    mainGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(110, 140, 240), 0.0)); // 상단 하이라이트
                    mainGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(78, 106, 223), 0.4));  // 메인 블루
                    mainGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(50, 75, 180), 1.0));   // 하단 쉐도우
                    
                    Application.Current.Resources["ThemeTitleBackground"] = mainGlossyBrush;
                    Application.Current.Resources["ThemeTitleForeground"] = Brushes.White;

                    // Light Glossy Gradient for Simple Title Bar
                    var simpleGlossyBrush = new LinearGradientBrush();
                    simpleGlossyBrush.StartPoint = new Point(0, 0);
                    simpleGlossyBrush.EndPoint = new Point(0, 1);
                    simpleGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(225, 240, 255), 0.0));
                    simpleGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(206, 230, 255), 0.5));
                    simpleGlossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(185, 210, 245), 1.0));
                    
                    Application.Current.Resources["ThemeSimpleTitleBackground"] = simpleGlossyBrush;
                    Application.Current.Resources["ThemeSimpleTitleForeground"] = new SolidColorBrush(Color.FromRgb(32, 61, 133)); 
                    
                    // Match window border to the bottom color of title for seamless look
                    Application.Current.Resources["ThemeWindowBorder"] = new SolidColorBrush(Color.FromRgb(70, 95, 200));
                }
                else if (settings.ThemeMode == "Dark")
                {
                    // High-Glossy Dark Gradient (하이그로시 느낌의 블랙 그라데이션)
                    var glossyBrush = new LinearGradientBrush();
                    glossyBrush.StartPoint = new Point(0, 0);
                    glossyBrush.EndPoint = new Point(0, 1);
                    glossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(55, 55, 55), 0.0)); // 상단: 반사 느낌의 그레이
                    glossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(30, 30, 30), 0.4)); // 중간
                    glossyBrush.GradientStops.Add(new GradientStop(Color.FromRgb(10, 10, 10), 1.0)); // 하단: 딥 블랙
                    
                    Application.Current.Resources["ThemeTitleBackground"] = glossyBrush;
                    Application.Current.Resources["ThemeTitleForeground"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                    
                    Application.Current.Resources["ThemeSimpleTitleBackground"] = glossyBrush;
                    Application.Current.Resources["ThemeSimpleTitleForeground"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                    
                    // Sharp glossy outline (선명한 하이그로시 윤곽선)
                    Application.Current.Resources["ThemeWindowBorder"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                }
                else
                {
                    // For Blue and Light modes, use theme-based colors
                    var bgBrush = new SolidColorBrush(bgColor);
                    var fgBrush = new SolidColorBrush(fgColor);
                    Application.Current.Resources["ThemeTitleBackground"] = bgBrush;
                    Application.Current.Resources["ThemeTitleForeground"] = fgBrush;
                    Application.Current.Resources["ThemeSimpleTitleBackground"] = bgBrush;
                    Application.Current.Resources["ThemeSimpleTitleForeground"] = fgBrush;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex.Message}");
        }
    }

    private static Color AdjustColor(Color baseColor, int amount)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(baseColor.R + amount, 0, 255),
            (byte)Math.Clamp(baseColor.G + amount, 0, 255),
            (byte)Math.Clamp(baseColor.B + amount, 0, 255)
        );
    }
}
