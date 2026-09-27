using System.Windows;
using TableSnap.Configuration;

namespace TableSnap;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalEnvironment.Load();
        base.OnStartup(e);
    }
}
