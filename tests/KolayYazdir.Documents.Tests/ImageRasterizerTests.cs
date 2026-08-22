using System.Drawing.Imaging;

namespace KolayYazdir.Documents.Tests;

public class ImageRasterizerTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string Fixture(int widthPx, int heightPx, float dpi, ImageFormat? format = null)
    {
        var path = ImageFixtures.Create(widthPx, heightPx, dpi, format);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void An_image_is_a_single_page_document()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        Assert.Equal(1, rasterizer.PageCount);
    }

    [Fact]
    public void Ninety_six_dpi_maps_pixels_to_three_quarters_of_a_point()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        var size = rasterizer.PageSize(0);

        // PNG çözünürlüğü metre başına piksel olarak saklar, bu yüzden 96 DPI
        // tam olarak geri gelmez (95.9866). Punto cinsinden sapma yarım puntonun
        // altında kalıyor; kağıtta karşılığı yok.
        Assert.Equal(450, size.Width, 0);   // 600 / 96 * 72
        Assert.Equal(300, size.Height, 0);  // 400 / 96 * 72
    }

    [Fact]
    public void A_three_hundred_dpi_photo_reports_its_real_physical_size()
    {
        // 10x15 cm vesikalık ölçüsüne yakın: 4x6 inç.
        using var rasterizer = new ImageRasterizer(Fixture(1200, 1800, 300, ImageFormat.Jpeg));

        var size = rasterizer.PageSize(0);

        Assert.Equal(288, size.Width, 1);   // 1200 / 300 * 72 = 4 inç
        Assert.Equal(432, size.Height, 1);  // 1800 / 300 * 72 = 6 inç
    }

    [Fact]
    public void Resolution_metadata_changes_the_physical_size_not_the_pixels()
    {
        // Aynı piksel boyutu, farklı DPI: gerçek boyut değişmeli.
        using var low = new ImageRasterizer(Fixture(600, 600, 96));
        using var high = new ImageRasterizer(Fixture(600, 600, 300));

        Assert.True(low.PageSize(0).Width > high.PageSize(0).Width,
            "96 DPI görsel, 300 DPI görselden fiziksel olarak daha büyük basılmalı");
    }

    [Fact]
    public void Render_returns_the_original_pixels_regardless_of_dpi()
    {
        using var rasterizer = new ImageRasterizer(Fixture(600, 400, 96));

        var raster = rasterizer.Render(0, dpi: 300);

        Assert.Equal(600, raster.WidthPx);
        Assert.Equal(400, raster.HeightPx);
        Assert.Equal(600 * 400 * 4, raster.Bgra.Length);
    }

    [Fact]
    public void Rendered_pixels_carry_the_source_colour()
    {
        using var rasterizer = new ImageRasterizer(Fixture(10, 10, 96));

        var raster = rasterizer.Render(0, dpi: 96);

        // BGRA düzeninde kırmızı: B=0, G=0, R=255, A=255
        Assert.Equal(0, raster.Bgra[0]);
        Assert.Equal(0, raster.Bgra[1]);
        Assert.Equal(255, raster.Bgra[2]);
        Assert.Equal(255, raster.Bgra[3]);
    }

    [Fact]
    public void Every_row_is_copied_not_just_the_first()
    {
        // Satır dolgusu yanlış hesaplanırsa alt satırlar boş kalır.
        using var rasterizer = new ImageRasterizer(Fixture(7, 5, 96));

        var raster = rasterizer.Render(0, dpi: 96);
        var lastPixel = raster.Bgra.Length - 4;

        Assert.Equal(0, raster.Bgra[lastPixel]);
        Assert.Equal(0, raster.Bgra[lastPixel + 1]);
        Assert.Equal(255, raster.Bgra[lastPixel + 2]);
    }

    [Fact]
    public void Unreadable_file_throws_a_document_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bozuk-{Guid.NewGuid():N}.png");
        File.WriteAllText(path, "bu bir görsel değil");
        _temporaryFiles.Add(path);

        var error = Assert.Throws<DocumentLoadException>(() => new ImageRasterizer(path));

        Assert.Contains(Path.GetFileName(path), error.Message);
    }

    [Fact]
    public void The_source_file_is_not_kept_locked()
    {
        var path = Fixture(20, 20, 96);

        using (var rasterizer = new ImageRasterizer(path))
        {
            _ = rasterizer.Render(0, 96);
        }

        // Kullanıcı dosyayı silmek isterse uygulama engel olmamalı.
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
