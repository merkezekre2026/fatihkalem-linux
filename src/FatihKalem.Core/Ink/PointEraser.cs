namespace FatihKalem.Core.Ink;

/// <summary>Erase-by-point: removes the parts of strokes that fall inside an axis-aligned rectangle.</summary>
public static class PointEraser
{
    public static IReadOnlyList<InkStroke> Apply(InkStroke stroke, InkPoint center, int width, int height)
    {
        var rect = new Rect(center.X - width / 2.0, center.Y - height / 2.0, center.X + width / 2.0, center.Y + height / 2.0);
        var pts = stroke.Points;

        if (pts.Count == 1)
            return rect.Contains(pts[0]) ? [] : [stroke];

        var pieces = new List<InkStroke>();
        var current = new List<InkPoint>();
        var touched = false;

        if (pts.Count > 0 && !rect.Contains(pts[0])) current.Add(pts[0]);

        for (var i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1];
            var b = pts[i];
            if (!rect.TryClip(a, b, out var t0, out var t1))
            {
                if (current.Count > 0 || !rect.Contains(a)) current.Add(b);
                continue;
            }

            touched = true;
            if (t0 > 0)
            {
                current.Add(Lerp(a, b, t0));
                Flush(pieces, current, stroke.Pen);
            }
            else
            {
                Flush(pieces, current, stroke.Pen);
            }

            current = [];
            if (t1 < 1)
            {
                current.Add(Lerp(a, b, t1));
                current.Add(b);
            }
        }

        if (!touched) return [stroke];
        Flush(pieces, current, stroke.Pen);
        return pieces;
    }

    /// <summary>Erases at <paramref name="center"/>; replaces strokes only when something was hit.</summary>
    public static void EraseAt(InkDocument doc, InkPoint center, int width, int height)
    {
        var changed = false;
        var result = new List<InkStroke>(doc.Strokes.Count);
        foreach (var stroke in doc.Strokes)
        {
            var pieces = Apply(stroke, center, width, height);
            if (pieces.Count != 1 || !ReferenceEquals(pieces[0], stroke)) changed = true;
            result.AddRange(pieces);
        }
        if (changed) doc.ReplaceStrokes(result);
    }

    private static void Flush(List<InkStroke> pieces, List<InkPoint> points, PenAttributes pen)
    {
        if (points.Count >= 2) pieces.Add(new InkStroke(points.ToArray(), pen));
    }

    private static InkPoint Lerp(InkPoint a, InkPoint b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private readonly record struct Rect(double MinX, double MinY, double MaxX, double MaxY)
    {
        public bool Contains(InkPoint p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

        /// <summary>Liang–Barsky: parameter interval of segment a→b inside the rectangle.</summary>
        public bool TryClip(InkPoint a, InkPoint b, out double t0, out double t1)
        {
            t0 = 0;
            t1 = 1;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            return Edge(-dx, a.X - MinX, ref t0, ref t1)
                && Edge(dx, MaxX - a.X, ref t0, ref t1)
                && Edge(-dy, a.Y - MinY, ref t0, ref t1)
                && Edge(dy, MaxY - a.Y, ref t0, ref t1);
        }

        private static bool Edge(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0) return q >= 0;
            var r = q / p;
            if (p < 0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }
    }
}
