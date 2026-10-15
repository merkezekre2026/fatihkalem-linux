using System.Runtime.InteropServices;
using Avalonia;

namespace FatihKalem.App.Platform;

/// <summary>Controls which part of a native window receives mouse/touch input.</summary>
public interface IInputRegion
{
    /// <param name="nativeWindow">Native window handle (the X11 window id on Linux).</param>
    /// <param name="rects">Window-pixel rectangles that stay clickable; <c>null</c> restores the whole window.</param>
    /// <returns><c>false</c> when the platform cannot do it (not X11, missing libraries, no handle).</returns>
    bool TrySetInputRegion(nint nativeWindow, IReadOnlyList<PixelRect>? rects);
}

/// <summary>
/// X11 implementation using the SHAPE extension's input region. Everything outside the given rectangles
/// lets clicks fall through to the windows underneath (the original app got this from WPF per-pixel hit testing).
/// </summary>
public sealed class X11InputRegion : IInputRegion
{
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int Unsorted = 0;

    public bool TrySetInputRegion(nint nativeWindow, IReadOnlyList<PixelRect>? rects)
    {
        if (nativeWindow == 0 || !OperatingSystem.IsLinux()) return false;
        try
        {
            var display = XOpenDisplay(0);
            if (display == 0) return false;
            try
            {
                var window = (nuint)nativeWindow;
                if (rects is null)
                {
                    XShapeCombineMask(display, window, ShapeInput, 0, 0, 0, ShapeSet);
                }
                else
                {
                    var native = rects.Select(r => new XRectangle
                    {
                        X = (short)r.X, Y = (short)r.Y, Width = (ushort)r.Width, Height = (ushort)r.Height,
                    }).ToArray();
                    XShapeCombineRectangles(display, window, ShapeInput, 0, 0, native, native.Length, ShapeSet, Unsorted);
                }
                XFlush(display);
                return true;
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRectangle
    {
        public short X;
        public short Y;
        public ushort Width;
        public ushort Height;
    }

    [DllImport("libX11.so.6")]
    private static extern nint XOpenDisplay(nint displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(nint display);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(nint display);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(
        nint display, nuint window, int destKind, int xOff, int yOff, XRectangle[] rects, int count, int op, int ordering);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineMask(
        nint display, nuint window, int destKind, int xOff, int yOff, nuint pixmap, int op);
}
