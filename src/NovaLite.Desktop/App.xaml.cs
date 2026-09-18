using System.IO;
using System.Windows;
namespace NovaLite.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (await DiagnosticCommandRunner.TryRunAsync(e.Args) is { } exitCode)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(exitCode);
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (e.Args.Length == 2 && e.Args[0] is "--smoke" or "--smoke-windows")
            window.RenderSmoke(Path.GetFullPath(e.Args[1]), e.Args[0] == "--smoke-windows");
        else if (e.Args.Length == 3 && e.Args[0] == "--smoke-theme")
            window.RenderSmoke(Path.GetFullPath(e.Args[2]), themeKey: e.Args[1]);
        else if (e.Args.Length == 3 && e.Args[0] == "--smoke-language")
            window.RenderSmoke(Path.GetFullPath(e.Args[2]), languageKey: e.Args[1]);
        else if (e.Args.Length == 3 && e.Args[0] == "--smoke-settings")
            window.RenderSmoke(Path.GetFullPath(e.Args[2]), languageKey: e.Args[1], showSettings: true);
    }
}
