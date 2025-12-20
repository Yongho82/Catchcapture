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
                Application.Current.Resources["ThemeBackground"] = new SolidColorBrush(bgColor);
                Application.Current.Resources["ThemeForeground"] = new SolidColorBrush(fgColor);

                // Derive a border color based on brightness
                double brightness = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
                Color borderColor;
                if (brightness < 0.5) // Dark background
                {
                    borderColor = Color.FromRgb(80, 80, 80); // Lighter border for dark bg
                }
                else // Light background
                {
                    borderColor = Color.FromRgb(220, 220, 220); // Darker border for light bg
                }
                Application.Current.Resources["ThemeBorder"] = new SolidColorBrush(borderColor);
                Application.Current.Resources["ThemeWindowBorder"] = new SolidColorBrush(borderColor); // Default to standard border

                // Title Bar Colors (Specific colors for General mode as per user feedback)
                if (settings.ThemeMode == "General")
                {
                    var mainBlue = Color.FromRgb(78, 106, 223);
                    Application.Current.Resources["ThemeTitleBackground"] = new SolidColorBrush(mainBlue); 
                    Application.Current.Resources["ThemeTitleForeground"] = Brushes.White;
                    Application.Current.Resources["ThemeSimpleTitleBackground"] = new SolidColorBrush(Color.FromRgb(206, 230, 255)); 
                    Application.Current.Resources["ThemeSimpleTitleForeground"] = new SolidColorBrush(Color.FromRgb(32, 61, 133)); 
                    
                    // Match window border to title for seamless look
                    Application.Current.Resources["ThemeWindowBorder"] = new SolidColorBrush(mainBlue);
                }
                else if (settings.ThemeMode == "Dark")
                {
                    Application.Current.Resources["ThemeTitleBackground"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                    Application.Current.Resources["ThemeTitleForeground"] = Brushes.White;
                    Application.Current.Resources["ThemeSimpleTitleBackground"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                    Application.Current.Resources["ThemeSimpleTitleForeground"] = Brushes.White;
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
}
