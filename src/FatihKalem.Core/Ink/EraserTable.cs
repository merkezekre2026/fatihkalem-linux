namespace FatihKalem.Core.Ink;

public static class EraserTable
{
    private static readonly (int Width, int Height)[] Sizes = [(20, 32), (50, 83), (100, 160), (180, 288)];

    public static (int Width, int Height) Size(int level) =>
        level is >= 1 and <= 4 ? Sizes[level - 1] : throw new ArgumentOutOfRangeException(nameof(level));
}
