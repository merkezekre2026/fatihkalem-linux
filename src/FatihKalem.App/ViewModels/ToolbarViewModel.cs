using FatihKalem.App.Controls;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;

namespace FatihKalem.App.ViewModels;

/// <summary>Toolbar logic: picks tools, keeps favourites and persists every choice to settings.json.</summary>
public sealed class ToolbarViewModel
{
    private const int ColourfulNo = 7;

    private readonly ToolState _tool;
    private readonly InkDocument _doc;
    private readonly SettingsStore _store;
    private readonly Autostart _autostart;
    private AppSettings _settings;

    public ToolbarViewModel(ToolState tool, InkDocument doc, SettingsStore store, Autostart autostart)
    {
        _tool = tool;
        _doc = doc;
        _store = store;
        _autostart = autostart;
        _settings = store.Load();

        var kind = (PenKind)Math.Min(_settings.PenStyle, (byte)PenKind.Highlighter);
        _tool.Pen = kind;
        _tool.SizeLevel = _settings.InkSize;
        _tool.Argb = _settings.ColorNo == ColourfulNo ? _settings.InkColor : PenTable.ColorFor(kind, _settings.ColorNo);
        _doc.Changed += () => HistoryChanged?.Invoke();
    }

    public event Action? ModeChanged;
    public event Action? FavoritesChanged;
    public event Action? LibraryRequested;
    public event Action? BackgroundRequested;

    /// <summary>Fires after any setting (including the current pen colour) changed.</summary>
    public event Action? SettingsChanged;

    /// <summary>Fires when undo/redo availability may have changed.</summary>
    public event Action? HistoryChanged;

    public InputMode Mode { get; private set; } = InputMode.Pen;

    public AppSettings Settings => _settings;

    public ToolState Tool => _tool;

    public bool CanUndo => _doc.CanUndo;

    public bool CanRedo => _doc.CanRedo;

    public IReadOnlyList<string> Favorites => _settings.Fav.Where(f => !string.IsNullOrEmpty(f)).ToList();

    public void SelectPen(PenKind kind, int colorNo)
    {
        var argb = PenTable.ColorFor(kind, colorNo);
        UseDrawingTool();
        _tool.Pen = kind;
        _tool.Argb = argb;
        Update(s => s with { PenStyle = (byte)kind, ColorNo = (byte)colorNo, InkColor = argb });
    }

    /// <summary>Switches to a pen type and keeps the current colour (palette entry or picked colour).</summary>
    public void SelectPenKind(PenKind kind)
    {
        if (_settings.ColorNo == ColourfulNo)
        {
            UseDrawingTool();
            _tool.Pen = kind;
            Update(s => s with { PenStyle = (byte)kind });
        }
        else
        {
            SelectPen(kind, _settings.ColorNo);
        }
    }

    /// <summary>The "colourful" entry: any colour chosen in the colour picker.</summary>
    public void SelectColor(uint argb)
    {
        UseDrawingTool();
        _tool.Argb = argb;
        Update(s => s with { ColorNo = ColourfulNo, InkColor = argb });
    }

    public void SelectSize(int sizeLevel)
    {
        _tool.SizeLevel = sizeLevel;
        Update(s => s with { InkSize = (byte)sizeLevel });
    }

    public void SelectEraser(int level)
    {
        SetMode(InputMode.Pen);
        _tool.Shape = null;
        _tool.EraserLevel = level;
        _tool.IsEraser = true;
    }

    public void SelectShape(DrawKind kind)
    {
        SetMode(InputMode.Pen);
        _tool.IsEraser = false;
        _tool.Shape = kind;
    }

    public void ToggleHand() => SetMode(Mode == InputMode.Pen ? InputMode.Hand : InputMode.Pen);

    public void Undo() => _doc.Undo();

    public void Redo() => _doc.Redo();

    public void Clear() => _doc.Clear();

