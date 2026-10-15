using FatihKalem.App.Library;

namespace FatihKalem.App.Tests;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fk lib Ş-" + Guid.NewGuid().ToString("N"));
    private string BuiltIn => Path.Combine(_root, "built in");
    private string User => Path.Combine(_root, "user");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void Touch(string root, params string[] parts)
    {
        var path = Path.Combine([root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x89, 0x50]);
    }

    private LibraryService Service() => new(BuiltIn, User);

    [Fact]
    public void Lists_Images_RecursivelyByCategory()
    {
        Touch(BuiltIn, "Görseller", "Diğer Şekiller", "Daire.png");
        Touch(BuiltIn, "Görseller", "Hayvanlar", "Ayı.png");
        var images = Service().Images();
        Assert.Equal(2, images.Count);
        var daire = Assert.Single(images, e => e.Name == "Daire");
        Assert.Equal("Diğer Şekiller", daire.Category);
        Assert.True(daire.IsBuiltIn);
        Assert.Equal(Path.Combine(BuiltIn, "Görseller", "Diğer Şekiller", "Daire.png"), daire.FullPath);
    }

    [Fact]
    public void NestedCategory_IsJoinedWithSlash()
    {
        Touch(BuiltIn, "Görseller", "Haritalar", "Dünya Coğrafi Haritaları", "Asya.png");
        var e = Assert.Single(Service().Images());
        Assert.Equal("Haritalar/Dünya Coğrafi Haritaları", e.Category);
    }

    [Fact]
    public void Lists_Backgrounds_IncludingRootLevelFiles()
    {
        Touch(BuiltIn, "Arka Plan Sayfaları", "Beyaz.png");
        Touch(BuiltIn, "Arka Plan Sayfaları", "Renkli", "Mavi.png");
        Touch(BuiltIn, "Görseller", "Hayvanlar", "Ayı.png");
        var backgrounds = Service().Backgrounds();
        Assert.Equal(2, backgrounds.Count);
        Assert.Equal("", Assert.Single(backgrounds, b => b.Name == "Beyaz").Category);
        Assert.Equal("Renkli", Assert.Single(backgrounds, b => b.Name == "Mavi").Category);
        Assert.DoesNotContain(backgrounds, b => b.Name == "Ayı");
    }

    [Fact]
    public void Handles_TurkishNamesAndSpaces()
    {
        Touch(BuiltIn, "Görseller", "Diğer Şekiller", "İkizkenar Üçgen.png");
        Touch(BuiltIn, "Arka Plan Sayfaları", "Süslü", "Yeşil Papağan.png");
        Assert.Equal("İkizkenar Üçgen", Assert.Single(Service().Images()).Name);
        var bg = Assert.Single(Service().Backgrounds());
        Assert.Equal("Yeşil Papağan", bg.Name);
        Assert.True(File.Exists(bg.FullPath));
    }

    [Fact]
    public void User_Adds_To_BuiltIn_WithoutReplacing()
    {
        Touch(BuiltIn, "Görseller", "Hayvanlar", "Ayı.png");
        Touch(User, "Görseller", "Hayvanlar", "Ayı.png");
        Touch(User, "Görseller", "Benim", "Resim.jpg");
        var images = Service().Images();
        Assert.Equal(3, images.Count);
        Assert.Equal(2, images.Count(i => i.Name == "Ayı"));
        Assert.Single(images, i => i.Name == "Ayı" && i.IsBuiltIn);
        Assert.Single(images, i => i.Name == "Ayı" && !i.IsBuiltIn);
        Assert.False(Assert.Single(images, i => i.Name == "Resim").IsBuiltIn);
    }

    [Fact]
    public void Ignores_NonImages()
    {
        Touch(BuiltIn, "Görseller", "desktop.ini");
        Touch(BuiltIn, "Görseller", "Notlar", "readme.txt");
        Touch(BuiltIn, "Görseller", "Notlar", "Gerçek.png");
        Assert.Equal("Gerçek", Assert.Single(Service().Images()).Name);
    }

    [Theory]
    [InlineData("a.PNG")] [InlineData("a.Jpg")] [InlineData("a.jpeg")] [InlineData("a.gif")] [InlineData("a.bmp")] [InlineData("a.tif")]
    public void Extensions_AreCaseInsensitive(string file)
    {
        Touch(BuiltIn, "Görseller", file);
        Assert.Single(Service().Images());
    }

    [Fact]
    public void Sorted_Ordinal_ByCategoryThenName()
    {
        Touch(BuiltIn, "Görseller", "B", "z.png");
        Touch(BuiltIn, "Görseller", "B", "a.png");
        Touch(BuiltIn, "Görseller", "A", "m.png");
        Assert.Equal(["A/m", "B/a", "B/z"], Service().Images().Select(i => i.Category + "/" + i.Name));
    }

    [Fact]
    public void MissingRoot_ReturnsEmpty()
    {
        var s = Service();
        Assert.Empty(s.Images());
        Assert.Empty(s.Backgrounds());
    }

    [Fact]
    public void ResolveBuiltInRoot_PrefersAppDir()
    {
        Assert.Equal(
            Path.Combine("/tmp/.mount_x", "usr", "share", "FatihKalem", "library"),
            LibraryService.ResolveBuiltInRoot("/tmp/.mount_x", "/opt/app/"));
        Assert.Equal(
            Path.Combine("/opt/app/", "library"),
            LibraryService.ResolveBuiltInRoot(null, "/opt/app/"));
    }

    [Fact]
    public void ResolveUserRoot_UsesXdgDataHomeOrLocalShare()
    {
        Assert.Equal(
            Path.Combine("/x/data", "FatihKalem", "library"),
            LibraryService.ResolveUserRoot("/x/data", "/home/u"));
        Assert.Equal(
            Path.Combine("/home/a b", ".local", "share", "FatihKalem", "library"),
            LibraryService.ResolveUserRoot(null, "/home/a b"));
    }
}
