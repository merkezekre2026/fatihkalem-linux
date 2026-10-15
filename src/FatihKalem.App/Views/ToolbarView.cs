using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FatihKalem.App.ViewModels;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;
using SkiaSharp;

namespace FatihKalem.App.Views;

/// <summary>
/// The tool strip: hand/pen, undo/redo, pens, colours, sizes, erasers, shapes, library, favourites and
/// settings, drawn with the original application's icons. All behaviour lives in <see cref="ToolbarViewModel"/>.
/// </summary>
public sealed class ToolbarView : Border
{
    private const double ButtonSize = 42;
    private const double DefaultMargin = 20;
    private static readonly IBrush Selected = new SolidColorBrush(Color.FromArgb(0x55, 0x2D, 0x7D, 0xE0));
    private static readonly IBrush Idle = Brushes.Transparent;
    private static readonly string[] ColorNames = ["red", "blue", "green", "orange", "black", "white"];

    private readonly ToolbarViewModel _vm;
    private readonly Dictionary<string, Bitmap?> _icons = new();
    private readonly WrapPanel _favorites = new() { Orientation = Orientation.Horizontal };
    private readonly Button _hand, _undo, _redo;
    private readonly Dictionary<PenKind, Button> _penButtons = new();
    private readonly List<Button> _colorButtons = [];
    private readonly List<Button> _sizeButtons = [];
    private readonly List<Button> _eraserButtons = [];
    private readonly Dictionary<DrawKind, Button> _shapeButtons = new();
    private readonly Border _settingsPanel;
    private readonly Border _cartelaPanel;
    private SKBitmap? _cartelaBitmap;
    private Point _dragStart;
    private Thickness _marginAtDragStart;
    private bool _dragging;

