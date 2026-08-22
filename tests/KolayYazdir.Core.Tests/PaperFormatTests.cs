using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class PaperFormatTests
{
    [Theory]
    [InlineData(PaperFormat.A4, 595.276, 841.890)]
    [InlineData(PaperFormat.A5, 419.528, 595.276)]
    [InlineData(PaperFormat.A3, 841.890, 1190.551)]
    public void SizeOf_portrait_returns_iso_dimensions_in_points(
        PaperFormat format, double expectedWidth, double expectedHeight)
    {
        var size = Paper.SizeOf(format, Orientation.Portrait);

        Assert.Equal(expectedWidth, size.Width, 3);
        Assert.Equal(expectedHeight, size.Height, 3);
    }

    [Fact]
    public void SizeOf_landscape_swaps_width_and_height()
    {
        var portrait = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
        var landscape = Paper.SizeOf(PaperFormat.A4, Orientation.Landscape);

        Assert.Equal(portrait.Height, landscape.Width, 3);
        Assert.Equal(portrait.Width, landscape.Height, 3);
    }

    [Fact]
    public void MmToPt_converts_one_inch_correctly()
    {
        Assert.Equal(72.0, Paper.MmToPt(25.4), 6);
    }
}
