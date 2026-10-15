using FatihKalem.Core.Settings;

namespace FatihKalem.Core.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk-autostart-" + Guid.NewGuid().ToString("N"), "autostart");
    private string EntryPath => Path.Combine(_dir, "fatih-kalem.desktop");

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_dir)!;
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string ExecLine() => File.ReadAllLines(EntryPath).Single(l => l.StartsWith("Exec=", StringComparison.Ordinal));

    [Fact]
    public void Enable_WritesDesktopEntry()
    {
        new Autostart(_dir).Enable("/opt/fatih kalem/FatihKalem", preload: false);
        var lines = File.ReadAllLines(EntryPath);
        Assert.Equal("[Desktop Entry]", lines[0]);
        Assert.Contains("Type=Application", lines);
        Assert.Contains("Name=Fatih Pen", lines);
        Assert.Equal("Exec=\"/opt/fatih kalem/FatihKalem\"", ExecLine());
    }

    [Fact]
    public void Enable_Preload_AppendsFlag()
    {
        new Autostart(_dir).Enable("/opt/FatihKalem", preload: true);
        Assert.EndsWith("\" /PreLoad", ExecLine());
    }

    [Fact]
    public void Enable_QuotesPathsWithSpacesAndTurkishChars()
    {
        const string path = "/home/a b/Fatih Kalem Ş.AppImage";
        new Autostart(_dir).Enable(path, preload: false);
        Assert.Equal($"Exec=\"{path}\"", ExecLine());
        var bytes = File.ReadAllBytes(EntryPath);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "no UTF-8 BOM");
    }

    [Fact]
    public void Enable_EscapesSpecialCharacters()
    {
        new Autostart(_dir).Enable("/opt/we$ird`dir/100%/App", preload: false);
        // Exec-level escape (\$) is doubled by the .desktop string layer (\\$).
        Assert.Equal("Exec=\"/opt/we\\\\$ird\\\\`dir/100%%/App\"", ExecLine());
    }

    [Fact]
    public void Enable_CreatesDirectory_AndOverwrites()
    {
        var a = new Autostart(_dir);
        a.Enable("/one", preload: false);
        a.Enable("/two", preload: true);
        Assert.Equal("Exec=\"/two\" /PreLoad", ExecLine());
    }

    [Fact]
    public void Disable_RemovesFile()
    {
        var a = new Autostart(_dir);
        a.Enable("/x", preload: false);
        a.Disable();
        Assert.False(File.Exists(EntryPath));
    }

    [Fact]
    public void Disable_WhenMissing_DoesNotThrow() => new Autostart(_dir).Disable();

    [Fact]
    public void IsEnabled_ReflectsFile()
    {
        var a = new Autostart(_dir);
        Assert.False(a.IsEnabled);
        a.Enable("/x", preload: false);
        Assert.True(a.IsEnabled);
        a.Disable();
        Assert.False(a.IsEnabled);
    }

    [Fact]
    public void IsPreload_ReadsBackFlag()
    {
        var a = new Autostart(_dir);
        Assert.False(a.IsPreload);
        a.Enable("/x", preload: false);
        Assert.False(a.IsPreload);
        a.Enable("/x", preload: true);
        Assert.True(a.IsPreload);
    }

    [Fact]
    public void ResolveExecPath_PrefersAppImageEnv() =>
        Assert.Equal("/tmp/Fatih.AppImage", Autostart.ResolveExecPath("/tmp/Fatih.AppImage", "/tmp/.mount_abc/usr/bin/FatihKalem"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveExecPath_FallsBackToProcessPath(string? appImage) =>
        Assert.Equal("/usr/bin/FatihKalem", Autostart.ResolveExecPath(appImage, "/usr/bin/FatihKalem"));

    [Fact]
    public void ResolveDirectory_UsesXdgOrDotConfig()
    {
        Assert.Equal(Path.Combine("/x/cfg", "autostart"), Autostart.ResolveDirectory("/x/cfg", "/home/u"));
        Assert.Equal(Path.Combine("/home/u", ".config", "autostart"), Autostart.ResolveDirectory(null, "/home/u"));
    }
}
