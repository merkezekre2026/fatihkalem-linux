using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FatihKalem.Core.Settings;

/// <summary>Reads and writes settings.json using the original application's key names.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly AppSettings Defaults = new();
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string DefaultPath => ResolveDefaultPath(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string ResolveDefaultPath(string? xdgConfigHome, string home)
    {
        var config = string.IsNullOrEmpty(xdgConfigHome) ? Path.Combine(home, ".config") : xdgConfigHome;
        return Path.Combine(config, "FatihKalem", "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(path)) return Defaults;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            root = null;
        }

        if (root is null)
        {
            File.Move(path, path + ".bak", overwrite: true);
            return Defaults;
        }

        var fav = new string[AppSettings.FavoriteSlots];
        for (var i = 0; i < fav.Length; i++)
            fav[i] = ReadString(root, $"Fav{i + 1}") ?? "";

        return new AppSettings
        {
            PenStyle = (byte)ReadInt(root, "PenStyle", 0, 2, Defaults.PenStyle),
            ColorNo = (byte)ReadInt(root, "ColorNo", 1, 7, Defaults.ColorNo),
            InkSize = (byte)ReadInt(root, "InkSize", 1, 6, Defaults.InkSize),
            IsStartupPositionDefault = ReadBool(root, "isStartupPositionDefault", Defaults.IsStartupPositionDefault),
            Left = ReadInt(root, "Left", int.MinValue, int.MaxValue, Defaults.Left),
            Top = ReadInt(root, "Top", int.MinValue, int.MaxValue, Defaults.Top),
            AutoStart = ReadBool(root, "AutoStart", Defaults.AutoStart),
            InkColor = ReadColor(root, "InkColor", Defaults.InkColor),
            IsSideArrowsEnabled = ReadBool(root, "isSideArrowsEnabled", Defaults.IsSideArrowsEnabled),
            IsShortGestureEnabled = ReadBool(root, "isShortGestureEnabled", Defaults.IsShortGestureEnabled),
            IsCloseConfirmationEnabled = ReadBool(root, "isCLoseConfirmationEnabled", Defaults.IsCloseConfirmationEnabled),
            IsGestureEnabled = ReadBool(root, "isGestureEnabled", Defaults.IsGestureEnabled),
            Fav = fav,
            IFav = (byte)ReadInt(root, "iFav", 0, AppSettings.FavoriteSlots, Defaults.IFav),
        };
    }

    public void Save(AppSettings s)
    {
        var root = new JsonObject
        {
            ["PenStyle"] = s.PenStyle,
            ["ColorNo"] = s.ColorNo,
            ["InkSize"] = s.InkSize,
            ["isStartupPositionDefault"] = s.IsStartupPositionDefault,
            ["Left"] = s.Left,
            ["Top"] = s.Top,
            ["AutoStart"] = s.AutoStart,
            ["InkColor"] = "#" + s.InkColor.ToString("X8", CultureInfo.InvariantCulture),
            ["isSideArrowsEnabled"] = s.IsSideArrowsEnabled,
            ["isShortGestureEnabled"] = s.IsShortGestureEnabled,
            ["isCLoseConfirmationEnabled"] = s.IsCloseConfirmationEnabled,
            ["isGestureEnabled"] = s.IsGestureEnabled,
        };
        for (var i = 0; i < AppSettings.FavoriteSlots; i++)
            root[$"Fav{i + 1}"] = i < s.Fav.Length ? s.Fav[i] ?? "" : "";
        root["iFav"] = s.IFav;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static int ReadInt(JsonObject o, string key, int min, int max, int fallback) =>
        o[key] is JsonValue v && v.TryGetValue(out long n) && n >= min && n <= max ? (int)n : fallback;

    private static bool ReadBool(JsonObject o, string key, bool fallback) =>
        o[key] is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;

    private static string? ReadString(JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;

    private static uint ReadColor(JsonObject o, string key, uint fallback)
    {
        if (o[key] is not JsonValue v) return fallback;
        if (v.TryGetValue(out string? s) && s is { Length: > 1 } && s[0] == '#')
        {
            var hex = s[1..];
            if (hex.Length == 6) hex = "FF" + hex;
            if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                return argb;
            return fallback;
        }
        return v.TryGetValue(out long n) && n >= 0 && n <= uint.MaxValue ? (uint)n : fallback;
    }
}
