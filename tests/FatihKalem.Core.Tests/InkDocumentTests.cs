using FatihKalem.Core.Ink;

namespace FatihKalem.Core.Tests;

public class InkDocumentTests
{
    private static readonly PenAttributes Pen = PenTable.Create(PenKind.Marker, 3, 0xFF000000);

    private static InkStroke Stroke(params (double X, double Y)[] pts) =>
        new(pts.Select(p => new InkPoint(p.X, p.Y)).ToArray(), Pen);

    private static InkStroke Line(double x) => Stroke((x, 0), (x, 10));

    [Fact]
    public void Add_ThenUndo_RestoresEmpty()
    {
        var doc = new InkDocument();
        doc.Add(Line(1));
        Assert.Single(doc.Strokes);
        doc.Undo();
        Assert.Empty(doc.Strokes);
    }

    [Fact]
    public void Undo_ThenRedo_Reapplies()
    {
        var doc = new InkDocument();
        var s = Line(1);
        doc.Add(s);
        doc.Undo();
        Assert.True(doc.CanRedo);
        doc.Redo();
        Assert.Same(s, Assert.Single(doc.Strokes));
    }

    [Fact]
    public void NewOperation_AfterUndo_ClearsRedo()
    {
        var doc = new InkDocument();
        doc.Add(Line(1));
        doc.Undo();
        doc.Add(Line(2));
        Assert.False(doc.CanRedo);
    }

    [Fact]
    public void Undo_OnEmpty_IsNoOp()
    {
        var doc = new InkDocument();
        var fired = 0;
        doc.Changed += () => fired++;
        doc.Undo();
        Assert.False(doc.CanUndo);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Redo_OnEmpty_IsNoOp()
    {
        var doc = new InkDocument();
        var fired = 0;
        doc.Changed += () => fired++;
        doc.Redo();
        Assert.False(doc.CanRedo);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Group_IsSingleUndoStep()
    {
        var doc = new InkDocument();
        using (doc.BeginGroup())
        {
            doc.Add(Line(1));
            doc.Add(Line(2));
            doc.Add(Line(3));
        }
        Assert.Equal(3, doc.Strokes.Count);
        doc.Undo();
        Assert.Empty(doc.Strokes);
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void Group_WithoutChanges_AddsNoUndoStep()
    {
        var doc = new InkDocument();
        using (doc.BeginGroup()) { }
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void Clear_IsUndoable()
    {
        var doc = new InkDocument();
        doc.Add(Line(1));
        doc.AddItem(new LibraryItem("a.png", 1, 2, 1, 0));
        doc.SetBackground("bg.png");
        doc.Clear();
        Assert.Empty(doc.Strokes);
        Assert.Empty(doc.Items);
        Assert.Null(doc.BackgroundPath);
        doc.Undo();
        Assert.Single(doc.Strokes);
        Assert.Single(doc.Items);
        Assert.Equal("bg.png", doc.BackgroundPath);
    }

    [Fact]
    public void Clear_OnEmpty_IsNoOp()
    {
        var doc = new InkDocument();
        doc.Clear();
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void AddItem_And_SetBackground_AreUndoable()
    {
        var doc = new InkDocument();
        doc.AddItem(new LibraryItem("a.png", 0, 0, 1, 0));
        doc.SetBackground("bg.png");
        doc.Undo();
        Assert.Null(doc.BackgroundPath);
        Assert.Single(doc.Items);
        doc.Undo();
        Assert.Empty(doc.Items);
    }

    [Fact]
    public void UpdateItem_And_RemoveItem_AreUndoable()
    {
        var doc = new InkDocument();
        doc.AddItem(new LibraryItem("a.png", 0, 0, 1, 0));
        doc.UpdateItem(0, new LibraryItem("a.png", 5, 5, 2, 90));
        Assert.Equal(2, doc.Items[0].Scale);
        doc.Undo();
        Assert.Equal(1, doc.Items[0].Scale);
        doc.RemoveItem(0);
        Assert.Empty(doc.Items);
        doc.Undo();
        Assert.Single(doc.Items);
    }

    [Fact]
    public void Changed_FiresOncePerOperation_AndOncePerGroup()
    {
        var doc = new InkDocument();
        var fired = 0;
        doc.Changed += () => fired++;
        doc.Add(Line(1));
        Assert.Equal(1, fired);
        using (doc.BeginGroup())
        {
            doc.Add(Line(2));
            doc.Add(Line(3));
            Assert.Equal(1, fired);
        }
        Assert.Equal(2, fired);
    }

    [Fact]
    public void Mutated_FiresOnEveryMutation_EvenInsideGroup()
    {
        var doc = new InkDocument();
        var fired = 0;
        doc.Mutated += () => fired++;
        using (doc.BeginGroup())
        {
            doc.Add(Line(1));
            doc.Add(Line(2));
        }
        Assert.Equal(2, fired);
    }

    [Fact]
    public void SingleVertexStroke_IsAccepted()
    {
        var doc = new InkDocument();
        doc.Add(Stroke((5, 5)));
        Assert.Single(Assert.Single(doc.Strokes).Points);
    }

    [Fact]
    public void Remove_And_ReplaceStrokes_AreUndoable()
    {
        var doc = new InkDocument();
        var a = Line(1);
        var b = Line(2);
        doc.Add(a);
        doc.Add(b);
        doc.Remove(a);
        Assert.Same(b, Assert.Single(doc.Strokes));
        doc.ReplaceStrokes([]);
        Assert.Empty(doc.Strokes);
        doc.Undo();
        Assert.Same(b, Assert.Single(doc.Strokes));
        doc.Undo();
        Assert.Equal(2, doc.Strokes.Count);
    }

    [Fact]
    public void Remove_UnknownStroke_IsNoOp()
    {
        var doc = new InkDocument();
        doc.Add(Line(1));
        doc.Remove(Line(1));
        Assert.Single(doc.Strokes);
        doc.Undo();
        Assert.Empty(doc.Strokes);
    }
}
