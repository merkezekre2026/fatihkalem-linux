using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FatihKalem.App.Library;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Views;

public enum LibraryPickerMode
{
    /// <summary>Pictures from "Görseller", placed on the board.</summary>
    Images,

    /// <summary>Pages from "Arka Plan Sayfaları", used as the board background.</summary>
    Backgrounds,
}

/// <summary>Where a picked picture goes and how large it may be (both in board coordinates).</summary>
public readonly record struct LibraryPlacement(double CenterX, double CenterY, double MaxSide);

/// <summary>Grid of library thumbnails, grouped by category. Choosing one closes the picker.</summary>
public sealed class LibraryPickerView : Border
{
    private const int ThumbnailWidth = 96;

    private readonly InkDocument _doc;
    private readonly LibraryPickerMode _mode;
    private readonly Func<LibraryPlacement> _placement;

    public LibraryPickerView(LibraryService service, InkDocument doc, LibraryPickerMode mode, Func<LibraryPlacement> placement)
    {
        _doc = doc;
        _mode = mode;
        _placement = placement;

        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB5));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(8);
        MaxWidth = 760;
        MaxHeight = 560;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var entries = mode == LibraryPickerMode.Images ? service.Images() : service.Backgrounds();
        var content = new StackPanel { Spacing = 6 };
        var index = 0;
        foreach (var group in entries.GroupBy(e => e.Category))
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var entry in group)
            {
                if (Thumbnail(entry.FullPath) is not { } thumbnail) continue;
                panel.Children.Add(ItemButton(entry, thumbnail, index++));
            }
            if (panel.Children.Count == 0) continue;
            if (group.Key != "")
                content.Children.Add(new TextBlock { Name = $"Category{content.Children.Count}", Text = group.Key, FontWeight = FontWeight.SemiBold });
            content.Children.Add(panel);
        }
        if (index == 0)
            content.Children.Add(new TextBlock { Name = "EmptyMessage", Text = "Görsel bulunamadı" });

        var title = new TextBlock
        {
            Text = mode == LibraryPickerMode.Images ? "Görseller" : "Arka plan sayfaları",
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var close = new Button { Name = "Close", Content = "Kapat" };
        close.Click += (_, _) => CloseRequested?.Invoke();
        var bar = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(close, Dock.Right);
        bar.Children.Add(close);
        if (mode == LibraryPickerMode.Backgrounds)
        {
            var none = new Button { Name = "NoBackground", Content = "Arka plan yok", Margin = new Thickness(0, 0, 6, 0) };
            none.Click += (_, _) =>
            {
                _doc.SetBackground(null);
                CloseRequested?.Invoke();
            };
            DockPanel.SetDock(none, Dock.Right);
            bar.Children.Add(none);
        }
        DockPanel.SetDock(title, Dock.Left);
        bar.Children.Add(title);

        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        dock.Children.Add(new ScrollViewer { Content = content, Margin = new Thickness(0, 8, 0, 0) });
        Child = dock;
    }

    public event Action? CloseRequested;

    private Button ItemButton(LibraryEntry entry, Bitmap thumbnail, int index)
    {
        var button = new Button
        {
            Name = $"Item{index}",
            Padding = new Thickness(2),
            Margin = new Thickness(2),
            Content = new Image { Source = thumbnail, Width = ThumbnailWidth, Stretch = Stretch.Uniform },
        };
        ToolTip.SetTip(button, entry.Name);
        button.Click += (_, _) => Pick(entry);
        return button;
    }

    private void Pick(LibraryEntry entry)
    {
        if (!File.Exists(entry.FullPath)) return;
        if (_mode == LibraryPickerMode.Backgrounds)
        {
            _doc.SetBackground(entry.FullPath);
        }
        else
        {
            if (NaturalSize(entry.FullPath) is not { } size) return;
            var place = _placement();
            var scale = Math.Min(1.0, place.MaxSide / Math.Max(size.Width, size.Height));
            _doc.AddItem(new LibraryItem(entry.FullPath, place.CenterX, place.CenterY, scale, 0));
        }
        CloseRequested?.Invoke();
    }

    private static Size? NaturalSize(string path)
    {
        try
        {
            using var bitmap = new Bitmap(path);
            return bitmap.Size;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static Bitmap? Thumbnail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, ThumbnailWidth, BitmapInterpolationMode.MediumQuality);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null; // corrupt or unreadable file: leave it out of the grid
        }
    }
}
