using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FatihKalem.App.Controls;
using FatihKalem.App.Platform;
using FatihKalem.Core.Ink;

namespace FatihKalem.App.Windows;

public enum InputMode
{
    /// <summary>Strokes are drawn; the whole window takes input.</summary>
    Pen,

    /// <summary>Clicks pass through to the desktop underneath; only registered controls (the toolbar) stay clickable.</summary>
    Hand,
}

/// <summary>
/// The full-screen, transparent, always-on-top window the board is drawn on. If the platform cannot
/// provide transparency it falls back to an opaque white board.
/// </summary>
public sealed class OverlayWindow : Window
{
    private readonly IInputRegion _region;
    private readonly List<Control> _interactive = [];
    private readonly Dictionary<Control, EventHandler> _layoutHandlers = new();
    private InputMode _mode = InputMode.Pen;
    private IReadOnlyList<PixelRect>? _lastRects;

    public OverlayWindow(InkDocument doc, ToolState tool, IInputRegion? region = null)
    {
        _region = region ?? new X11InputRegion();

        Title = "Fatih Kalem";
        Topmost = true;
        ShowInTaskbar = true;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;

        Surface = new InkSurface(doc, tool);
        Root.Children.Add(Surface);
        Content = Root;

        Opened += (_, _) =>
        {
            ApplyWorkArea();
            ApplyTransparencyLevel(ActualTransparencyLevel);
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActualTransparencyLevelProperty)
            ApplyTransparencyLevel((WindowTransparencyLevel)change.NewValue!);
    }

    /// <summary>Layers stacked over each other: the ink surface, then toolbar and other overlays.</summary>
    public Grid Root { get; } = new();

    public InkSurface Surface { get; }

    public bool IsTransparentSupported { get; private set; } = true;

    public InputMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Surface.IsHitTestVisible = value == InputMode.Pen;
            UpdateInputRegion();
        }
    }

    /// <summary>A control that stays clickable while the window is in <see cref="InputMode.Hand"/>.</summary>
    public void RegisterInteractive(Control control)
    {
        _interactive.Add(control);
        EventHandler handler = (_, _) =>
        {
            if (_mode == InputMode.Hand) UpdateInputRegion();
        };
        _layoutHandlers[control] = handler;
        control.LayoutUpdated += handler;
        if (_mode == InputMode.Hand) UpdateInputRegion();
    }

    public void UnregisterInteractive(Control control)
    {
        if (!_interactive.Remove(control)) return;
        if (_layoutHandlers.Remove(control, out var handler)) control.LayoutUpdated -= handler;
        if (_mode == InputMode.Hand) UpdateInputRegion();
    }

    internal void ApplyTransparencyLevel(WindowTransparencyLevel actual)
    {
        IsTransparentSupported = actual == WindowTransparencyLevel.Transparent;
        Background = IsTransparentSupported ? Brushes.Transparent : Brushes.White;
    }

    private void ApplyWorkArea()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        Position = area.Position;
        Width = area.Width / screen.Scaling;
        Height = area.Height / screen.Scaling;
    }

    private void UpdateInputRegion()
    {
        var rects = _mode == InputMode.Hand ? InteractiveRects() : null;
        if (rects is not null && _lastRects is not null && rects.SequenceEqual(_lastRects)) return;
        _lastRects = rects;
        _region.TrySetInputRegion(TryGetPlatformHandle()?.Handle ?? 0, rects);
    }

    private List<PixelRect> InteractiveRects()
    {
        var scale = RenderScaling;
        var rects = new List<PixelRect>();
        foreach (var control in _interactive)
        {
            if (!control.IsVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0) continue;
            if (control.TranslatePoint(default, this) is not { } origin) continue;
            rects.Add(new PixelRect(
                (int)Math.Round(origin.X * scale), (int)Math.Round(origin.Y * scale),
                (int)Math.Round(control.Bounds.Width * scale), (int)Math.Round(control.Bounds.Height * scale)));
        }
        return rects;
    }
}
