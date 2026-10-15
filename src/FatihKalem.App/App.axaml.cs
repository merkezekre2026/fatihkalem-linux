using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace FatihKalem.App;

public partial class App : Application
{
    private SingleInstance? _instance;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartBoard(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void StartBoard(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var options = AppLifetime.ParseArgs(desktop.Args ?? []);
        var lockPath = AppLifetime.DefaultLockPath;

        _instance = SingleInstance.TryAcquire(lockPath);
        if (_instance is null)
        {
            // Another board is already running: ask it to come forward and leave.
            SingleInstance.SignalExisting(lockPath);
            desktop.Shutdown();
            return;
        }

        var shell = new AppShell(AppPaths.Default);
        _instance.ActivateRequested += () => Dispatcher.UIThread.Post(shell.Activate);
        shell.ExitRequested += () => desktop.Shutdown();
        shell.Window.Closed += (_, _) => desktop.Shutdown();
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.Exit += (_, _) => _instance.Dispose();

        // /PreLoad keeps the board hidden until a later launch asks for it.
        if (!options.PreLoad) shell.Activate();
    }
}
