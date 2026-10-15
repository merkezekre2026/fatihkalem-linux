namespace FatihKalem.Core.Ink;

/// <summary>Pure geometry for moving, resizing and rotating a placed library image by dragging a handle.</summary>
public static class ItemTransform
{
    public const double MinScale = 0.05;

    public static LibraryItem Move(LibraryItem item, double dx, double dy) =>
        item with { X = item.X + dx, Y = item.Y + dy };

    /// <summary>Scale changes by the ratio of the handle's distance from the centre after and before the drag.</summary>
    public static LibraryItem ScaleFromHandle(LibraryItem item, InkPoint center, InkPoint handleFrom, InkPoint handleTo)
    {
        var before = Distance(center, handleFrom);
        if (before == 0) return item;
        var scale = item.Scale * Distance(center, handleTo) / before;
        return item with { Scale = Math.Max(MinScale, scale) };
    }

    /// <summary>Rotation changes by the angle the handle swept around the centre (screen coordinates, y down).</summary>
    public static LibraryItem RotateFromHandle(LibraryItem item, InkPoint center, InkPoint handleFrom, InkPoint handleTo)
    {
        if (Distance(center, handleFrom) == 0 || Distance(center, handleTo) == 0) return item;
        var delta = Angle(center, handleTo) - Angle(center, handleFrom);
        var degrees = (item.RotationDegrees + delta) % 360;
        if (degrees < 0) degrees += 360;
        return item with { RotationDegrees = degrees };
    }

    private static double Distance(InkPoint a, InkPoint b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private static double Angle(InkPoint center, InkPoint p) =>
        Math.Atan2(p.Y - center.Y, p.X - center.X) * 180 / Math.PI;
}
