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
