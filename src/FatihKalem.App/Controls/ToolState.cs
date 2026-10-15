using FatihKalem.Core.Ink;

namespace FatihKalem.App.Controls;

/// <summary>The active drawing tool. <see cref="Shape"/> wins over the pen; <see cref="IsEraser"/> wins over both.</summary>
public sealed class ToolState
{
    private DrawKind? _shape;
    private bool _isEraser;
    private int _eraserLevel = 1;
    private PenKind _pen = PenKind.Stylograph;
    private int _sizeLevel = 3;
    private uint _argb = 0xFF00009D;

    public event Action? Changed;

    public DrawKind? Shape { get => _shape; set => Set(ref _shape, value); }
    public bool IsEraser { get => _isEraser; set => Set(ref _isEraser, value); }
    public int EraserLevel { get => _eraserLevel; set => Set(ref _eraserLevel, value); }
    public PenKind Pen { get => _pen; set => Set(ref _pen, value); }
    public int SizeLevel { get => _sizeLevel; set => Set(ref _sizeLevel, value); }
    public uint Argb { get => _argb; set => Set(ref _argb, value); }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke();
    }
}