    public ToolbarView(ToolbarViewModel vm)
    {
        _vm = vm;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0xF7, 0xF7, 0xFA));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB5));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(6);
        MaxWidth = 8 * ButtonSize + 12;
        Margin = vm.Settings.IsStartupPositionDefault
            ? new Thickness(DefaultMargin, DefaultMargin, 0, 0)
            : new Thickness(vm.Settings.Left, vm.Settings.Top, 0, 0);

        var tools = new WrapPanel { Orientation = Orientation.Horizontal };

        _hand = Tool("Hand", "hand.png", "El / Kalem", vm.ToggleHand, favorite: null);
        _undo = Tool("Undo", "favundo.png", "Geri al", vm.Undo, "Undo");
        _redo = Tool("Redo", "favredo.png", "Yinele", vm.Redo, "Redo");
        tools.Children.Add(_hand);
        tools.Children.Add(_undo);
        tools.Children.Add(_redo);
        tools.Children.Add(Tool("Clear", "eraser.png", "Tümünü temizle", vm.Clear, favorite: null));

        foreach (var (kind, tip) in new[]
                 {
                     (PenKind.Marker, "Tahta Kalemi"), (PenKind.Stylograph, "Kesik Uçlu Kalem"), (PenKind.Highlighter, "Fosforlu Kalem"),
                 })
        {
            var k = kind;
            var button = Tool(kind.ToString(), "marker.png", tip, () => vm.SelectPenKind(k), favorite: null);
            _penButtons[kind] = button;
            tools.Children.Add(button);
        }

        for (var i = 1; i <= 6; i++)
        {
            var n = i;
            var b = Tool($"Color{i}", "markerred.png", "Renk", () => vm.SelectPen(vm.Tool.Pen, n), favorite: null);
            _colorButtons.Add(b);
            tools.Children.Add(b);
        }
        tools.Children.Add(Tool("ColorPicker", "colorselector.png", "Renk seç", ToggleCartela, favorite: null));

        for (var i = 1; i <= 6; i++)
        {
            var n = i;
            var b = Tool($"Size{i}", $"favsize{i}.png", $"Kalınlık {i}", () => vm.SelectSize(n), $"Size{i}");
            _sizeButtons.Add(b);
            tools.Children.Add(b);
        }

        for (var i = 1; i <= 4; i++)
        {
            var n = i;
            var b = Tool($"Eraser{i}", $"faveraser{i}.png", $"Silgi {i}", () => vm.SelectEraser(n), $"Eraser{i}");
            _eraserButtons.Add(b);
            tools.Children.Add(b);
        }

        foreach (var (kind, tip) in new[]
                 {
                     (DrawKind.Line, "Çizgi"), (DrawKind.DashLine, "Kesikli çizgi"), (DrawKind.Arrow, "Ok"),
                     (DrawKind.Rectangle, "Dikdörtgen"), (DrawKind.Ellipse, "Elips"), (DrawKind.Triangle, "Üçgen"),
                 })
        {
            var k = kind;
            var b = Tool(kind.ToString(), $"fav{kind.ToString().ToLowerInvariant()}.png", tip, () => vm.SelectShape(k), kind.ToString());
            _shapeButtons[kind] = b;
            tools.Children.Add(b);
        }

        tools.Children.Add(Tool("Library", "favlibrary.png", "Görseller", vm.OpenLibrary, "Library"));
        tools.Children.Add(Tool("Background", "favbackground.png", "Arka plan sayfaları", vm.OpenBackground, "Background"));
        tools.Children.Add(Tool("Settings", "settings.png", "Ayarlar", () => _settingsPanel!.IsVisible = !_settingsPanel.IsVisible, favorite: null));

        _settingsPanel = BuildSettingsPanel();
        _cartelaPanel = BuildCartelaPanel();

        var header = BuildHeader();
        Child = new StackPanel
        {
            Spacing = 4,
            Children = { header, tools, _cartelaPanel, _settingsPanel, _favorites },
        };

        vm.ModeChanged += Refresh;
        vm.HistoryChanged += Refresh;
        vm.FavoritesChanged += RebuildFavorites;
        vm.Tool.Changed += Refresh;
        RebuildFavorites();
        Refresh();
    }

    public event Action? CloseRequested;

    private Border BuildHeader()
    {
        var close = Tool("Close", "close.png", "Kapat", () => CloseRequested?.Invoke(), favorite: null);
        close.Width = close.Height = 30;
        var logo = new Image
        {
            Source = Icon("fatihpenlogo_wide.png"),
            Height = 26,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
        };
        var handle = new Border
        {
            Name = "DragHandle",
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            Padding = new Thickness(4),
            Child = logo,
        };
        handle.PointerPressed += OnDragStart;
        handle.PointerMoved += OnDragMove;
        handle.PointerReleased += OnDragEnd;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(handle);
        Grid.SetColumn(close, 1);
        grid.Children.Add(close);
        return new Border { Child = grid };
    }

    private Border BuildSettingsPanel()
    {
        var s = _vm.Settings;
        var autostart = new CheckBox { Name = "AutoStart", Content = "Oturum açılışında başlat", IsChecked = s.AutoStart };
        autostart.IsCheckedChanged += (_, _) => _vm.SetAutoStart(autostart.IsChecked == true, Autostart.ResolveExecPath());
        var confirm = new CheckBox { Name = "CloseConfirmation", Content = "Kapatırken onay iste", IsChecked = s.IsCloseConfirmationEnabled };
        confirm.IsCheckedChanged += (_, _) => _vm.SetCloseConfirmation(confirm.IsChecked == true);
        var arrows = new CheckBox { Name = "SideArrows", Content = "Yan okları göster", IsChecked = s.IsSideArrowsEnabled };
        arrows.IsCheckedChanged += (_, _) => _vm.SetSideArrows(arrows.IsChecked == true);
        return new Border
        {
            IsVisible = false,
            Padding = new Thickness(6),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Child = new StackPanel { Children = { autostart, confirm, arrows } },
        };
    }

    private Border BuildCartelaPanel()
    {
        var image = new Image { Name = "Cartela", Source = Icon("colorselector.png"), Stretch = Stretch.Fill, Width = 220 };
        if (image.Source is Bitmap bmp) image.Height = 220.0 * bmp.PixelSize.Height / bmp.PixelSize.Width;
        image.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(image);
            if (PickColor(p.X / image.Width, p.Y / image.Height) is { } argb) _vm.SelectColor(argb);
        };
        return new Border { IsVisible = false, Padding = new Thickness(4), Child = image };
    }

    private void ToggleCartela() => _cartelaPanel.IsVisible = !_cartelaPanel.IsVisible;

    /// <summary>Colour under a relative position of the colour chart image, or null on transparent pixels.</summary>
    private uint? PickColor(double relX, double relY)
    {
        if (_cartelaBitmap is null)
        {
            using var stream = AssetLoader.Open(new Uri(IconUri("colorselector.png")));
            _cartelaBitmap = SKBitmap.Decode(stream);
        }
        if (_cartelaBitmap is null) return null;
        var x = Math.Clamp((int)(relX * _cartelaBitmap.Width), 0, _cartelaBitmap.Width - 1);
        var y = Math.Clamp((int)(relY * _cartelaBitmap.Height), 0, _cartelaBitmap.Height - 1);
        var c = _cartelaBitmap.GetPixel(x, y);
        return c.Alpha == 0 ? null : 0xFF000000u | (uint)(c.Red << 16) | (uint)(c.Green << 8) | c.Blue;
    }

    private void OnDragStart(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _dragStart = e.GetPosition(TopLevel.GetTopLevel(this));
        _marginAtDragStart = Margin;
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void OnDragMove(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;
        var delta = e.GetPosition(TopLevel.GetTopLevel(this)) - _dragStart;
        Margin = new Thickness(
            Math.Max(0, _marginAtDragStart.Left + delta.X), Math.Max(0, _marginAtDragStart.Top + delta.Y), 0, 0);
    }

    private void OnDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        _vm.SaveToolbarPosition((int)Math.Round(Margin.Left), (int)Math.Round(Margin.Top));
    }

    private void Refresh()
    {
        var tool = _vm.Tool;
        var handMode = _vm.Mode == InputMode.Hand;
        ((Image)_hand.Content!).Source = Icon(handMode ? "hand.png" : "pen.png");
        _hand.Background = handMode ? Selected : Idle;

        _undo.IsEnabled = _vm.CanUndo;
        ((Image)_undo.Content!).Source = Icon(_vm.CanUndo ? "favundo.png" : "favgrayedundo.png");
        _redo.IsEnabled = _vm.CanRedo;
        ((Image)_redo.Content!).Source = Icon(_vm.CanRedo ? "favredo.png" : "favgrayedredo.png");

        var drawing = !tool.IsEraser && tool.Shape is null && !handMode;
        var colorNo = _vm.Settings.ColorNo;
        var colorName = colorNo is >= 1 and <= 6 ? ColorNames[colorNo - 1] : "colorful";
        foreach (var (kind, button) in _penButtons)
        {
            ((Image)button.Content!).Source = Icon($"{kind.ToString().ToLowerInvariant()}{colorName}.png");
            button.Background = drawing && tool.Pen == kind ? Selected : Idle;
        }
        for (var i = 0; i < _colorButtons.Count; i++)
        {
            ((Image)_colorButtons[i].Content!).Source = Icon($"{tool.Pen.ToString().ToLowerInvariant()}{ColorNames[i]}.png");
            _colorButtons[i].Background = colorNo == i + 1 ? Selected : Idle;
        }
        for (var i = 0; i < _sizeButtons.Count; i++)
            _sizeButtons[i].Background = tool.SizeLevel == i + 1 ? Selected : Idle;
        for (var i = 0; i < _eraserButtons.Count; i++)
            _eraserButtons[i].Background = tool.IsEraser && tool.EraserLevel == i + 1 ? Selected : Idle;
        foreach (var (kind, button) in _shapeButtons)
            button.Background = !tool.IsEraser && tool.Shape == kind ? Selected : Idle;
    }

    private void RebuildFavorites()
    {
        _favorites.Children.Clear();
        var names = _vm.Favorites;
        for (var i = 0; i < names.Count; i++)
        {
            var index = i;
            var name = names[i];
            var button = new Button
            {
                Name = $"Favorite{i}",
                Width = ButtonSize,
                Height = ButtonSize,
                Padding = new Thickness(2),
                Background = Idle,
                Content = new Image { Source = TryIcon(FavoriteCatalog.IconUri(name)), Width = 32, Height = 32 },
            };
            button.Click += (_, _) => _vm.ActivateFavorite(index);
            var remove = new MenuItem { Header = "Favorilerden kaldır" };
            remove.Click += (_, _) => _vm.RemoveFavorite(name);
            button.ContextMenu = new ContextMenu { ItemsSource = new[] { remove } };
            AutomationProperties.SetName(button, name);
            _favorites.Children.Add(button);
        }
    }

    private Button Tool(string name, string iconFile, string tip, Action onClick, string? favorite)
    {
        var button = new Button
        {
            Name = name,
            Width = ButtonSize,
            Height = ButtonSize,
            Padding = new Thickness(2),
            Background = Idle,
            Content = new Image { Source = Icon(iconFile), Width = 32, Height = 32 },
        };
        button.Click += (_, _) => onClick();
        if (favorite is not null)
        {
            var add = new MenuItem { Header = "Favorilere ekle" };
            add.Click += (_, _) => _vm.AddFavorite(favorite);
            button.ContextMenu = new ContextMenu { ItemsSource = new[] { add } };
        }
        AutomationProperties.SetName(button, tip);
        return button;
    }

    private static string IconUri(string file) => $"avares://FatihKalem/Assets/Icons/{file}";

    private Bitmap? Icon(string file) => TryIcon(IconUri(file));

    private Bitmap? TryIcon(string uri)
    {
        if (_icons.TryGetValue(uri, out var cached)) return cached;
        Bitmap? bitmap = null;
        try
        {
            var u = new Uri(uri);
            if (AssetLoader.Exists(u)) bitmap = new Bitmap(AssetLoader.Open(u));
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            // a missing icon must not take the toolbar down
        }
        return _icons[uri] = bitmap;
    }
}
