using System.Text.Json.Nodes;
using FatihKalem.Core.Settings;

namespace FatihKalem.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "sub dir", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(0xFF00009Du, s.InkColor);
        Assert.Equal((byte)1, s.PenStyle);
        Assert.Equal((byte)2, s.ColorNo);
        Assert.Equal((byte)3, s.InkSize);
        Assert.True(s.IsStartupPositionDefault);
        Assert.False(s.AutoStart);
        Assert.True(s.IsSideArrowsEnabled);
        Assert.True(s.IsShortGestureEnabled);
        Assert.False(s.IsCloseConfirmationEnabled);
        Assert.True(s.IsGestureEnabled);
        Assert.Equal(20, s.Fav.Length);
        Assert.All(s.Fav, f => Assert.Equal("", f));
        Assert.Equal((byte)0, s.IFav);
    }

    [Fact]
    public void RoundTrip_PreservesAll()
    {
        var fav = Enumerable.Repeat("", 20).ToArray();
        fav[0] = "MarkerRed";
        fav[19] = "Eraser2";
        var original = new AppSettings
        {
            PenStyle = 2, ColorNo = 4, InkSize = 6, IsStartupPositionDefault = false, Left = 120, Top = -30,
            AutoStart = true, InkColor = 0xFF123456, IsSideArrowsEnabled = false, IsShortGestureEnabled = false,
            IsCloseConfirmationEnabled = true, IsGestureEnabled = false, Fav = fav, IFav = 2,
        };
        var store = new SettingsStore(FilePath);
        store.Save(original);
        var loaded = store.Load();
        Assert.Equal(original with { Fav = [] }, loaded with { Fav = [] });
        Assert.Equal(fav, loaded.Fav);
    }

    [Fact]
    public void Save_WritesOriginalKeyNames()
    {
        new SettingsStore(FilePath).Save(new AppSettings());
        var text = File.ReadAllText(FilePath);
        foreach (var key in new[]
                 {
                     "PenStyle", "ColorNo", "InkSize", "isStartupPositionDefault", "Left", "Top", "AutoStart", "InkColor",
                     "isSideArrowsEnabled", "isShortGestureEnabled", "isCLoseConfirmationEnabled", "isGestureEnabled",
                     "Fav1", "Fav20", "iFav",
                 })
            Assert.Contains($"\"{key}\"", text);
        Assert.Contains("\"#FF00009D\"", text);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        new SettingsStore(FilePath).Save(new AppSettings());
        Assert.True(File.Exists(FilePath));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Load_CorruptJson_ReturnsDefaultsAndWritesBak(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(new AppSettings().InkColor, s.InkColor);
        Assert.True(File.Exists(FilePath + ".bak"));
        Assert.Equal(content, File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void Load_UnknownKeys_Ignored()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "Mystery": 5, "PenStyle": 2, "Fav21": "x" }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal((byte)2, s.PenStyle);
        Assert.False(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void Load_PartialFile_FillsMissingWithDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "InkSize": 5 }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal((byte)5, s.InkSize);
        Assert.Equal((byte)1, s.PenStyle);
        Assert.Equal(0xFF00009Du, s.InkColor);
    }

    [Fact]
    public void Load_WrongTypesOrOutOfRange_FallBackPerKey()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """
            { "PenStyle": 9, "ColorNo": "red", "InkSize": 0, "InkColor": "nope", "iFav": 99, "Left": "x", "AutoStart": "yes" }
            """);
        var s = new SettingsStore(FilePath).Load();
        var d = new AppSettings();
        Assert.Equal(d.PenStyle, s.PenStyle);
        Assert.Equal(d.ColorNo, s.ColorNo);
        Assert.Equal(d.InkSize, s.InkSize);
        Assert.Equal(d.InkColor, s.InkColor);
        Assert.Equal(d.IFav, s.IFav);
        Assert.Equal(d.Left, s.Left);
        Assert.Equal(d.AutoStart, s.AutoStart);
    }

    [Fact]
    public void Save_IsValidJsonObject()
    {
        new SettingsStore(FilePath).Save(new AppSettings());
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(FilePath)));
    }

    [Fact]
    public void DefaultPath_UsesXdgConfigHomeWhenSet()
    {
        Assert.Equal(
            Path.Combine("/x/cfg", "FatihKalem", "settings.json"),
            SettingsStore.ResolveDefaultPath("/x/cfg", "/home/u"));
    }

    [Fact]
    public void DefaultPath_FallsBackToDotConfigUnderHome()
    {
        Assert.Equal(
            Path.Combine("/home/a b", ".config", "FatihKalem", "settings.json"),
            SettingsStore.ResolveDefaultPath(null, "/home/a b"));
        Assert.EndsWith(
            Path.Combine("FatihKalem", "settings.json"),
            SettingsStore.DefaultPath);
    }
}
