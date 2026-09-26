using PiRadar.Core.Images;

namespace PiRadar.Core.Tests.Images;

public class ImageSizeTests
{
    [Theory]
    [InlineData(3024, 4032, 1200, 1600)]
    [InlineData(4032, 3024, 1600, 1200)]
    [InlineData(1000, 1500, 1000, 1500)]
    [InlineData(1600, 900, 1600, 900)]
    [InlineData(10000, 3, 1600, 1)]
    public void FitWithin_KeepsAspectAndNeverUpscales(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ImageSize.FitWithin(width, height, ImageSize.ReceiptMaxSide));
    }
}
