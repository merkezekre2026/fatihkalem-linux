using FatihKalem.Core.Ink;

namespace FatihKalem.Core.Tests;

public class ShapeBuilderTests
{
    private static readonly PenAttributes Pen = PenTable.Create(PenKind.Marker, 3, 0xFF000000);
    private static readonly InkPoint A = new(10, 20);
    private static readonly InkPoint B = new(110, 80);

    private static IReadOnlyList<InkStroke> Build(DrawKind k, InkPoint s, InkPoint e) =>
        ShapeBuilder.Build(k, s, e, Pen);

    [Fact]
    public void Line_HasTwoPointsStartToEnd()
    {
        var stroke = Assert.Single(Build(DrawKind.Line, A, B));
        Assert.Equal([A, B], stroke.Points);
        Assert.Same(Pen, stroke.Pen);
    }

    [Fact]
    public void Rectangle_IsClosedFivePoints()
    {
        var stroke = Assert.Single(Build(DrawKind.Rectangle, A, B));
        Assert.Equal(5, stroke.Points.Count);
        Assert.Equal(stroke.Points[0], stroke.Points[4]);
        Assert.Equal(
            new HashSet<InkPoint> { new(10, 20), new(110, 20), new(110, 80), new(10, 80) },
            stroke.Points.Take(4).ToHashSet());
    }

    [Fact]
    public void Ellipse_PointsLieOnEllipse_AndClose()
    {
        var stroke = Assert.Single(Build(DrawKind.Ellipse, A, B));
        Assert.True(stroke.Points.Count <= 73);
        Assert.True(stroke.Points.Count >= 24);
        Assert.Equal(stroke.Points[0], stroke.Points[^1]);
        const double cx = 60, cy = 50, a = 50, b = 30;
        foreach (var p in stroke.Points)
        {
            var v = Math.Pow((p.X - cx) / a, 2) + Math.Pow((p.Y - cy) / b, 2);
            Assert.Equal(1.0, v, 6);
        }
    }

    [Fact]
    public void Triangle_IsIsoscelesInBox()
    {
        var stroke = Assert.Single(Build(DrawKind.Triangle, A, B));
        Assert.Equal(4, stroke.Points.Count);
        Assert.Equal(stroke.Points[0], stroke.Points[3]);
        Assert.Equal(new InkPoint(60, 20), stroke.Points[0]); // apex: top centre
        Assert.Equal(new InkPoint(110, 80), stroke.Points[1]);
        Assert.Equal(new InkPoint(10, 80), stroke.Points[2]);
    }

    [Fact]
    public void DashLine_ReturnsMultipleSegmentsAlongLine()
    {
        var start = new InkPoint(0, 0);
        var end = new InkPoint(200, 0);
        var dashes = Build(DrawKind.DashLine, start, end);
        Assert.True(dashes.Count >= 3);
        Assert.All(dashes.SelectMany(d => d.Points), p =>
        {
            Assert.Equal(0, p.Y, 9);
            Assert.InRange(p.X, -1e-9, 200 + 1e-9);
        });
        for (var i = 1; i < dashes.Count; i++)
            Assert.True(dashes[i].Points[0].X > dashes[i - 1].Points[^1].X, "gap between dashes");
    }

    [Fact]
    public void Arrow_ContainsShaftAndTwoHeadStrokes()
    {
        var start = new InkPoint(0, 0);
        var end = new InkPoint(100, 0);
        var strokes = Build(DrawKind.Arrow, start, end);
        Assert.Equal(3, strokes.Count);
        Assert.Equal([start, end], strokes[0].Points);
        Assert.Equal(end, strokes[1].Points[^1]);
        Assert.Equal(end, strokes[2].Points[^1]);
        // wings sit behind the tip, on opposite sides of the shaft
        Assert.True(strokes[1].Points[0].X < end.X && strokes[2].Points[0].X < end.X);
        Assert.True(strokes[1].Points[0].Y * strokes[2].Points[0].Y < 0);
    }

    [Theory]
    [InlineData(DrawKind.Line)] [InlineData(DrawKind.DashLine)] [InlineData(DrawKind.Arrow)]
    [InlineData(DrawKind.Rectangle)] [InlineData(DrawKind.Ellipse)] [InlineData(DrawKind.Triangle)]
    public void ZeroLength_DoesNotThrow_AndHasNoNaN(DrawKind kind)
    {
        var strokes = Build(kind, A, A);
        Assert.NotEmpty(strokes);
        Assert.All(strokes.SelectMany(s => s.Points), p =>
        {
            Assert.False(double.IsNaN(p.X) || double.IsNaN(p.Y));
            Assert.False(double.IsInfinity(p.X) || double.IsInfinity(p.Y));
        });
    }

    [Fact]
    public void ReversedDrag_NormalizesBox()
    {
        var forward = Assert.Single(Build(DrawKind.Rectangle, A, B));
        var reversed = Assert.Single(Build(DrawKind.Rectangle, B, A));
        Assert.Equal(forward.Points.ToHashSet(), reversed.Points.ToHashSet());
        var tri = Assert.Single(Build(DrawKind.Triangle, B, A));
        Assert.Equal(new InkPoint(60, 20), tri.Points[0]);
    }
}
