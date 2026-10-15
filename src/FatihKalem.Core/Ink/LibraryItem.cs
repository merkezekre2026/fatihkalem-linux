namespace FatihKalem.Core.Ink;

/// <summary>An image from the library placed on the board.</summary>
public sealed record LibraryItem(string Path, double X, double Y, double Scale, double RotationDegrees);
