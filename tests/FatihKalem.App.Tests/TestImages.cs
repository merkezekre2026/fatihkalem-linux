using SkiaSharp;

namespace FatihKalem.App.Tests;

internal static class TestImages
{
    /// <summary>Writes a solid-colour PNG (a real image, so Avalonia can decode it).</summary>
    public static string WritePng(string path, int width, int height, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
