using FatihKalem.Core.Ink;

namespace FatihKalem.Core.Tests;

public class ItemTransformTests
{
    private static readonly LibraryItem Item = new("a.png", X: 100, Y: 50, Scale: 1, RotationDegrees: 0);
    private static readonly InkPoint Center = new(100, 50);

    [Fact]
    public void Move_AddsDelta()
    {
        var moved = ItemTransform.Move(Item, 15, -5);
        Assert.Equal((115.0, 45.0), (moved.X, moved.Y));
        Assert.Equal(Item.Scale, moved.Scale);
        Assert.Equal(Item.Path, moved.Path);
    }

    [Fact]
    public void Scale_DoublesWhenHandleDistanceDoubles()
    {
        var scaled = ItemTransform.ScaleFromHandle(Item, Center, new InkPoint(110, 50), new InkPoint(120, 50));
        Assert.Equal(2.0, scaled.Scale, 9);
        Assert.Equal((100.0, 50.0), (scaled.X, scaled.Y));
    }

    [Fact]
    public void Scale_IsRelativeToCurrentScale()
    {
        var item = Item with { Scale = 0.5 };
        var scaled = ItemTransform.ScaleFromHandle(item, Center, new InkPoint(110, 50), new InkPoint(130, 50));
        Assert.Equal(1.5, scaled.Scale, 9);
    }

    [Fact]
    public void Scale_NeverBelowMinimum()
    {
        var scaled = ItemTransform.ScaleFromHandle(Item, Center, new InkPoint(200, 50), new InkPoint(101, 50));
        Assert.Equal(ItemTransform.MinScale, scaled.Scale);
        Assert.Equal(0.05, ItemTransform.MinScale);
    }

    [Fact]
    public void Scale_ZeroDistanceHandle_DoesNotProduceNaN()
    {
        var fromCenter = ItemTransform.ScaleFromHandle(Item, Center, Center, new InkPoint(120, 50));
        Assert.Equal(Item.Scale, fromCenter.Scale);
        var toCenter = ItemTransform.ScaleFromHandle(Item, Center, new InkPoint(120, 50), Center);
        Assert.Equal(ItemTransform.MinScale, toCenter.Scale);
    }

    [Fact]
    public void Rotate_QuarterTurn_RightToDown()
    {
        var rotated = ItemTransform.RotateFromHandle(Item, Center, new InkPoint(110, 50), new InkPoint(100, 60));
        Assert.Equal(90.0, rotated.RotationDegrees, 9);
    }

    [Theory]
    [InlineData(350, 20, 10)]   // wraps past 360
    [InlineData(10, -20, 350)]  // negative delta wraps below 0
    [InlineData(0, 360, 0)]
    public void Rotate_NormalizesTo0_360(double start, double delta, double expected)
    {
        var item = Item with { RotationDegrees = start };
        var rad = delta * Math.PI / 180;
        var to = new InkPoint(100 + 10 * Math.Cos(rad), 50 + 10 * Math.Sin(rad));
        var rotated = ItemTransform.RotateFromHandle(item, Center, new InkPoint(110, 50), to);
        Assert.InRange(rotated.RotationDegrees, 0, 360);
        Assert.Equal(expected, rotated.RotationDegrees, 6);
    }

    [Fact]
    public void Rotate_ZeroDistanceHandle_LeavesRotationUnchanged()
    {
        var item = Item with { RotationDegrees = 33 };
        Assert.Equal(33, ItemTransform.RotateFromHandle(item, Center, Center, new InkPoint(120, 50)).RotationDegrees);
        Assert.Equal(33, ItemTransform.RotateFromHandle(item, Center, new InkPoint(120, 50), Center).RotationDegrees);
    }
}
