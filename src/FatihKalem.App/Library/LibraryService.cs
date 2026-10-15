namespace FatihKalem.App.Library;

public sealed record LibraryEntry(string Category, string Name, string FullPath, bool IsBuiltIn);

/// <summary>
/// Lists the picture library: a read-only built-in root (shipped inside the AppImage) merged with a
/// user root. Both roots hold the original folders "Görseller" and "Arka Plan Sayfaları".
/// </summary>
public sealed class LibraryService(string builtInRoot, string userRoot)
{
    public const string ImagesFolder = "Görseller";
    public const string BackgroundsFolder = "Arka Plan Sayfaları";

    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif" };

    public static string DefaultBuiltInRoot => ResolveBuiltInRoot(
        Environment.GetEnvironmentVariable("APPDIR"), AppContext.BaseDirectory);

    public static string DefaultUserRoot => ResolveUserRoot(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string ResolveBuiltInRoot(string? appDir, string baseDirectory) =>
        string.IsNullOrEmpty(appDir)
            ? Path.Combine(baseDirectory, "library")
            : Path.Combine(appDir, "usr", "share", "FatihKalem", "library");

    public static string ResolveUserRoot(string? xdgDataHome, string home)
    {
        var data = string.IsNullOrEmpty(xdgDataHome) ? Path.Combine(home, ".local", "share") : xdgDataHome;
        return Path.Combine(data, "FatihKalem", "library");
    }

    public IReadOnlyList<LibraryEntry> Images() => Scan(ImagesFolder);

    public IReadOnlyList<LibraryEntry> Backgrounds() => Scan(BackgroundsFolder);

    private List<LibraryEntry> Scan(string section)
    {
        var entries = new List<LibraryEntry>();
        Collect(entries, Path.Combine(builtInRoot, section), isBuiltIn: true);
        Collect(entries, Path.Combine(userRoot, section), isBuiltIn: false);
        entries.Sort((a, b) =>
        {
            var c = string.CompareOrdinal(a.Category, b.Category);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Name, b.Name);
            return c != 0 ? c : b.IsBuiltIn.CompareTo(a.IsBuiltIn);
        });
        return entries;
    }

    private static void Collect(List<LibraryEntry> into, string sectionRoot, bool isBuiltIn)
    {
        if (!Directory.Exists(sectionRoot)) return;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(sectionRoot, "*", options))
        {
            if (!Extensions.Contains(Path.GetExtension(file))) continue;
            var dir = Path.GetRelativePath(sectionRoot, Path.GetDirectoryName(file)!);
            var category = dir == "." ? "" : dir.Replace('\\', '/').Replace(Path.DirectorySeparatorChar, '/');
            into.Add(new LibraryEntry(category, Path.GetFileNameWithoutExtension(file), file, isBuiltIn));
        }
    }
}
