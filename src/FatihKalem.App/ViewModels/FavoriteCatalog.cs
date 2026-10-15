namespace FatihKalem.App.ViewModels;

/// <summary>The tool names that can be stored as favourites; they are the original application's names.</summary>
public static class FavoriteCatalog
{
    private static readonly string[] PenKinds = ["Marker", "Stylograph", "Highlighter"];
    private static readonly string[] Colors = ["Red", "Blue", "Green", "Orange", "Black", "White", "ColorFul", "NoPen"];

    public static IReadOnlyList<string> Names { get; } = Build();

    public static bool Contains(string name) => NameSet.Contains(name);

    /// <summary>Icon resource of a favourite slot, e.g. MarkerRed → fav + "markerred" + .png.</summary>
    public static string IconUri(string name) =>
        $"avares://FatihKalem/Assets/Icons/fav{name.ToLowerInvariant()}.png";

    private static readonly HashSet<string> NameSet = new(Names, StringComparer.Ordinal);

    private static List<string> Build()
    {
        var names = new List<string>();
        foreach (var color in Colors)
            foreach (var pen in PenKinds)
                names.Add(pen + ColorName(pen, color));
        names.AddRange(Enumerable.Range(1, 6).Select(i => $"Size{i}"));
        names.AddRange(["Undo", "Redo"]);
        names.AddRange(Enumerable.Range(1, 4).Select(i => $"Eraser{i}"));
        names.AddRange(["Line", "DashLine", "Arrow", "Rectangle", "Ellipse", "Triangle"]);
        names.AddRange(["Library", "Background"]);
        return names;
    }

    // The original names the 4th palette entry "Orange" for markers and "Yellow" for the highlighter.
    private static string ColorName(string pen, string color) =>
        color == "Orange" && pen == "Highlighter" ? "Yellow" : color;
}
