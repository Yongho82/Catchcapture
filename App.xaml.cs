using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading; 

namespace CatchCapture;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        // Global-named mutex to ensure single instance across user sessions as well
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Global\\CatchCapture_SingleInstance", out createdNew);

        if (!createdNew)
        {
            // Already running: inform the user and exit
            MessageBox.Show("CatchCapture가 이미 실행 중입니다.", "CatchCapture", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 언어 설정 로드 및 적용
        var settings = CatchCapture.Models.Settings.Load();
        CatchCapture.Resources.LocalizationManager.SetLanguage(settings.Language ?? "ko");
        
        // 자동시작 여부 확인 (명령줄 인자에 /autostart 또는 --autostart가 있는지 확인)
        bool isAutoStart = e.Args.Length > 0 && 
                          (e.Args[0].Equals("/autostart", StringComparison.OrdinalIgnoreCase) || 
                           e.Args[0].Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        
        // MainWindow 생성 시 자동시작 여부 전달
        var mainWindow = new MainWindow(isAutoStart);
        Application.Current.MainWindow = mainWindow;
        
        // 자동시작이 아닌 경우에만 창 표시 (자동시작은 MainWindow 내부에서 처리)
        if (!isAutoStart)
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
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