    public void AddFavorite(string name)
    {
        var current = Favorites.ToList();
        if (current.Count >= AppSettings.FavoriteSlots || !FavoriteCatalog.Contains(name) || current.Contains(name)) return;
        current.Add(name);
        var slots = new string[AppSettings.FavoriteSlots];
        for (var i = 0; i < slots.Length; i++) slots[i] = i < current.Count ? current[i] : "";
        Update(s => s with { Fav = slots, IFav = (byte)current.Count });
        FavoritesChanged?.Invoke();
    }

    public void OpenLibrary() => LibraryRequested?.Invoke();

    public void OpenBackground() => BackgroundRequested?.Invoke();

    public void RemoveFavorite(string name)
    {
        var current = Favorites.ToList();
        if (!current.Remove(name)) return;
        var slots = new string[AppSettings.FavoriteSlots];
        for (var i = 0; i < slots.Length; i++) slots[i] = i < current.Count ? current[i] : "";
        Update(s => s with { Fav = slots, IFav = (byte)current.Count });
        FavoritesChanged?.Invoke();
    }

    public void ActivateFavorite(int index)
    {
        var favorites = Favorites;
        if (index < 0 || index >= favorites.Count) return;
        Run(favorites[index]);
    }

    /// <returns><c>false</c> if the autostart entry could not be written or removed; the setting is then left unchanged.</returns>
    public bool SetAutoStart(bool enabled, string execPath)
    {
        try
        {
            if (enabled) _autostart.Enable(execPath, preload: false);
            else _autostart.Disable();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        Update(s => s with { AutoStart = enabled });
        return true;
    }

    public void SetCloseConfirmation(bool enabled) => Update(s => s with { IsCloseConfirmationEnabled = enabled });

    public void SetSideArrows(bool enabled) => Update(s => s with { IsSideArrowsEnabled = enabled });

    public void SaveToolbarPosition(int left, int top) =>
        Update(s => s with { IsStartupPositionDefault = false, Left = left, Top = top });

    private void Run(string name)
    {
        if (!FavoriteCatalog.Contains(name)) return; // e.g. "Curtain" (v2) or a stale name
        foreach (var kindName in Enum.GetNames<PenKind>())
        {
            if (name.StartsWith(kindName, StringComparison.Ordinal))
            {
                RunPen(Enum.Parse<PenKind>(kindName), name[kindName.Length..]);
                return;
            }
        }
        if (name.StartsWith("Size", StringComparison.Ordinal)) SelectSize(name[^1] - '0');
        else if (name.StartsWith("Eraser", StringComparison.Ordinal)) SelectEraser(name[^1] - '0');
        else if (Enum.TryParse<DrawKind>(name, out var shape)) SelectShape(shape);
        else if (name == "Undo") Undo();
        else if (name == "Redo") Redo();
        else if (name == "Library") OpenLibrary();
        else if (name == "Background") OpenBackground();
    }

    private void RunPen(PenKind kind, string color)
    {
        switch (color)
        {
            case "NoPen":
                SetMode(InputMode.Hand);
                return;
            case "ColorFul":
                UseDrawingTool();
                _tool.Pen = kind;
                _tool.Argb = _settings.InkColor;
                Update(s => s with { PenStyle = (byte)kind, ColorNo = ColourfulNo });
                return;
        }
        var colorNo = color switch
        {
            "Red" => 1, "Blue" => 2, "Green" => 3, "Orange" or "Yellow" => 4, "Black" => 5, "White" => 6, _ => 0,
        };
        if (colorNo != 0) SelectPen(kind, colorNo);
    }

    private void UseDrawingTool()
    {
        SetMode(InputMode.Pen);
        _tool.IsEraser = false;
        _tool.Shape = null;
    }

    private void SetMode(InputMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        ModeChanged?.Invoke();
    }

    private void Update(Func<AppSettings, AppSettings> change)
    {
        _settings = change(_settings);
        try
        {
            _store.Save(_settings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience: with an unwritable home folder the board keeps working
            // and simply forgets its choices at exit.
        }
        SettingsChanged?.Invoke();
    }
}
