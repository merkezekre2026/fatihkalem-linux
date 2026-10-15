using Avalonia;

namespace FatihKalem.App;

internal static class Program
{
    /// <summary>The lock this process holds while it is the running board.</summary>
    internal static SingleInstance? Instance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = AppLifetime.AcquireOrSignal(AppLifetime.DefaultLockPath);
        if (instance is null) return; // a board is already running and has been asked to come forward

        Instance = instance;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
