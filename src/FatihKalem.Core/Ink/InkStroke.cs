namespace FatihKalem.Core.Ink;

public sealed record InkStroke(IReadOnlyList<InkPoint> Points, PenAttributes Pen);
