using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Controls;

/// <summary>
/// Draws an <see cref="InkDocument"/> and turns pointer input (mouse, pen, touch) into strokes, shapes
/// or erasing, according to the <see cref="ToolState"/>. Replaces the original WPF InkCanvas.
/// </summary>
public sealed class InkSurface : Control
{
    private enum Mode { None, Freehand, Shape, Erase }

    private const double HighlighterAlpha = 0.5;
    private const double MinShapeLength = 1.0;

    private static readonly ConditionalWeakTable<InkStroke, Geometry> GeometryCache = new();
    // Almost transparent but not null: keeps the whole surface hit-testable, like the original alpha=1 brush.
    private static readonly IBrush HitBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

    private readonly InkDocument _doc;
    private readonly ToolState _tool;
    private readonly Dictionary<(uint Argb, bool Highlighter), IBrush> _brushes = new();

    private Mode _mode;
    private List<InkPoint> _points = [];
    private InkPoint _start;
    private InkPoint _last;
    private PenAttributes? _pen;
    private IDisposable? _group;

    public InkSurface(InkDocument doc, ToolState tool)
    {
        _doc = doc;
        _tool = tool;
        _doc.Mutated += InvalidateVisual;
        Preview = [];
    }

    /// <summary>Strokes being drawn right now; not part of the document until the pointer is released.</summary>
    public IReadOnlyList<InkStroke> Preview { get; private set; }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(HitBrush, null, new Rect(Bounds.Size));
        foreach (var stroke in _doc.Strokes) DrawStroke(context, stroke);
        foreach (var stroke in Preview) DrawStroke(context, stroke);
    }

    private void DrawStroke(DrawingContext context, InkStroke stroke)
    {
        var geometry = GeometryCache.GetValue(stroke, StrokeGeometry.Build);
        context.DrawGeometry(BrushFor(stroke.Pen), null, geometry);
    }

    private IBrush BrushFor(PenAttributes pen)
    {
        var key = (pen.Argb, pen.IsHighlighter);
        if (_brushes.TryGetValue(key, out var brush)) return brush;
        var color = Color.FromUInt32(pen.Argb);
        if (pen.IsHighlighter) color = Color.FromArgb((byte)Math.Round(color.A * HighlighterAlpha), color.R, color.G, color.B);
        return _brushes[key] = new SolidColorBrush(color);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_mode != Mode.None) return; // follow the first contact only
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var p = ToInk(e.GetPosition(this));
        e.Pointer.Capture(this);
        _start = _last = p;

        if (_tool.IsEraser)
        {
            _mode = Mode.Erase;
            _group = _doc.BeginGroup();
            EraseAlong(p, p);
        }
        else if (_tool.Shape is not null)
        {
            _mode = Mode.Shape;
            var size = PenTable.ShapeSize(_tool.SizeLevel);
            _pen = new PenAttributes(_tool.Argb, size, size, IsHighlighter: false);
        }
        else
        {
            _mode = Mode.Freehand;
            _pen = PenTable.Create(_tool.Pen, _tool.SizeLevel, _tool.Argb);
            _points = [p];
            Preview = [new InkStroke(_points.ToArray(), _pen)];
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        switch (_mode)
        {
            case Mode.Freehand:
                foreach (var ip in e.GetIntermediatePoints(this))
                {
                    var p = ToInk(ip.Position);
                    if (p != _points[^1]) _points.Add(p);
                }
                Preview = [new InkStroke(_points.ToArray(), _pen!)];
                InvalidateVisual();
                break;
            case Mode.Shape:
                _last = ToInk(e.GetPosition(this));
                Preview = ShapeBuilder.Build(_tool.Shape!.Value, _start, _last, _pen!);
                InvalidateVisual();
                break;
            case Mode.Erase:
                var pos = ToInk(e.GetPosition(this));
                EraseAlong(_last, pos);
                _last = pos;
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_mode == Mode.Shape) _last = ToInk(e.GetPosition(this));
        Finish();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Finish();
    }

    private void Finish()
    {
        switch (_mode)
        {
            case Mode.Freehand:
                _doc.Add(new InkStroke(_points.ToArray(), _pen!));
                break;
            case Mode.Shape:
                var dx = _last.X - _start.X;
                var dy = _last.Y - _start.Y;
                if (Math.Sqrt(dx * dx + dy * dy) >= MinShapeLength)
                {
                    using var group = _doc.BeginGroup();
                    foreach (var s in ShapeBuilder.Build(_tool.Shape!.Value, _start, _last, _pen!)) _doc.Add(s);
                }
                break;
            case Mode.Erase:
                _group?.Dispose();
                _group = null;
                break;
        }
        _mode = Mode.None;
        _points = [];
        Preview = [];
        InvalidateVisual();
    }

    /// <summary>Erases at every step between two positions so a fast drag cannot skip over ink.</summary>
    private void EraseAlong(InkPoint from, InkPoint to)
    {
        var (w, h) = EraserTable.Size(_tool.EraserLevel);
        var step = Math.Max(1.0, Math.Min(w, h) / 2.0);
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / step));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            PointEraser.EraseAt(_doc, new InkPoint(from.X + dx * t, from.Y + dy * t), w, h);
        }
    }

    private static InkPoint ToInk(Point p) => new(p.X, p.Y);
}
