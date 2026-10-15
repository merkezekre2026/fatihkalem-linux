using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Controls;

/// <summary>The background page, stretched over the whole board.</summary>
public sealed class BackgroundPageView : Image
{
    private readonly InkDocument _doc;
    private readonly ImageCache _cache;

    public BackgroundPageView(InkDocument doc, ImageCache cache)
    {
        _doc = doc;
        _cache = cache;
        Stretch = Stretch.Fill;
        IsHitTestVisible = false;
        _doc.Changed += Update;
        Update();
    }

    private void Update() => Source = _doc.BackgroundPath is { } path ? _cache.Get(path) : null;
}

/// <summary>Library pictures placed on the board. Drawn below the ink so that one can write on them.</summary>
public sealed class BoardItemsView : Canvas
{
    private readonly InkDocument _doc;
    private readonly ImageCache _cache;

    public BoardItemsView(InkDocument doc, ImageCache cache)
    {
        _doc = doc;
        _cache = cache;
        IsHitTestVisible = false;
        _doc.Mutated += Rebuild;
        Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        foreach (var item in _doc.Items)
        {
            if (_cache.Get(item.Path) is not { } bitmap) continue;
            var image = new Image
            {
                Source = bitmap,
                Width = bitmap.Size.Width,
                Height = bitmap.Size.Height,
                Stretch = Stretch.Fill,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new TransformGroup
                {
                    Children = { new ScaleTransform(item.Scale, item.Scale), new RotateTransform(item.RotationDegrees) },
                },
            };
            SetLeft(image, item.X - bitmap.Size.Width / 2);
            SetTop(image, item.Y - bitmap.Size.Height / 2);
            Children.Add(image);
        }
    }
}

/// <summary>
/// Move, resize and rotate handles for the most recently placed picture. They live above the ink surface,
/// while the picture body stays below it, so the picture can still be drawn on.
/// </summary>
public sealed class ItemHandlesView : Canvas
{
    private enum Handle { Move, Resize, Rotate }

    private const double HandleSize = 36;

    private readonly InkDocument _doc;
    private readonly ImageCache _cache;
    private readonly Dictionary<Handle, Image> _handles = new();

    private Handle? _active;
    private IDisposable? _group;
    private LibraryItem? _startItem;
    private int _index;
    private Point _from;

    public ItemHandlesView(InkDocument doc, ImageCache cache)
    {
        _doc = doc;
        _cache = cache;
        Add(Handle.Move, "MoveHandle", "hand.png");
        Add(Handle.Resize, "ResizeHandle", "resizeicon.png");
        Add(Handle.Rotate, "RotateHandle", "rotateicon.png");
        _doc.Mutated += Reposition;
        Reposition();
    }

    private void Add(Handle kind, string name, string icon)
    {
        var uri = new Uri($"avares://FatihKalem/Assets/Icons/{icon}");
        var image = new Image
        {
            Name = name,
            Width = HandleSize,
            Height = HandleSize,
            Source = AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null,
            IsVisible = false,
            Cursor = new Cursor(kind == Handle.Move ? StandardCursorType.SizeAll : StandardCursorType.Hand),
        };
        image.PointerPressed += (_, e) => OnPressed(kind, e);
        image.PointerMoved += (_, e) => OnMoved(e);
        image.PointerReleased += (_, e) => OnReleased(e);
        image.PointerCaptureLost += (_, _) => EndDrag();
        _handles[kind] = image;
        Children.Add(image);
    }

    private void Reposition()
    {
        var item = _doc.Items.Count > 0 ? _doc.Items[^1] : null;
        var size = item is null ? null : _cache.Get(item.Path)?.Size;
        foreach (var image in _handles.Values) image.IsVisible = item is not null && size is not null;
        if (item is null || size is null) return;

        double hw = size.Value.Width * item.Scale / 2, hh = size.Value.Height * item.Scale / 2;
        Place(Handle.Move, item, 0, 0);
        Place(Handle.Resize, item, hw, hh);
        Place(Handle.Rotate, item, hw, -hh);
    }

    /// <summary>Puts a handle at a corner offset of the item, rotated with it (clockwise-positive, y down).</summary>
    private void Place(Handle kind, LibraryItem item, double dx, double dy)
    {
        var (sin, cos) = Math.SinCos(item.RotationDegrees * Math.PI / 180);
        var x = item.X + dx * cos - dy * sin;
        var y = item.Y + dx * sin + dy * cos;
        SetLeft(_handles[kind], x - HandleSize / 2);
        SetTop(_handles[kind], y - HandleSize / 2);
    }

    private void OnPressed(Handle kind, PointerPressedEventArgs e)
    {
        if (_active is not null || _doc.Items.Count == 0) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _active = kind;
        _index = _doc.Items.Count - 1;
        _startItem = _doc.Items[_index];
        _from = e.GetPosition(this);
        _group = _doc.BeginGroup();
        e.Pointer.Capture(_handles[kind]);
        e.Handled = true;
    }

    private void OnMoved(PointerEventArgs e)
    {
        if (_active is not { } kind || _startItem is not { } start) return;
        var pos = e.GetPosition(this);
        var center = new InkPoint(start.X, start.Y);
        var updated = kind switch
        {
            Handle.Move => ItemTransform.Move(start, pos.X - _from.X, pos.Y - _from.Y),
            Handle.Resize => ItemTransform.ScaleFromHandle(start, center, ToInk(_from), ToInk(pos)),
            _ => ItemTransform.RotateFromHandle(start, center, ToInk(_from), ToInk(pos)),
        };
        _doc.UpdateItem(_index, updated);
    }

    private void OnReleased(PointerReleasedEventArgs e)
    {
        EndDrag();
        e.Pointer.Capture(null);
    }

    private void EndDrag()
    {
        _group?.Dispose();
        _group = null;
        _active = null;
        _startItem = null;
    }

    private static InkPoint ToInk(Point p) => new(p.X, p.Y);
}
