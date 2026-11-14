using System.Windows;
namespace ArcHive.App;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "Local\\ArcHive-" + Environment.UserName, out var created);
        if (!created) { MessageBox.Show("ArcHive is already open."); Shutdown(); return; }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
