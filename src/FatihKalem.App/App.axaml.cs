using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace FatihKalem.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartBoard(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void StartBoard(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var options = AppLifetime.ParseArgs(desktop.Args ?? []);
        var shell = new AppShell(AppPaths.Default);

        // a later launch finds the lock taken and asks us to come forward (see Program.Main)
        if (Program.Instance is { } instance)
            instance.ActivateRequested += () => Dispatcher.UIThread.Post(shell.Activate);

        shell.ExitRequested += () => desktop.Shutdown();
        shell.Window.Closed += (_, _) => desktop.Shutdown();
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // /PreLoad keeps the board hidden until a later launch asks for it.
        if (!options.PreLoad) shell.Activate();
    }
}
