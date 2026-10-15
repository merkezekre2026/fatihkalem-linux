using Avalonia.Media.Imaging;

namespace FatihKalem.App.Controls;

/// <summary>Decodes board images once. A file that is missing or cannot be decoded yields <c>null</c>, never an exception.</summary>
public sealed class ImageCache
{
    private readonly Dictionary<string, Bitmap?> _bitmaps = new(StringComparer.Ordinal);

    public Bitmap? Get(string path)
    {
        if (_bitmaps.TryGetValue(path, out var cached)) return cached;
        Bitmap? bitmap = null;
        try
        {
            if (File.Exists(path)) bitmap = new Bitmap(path);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // unreadable or corrupt image: treated as absent
        }
        return _bitmaps[path] = bitmap;
    }
}
