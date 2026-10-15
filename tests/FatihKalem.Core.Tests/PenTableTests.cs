using FatihKalem.Core.Ink;

namespace FatihKalem.Core.Tests;

public class PenTableTests
{
    public static TheoryData<PenKind, int, double, double> Dimensions => new()
    {
        { PenKind.Marker, 1, 1, 1 },        { PenKind.Highlighter, 1, 5, 10 },   { PenKind.Stylograph, 1, 0.75, 2.25 },
        { PenKind.Marker, 2, 2, 2 },        { PenKind.Highlighter, 2, 8, 16 },   { PenKind.Stylograph, 2, 1.25, 3.75 },
        { PenKind.Marker, 3, 3, 3 },        { PenKind.Highlighter, 3, 11, 22 },  { PenKind.Stylograph, 3, 1.75, 6 },
        { PenKind.Marker, 4, 5, 5 },        { PenKind.Highlighter, 4, 14, 28 },  { PenKind.Stylograph, 4, 2.25, 9 },
        { PenKind.Marker, 5, 10, 10 },      { PenKind.Highlighter, 5, 20, 40 },  { PenKind.Stylograph, 5, 5, 20 },
        { PenKind.Marker, 6, 22, 22 },      { PenKind.Highlighter, 6, 30, 60 },  { PenKind.Stylograph, 6, 8, 35 },
    };

    [Theory]
    [MemberData(nameof(Dimensions))]
    public void Create_ReturnsOriginalDimensions(PenKind kind, int level, double w, double h)
    {
        var pen = PenTable.Create(kind, level, 0xFF112233);
        Assert.Equal(w, pen.Width);
        Assert.Equal(h, pen.Height);
        Assert.Equal(0xFF112233u, pen.Argb);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 3)]
    [InlineData(4, 5)] [InlineData(5, 10)] [InlineData(6, 22)]
    public void ShapeSize_MapsLevels(int level, int expected) =>
        Assert.Equal(expected, PenTable.ShapeSize(level));

    [Theory]
    [InlineData(1, 20, 32)] [InlineData(2, 50, 83)]
    [InlineData(3, 100, 160)] [InlineData(4, 180, 288)]
    public void Eraser_Sizes(int level, int w, int h) =>
        Assert.Equal((w, h), EraserTable.Size(level));

    [Fact]
    public void Create_Highlighter_SetsIsHighlighter()
    {
        Assert.True(PenTable.Create(PenKind.Highlighter, 3, 0xFF000000).IsHighlighter);
        Assert.False(PenTable.Create(PenKind.Marker, 3, 0xFF000000).IsHighlighter);
        Assert.False(PenTable.Create(PenKind.Stylograph, 3, 0xFF000000).IsHighlighter);
    }

    [Theory]
    [InlineData(0)] [InlineData(7)]
    public void Size_OutOfRange_Throws(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PenTable.Create(PenKind.Marker, level, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PenTable.ShapeSize(level));
    }

    [Theory]
    [InlineData(0)] [InlineData(5)]
    public void EraserLevel_OutOfRange_Throws(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EraserTable.Size(level));

    public static TheoryData<PenKind, int, uint> Palette => new()
    {
        { PenKind.Marker, 1, 0xFFFF0000 }, { PenKind.Stylograph, 1, 0xFFE61B1B }, { PenKind.Highlighter, 1, 0xFFFF00FF },
        { PenKind.Marker, 2, 0xFF0000FF }, { PenKind.Stylograph, 2, 0xFF00009D }, { PenKind.Highlighter, 2, 0xFF01FFFF },
        { PenKind.Marker, 3, 0xFF006E00 }, { PenKind.Stylograph, 3, 0xFF006E00 }, { PenKind.Highlighter, 3, 0xFF00FF01 },
        { PenKind.Marker, 4, 0xFFFF7300 }, { PenKind.Stylograph, 4, 0xFFFF9800 }, { PenKind.Highlighter, 4, 0xFFFFFF01 },
        { PenKind.Marker, 5, 0xFF000000 }, { PenKind.Stylograph, 5, 0xFF141414 }, { PenKind.Highlighter, 5, 0xFF000000 },
        { PenKind.Marker, 6, 0xFFFFFFFF }, { PenKind.Stylograph, 6, 0xFFFFFFFF }, { PenKind.Highlighter, 6, 0xFFFFFFFF },
    };

    [Theory]
    [MemberData(nameof(Palette))]
    public void ColorFor_MatchesOriginalPalette(PenKind kind, int colorNo, uint argb) =>
        Assert.Equal(argb, PenTable.ColorFor(kind, colorNo));

    [Theory]
    [InlineData(0)] [InlineData(7)]
    public void ColorFor_OutOfRange_Throws(int colorNo) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PenTable.ColorFor(PenKind.Marker, colorNo));
}
