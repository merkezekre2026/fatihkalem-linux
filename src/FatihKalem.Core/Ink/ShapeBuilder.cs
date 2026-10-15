namespace FatihKalem.Core.Ink;

/// <summary>Turns a two-point drag into the strokes of a shape.</summary>
public static class ShapeBuilder
{
    private const int EllipseSegments = 72;
    private const double ArrowWingAngle = Math.PI / 6; // 30°

    public static IReadOnlyList<InkStroke> Build(DrawKind kind, InkPoint start, InkPoint end, PenAttributes pen) =>
        kind switch
        {
            DrawKind.Line => [new InkStroke([start, end], pen)],
            DrawKind.DashLine => Dashes(start, end, pen),
            DrawKind.Arrow => Arrow(start, end, pen),
            DrawKind.Rectangle => [Rectangle(start, end, pen)],
            DrawKind.Ellipse => [Ellipse(start, end, pen)],
            DrawKind.Triangle => [Triangle(start, end, pen)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static InkStroke Rectangle(InkPoint s, InkPoint e, PenAttributes pen)
    {
        var (x0, y0, x1, y1) = Box(s, e);
        return new InkStroke(
            [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)], pen);
    }

    private static InkStroke Triangle(InkPoint s, InkPoint e, PenAttributes pen)
    {
        var (x0, y0, x1, y1) = Box(s, e);
        var apex = new InkPoint((x0 + x1) / 2, y0);
        return new InkStroke([apex, new(x1, y1), new(x0, y1), apex], pen);
    }

    private static InkStroke Ellipse(InkPoint s, InkPoint e, PenAttributes pen)
    {
        var (x0, y0, x1, y1) = Box(s, e);
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, a = (x1 - x0) / 2, b = (y1 - y0) / 2;
        var points = new InkPoint[EllipseSegments + 1];
        for (var i = 0; i < EllipseSegments; i++)
        {
            var t = 2 * Math.PI * i / EllipseSegments;
            points[i] = new InkPoint(cx + a * Math.Cos(t), cy + b * Math.Sin(t));
        }
        points[EllipseSegments] = points[0];
        return new InkStroke(points, pen);
    }

    private static IReadOnlyList<InkStroke> Arrow(InkPoint s, InkPoint e, PenAttributes pen)
    {
        double dx = e.X - s.X, dy = e.Y - s.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var (ux, uy) = length > 0 ? (dx / length, dy / length) : (1.0, 0.0);
        var thickness = Math.Max(pen.Width, pen.Height);
        var head = Math.Max(14, 4 * thickness);
        if (length > 0) head = Math.Min(head, length);

        InkPoint Wing(double sign)
        {
            var (sin, cos) = Math.SinCos(sign * ArrowWingAngle);
            // rotate the backward direction (-u) by ±30°
            var bx = -ux * cos + uy * sin;
            var by = -ux * sin - uy * cos;
            return new InkPoint(e.X + bx * head, e.Y + by * head);
        }

        return
        [
            new InkStroke([s, e], pen),
            new InkStroke([Wing(+1), e], pen),
            new InkStroke([Wing(-1), e], pen),
        ];
    }

    private static IReadOnlyList<InkStroke> Dashes(InkPoint s, InkPoint e, PenAttributes pen)
    {
        double dx = e.X - s.X, dy = e.Y - s.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return [new InkStroke([s, s], pen)];

        var thickness = Math.Max(pen.Width, pen.Height);
        double dash = Math.Max(12, 3 * thickness), gap = Math.Max(8, 2 * thickness);
        double ux = dx / length, uy = dy / length;

        var strokes = new List<InkStroke>();
        for (var pos = 0.0; pos < length; pos += dash + gap)
        {
            var to = Math.Min(pos + dash, length);
            strokes.Add(new InkStroke(
                [new InkPoint(s.X + ux * pos, s.Y + uy * pos), new InkPoint(s.X + ux * to, s.Y + uy * to)], pen));
        }
        return strokes;
    }

    private static (double X0, double Y0, double X1, double Y1) Box(InkPoint a, InkPoint b) =>
        (Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}
