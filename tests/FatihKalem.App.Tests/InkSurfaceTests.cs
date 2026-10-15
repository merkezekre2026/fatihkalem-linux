using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using FatihKalem.App.Controls;
using FatihKalem.Core.Ink;
using SkiaSharp;

namespace FatihKalem.App.Tests;

public class InkSurfaceTests
{
    private sealed class Rig
    {
        public InkDocument Doc { get; } = new();
        public ToolState Tool { get; } = new();
        public InkSurface Surface { get; }
        public Window Window { get; }

        public Rig(int size = 200)
        {
            Surface = new InkSurface(Doc, Tool);
            Window = new Window { Width = size, Height = size, Content = Surface };
            Window.Show();
        }

        public void Drag(params (double X, double Y)[] path)
        {
            Window.MouseDown(new Point(path[0].X, path[0].Y), MouseButton.Left);
            foreach (var p in path.Skip(1)) Window.MouseMove(new Point(p.X, p.Y));
            Window.MouseUp(new Point(path[^1].X, path[^1].Y), MouseButton.Left);
        }

        /// <summary>Renders the surface and returns BGRA pixel reader.</summary>
        public SKBitmap Render(int size = 200)
        {
            Surface.Measure(new Size(size, size));
            Surface.Arrange(new Rect(0, 0, size, size));
            using var rtb = new RenderTargetBitmap(new PixelSize(size, size));
            rtb.Render(Surface);
            using var ms = new MemoryStream();
            rtb.Save(ms);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }
    }

