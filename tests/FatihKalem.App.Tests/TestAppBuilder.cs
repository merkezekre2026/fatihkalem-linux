using Avalonia;
using Avalonia.Headless;
using FatihKalem.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace FatihKalem.App.Tests;

public static class TestAppBuilder
{
    // Real Skia rendering (not the null drawing backend) so tests can read pixels back.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<FatihKalem.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
