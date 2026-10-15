using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using FatihKalem.App.Controls;
using FatihKalem.App.ViewModels;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;

namespace FatihKalem.App.Tests;

public sealed class ToolbarViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk-toolbar-" + Guid.NewGuid().ToString("N"));
    private readonly ToolState _tool = new();
    private readonly InkDocument _doc = new();

    private string SettingsPath => Path.Combine(_dir, "settings.json");
    private string AutostartDir => Path.Combine(_dir, "autostart");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ToolbarViewModel Make() =>
        new(_tool, _doc, new SettingsStore(SettingsPath), new Autostart(AutostartDir));

    private AppSettings Saved() => new SettingsStore(SettingsPath).Load();

    [Fact]
    public void SelectPen_UpdatesToolAndPersists()
    {
        var vm = Make();
        vm.SelectPen(PenKind.Marker, 1);
        Assert.Equal(PenKind.Marker, _tool.Pen);
        Assert.Equal(0xFFFF0000u, _tool.Argb);
        Assert.Null(_tool.Shape);
        Assert.False(_tool.IsEraser);
        var s = Saved();
        Assert.Equal((byte)0, s.PenStyle);
        Assert.Equal((byte)1, s.ColorNo);
        Assert.Equal(0xFFFF0000u, s.InkColor);
    }

    [Fact]
    public void SelectPen_FromHandMode_SwitchesToPenMode()
    {
        var vm = Make();
        var changes = new List<InputMode>();
        vm.ModeChanged += () => changes.Add(vm.Mode);
        vm.ToggleHand();
        vm.SelectPen(PenKind.Highlighter, 4);
        Assert.Equal(InputMode.Pen, vm.Mode);
        Assert.Equal([InputMode.Hand, InputMode.Pen], changes);
        Assert.Equal(0xFFFFFF01u, _tool.Argb);
    }

    [Fact]
    public void SelectColor_Colourful_StoresChosenColourAsColorNo7()
    {
        var vm = Make();
        vm.SelectColor(0xFF336699);
        Assert.Equal(0xFF336699u, _tool.Argb);
        var s = Saved();
        Assert.Equal((byte)7, s.ColorNo);
        Assert.Equal(0xFF336699u, s.InkColor);
    }

    [Fact]
    public void SelectSize_SelectEraser_SelectShape_AreMutuallyConsistent()
    {
        var vm = Make();
        vm.SelectSize(6);
        Assert.Equal(6, _tool.SizeLevel);
        Assert.Equal((byte)6, Saved().InkSize);

        vm.SelectEraser(3);
        Assert.True(_tool.IsEraser);
        Assert.Equal(3, _tool.EraserLevel);
        Assert.Null(_tool.Shape);

        vm.SelectShape(DrawKind.Arrow);
        Assert.Equal(DrawKind.Arrow, _tool.Shape);
        Assert.False(_tool.IsEraser);

        vm.SelectPen(PenKind.Marker, 5);
        Assert.Null(_tool.Shape);
        Assert.False(_tool.IsEraser);
    }

    [Fact]
    public void ToggleHand_SwitchesMode()
    {
        var vm = Make();
        Assert.Equal(InputMode.Pen, vm.Mode);
        vm.ToggleHand();
        Assert.Equal(InputMode.Hand, vm.Mode);
        vm.ToggleHand();
        Assert.Equal(InputMode.Pen, vm.Mode);
    }

    [Fact]
    public void AddFavorite_PersistsInFavAndIFav()
    {
        var vm = Make();
        vm.AddFavorite("MarkerRed");
        vm.AddFavorite("Eraser2");
        Assert.Equal(["MarkerRed", "Eraser2"], vm.Favorites);
        var s = Saved();
        Assert.Equal("MarkerRed", s.Fav[0]);
        Assert.Equal("Eraser2", s.Fav[1]);
        Assert.Equal((byte)2, s.IFav);
    }

    [Fact]
    public void AddFavorite_Beyond20_Ignored()
    {
        var vm = Make();
        for (var i = 0; i < 25; i++) vm.AddFavorite(i % 2 == 0 ? $"Size{i % 6 + 1}" : $"Eraser{i % 4 + 1}");
        Assert.True(vm.Favorites.Count <= 20);
        Assert.Equal(vm.Favorites.Count, Saved().IFav);
    }

    [Fact]
    public void AddFavorite_DuplicateOrUnknown_Ignored()
    {
        var vm = Make();
        vm.AddFavorite("Line");
        vm.AddFavorite("Line");
        vm.AddFavorite("NoSuchTool");
        vm.AddFavorite("");
        Assert.Equal(["Line"], vm.Favorites);
    }

    [Theory]
    [InlineData("Eraser2", true, 2)]
    [InlineData("Eraser4", true, 4)]
    public void ActivateFavorite_Eraser_SetsEraserLevel(string name, bool eraser, int level)
    {
        var vm = Make();
        vm.AddFavorite(name);
        vm.ActivateFavorite(0);
        Assert.Equal(eraser, _tool.IsEraser);
        Assert.Equal(level, _tool.EraserLevel);
    }

    [Fact]
    public void ActivateFavorite_PenSizeShapeAndActions()
    {
        var vm = Make();
        foreach (var n in new[] { "HighlighterYellow", "Size3", "Line", "StylographBlue", "Undo", "Redo" }) vm.AddFavorite(n);

        vm.ActivateFavorite(0);
        Assert.Equal((PenKind.Highlighter, 0xFFFFFF01u), (_tool.Pen, _tool.Argb));
        vm.ActivateFavorite(1);
        Assert.Equal(3, _tool.SizeLevel);
        vm.ActivateFavorite(2);
        Assert.Equal(DrawKind.Line, _tool.Shape);
        vm.ActivateFavorite(3);
        Assert.Equal((PenKind.Stylograph, 0xFF00009Du), (_tool.Pen, _tool.Argb));

        _doc.Add(new InkStroke([new InkPoint(1, 1)], PenTable.Create(PenKind.Marker, 1, 0xFF000000)));
        vm.ActivateFavorite(4); // Undo
        Assert.Empty(_doc.Strokes);
        vm.ActivateFavorite(5); // Redo
        Assert.Single(_doc.Strokes);
    }

    [Fact]
    public void ActivateFavorite_NoPen_SwitchesToHand()
    {
        var vm = Make();
        vm.AddFavorite("MarkerNoPen");
        vm.ActivateFavorite(0);
        Assert.Equal(InputMode.Hand, vm.Mode);
    }

    [Fact]
    public void ActivateFavorite_LibraryAndBackground_RaiseRequests()
    {
        var vm = Make();
        var requested = new List<string>();
        vm.LibraryRequested += () => requested.Add("library");
        vm.BackgroundRequested += () => requested.Add("background");
        vm.AddFavorite("Library");
        vm.AddFavorite("Background");
        vm.ActivateFavorite(0);
        vm.ActivateFavorite(1);
        Assert.Equal(["library", "background"], requested);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public void ActivateFavorite_OutOfRange_IsIgnored(int index)
    {
        var vm = Make();
        vm.AddFavorite("Size2");
        _tool.SizeLevel = 4;
        vm.ActivateFavorite(index);
        Assert.Equal(4, _tool.SizeLevel);
    }

    [Fact]
    public void ActivateFavorite_UnknownNameStoredInSettings_IsIgnored()
    {
        var fav = Enumerable.Repeat("", 20).ToArray();
        fav[0] = "Curtain"; // known to the original, not part of v1
        fav[1] = "Whatever";
        new SettingsStore(SettingsPath).Save(new AppSettings { Fav = fav, IFav = 2 });
        var vm = Make();
        _tool.SizeLevel = 4;
        vm.ActivateFavorite(0);
        vm.ActivateFavorite(1);
        Assert.Equal(4, _tool.SizeLevel);
    }

    [Fact]
    public void Startup_RestoresToolFromSettings()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings { PenStyle = 2, ColorNo = 4, InkSize = 6 });
        Make();
        Assert.Equal(PenKind.Highlighter, _tool.Pen);
        Assert.Equal(0xFFFFFF01u, _tool.Argb);
        Assert.Equal(6, _tool.SizeLevel);
    }

    [Fact]
    public void Startup_Colourful_UsesSavedInkColor()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings { PenStyle = 0, ColorNo = 7, InkColor = 0xFF102030 });
        Make();
        Assert.Equal(0xFF102030u, _tool.Argb);
    }

    [Fact]
    public void Startup_Defaults_MatchOriginal()
    {
        Make();
        Assert.Equal(PenKind.Stylograph, _tool.Pen);
        Assert.Equal(0xFF00009Du, _tool.Argb);
        Assert.Equal(3, _tool.SizeLevel);
    }

    [Fact]
    public void AutoStartToggle_WritesAndRemovesDesktopEntry()
    {
        var vm = Make();
        vm.SetAutoStart(true, "/opt/Fatih Kalem.AppImage");
        Assert.True(new Autostart(AutostartDir).IsEnabled);
        Assert.True(Saved().AutoStart);
        vm.SetAutoStart(false, "/opt/Fatih Kalem.AppImage");
        Assert.False(new Autostart(AutostartDir).IsEnabled);
        Assert.False(Saved().AutoStart);
    }

    [Fact]
    public void SettingsToggles_Persist()
    {
        var vm = Make();
        vm.SetCloseConfirmation(true);
        vm.SetSideArrows(false);
        var s = Saved();
        Assert.True(s.IsCloseConfirmationEnabled);
        Assert.False(s.IsSideArrowsEnabled);
    }

    [Fact]
    public void ToolbarPosition_Persists_AndClearsStartupDefault()
    {
        var vm = Make();
        vm.SaveToolbarPosition(120, 45);
        var s = Saved();
        Assert.False(s.IsStartupPositionDefault);
        Assert.Equal((120, 45), (s.Left, s.Top));
    }

    [Fact]
    public void UndoRedoClear_DelegateToDocument()
    {
        var vm = Make();
        _doc.Add(new InkStroke([new InkPoint(1, 1)], PenTable.Create(PenKind.Marker, 1, 0xFF000000)));
        vm.Clear();
        Assert.Empty(_doc.Strokes);
        vm.Undo();
        Assert.Single(_doc.Strokes);
        vm.Undo();
        Assert.Empty(_doc.Strokes);
        vm.Redo();
        Assert.Single(_doc.Strokes);
    }

    [AvaloniaFact]
    public void EveryFavoriteHasAnIconAsset()
    {
        foreach (var name in FavoriteCatalog.Names)
        {
            var uri = new Uri(FavoriteCatalog.IconUri(name));
            Assert.True(AssetLoader.Exists(uri), $"missing icon for {name}: {uri}");
        }
        Assert.Equal(45 - 1, FavoriteCatalog.Names.Count); // 45 original names minus Curtain (v2)
    }
}
