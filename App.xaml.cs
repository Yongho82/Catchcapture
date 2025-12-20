using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;

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
}
