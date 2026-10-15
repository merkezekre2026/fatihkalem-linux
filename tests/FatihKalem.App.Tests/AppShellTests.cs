using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FatihKalem.App.Platform;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;
using SkiaSharp;

namespace FatihKalem.App.Tests;

public sealed class AppShellTests : IDisposable
{
    private sealed class FakeRegion : IInputRegion
    {
        public List<IReadOnlyList<PixelRect>?> Calls { get; } = [];

        public bool TrySetInputRegion(nint nativeWindow, IReadOnlyList<PixelRect>? rects)
        {
            Calls.Add(rects);
            return true;
        }
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk shell Ş-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRegion _region = new();

    private AppPaths Paths => new(
        SettingsFile: Path.Combine(_dir, "config", "settings.json"),
        AutostartDir: Path.Combine(_dir, "autostart"),
        LibraryBuiltIn: Path.Combine(_dir, "library"),
        LibraryUser: Path.Combine(_dir, "user library"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private AppShell Make(bool show = true)
    {
        var shell = new AppShell(Paths, _region);
        if (show) shell.Window.Show();
        return shell;
    }

    private static Button Find(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    private static void Click(Visual root, string name) =>
        Find(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void Toolbar_And_Surface_DrawAStrokeWithTheChosenSize()
    {
        var shell = Make();
        Click(shell.Window, "Size6");
        shell.Window.MouseDown(new Point(700, 500), MouseButton.Left);
        shell.Window.MouseMove(new Point(760, 520));
        shell.Window.MouseUp(new Point(760, 520), MouseButton.Left);

        var stroke = Assert.Single(shell.Document.Strokes);
        // default pen is the stylograph (original default PenStyle=1); level 6 = 8 x 35
        Assert.Equal((8.0, 35.0), (stroke.Pen.Width, stroke.Pen.Height));
        Assert.Equal(0xFF00009Du, stroke.Pen.Argb);
    }

    [AvaloniaFact]
    public void HandMode_DisablesInkAndKeepsToolbarClickable()
    {
        var shell = Make();
        Click(shell.Window, "Hand");
        Assert.Equal(InputMode.Hand, shell.Window.Mode);
        Assert.False(shell.Window.Surface.IsHitTestVisible);

        var rects = Assert.IsAssignableFrom<IReadOnlyList<PixelRect>>(_region.Calls[^1]);
        Assert.NotEmpty(rects);
        var toolbar = shell.Toolbar;
        var origin = toolbar.TranslatePoint(default, shell.Window)!.Value;
        Assert.Contains(rects, r => r.X == (int)Math.Round(origin.X) && r.Y == (int)Math.Round(origin.Y));

        Click(shell.Window, "Hand");
        Assert.Equal(InputMode.Pen, shell.Window.Mode);
        Assert.True(shell.Window.Surface.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Library_OpensPicker_PlacesImage_AndClosesPicker()
    {
        TestImages.WritePng(Path.Combine(Paths.LibraryBuiltIn, "Görseller", "Hayvanlar", "Ayı.png"), 100, 50, SKColors.Red);
        var shell = Make();
        Click(shell.Window, "Library");
        shell.Window.UpdateLayout(); // the picker's ScrollViewer template is applied on the next layout pass
        Click(shell.Window, "Item0");
        var item = Assert.Single(shell.Document.Items);
        Assert.EndsWith("Ayı.png", item.Path);
        Assert.DoesNotContain(shell.Window.GetVisualDescendants().OfType<Button>(), b => b.Name == "Item0");
    }

    [AvaloniaFact]
    public void Background_PickerSetsBackgroundPage()
    {
        TestImages.WritePng(Path.Combine(Paths.LibraryBuiltIn, "Arka Plan Sayfaları", "Beyaz.png"), 192, 108, SKColors.White);
        var shell = Make();
        Click(shell.Window, "Background");
        shell.Window.UpdateLayout();
        Click(shell.Window, "Item0");
        Assert.EndsWith("Beyaz.png", shell.Document.BackgroundPath);
        Assert.NotNull(shell.BackgroundView.Source);
    }

    [AvaloniaFact]
    public void Close_WithoutConfirmation_RequestsExit()
    {
        var shell = Make();
        var exits = 0;
        shell.ExitRequested += () => exits++;
        Click(shell.Window, "Close");
        Assert.Equal(1, exits);
    }

    [AvaloniaFact]
    public void Close_WithConfirmation_AsksFirst_NoKeeps_YesExits()
    {
        new SettingsStore(Paths.SettingsFile).Save(new AppSettings { IsCloseConfirmationEnabled = true });
        var shell = Make();
        var exits = 0;
        shell.ExitRequested += () => exits++;

        Click(shell.Window, "Close");
        Assert.Equal(0, exits);
        Click(shell.Window, "CloseNo");
        Assert.Equal(0, exits);
        Assert.DoesNotContain(shell.Window.GetVisualDescendants().OfType<Button>(), b => b.Name == "CloseYes");

        Click(shell.Window, "Close");
        Click(shell.Window, "CloseYes");
        Assert.Equal(1, exits);
    }

    [AvaloniaFact]
    public void PreLoad_StartsHidden_AndActivateShows()
    {
        var shell = Make(show: false);
        Assert.False(shell.Window.IsVisible);
        shell.Activate();
        Assert.True(shell.Window.IsVisible);
    }

    [AvaloniaFact]
    public void Layers_AreStackedBackgroundItemsInkHandlesToolbar()
    {
        var shell = Make();
        var kinds = shell.Window.Root.Children.Select(c => c.GetType().Name).ToList();
        int At(string n) => kinds.IndexOf(n);
        Assert.True(At("BackgroundPageView") < At("BoardItemsView"));
        Assert.True(At("BoardItemsView") < At("InkSurface"));
        Assert.True(At("InkSurface") < At("ItemHandlesView"));
        Assert.True(At("ItemHandlesView") < At("ToolbarView"));
    }
}
