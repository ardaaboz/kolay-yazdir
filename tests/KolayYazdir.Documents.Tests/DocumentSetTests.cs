using System.Drawing.Imaging;
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class DocumentSetTests : IDisposable
{
    private const double A4WidthPt = 595.276;
    private const double A4HeightPt = 841.890;

    private readonly string _cacheRoot = Directory.CreateTempSubdirectory("kolayyazdir-set").FullName;
    private readonly List<string> _temporaryFiles = [];

    private async Task<SourceDocument> Load(string path)
    {
        _temporaryFiles.Add(path);
        var loader = new DocumentLoader(OfficeConverterChain.Default, new ConversionCache(_cacheRoot));
        return await loader.LoadAsync(path, CancellationToken.None);
    }

    private Task<SourceDocument> TwoPagePdf() =>
        Load(PdfFixtures.Create((A4WidthPt, A4HeightPt), (A4WidthPt, A4HeightPt)));

    private Task<SourceDocument> Image() => Load(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

    [Fact]
    public async Task Pages_of_every_document_are_concatenated_in_order()
    {
        using var set = new DocumentSet([await TwoPagePdf(), await Image()]);

        Assert.Equal(3, set.Pages.Count);
    }

    [Fact]
    public async Task Combined_indexes_run_from_zero_without_gaps()
    {
        using var set = new DocumentSet([await TwoPagePdf(), await Image()]);

        Assert.Equal([0, 1, 2], set.Pages.Select(p => p.Index));
    }

    [Fact]
    public async Task Each_page_carries_the_size_of_its_own_document()
    {
        using var set = new DocumentSet([await Load(PdfFixtures.Create((A4WidthPt, A4HeightPt))), await Image()]);

        Assert.Equal(A4WidthPt, set.Pages[0].Size.Width, 0);
        Assert.Equal(450, set.Pages[1].Size.Width, 0);
    }

    [Fact]
    public async Task Render_reaches_the_right_document()
    {
        using var set = new DocumentSet([await Load(PdfFixtures.Create((A4WidthPt, A4HeightPt))), await Image()]);

        var second = set.Render(1, dpi: 96);

        Assert.Equal(600, second.WidthPx);
        Assert.Equal(400, second.HeightPx);
    }

    [Fact]
    public async Task Render_reaches_the_right_page_inside_a_document()
    {
        // İkinci sayfası farklı boyutta bir PDF: yanlış sayfaya giden bir
        // uygulama ilk sayfanın ölçüsünü döndürürdü.
        using var set = new DocumentSet([await Load(PdfFixtures.Create((A4WidthPt, A4HeightPt), (300, 300)))]);

        var second = set.Render(1, dpi: 72);

        Assert.InRange(second.WidthPx, 298, 302);
    }

    [Fact]
    public async Task File_name_lookup_reports_the_owning_document()
    {
        var pdf = await TwoPagePdf();
        var image = await Image();
        using var set = new DocumentSet([pdf, image]);

        Assert.Equal(pdf.FileName, set.FileNameOf(1));
        Assert.Equal(image.FileName, set.FileNameOf(2));
    }

    [Fact]
    public void An_empty_set_has_no_pages()
    {
        using var set = new DocumentSet([]);

        Assert.Empty(set.Pages);
    }

    [Fact]
    public async Task Out_of_range_index_is_rejected()
    {
        using var set = new DocumentSet([await Load(PdfFixtures.Create((A4WidthPt, A4HeightPt)))]);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Render(5, 96));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Render(-1, 96));
    }

    [Fact]
    public async Task Disposing_the_set_closes_every_document()
    {
        var path = ImageFixtures.Create(20, 20, 96);
        var document = await Load(path);

        new DocumentSet([document]).Dispose();

        // Belge kapandıysa dosya kilidi de bırakılmış olmalı.
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
        try { Directory.Delete(_cacheRoot, recursive: true); } catch (IOException) { }
    }
}
