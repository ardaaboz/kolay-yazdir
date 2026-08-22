namespace KolayYazdir.Documents.Tests;

public class PdfRasterizerTests : IDisposable
{
    private const double A4WidthPt = 595.276;
    private const double A4HeightPt = 841.890;
    private const double A5WidthPt = 419.528;

    private readonly List<string> _temporaryFiles = [];

    private string Fixture(params (double, double)[] pages)
    {
        var path = PdfFixtures.Create(pages);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void Page_count_matches_the_document()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt), (A4WidthPt, A4HeightPt)));

        Assert.Equal(2, rasterizer.PageCount);
    }

    [Fact]
    public void Page_size_is_reported_in_points()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var size = rasterizer.PageSize(0);

        Assert.Equal(A4WidthPt, size.Width, 0);
        Assert.Equal(A4HeightPt, size.Height, 0);
    }

    [Fact]
    public void Pages_of_different_sizes_are_reported_separately()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt), (A5WidthPt, A4WidthPt)));

        Assert.Equal(A4WidthPt, rasterizer.PageSize(0).Width, 0);
        Assert.Equal(A5WidthPt, rasterizer.PageSize(1).Width, 0);
    }

    [Fact]
    public void Render_produces_pixels_matching_the_requested_dpi()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var raster = rasterizer.Render(0, dpi: 150);

        // 595.276 pt / 72 * 150 = 1240 px; 841.890 pt / 72 * 150 = 1754 px
        Assert.InRange(raster.WidthPx, 1238, 1242);
        Assert.InRange(raster.HeightPx, 1752, 1756);
    }

    [Fact]
    public void Doubling_the_dpi_doubles_the_pixels()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var low = rasterizer.Render(0, dpi: 72);
        var high = rasterizer.Render(0, dpi: 144);

        Assert.InRange(high.WidthPx, low.WidthPx * 2 - 2, low.WidthPx * 2 + 2);
    }

    [Fact]
    public void Render_returns_four_bytes_per_pixel()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var raster = rasterizer.Render(0, dpi: 36);

        Assert.Equal(raster.WidthPx * raster.HeightPx * 4, raster.Bgra.Length);
    }

    [Fact]
    public void Render_draws_actual_content()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var raster = rasterizer.Render(0, dpi: 72);

        // Fixture her sayfaya siyah bir dikdörtgen çizer; en az bir koyu piksel olmalı.
        Assert.True(HasDarkPixel(raster), "render edilen sayfa tamamen boş çıktı");
    }

    [Fact]
    public void Render_reaches_the_requested_page()
    {
        // İlk sayfa boş, ikincisi dolu: yanlış sayfayı render eden bir uygulama
        // birinci sayfada koyu piksel bulurdu.
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt), (A5WidthPt, A4WidthPt)));

        var second = rasterizer.Render(1, dpi: 72);

        Assert.InRange(second.WidthPx, 418, 422);
        Assert.True(HasDarkPixel(second));
    }

    [Fact]
    public void The_page_background_is_white_not_transparent()
    {
        using var rasterizer = new PdfRasterizer(Fixture((A4WidthPt, A4HeightPt)));

        var raster = rasterizer.Render(0, dpi: 36);

        // Sol üst köşe kenar boşluğunda kalır ve opak beyaz olmalı; saydam bir
        // sayfa kağıda basılırken beklenmedik sonuç verir. PDFium bugün zaten
        // beyaz zemin üretiyor, yani bu test bizim BackgroundColor ayarımızı
        // ayırt etmiyor — kütüphane varsayılanı değişirse yakalar.
        Assert.Equal(255, raster.Bgra[0]);
        Assert.Equal(255, raster.Bgra[1]);
        Assert.Equal(255, raster.Bgra[2]);
        Assert.Equal(255, raster.Bgra[3]);
    }

    [Fact]
    public void Unreadable_file_throws_a_document_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bozuk-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "bu bir pdf değil");
        _temporaryFiles.Add(path);

        var error = Assert.Throws<DocumentLoadException>(() => new PdfRasterizer(path));

        Assert.Contains(Path.GetFileName(path), error.Message);
    }

    [Fact]
    public void A_missing_file_throws_a_document_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"yok-{Guid.NewGuid():N}.pdf");

        Assert.Throws<DocumentLoadException>(() => new PdfRasterizer(path));
    }

    private static bool HasDarkPixel(RasterPage raster)
    {
        for (var i = 0; i < raster.Bgra.Length; i += 4)
        {
            if (raster.Bgra[i] < 64 && raster.Bgra[i + 1] < 64 && raster.Bgra[i + 2] < 64) return true;
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
