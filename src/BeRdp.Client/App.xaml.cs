using System.Windows;
using BeRdp.Client.Services;

namespace BeRdp.Client;

public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _instance;
    private bool _ownsInstance;

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstance) _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        _instance = new System.Threading.Mutex(true, @"Local\BeRDP.Client." + sid, out _ownsInstance);
        if (!_ownsInstance)
        {
            MessageBox.Show("BeRDP zaten çalışıyor. Açık istemci penceresini kullanın.", "BeRDP");
            Shutdown();
            return;
        }
        base.OnStartup(e);
        ThemeManager.Initialize();
    }
}
