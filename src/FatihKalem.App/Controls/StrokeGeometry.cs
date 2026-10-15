using Avalonia;
using Avalonia.Media;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Controls;

/// <summary>
/// Builds the outline of a stroke as the sweep of the pen tip (an axis-aligned ellipse of
/// Width x Height, like the original StylusTip.Ellipse) along the path. Every segment contributes the
/// convex hull of two tip positions; the hulls share one winding direction and are filled non-zero,
/// so the stroke is a single union: a translucent highlighter has uniform alpha where it overlaps itself.
/// </summary>
internal static class StrokeGeometry
{
    private const int TipSides = 24;

    public static Geometry Build(InkStroke stroke)
    {
        var tip = TipPolygon(stroke.Pen.Width, stroke.Pen.Height);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.SetFillRule(FillRule.NonZero);
            var points = stroke.Points;
            for (var i = 0; i < Math.Max(1, points.Count - 1); i++)
            {
                var a = points[i];
                var b = points.Count > 1 ? points[i + 1] : a;
                var hull = Hull(tip, a, b);
                if (hull.Count < 3) continue;
                ctx.BeginFigure(hull[0], isFilled: true);
                for (var k = 1; k < hull.Count; k++) ctx.LineTo(hull[k]);
                ctx.EndFigure(isClosed: true);
            }
        }
        return geometry;
    }

    private static Point[] TipPolygon(double width, double height)
    {
        var tip = new Point[TipSides];
        for (var i = 0; i < TipSides; i++)
        {
            var t = 2 * Math.PI * i / TipSides;
            tip[i] = new Point(Math.Cos(t) * width / 2, Math.Sin(t) * height / 2);
        }
        return tip;
    }

    private static List<Point> Hull(Point[] tip, InkPoint a, InkPoint b)
    {
        var all = new List<Point>(tip.Length * 2);
        foreach (var t in tip) all.Add(new Point(a.X + t.X, a.Y + t.Y));
        if (a != b)
            foreach (var t in tip) all.Add(new Point(b.X + t.X, b.Y + t.Y));
        return MonotoneChain(all);
    }

    /// <summary>Andrew's monotone chain; always returns the same winding direction.</summary>
    private static List<Point> MonotoneChain(List<Point> pts)
    {
        pts.Sort((p, q) => p.X != q.X ? p.X.CompareTo(q.X) : p.Y.CompareTo(q.Y));
        var hull = new List<Point>(pts.Count);
        foreach (var p in pts)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        var lower = hull.Count + 1;
        for (var i = pts.Count - 2; i >= 0; i--)
        {
            var p = pts[i];
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static double Cross(Point o, Point a, Point b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}
