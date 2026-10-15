using FatihKalem.Core.Ink;

namespace FatihKalem.Core.Tests;

public class PointEraserTests
{
    private static readonly PenAttributes Pen = PenTable.Create(PenKind.Stylograph, 2, 0xFF123456);

    private static InkStroke Horizontal(int count) =>
        new(Enumerable.Range(0, count).Select(i => new InkPoint(i, 0)).ToArray(), Pen);

    [Fact]
    public void Apply_FullyInside_ReturnsEmpty()
    {
        var s = Horizontal(5);
        Assert.Empty(PointEraser.Apply(s, new InkPoint(2, 0), 20, 20));
    }

    [Fact]
    public void Apply_MiddleHit_SplitsIntoTwo()
    {
        var s = Horizontal(10);
        var pieces = PointEraser.Apply(s, new InkPoint(4.5, 0), 3, 3); // x in [3, 6]
        Assert.Equal(2, pieces.Count);
        Assert.Equal(new InkPoint(0, 0), pieces[0].Points[0]);
        Assert.Equal(new InkPoint(9, 0), pieces[1].Points[^1]);
        Assert.All(pieces[0].Points, p => Assert.True(p.X <= 3));
        Assert.All(pieces[1].Points, p => Assert.True(p.X >= 6));
    }

    [Fact]
    public void Apply_LineWithNoVertexInside_IsStillCut()
    {
        var s = new InkStroke([new InkPoint(0, 0), new InkPoint(100, 0)], Pen);
        var pieces = PointEraser.Apply(s, new InkPoint(50, 0), 10, 10);
        Assert.Equal(2, pieces.Count);
        Assert.Equal(45, pieces[0].Points[^1].X, 9);
        Assert.Equal(55, pieces[1].Points[0].X, 9);
    }

    [Fact]
    public void Apply_EraseAtEnd_TrimsStroke()
    {
        var pieces = PointEraser.Apply(Horizontal(10), new InkPoint(9, 0), 4, 4);
        var piece = Assert.Single(pieces);
        Assert.Equal(new InkPoint(0, 0), piece.Points[0]);
        Assert.True(piece.Points[^1].X <= 7);
    }

    [Fact]
    public void Apply_Miss_ReturnsSameStroke()
    {
        var s = Horizontal(10);
        Assert.Same(s, Assert.Single(PointEraser.Apply(s, new InkPoint(5, 50), 4, 4)));
    }

    [Fact]
    public void Apply_SinglePointInside_ReturnsEmpty()
    {
        var s = new InkStroke([new InkPoint(5, 5)], Pen);
        Assert.Empty(PointEraser.Apply(s, new InkPoint(5, 5), 4, 4));
    }

    [Fact]
    public void Apply_SinglePointOutside_ReturnsSameStroke()
    {
        var s = new InkStroke([new InkPoint(5, 5)], Pen);
        Assert.Same(s, Assert.Single(PointEraser.Apply(s, new InkPoint(50, 50), 4, 4)));
    }

    [Fact]
    public void Apply_KeepsPenAttributes()
    {
        var pieces = PointEraser.Apply(Horizontal(10), new InkPoint(4.5, 0), 3, 3);
        Assert.All(pieces, p => Assert.Same(Pen, p.Pen));
    }

    [Fact]
    public void EraseAt_AcrossDragWithGroup_IsOneUndoStep()
    {
        var doc = new InkDocument();
        doc.Add(Horizontal(40));
        using (doc.BeginGroup())
        {
            for (var x = 5; x < 35; x += 6)
                PointEraser.EraseAt(doc, new InkPoint(x, 0), 4, 4);
        }
        Assert.True(doc.Strokes.Count > 1);
        doc.Undo(); // undoes the whole drag
        Assert.Single(doc.Strokes);
        Assert.Equal(40, doc.Strokes[0].Points.Count);
    }

    [Fact]
    public void EraseAt_Miss_AddsNoUndoStep()
    {
        var doc = new InkDocument();
        doc.Add(Horizontal(10));
        doc.Undo();
        doc.Redo();
        var before = doc.Strokes[0];
        PointEraser.EraseAt(doc, new InkPoint(5, 90), 4, 4);
        Assert.Same(before, doc.Strokes[0]);
        doc.Undo();
        Assert.Empty(doc.Strokes); // the Add was the only undo step
    }
}
