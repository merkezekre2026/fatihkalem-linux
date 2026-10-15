using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FatihKalem.App.Controls;
using FatihKalem.App.ViewModels;
using FatihKalem.App.Views;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;

namespace FatihKalem.App.Tests;

public sealed class ToolbarViewTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk-toolbarview-" + Guid.NewGuid().ToString("N"));
    private readonly ToolState _tool = new();
    private readonly InkDocument _doc = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private (ToolbarViewModel Vm, ToolbarView View, Window Window) Make()
    {
        var vm = new ToolbarViewModel(_tool, _doc,
            new SettingsStore(Path.Combine(_dir, "settings.json")), new Autostart(Path.Combine(_dir, "autostart")));
        var view = new ToolbarView(vm);
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        return (vm, view, window);
    }

    private static Button Find(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    private static void Click(Visual root, string name) =>
        Find(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void ToolButtons_RouteToViewModel()
    {
        var (vm, view, _) = Make();
        Click(view, "Hand");
        Assert.Equal(InputMode.Hand, vm.Mode);
        Click(view, "Size6");
        Assert.Equal(6, _tool.SizeLevel);
        Assert.Equal(InputMode.Hand, vm.Mode); // choosing a size does not leave hand mode by itself
        Click(view, "Eraser2");
        Assert.True(_tool.IsEraser);
        Assert.Equal(2, _tool.EraserLevel);
        Assert.Equal(InputMode.Pen, vm.Mode);
        Click(view, "Rectangle");
        Assert.Equal(DrawKind.Rectangle, _tool.Shape);
        Click(view, "Marker");
        Assert.Equal(PenKind.Marker, _tool.Pen);
        Assert.Null(_tool.Shape);
    }

    [AvaloniaFact]
    public void ColorButtons_SelectPaletteColour()
    {
        var (vm, view, _) = Make();
        Click(view, "Highlighter");
        Click(view, "Color4");
        Assert.Equal((PenKind.Highlighter, 0xFFFFFF01u), (_tool.Pen, _tool.Argb));
        Assert.Equal((byte)4, vm.Settings.ColorNo);
    }

    [AvaloniaFact]
    public void Undo_Redo_Clear_Buttons_ReflectAndDriveHistory()
    {
        var (_, view, _) = Make();
        Assert.False(Find(view, "Undo").IsEnabled);
        Assert.False(Find(view, "Redo").IsEnabled);

        _doc.Add(new InkStroke([new InkPoint(1, 1)], PenTable.Create(PenKind.Marker, 1, 0xFF000000)));
        Assert.True(Find(view, "Undo").IsEnabled);

        Click(view, "Undo");
        Assert.Empty(_doc.Strokes);
        Assert.True(Find(view, "Redo").IsEnabled);
        Click(view, "Redo");
        Assert.Single(_doc.Strokes);
        Click(view, "Clear");
        Assert.Empty(_doc.Strokes);
    }

    [AvaloniaFact]
    public void Favorites_AreShown_Activated_AndRemovable()
    {
        var (vm, view, _) = Make();
        vm.AddFavorite("Size2");
        vm.AddFavorite("Eraser3");
        Click(view, "Favorite1");
        Assert.True(_tool.IsEraser);
        Assert.Equal(3, _tool.EraserLevel);
        Click(view, "Favorite0");
        Assert.Equal(2, _tool.SizeLevel);

        vm.RemoveFavorite("Size2");
        Assert.Equal(["Eraser3"], vm.Favorites);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), b => b.Name == "Favorite1");
    }

    [AvaloniaFact]
    public void LibraryAndBackgroundButtons_RaiseViewRequests()
    {
        var (vm, view, _) = Make();
        var requested = new List<string>();
        vm.LibraryRequested += () => requested.Add("library");
        vm.BackgroundRequested += () => requested.Add("background");
        Click(view, "Library");
        Click(view, "Background");
        Assert.Equal(["library", "background"], requested);
    }

    [AvaloniaFact]
    public void CloseButton_RaisesCloseRequested()
    {
        var (_, view, _) = Make();
        var closed = 0;
        view.CloseRequested += () => closed++;
        Click(view, "Close");
        Assert.Equal(1, closed);
    }

    [AvaloniaFact]
    public void SettingsPanel_TogglesPersist()
    {
        var (vm, view, _) = Make();
        var check = view.GetVisualDescendants()
            .OfType<CheckBox>().FirstOrDefault(c => c.Name == "CloseConfirmation");
        Assert.NotNull(check);
        check!.IsChecked = true;
        Assert.True(vm.Settings.IsCloseConfirmationEnabled);
    }

    [AvaloniaFact]
    public void HeaderDrag_MovesToolbar_AndSavesPosition()
    {
        var (vm, view, window) = Make();
        var handle = view.GetVisualDescendants().OfType<Border>().First(b => b.Name == "DragHandle");
        var start = handle.TranslatePoint(new Point(5, 5), window)!.Value;
        var before = view.Margin;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(40, 25));
        window.MouseUp(start + new Vector(40, 25), MouseButton.Left);

        Assert.Equal(before.Left + 40, view.Margin.Left, 1);
        Assert.Equal(before.Top + 25, view.Margin.Top, 1);
        Assert.False(vm.Settings.IsStartupPositionDefault);
        Assert.Equal((int)Math.Round(before.Left + 40), vm.Settings.Left);
        Assert.Equal((int)Math.Round(before.Top + 25), vm.Settings.Top);
    }

    [AvaloniaFact]
    public void SavedPosition_IsRestoredAtStartup()
    {
        new SettingsStore(Path.Combine(_dir, "settings.json"))
            .Save(new AppSettings { IsStartupPositionDefault = false, Left = 200, Top = 80 });
        var (_, view, _) = Make();
        Assert.Equal(200, view.Margin.Left);
        Assert.Equal(80, view.Margin.Top);
    }
}

