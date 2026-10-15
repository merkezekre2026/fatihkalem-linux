using System.Collections.Immutable;

namespace FatihKalem.Core.Ink;

/// <summary>
/// Board contents (strokes, placed library images, background page) with snapshot-based undo/redo.
/// <see cref="Changed"/> fires once per committed operation; <see cref="Mutated"/> fires on every
/// mutation, including inside a group, so a view can repaint live during a drag.
/// </summary>
public sealed class InkDocument
{
    private sealed record Snapshot(
        ImmutableList<InkStroke> Strokes,
        ImmutableList<LibraryItem> Items,
        string? Background);

    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();
    private Snapshot _current = new(ImmutableList<InkStroke>.Empty, ImmutableList<LibraryItem>.Empty, null);
    private Snapshot? _groupBase;
    private int _groupDepth;

    public event Action? Changed;
    public event Action? Mutated;

    public IReadOnlyList<InkStroke> Strokes => _current.Strokes;
    public IReadOnlyList<LibraryItem> Items => _current.Items;
    public string? BackgroundPath => _current.Background;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Add(InkStroke stroke) =>
        Apply(_current with { Strokes = _current.Strokes.Add(stroke) });

    public void Remove(InkStroke stroke)
    {
        var index = IndexOfReference(_current.Strokes, stroke);
        if (index < 0) return;
        Apply(_current with { Strokes = _current.Strokes.RemoveAt(index) });
    }

    public void ReplaceStrokes(IReadOnlyList<InkStroke> strokes) =>
        Apply(_current with { Strokes = ImmutableList.CreateRange(strokes) });

    public void AddItem(LibraryItem item) =>
        Apply(_current with { Items = _current.Items.Add(item) });

    public void UpdateItem(int index, LibraryItem item) =>
        Apply(_current with { Items = _current.Items.SetItem(index, item) });

    public void RemoveItem(int index) =>
        Apply(_current with { Items = _current.Items.RemoveAt(index) });

    public void SetBackground(string? path)
    {
        if (path == _current.Background) return;
        Apply(_current with { Background = path });
    }

    public void Clear()
    {
        if (_current.Strokes.IsEmpty && _current.Items.IsEmpty && _current.Background is null) return;
        Apply(new Snapshot(ImmutableList<InkStroke>.Empty, ImmutableList<LibraryItem>.Empty, null));
    }

    /// <summary>All operations until the returned scope is disposed form one undo step.</summary>
    public IDisposable BeginGroup()
    {
        if (_groupDepth == 0) _groupBase = _current;
        _groupDepth++;
        return new GroupScope(this);
    }

    public void Undo()
    {
        if (_groupDepth > 0 || _undo.Count == 0) return;
        _redo.Push(_current);
        _current = _undo.Pop();
        Mutated?.Invoke();
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_groupDepth > 0 || _redo.Count == 0) return;
        _undo.Push(_current);
        _current = _redo.Pop();
        Mutated?.Invoke();
        Changed?.Invoke();
    }

    private void Apply(Snapshot next)
    {
        if (ReferenceEquals(next, _current)) return;
        if (_groupDepth > 0)
        {
            _current = next;
            Mutated?.Invoke();
            return;
        }
        _undo.Push(_current);
        _redo.Clear();
        _current = next;
        Mutated?.Invoke();
        Changed?.Invoke();
    }

    private void EndGroup()
    {
        if (--_groupDepth > 0) return;
        if (!ReferenceEquals(_current, _groupBase))
        {
            _undo.Push(_groupBase!);
            _redo.Clear();
            Changed?.Invoke();
        }
        _groupBase = null;
    }

    private static int IndexOfReference(ImmutableList<InkStroke> list, InkStroke stroke)
    {
        for (var i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], stroke)) return i;
        return -1;
    }

    private sealed class GroupScope(InkDocument owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner.EndGroup();
        }
    }
}