    private static int VisiblePixels(SKBitmap bmp)
    {
        var count = 0;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y).Alpha > 10) count++;
        return count;
    }

    [AvaloniaFact]
    public void FreehandDrag_AddsOneStroke()
    {
        var rig = new Rig();
        rig.Drag((10, 10), (50, 50), (90, 60));
        var stroke = Assert.Single(rig.Doc.Strokes);
        Assert.True(stroke.Points.Count >= 3);
        Assert.Equal(new InkPoint(10, 10), stroke.Points[0]);
        Assert.Equal(PenKind.Stylograph, PenKindOf(rig, stroke));
    }

    private static PenKind PenKindOf(Rig rig, InkStroke s) =>
        s.Pen.Equals(PenTable.Create(rig.Tool.Pen, rig.Tool.SizeLevel, rig.Tool.Argb)) ? rig.Tool.Pen : PenKind.Marker;

    [AvaloniaFact]
    public void Click_WithoutMove_AddsSinglePointStroke()
    {
        var rig = new Rig();
        rig.Drag((30, 30));
        var stroke = Assert.Single(rig.Doc.Strokes);
        Assert.Single(stroke.Points);
    }

    [AvaloniaFact]
    public void ShapeDrag_AddsShapeOnRelease_NotDuringDrag()
    {
        var rig = new Rig();
        rig.Tool.Shape = DrawKind.Rectangle;
        rig.Window.MouseDown(new Point(20, 20), MouseButton.Left);
        rig.Window.MouseMove(new Point(120, 90));
        Assert.Empty(rig.Doc.Strokes);
        Assert.Single(rig.Surface.Preview);
        rig.Window.MouseUp(new Point(120, 90), MouseButton.Left);
        Assert.Single(rig.Doc.Strokes);
        Assert.Empty(rig.Surface.Preview);
        rig.Doc.Undo();
        Assert.Empty(rig.Doc.Strokes);
    }

    [AvaloniaFact]
    public void ShapeClick_WithoutDrag_AddsNothing()
    {
        var rig = new Rig();
        rig.Tool.Shape = DrawKind.Ellipse;
        rig.Drag((40, 40));
        Assert.Empty(rig.Doc.Strokes);
    }

    [AvaloniaFact]
    public void Arrow_IsThreeStrokesButOneUndoStep()
    {
        var rig = new Rig();
        rig.Tool.Shape = DrawKind.Arrow;
        rig.Drag((20, 20), (150, 20));
        Assert.Equal(3, rig.Doc.Strokes.Count);
        rig.Doc.Undo();
        Assert.Empty(rig.Doc.Strokes);
    }

    [AvaloniaFact]
    public void Shape_UsesRoundPenOfShapeSize()
    {
        var rig = new Rig();
        rig.Tool.Shape = DrawKind.Line;
        rig.Tool.SizeLevel = 5; // ShapeSize 10
        rig.Drag((20, 20), (150, 20));
        var pen = Assert.Single(rig.Doc.Strokes).Pen;
        Assert.Equal((10.0, 10.0, false), (pen.Width, pen.Height, pen.IsHighlighter));
    }

    [AvaloniaFact]
    public void EraserDrag_IsSingleUndoStep()
    {
        var rig = new Rig();
        rig.Doc.Add(Horizontal(y: 100, from: 0, to: 199));
        rig.Tool.IsEraser = true;
        rig.Tool.EraserLevel = 1; // 20 x 32
        rig.Drag((40, 100), (80, 100), (120, 100));
        Assert.True(rig.Doc.Strokes.Count >= 1);
        Assert.NotEqual(200, rig.Doc.Strokes.Sum(s => s.Points.Count));
        rig.Doc.Undo();
        var restored = Assert.Single(rig.Doc.Strokes);
        Assert.Equal(200, restored.Points.Count);
    }

    [AvaloniaFact]
    public void EraserFastDrag_LeavesNoGaps()
    {
        var rig = new Rig();
        rig.Doc.Add(Horizontal(y: 100, from: 0, to: 199));
        rig.Tool.IsEraser = true;
        rig.Tool.EraserLevel = 1; // 20 wide: a single 190px jump must still clear everything
        rig.Drag((0, 100), (190, 100));
        Assert.True(rig.Doc.Strokes.All(s => s.Points.All(p => p.X > 195)), "only the far tail may remain");
    }

    [AvaloniaFact]
    public void Render_Marker_ProducesNonEmptyBitmap()
    {
        var rig = new Rig();
        rig.Doc.Add(new InkStroke(
            [new InkPoint(20, 100), new InkPoint(180, 100)],
            PenTable.Create(PenKind.Marker, 6, 0xFFFF0000)));
        using var bmp = rig.Render();
        Assert.True(VisiblePixels(bmp) > 200);
        var center = bmp.GetPixel(100, 100);
        Assert.Equal((255, 255, 0, 0), (center.Alpha, center.Red, center.Green, center.Blue));
    }

    [AvaloniaFact]
    public void Render_Highlighter_IsSemiTransparent_EvenWhereItOverlapsItself()
    {
        var rig = new Rig();
        // one stroke that retraces itself: the overlap must not get darker than a single pass
        rig.Doc.Add(new InkStroke(
            [new InkPoint(20, 100), new InkPoint(180, 100), new InkPoint(20, 100)],
            PenTable.Create(PenKind.Highlighter, 4, 0xFFFFFF01)));
        using var bmp = rig.Render();
        var alpha = bmp.GetPixel(100, 100).Alpha;
        Assert.InRange((int)alpha, 100, 160);
    }

    [AvaloniaFact]
    public void Render_AfterUndo_ClearsPixels()
    {
        var rig = new Rig();
        rig.Doc.Add(new InkStroke(
            [new InkPoint(20, 100), new InkPoint(180, 100)],
            PenTable.Create(PenKind.Marker, 6, 0xFF000000)));
        using (var before = rig.Render()) Assert.True(VisiblePixels(before) > 0);
        rig.Doc.Undo();
        using var after = rig.Render();
        Assert.Equal(0, VisiblePixels(after));
    }

    private static InkStroke Horizontal(double y, int from, int to) =>
        new(Enumerable.Range(from, to - from + 1).Select(x => new InkPoint(x, y)).ToArray(),
            PenTable.Create(PenKind.Marker, 1, 0xFF000000));
}
