using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FatihKalem.App.Controls;
using FatihKalem.Core.Ink;
using SkiaSharp;

namespace FatihKalem.App.Tests;

public sealed class BoardLayersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fk-board-" + Guid.NewGuid().ToString("N"));
    private readonly InkDocument _doc = new();
    private readonly ImageCache _cache = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Png(string name, int w, int h, SKColor color) => TestImages.WritePng(Path.Combine(_dir, name), w, h, color);

    private Window Host(params Control[] layers)
    {
        var grid = new Grid();
        foreach (var l in layers) grid.Children.Add(l);
        var window = new Window { Width = 800, Height = 600, Content = grid };
        window.Show();
        return window;
    }

    private static Image Handle(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Image>().First(i => i.Name == name);

    private static Point CenterOf(Visual handle, Visual window) =>
        handle.TranslatePoint(new Point(handle.Bounds.Width / 2, handle.Bounds.Height / 2), window)!.Value;

    private static void Drag(Window w, Point from, Point to)
    {
        w.MouseDown(from, MouseButton.Left);
        w.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        w.MouseMove(to);
        w.MouseUp(to, MouseButton.Left);
    }

    // ---- background page -------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Background_FollowsDocument_AndToleratesBadPaths()
    {
        var view = new BackgroundPageView(_doc, _cache);
        Assert.Null(view.Source);
        _doc.SetBackground(Png("bg.png", 192, 108, SKColors.White));
        Assert.NotNull(view.Source);
        _doc.Undo();
        Assert.Null(view.Source);
        _doc.SetBackground(Path.Combine(_dir, "missing.png"));
        Assert.Null(view.Source);
        Assert.False(view.IsHitTestVisible);
    }

    // ---- items layer -----------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Items_AreDrawnWithScaleAndRotation()
    {
        var path = Png("red.png", 40, 20, SKColors.Red);
        var layer = new BoardItemsView(_doc, _cache);
        _doc.AddItem(new LibraryItem(path, X: 100, Y: 100, Scale: 2, RotationDegrees: 90));
        Host(layer);

        layer.Measure(new Size(200, 200));
        layer.Arrange(new Rect(0, 0, 200, 200));
        using var rtb = new RenderTargetBitmap(new PixelSize(200, 200));
        rtb.Render(layer);
        using var ms = new MemoryStream();
        rtb.Save(ms);
        ms.Position = 0;
        using var bmp = SKBitmap.Decode(ms);

        // 40x20 scaled x2 = 80x40, rotated 90° = 40 wide, 80 tall around (100,100)
        Assert.True(bmp.GetPixel(100, 100 + 30).Alpha > 200, "inside, along the tall axis");
        Assert.True(bmp.GetPixel(100 + 30, 100).Alpha < 10, "outside along the former long axis");
        Assert.False(layer.IsHitTestVisible);
    }

    // ---- handles ---------------------------------------------------------------------------------------

    private (Window Window, ItemHandlesView Handles) HostWithItem(LibraryItem item)
    {
        var handles = new ItemHandlesView(_doc, _cache);
        _doc.AddItem(item);
        var window = Host(new BoardItemsView(_doc, _cache), handles);
        return (window, handles);
    }

    [AvaloniaFact]
    public void Handles_AreHidden_WithoutItems_AndFollowTheLastItem()
    {
        var handles = new ItemHandlesView(_doc, _cache);
        var window = Host(handles);
        Assert.False(Handle(handles, "MoveHandle").IsVisible);

        _doc.AddItem(new LibraryItem(Png("a.png", 200, 100, SKColors.Red), 300, 200, 1, 0));
        Assert.True(Handle(handles, "MoveHandle").IsVisible);
        window.UpdateLayout(); // layout runs on the next pass; the test asks for arranged bounds
        Assert.Equal(new Point(300, 200), CenterOf(Handle(handles, "MoveHandle"), window));
        Assert.Equal(new Point(400, 250), CenterOf(Handle(handles, "ResizeHandle"), window));
        Assert.Equal(new Point(400, 150), CenterOf(Handle(handles, "RotateHandle"), window));

        _doc.Undo();
        Assert.False(Handle(handles, "MoveHandle").IsVisible);
    }

    [AvaloniaFact]
    public void DragMoveHandle_MovesItem_AsOneUndoStep()
    {
        var (window, handles) = HostWithItem(new LibraryItem(Png("a.png", 200, 100, SKColors.Red), 300, 200, 1, 0));
        Drag(window, CenterOf(Handle(handles, "MoveHandle"), window), new Point(340, 230));

        var item = Assert.Single(_doc.Items);
        Assert.Equal((340.0, 230.0), (item.X, item.Y));
        _doc.Undo();
        item = Assert.Single(_doc.Items);
        Assert.Equal((300.0, 200.0), (item.X, item.Y));
    }

    [AvaloniaFact]
    public void DragResizeHandle_ScalesItem()
    {
        var (window, handles) = HostWithItem(new LibraryItem(Png("a.png", 200, 100, SKColors.Red), 300, 200, 1, 0));
        // corner is at (400,250): dragging it to (500,300) doubles its distance from the centre
        Drag(window, CenterOf(Handle(handles, "ResizeHandle"), window), new Point(500, 300));
        Assert.Equal(2.0, Assert.Single(_doc.Items).Scale, 3);
    }

    [AvaloniaFact]
    public void DragRotateHandle_RotatesItem()
    {
        var (window, handles) = HostWithItem(new LibraryItem(Png("a.png", 200, 100, SKColors.Red), 300, 200, 1, 0));
        // handle sits at -26.57°; the point (350,300) is at +63.43° on the same radius: a 90° turn
        Drag(window, CenterOf(Handle(handles, "RotateHandle"), window), new Point(350, 300));
        Assert.Equal(90.0, Assert.Single(_doc.Items).RotationDegrees, 1);
    }

    [AvaloniaFact]
    public void Handles_RotateWithTheItem()
    {
        var (window, handles) = HostWithItem(new LibraryItem(Png("a.png", 200, 100, SKColors.Red), 300, 200, 1, 90));
        // rotated a quarter turn: the bottom-right corner (+100,+50) moves to (-50,+100)
        var p = CenterOf(Handle(handles, "ResizeHandle"), window);
        Assert.Equal(250, p.X, 3);
        Assert.Equal(300, p.Y, 3);
    }
}
