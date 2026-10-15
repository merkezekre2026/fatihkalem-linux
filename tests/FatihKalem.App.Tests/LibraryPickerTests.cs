using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FatihKalem.App.Library;
using FatihKalem.App.Views;
using FatihKalem.Core.Ink;
using SkiaSharp;

namespace FatihKalem.App.Tests;

public sealed class LibraryPickerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fk picker Ş-" + Guid.NewGuid().ToString("N"));
    private readonly InkDocument _doc = new();

    private string BuiltIn => Path.Combine(_root, "built in");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LibraryPickerView Make(LibraryPickerMode mode, out Window window)
    {
        var service = new LibraryService(BuiltIn, Path.Combine(_root, "user"));
        var picker = new LibraryPickerView(service, _doc, mode, () => new LibraryPlacement(500, 300, 200));
        window = new Window { Width = 800, Height = 600, Content = picker };
        window.Show();
        return picker;
    }

    private static Button Find(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    private static void Click(Visual root, string name) =>
        Find(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void PickImage_AddsItemAtCenter_ScaledToFit_AndIsUndoable()
    {
        var path = TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "Hayvanlar", "Ayı.png"), 400, 200, SKColors.Red);
        var picker = Make(LibraryPickerMode.Images, out _);
        var closed = 0;
        picker.CloseRequested += () => closed++;

        Click(picker, "Item0");

        var item = Assert.Single(_doc.Items);
        Assert.Equal(path, item.Path);
        Assert.Equal((500.0, 300.0), (item.X, item.Y));
        Assert.Equal(0.5, item.Scale, 9); // 400 px wide image must fit MaxSide = 200
        Assert.Equal(0, item.RotationDegrees);
        Assert.Equal(1, closed);
        _doc.Undo();
        Assert.Empty(_doc.Items);
    }

    [AvaloniaFact]
    public void PickImage_SmallImage_KeepsNaturalSize()
    {
        TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "x.png"), 40, 20, SKColors.Blue);
        var picker = Make(LibraryPickerMode.Images, out _);
        Click(picker, "Item0");
        Assert.Equal(1.0, Assert.Single(_doc.Items).Scale);
    }

    [AvaloniaFact]
    public void PickBackground_SetsBackground_AndUndoRestores()
    {
        var path = TestImages.WritePng(Path.Combine(BuiltIn, "Arka Plan Sayfaları", "Kareli", "Kareli Dar.png"), 192, 108, SKColors.White);
        var picker = Make(LibraryPickerMode.Backgrounds, out _);
        Click(picker, "Item0");
        Assert.Equal(path, _doc.BackgroundPath);
        _doc.Undo();
        Assert.Null(_doc.BackgroundPath);
    }

    [AvaloniaFact]
    public void NoBackgroundButton_ClearsBackground()
    {
        TestImages.WritePng(Path.Combine(BuiltIn, "Arka Plan Sayfaları", "Beyaz.png"), 192, 108, SKColors.White);
        _doc.SetBackground("something.png");
        var picker = Make(LibraryPickerMode.Backgrounds, out _);
        Click(picker, "NoBackground");
        Assert.Null(_doc.BackgroundPath);
    }

    [AvaloniaFact]
    public void ImagesMode_HasNoNoBackgroundButton()
    {
        TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "x.png"), 10, 10, SKColors.Red);
        var picker = Make(LibraryPickerMode.Images, out _);
        Assert.DoesNotContain(picker.GetVisualDescendants().OfType<Button>(), b => b.Name == "NoBackground");
    }

    [AvaloniaFact]
    public void UndecodableFile_IsSkipped_WithoutThrowing()
    {
        var bad = Path.Combine(BuiltIn, "Görseller", "Bozuk.png");
        Directory.CreateDirectory(Path.GetDirectoryName(bad)!);
        File.WriteAllBytes(bad, [0x89, 0x50]);
        TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "Sağlam.png"), 10, 10, SKColors.Red);

        var picker = Make(LibraryPickerMode.Images, out _);
        var buttons = picker.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Item") == true).ToList();
        Assert.Single(buttons);
        Click(picker, "Item0");
        Assert.EndsWith("Sağlam.png", Assert.Single(_doc.Items).Path);
    }

    [AvaloniaFact]
    public void FileDeletedAfterListing_ClickDoesNothing()
    {
        var path = TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "Gidecek.png"), 10, 10, SKColors.Red);
        var picker = Make(LibraryPickerMode.Images, out _);
        File.Delete(path);
        Click(picker, "Item0");
        Assert.Empty(_doc.Items);
    }

    [AvaloniaFact]
    public void Categories_GetHeaders_AndCloseButtonRaisesRequest()
    {
        TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "Hayvanlar", "a.png"), 10, 10, SKColors.Red);
        TestImages.WritePng(Path.Combine(BuiltIn, "Görseller", "Prizmalar", "b.png"), 10, 10, SKColors.Red);
        var picker = Make(LibraryPickerMode.Images, out _);
        var headers = picker.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name?.StartsWith("Category") == true)
            .Select(t => t.Text).ToList();
        Assert.Equal(["Hayvanlar", "Prizmalar"], headers);

        var closed = 0;
        picker.CloseRequested += () => closed++;
        Click(picker, "Close");
        Assert.Equal(1, closed);
        Assert.Empty(_doc.Items);
    }

    [AvaloniaFact]
    public void EmptyLibrary_ShowsMessage_AndDoesNotThrow()
    {
        var picker = Make(LibraryPickerMode.Images, out _);
        Assert.Contains(picker.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "EmptyMessage");
    }
}
