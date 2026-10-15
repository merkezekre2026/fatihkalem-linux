using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FatihKalem.App.Controls;
using FatihKalem.App.Platform;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Tests;

public class OverlayWindowTests
{
    private sealed class FakeRegion : IInputRegion
    {
        public List<IReadOnlyList<PixelRect>?> Calls { get; } = [];

        public bool TrySetInputRegion(IntPtr nativeWindow, IReadOnlyList<PixelRect>? rects)
        {
            Calls.Add(rects);
            return true;
        }
    }

    private static (OverlayWindow Window, FakeRegion Region, InkDocument Doc) Make()
    {
        var region = new FakeRegion();
        var doc = new InkDocument();
        return (new OverlayWindow(doc, new ToolState(), region), region, doc);
    }

    [AvaloniaFact]
    public void Window_Is_Topmost_Borderless_Transparent()
    {
        var (w, _, _) = Make();
        Assert.True(w.Topmost);
        Assert.Equal(SystemDecorations.None, w.SystemDecorations);
        Assert.False(w.CanResize);
        Assert.Contains(WindowTransparencyLevel.Transparent, w.TransparencyLevelHint);
        Assert.Equal(0, ((ISolidColorBrush)w.Background!).Color.A);
    }

    [AvaloniaFact]
    public void Window_Fills_WorkArea()
    {
        var (w, _, _) = Make();
        w.Show();
        var screen = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
        Assert.NotNull(screen);
        var scale = screen!.Scaling;
        Assert.Equal(screen.WorkingArea.Width / scale, w.Width, 3);
        Assert.Equal(screen.WorkingArea.Height / scale, w.Height, 3);
        Assert.Equal(screen.WorkingArea.X, w.Position.X);
        Assert.Equal(screen.WorkingArea.Y, w.Position.Y);
    }

    [AvaloniaFact]
    public void Mode_Pen_SurfaceHitTestable_Hand_NotAndRegionSet()
    {
        var (w, region, _) = Make();
        w.Show();
        Assert.Equal(InputMode.Pen, w.Mode);
        Assert.True(w.Surface.IsHitTestVisible);

        w.Mode = InputMode.Hand;
        Assert.False(w.Surface.IsHitTestVisible);
        Assert.NotNull(region.Calls[^1]); // only the registered interactive areas stay clickable

        w.Mode = InputMode.Pen;
        Assert.True(w.Surface.IsHitTestVisible);
        Assert.Null(region.Calls[^1]); // whole window clickable again
    }

    [AvaloniaFact]
    public void HandMode_KeepsRegisteredControlsClickable()
    {
        var (w, region, _) = Make();
        var toolbar = new Border { Width = 100, Height = 40, Margin = new Thickness(10, 20, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        w.Root.Children.Add(toolbar);
        w.RegisterInteractive(toolbar);
        w.Show();

        w.Mode = InputMode.Hand;
        var rect = Assert.Single(region.Calls[^1]!);
        Assert.Equal(new PixelRect(10, 20, 100, 40), rect);
    }

    [AvaloniaFact]
    public void ApplyTransparencyLevel_FallsBackToOpaqueWhenUnsupported()
    {
        var (w, _, _) = Make();
        w.ApplyTransparencyLevel(WindowTransparencyLevel.None);
        Assert.False(w.IsTransparentSupported);
        Assert.Equal(Colors.White, ((ISolidColorBrush)w.Background!).Color);

        w.ApplyTransparencyLevel(WindowTransparencyLevel.Transparent);
        Assert.True(w.IsTransparentSupported);
        Assert.Equal(0, ((ISolidColorBrush)w.Background!).Color.A);
    }

    [Fact]
    public void X11InputRegion_WithoutX11OrHandle_ReturnsFalseAndDoesNotThrow()
    {
        var region = new X11InputRegion();
        Assert.False(region.TrySetInputRegion(IntPtr.Zero, null));
        Assert.False(region.TrySetInputRegion(IntPtr.Zero, [new PixelRect(0, 0, 10, 10)]));
    }
}
