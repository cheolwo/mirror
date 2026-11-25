using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ssalddel.BatchMonitor.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs args)
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        base.OnStartup(args);
    }
}
