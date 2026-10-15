namespace FatihKalem.Core.Ink;

/// <summary>Pen dimensions and palette taken from the original application.</summary>
public static class PenTable
{
    private static readonly (double W, double H)[] Marker =
        [(1, 1), (2, 2), (3, 3), (5, 5), (10, 10), (22, 22)];

    private static readonly (double W, double H)[] Highlighter =
        [(5, 10), (8, 16), (11, 22), (14, 28), (20, 40), (30, 60)];

    private static readonly (double W, double H)[] Stylograph =
        [(0.75, 2.25), (1.25, 3.75), (1.75, 6), (2.25, 9), (5, 20), (8, 35)];

    private static readonly int[] ShapeSizes = [1, 2, 3, 5, 10, 22];

    // Row = colorNo - 1; columns = Marker, Stylograph, Highlighter.
    private static readonly uint[][] Palette =
    [
        [0xFFFF0000, 0xFFE61B1B, 0xFFFF00FF],
        [0xFF0000FF, 0xFF00009D, 0xFF01FFFF],
        [0xFF006E00, 0xFF006E00, 0xFF00FF01],
        [0xFFFF7300, 0xFFFF9800, 0xFFFFFF01],
        [0xFF000000, 0xFF141414, 0xFF000000],
        [0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF],
    ];

    public static PenAttributes Create(PenKind kind, int sizeLevel, uint argb)
    {
        var i = Index(sizeLevel);
        var (w, h) = kind switch
        {
            PenKind.Marker => Marker[i],
            PenKind.Stylograph => Stylograph[i],
            PenKind.Highlighter => Highlighter[i],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new PenAttributes(argb, w, h, kind == PenKind.Highlighter);
    }

    public static int ShapeSize(int sizeLevel) => ShapeSizes[Index(sizeLevel)];

    /// <summary>Colour for palette entries 1..6; the "colourful" entry comes from the user's chosen colour.</summary>
    public static uint ColorFor(PenKind kind, int colorNo)
    {
        if (colorNo is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(colorNo));
        return Palette[colorNo - 1][(int)kind];
    }

    private static int Index(int sizeLevel) =>
        sizeLevel is >= 1 and <= 6 ? sizeLevel - 1 : throw new ArgumentOutOfRangeException(nameof(sizeLevel));
}
