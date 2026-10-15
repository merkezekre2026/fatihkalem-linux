# Fatih Kalem — Linux (X11)

`Fatih Kalem` akıllı tahta kalem ve çizim uygulamasının Linux sürümü. Windows'taki WPF uygulamasının
(`Fatih Kalem.exe`) yeniden yazımıdır; Avalonia ve .NET 10 ile yerel çalışır. Tek dosyalık **AppImage** olarak
paketlenir, hedef makinede .NET kurulumu gerekmez.

## Çalıştırma

```bash
chmod +x FatihKalem-x86_64.AppImage
./FatihKalem-x86_64.AppImage
```

`libfuse2` yoksa AppImage'ı açmadan çalıştırın:

```bash
./FatihKalem-x86_64.AppImage --appimage-extract-and-run
```

### Önkoşullar (pakete dahil değildir)

- Bir **X11** oturumu (Wayland'de XWayland üzerinden denenir).
- `libX11`, `libXext`, `libICE`, `libSM`, `libfontconfig` — çoğu masaüstü dağıtımında vardır.
- Şeffaf ekran üstü çizim için bir **kompozitör** (çoğu masaüstünde açıktır). Kompozitör yoksa uygulama
  otomatik olarak opak, beyaz tam ekran tahta moduna geçer.

## Özellikler (v1)

- Tahta, kesik uçlu ve fosforlu kalem; 6 renk, renk seçici ve 6 kalınlık.
- Nokta silgi (4 boyut), çizgi, kesikli çizgi, ok, dikdörtgen, elips, üçgen.
- Geri al / yinele / tümünü temizle.
- Arka plan sayfaları ve görsel kütüphanesi (taşı, boyutlandır, döndür).
- 20 favori yuvası (araca sağ tık → *Favorilere ekle*), ayarlar paneli, oturum açılışında başlatma.
- **El** modu: ekranın geri kalanında fare/dokunma altındaki uygulamaya geçer, yalnızca araç çubuğu tıklanabilir.

Kullanıcı görselleri `~/.local/share/FatihKalem/library/Görseller` ve `.../Arka Plan Sayfaları` altına eklenebilir.
Ayarlar `~/.config/FatihKalem/settings.json` dosyasındadır.

## Doğrulananlar ve doğrulanamayanlar

Doğrulandı:

- Birim ve headless testleri (`dotnet test linux/FatihKalem.sln`).
- AppImage, WSL2/WSLg altındaki gerçek bir X11 sunucusunda (XWayland) hem düz hem `--appimage-extract-and-run`
  ile açılıyor; araç çubuğu, ikonlar ve Türkçe metinler görünüyor; sentetik fare girdisiyle çizim, geri al,
  boyut/şekil seçimi çalışıyor.
- "El" modunda X11 giriş bölgesi (XShape) gerçekten araç çubuğu dikdörtgenine daralıyor ve kalem moduna dönünce
  tüm pencereye genişliyor (sunucudan okunarak doğrulandı); El modunda çizim yapılmıyor.
- `~/.config/FatihKalem/settings.json` yazılıyor; otomatik başlatma girdisi `$APPIMAGE` yolunu içeriyor.

**Doğrulanamadı:**

- Gerçek akıllı tahta / dokunmatik donanım davranışı, çoklu dokunma, kalem basıncı.
- Kompozitörlü gerçek bir masaüstünde şeffaflık: WSLg'de kompozitör yoktur, bu yüzden uygulama opak beyaz
  tahta moduna düştü. Şeffaf mod ve "El" modunda tıklamaların alttaki uygulamaya geçmesi bu yüzden görülemedi.
- Wayland masaüstlerinde overlay davranışı.

Dokunma jestleri, perde ve kısa jest v2 kapsamındadır.

Not: Araç çubuğunda araç ipucu (tooltip) yoktur. X11'de (WSLg) ipucu penceresi açıldıktan sonraki ilk tıklama
kayboluyordu; düğmeler bunun yerine erişilebilirlik adı taşır.

## Derleme

Windows'ta (.NET 10 SDK):

```powershell
dotnet test linux\FatihKalem.sln
dotnet publish linux\src\FatihKalem.App -c Release -r linux-x64 --self-contained -p:InvariantGlobalization=true -o <publish-dir>
```

Linux/WSL'de AppImage üretimi:

```bash
packaging/build-appimage.sh <publish-dir> <klasör: Görseller + Arka Plan Sayfaları> FatihKalem-x86_64.AppImage
```

Betik `appimagetool` 1.9.1'i bir kez indirir ve GitHub sürümünün yayımladığı SHA-256 özetiyle eşleşmezse kullanmayı reddeder.

## Lisans notu

Bu, üçüncü tarafın uygulamasından türetilmiş kişisel bir porttur. Görseller ve logo özgün uygulamaya aittir;
dağıtımdan önce lisansları ayrıca kontrol edin.
