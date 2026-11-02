using PiRadar.Core.Images;

namespace PiRadar.Core.Tests.Images;

public class CropRectTests
{
    [Fact]
    public void Full_IsTheWholeImage()
    {
        Assert.True(CropRect.Full.IsFull);
        Assert.Equal((0, 0, 1200, 1600), CropRect.Full.ToPixels(1200, 1600));
    }

    [Fact]
    public void Normalize_ClampsToTheImageAndOrdersTheSides()
    {
        var crop = new CropRect(0.9, -0.2, 0.1, 1.3).Normalize();

        Assert.Equal(new CropRect(0.1, 0, 0.9, 1), crop);
        Assert.True(crop.IsFull is false);
    }

    [Fact]
    public void Normalize_GrowsATinyFrameToTheMinimumAroundItsCenter()
    {
        var crop = new CropRect(0.50, 0.50, 0.52, 0.51).Normalize();

        Assert.Equal(CropRect.MinSide, crop.Width, 6);
        Assert.Equal(CropRect.MinSide, crop.Height, 6);
        Assert.Equal(0.51, (crop.Left + crop.Right) / 2, 6);
    }

    [Fact]
    public void Normalize_TinyFrameAtTheEdge_StaysInside()
    {
        var crop = new CropRect(0.98, 0.0, 1.0, 0.01).Normalize();

        Assert.Equal(new CropRect(0.9, 0, 1, 0.1), crop with { Left = Math.Round(crop.Left, 6), Bottom = Math.Round(crop.Bottom, 6) });
    }

    [Fact]
    public void ToPixels_RoundsAndStaysInsideTheImage()
    {
        var px = new CropRect(0.1, 0.25, 0.9, 0.75).ToPixels(1000, 1600);

        Assert.Equal((100, 400, 800, 800), px);
    }
}
