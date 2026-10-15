using System.Text;

namespace FatihKalem.Core.Settings;

/// <summary>Manages the XDG autostart entry (replaces the original HKCU\...\Run registry value).</summary>
public sealed class Autostart(string directory)
{
    private const string FileName = "fatih-kalem.desktop";
    private const string PreloadFlag = " /PreLoad";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private string EntryPath => Path.Combine(directory, FileName);

    public static string DefaultDirectory => ResolveDirectory(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string ResolveDirectory(string? xdgConfigHome, string home) =>
        Path.Combine(string.IsNullOrEmpty(xdgConfigHome) ? Path.Combine(home, ".config") : xdgConfigHome, "autostart");

    public static string ResolveExecPath() =>
        ResolveExecPath(Environment.GetEnvironmentVariable("APPIMAGE"), Environment.ProcessPath);

    /// <summary>The AppImage mount point is temporary, so prefer the AppImage file itself.</summary>
    public static string ResolveExecPath(string? appImageEnv, string? processPath) =>
        !string.IsNullOrEmpty(appImageEnv) ? appImageEnv : processPath ?? "FatihKalem";

    public bool IsEnabled => File.Exists(EntryPath);

    public bool IsPreload =>
        IsEnabled && File.ReadLines(EntryPath).Any(l =>
            l.StartsWith("Exec=", StringComparison.Ordinal) && l.EndsWith(PreloadFlag, StringComparison.Ordinal));

    public void Enable(string execPath, bool preload)
    {
        Directory.CreateDirectory(directory);
        var exec = "\"" + EscapeExecArgument(execPath) + "\"" + (preload ? PreloadFlag : "");
        var content = string.Join('\n',
            "[Desktop Entry]",
            "Type=Application",
            "Name=Fatih Pen",
            "Exec=" + exec,
            "Terminal=false",
            "X-GNOME-Autostart-enabled=true",
            "");
        File.WriteAllText(EntryPath, content, Utf8NoBom);
    }

    public void Disable()
    {
        if (File.Exists(EntryPath)) File.Delete(EntryPath);
    }

    /// <summary>
    /// Desktop Entry spec: inside a quoted Exec argument, " ` $ \ are backslash-escaped, and the
    /// key-value string layer then doubles each backslash; % becomes %%.
    /// </summary>
    private static string EscapeExecArgument(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"' or '`' or '$' or '\\':
                    sb.Append("\\\\").Append(c);
                    break;
                case '%':
                    sb.Append("%%");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
