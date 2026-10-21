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

    [Theory]
    [InlineData(210, 297)] // A4
    [InlineData(297, 210)] // yatay A4
    [InlineData(148, 210)] // A5
    [InlineData(297, 420)] // A3
    [InlineData(216, 279)] // Letter
    public void A_standard_document_size_is_recognised(double widthMm, double heightMm)
    {
        var size = new SizePt(Paper.MmToPt(widthMm), Paper.MmToPt(heightMm));

        Assert.True(Paper.IsStandardDocumentSize(size));
    }

    [Fact]
    public void A_rounded_a4_is_still_recognised()
    {
        // PDF üreticileri A4'ü 595x842 punto diye yuvarlar.
        Assert.True(Paper.IsStandardDocumentSize(new SizePt(595, 842)));
    }

    [Theory]
    [InlineData(35, 45)]   // vesikalık
    [InlineData(100, 150)] // fotoğraf baskısı
    [InlineData(80, 80)]   // kare etiket
    public void Content_with_its_own_measurements_is_not_a_document_size(double widthMm, double heightMm)
    {
        var size = new SizePt(Paper.MmToPt(widthMm), Paper.MmToPt(heightMm));

        Assert.False(Paper.IsStandardDocumentSize(size));
    }
}
