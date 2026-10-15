namespace FatihKalem.Core.Settings;

/// <summary>User settings; defaults are the original application's defaults.</summary>
public sealed record AppSettings
{
    public const int FavoriteSlots = 20;

    public byte PenStyle { get; init; } = 1;
    public byte ColorNo { get; init; } = 2;
    public byte InkSize { get; init; } = 3;
    public bool IsStartupPositionDefault { get; init; } = true;
    public int Left { get; init; }
    public int Top { get; init; }
    public bool AutoStart { get; init; }
    public uint InkColor { get; init; } = 0xFF00009D;
    public bool IsSideArrowsEnabled { get; init; } = true;
    public bool IsShortGestureEnabled { get; init; } = true;
    public bool IsCloseConfirmationEnabled { get; init; }
    public bool IsGestureEnabled { get; init; } = true;
    public string[] Fav { get; init; } = Enumerable.Repeat("", FavoriteSlots).ToArray();
    public byte IFav { get; init; }
}
