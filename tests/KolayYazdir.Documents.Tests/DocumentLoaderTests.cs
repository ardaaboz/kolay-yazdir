using System.Drawing.Imaging;
using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class DocumentLoaderTests : IDisposable
{
    private const double A4WidthPt = 595.276;
    private const double A4HeightPt = 841.890;

    private readonly string _cacheRoot = Directory.CreateTempSubdirectory("kolayyazdir-loader").FullName;
    private readonly List<string> _temporaryFiles = [];

    private DocumentLoader Loader(IOfficeConverter? converter = null) =>
        new(converter ?? OfficeConverterChain.Default, new ConversionCache(_cacheRoot));

    private string Track(string path)
    {
        _temporaryFiles.Add(path);
        return path;
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.PDF")]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.png")]
    [InlineData("a.bmp")]
    [InlineData("a.gif")]
    [InlineData("a.tif")]
    [InlineData("a.tiff")]
    [InlineData("a.webp")]
    [InlineData("a.docx")]
    [InlineData("a.doc")]
    [InlineData("a.xlsx")]
    [InlineData("a.xls")]
    public void Supported_extensions_are_recognised(string name)
    {
        Assert.True(DocumentLoader.IsSupported(name));
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.exe")]
    [InlineData("a.zip")]
    [InlineData("a")]
    public void Other_extensions_are_rejected(string name)
    {
        Assert.False(DocumentLoader.IsSupported(name));
    }

    [Fact]
    public async Task A_pdf_loads_with_its_real_page_count()
    {
        var path = Track(PdfFixtures.Create((A4WidthPt, A4HeightPt), (A4WidthPt, A4HeightPt)));

        using var document = await Loader().LoadAsync(path, CancellationToken.None);

        Assert.Equal(2, document.PageCount);
        Assert.Equal(Path.GetFileName(path), document.FileName);
    }

    [Fact]
    public async Task An_image_loads_as_a_single_page_at_its_real_size()
    {
        var path = Track(ImageFixtures.Create(600, 400, 96, ImageFormat.Png));

        using var document = await Loader().LoadAsync(path, CancellationToken.None);

        Assert.Equal(1, document.PageCount);
        Assert.Equal(450, document.PageSize(0).Width, 0);
    }

    [Fact]
    public async Task An_office_file_is_converted_before_loading()
    {
        var docx = Track(OfficeFixtures.CreateDocx("Merhaba"));
        var converter = new RecordingConverter();

        using var document = await Loader(converter).LoadAsync(docx, CancellationToken.None);

        Assert.True(converter.WasCalled);
        Assert.True(document.PageCount >= 1);
    }

    [Fact]
    public async Task A_repeated_office_file_hits_the_cache()
    {
        var docx = Track(OfficeFixtures.CreateDocx("Merhaba"));
        var converter = new RecordingConverter();
        var loader = Loader(converter);

        (await loader.LoadAsync(docx, CancellationToken.None)).Dispose();
        converter.Reset();
        (await loader.LoadAsync(docx, CancellationToken.None)).Dispose();

        Assert.False(converter.WasCalled);
    }

    [Fact]
    public async Task A_failed_conversion_surfaces_as_a_document_load_error()
    {
        var docx = Track(OfficeFixtures.CreateDocx("Merhaba"));
        var converter = new FailingConverter();

        var error = await Assert.ThrowsAsync<DocumentLoadException>(
            () => Loader(converter).LoadAsync(docx, CancellationToken.None));

        Assert.Contains("çevrilemedi", error.Message);
    }

    [Fact]
    public async Task An_unsupported_extension_is_rejected_with_a_clear_message()
    {
        var path = Track(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt"));
        await File.WriteAllTextAsync(path, "merhaba");

        var error = await Assert.ThrowsAsync<DocumentLoadException>(
            () => Loader().LoadAsync(path, CancellationToken.None));

        Assert.Contains(".txt", error.Message);
    }

    [Fact]
    public async Task A_missing_file_is_reported_by_name()
    {
        var path = Path.Combine(Path.GetTempPath(), $"yok-{Guid.NewGuid():N}.pdf");

        var error = await Assert.ThrowsAsync<DocumentLoadException>(
            () => Loader().LoadAsync(path, CancellationToken.None));

        Assert.Contains(Path.GetFileName(path), error.Message);
    }

    [Fact]
    public void The_file_dialog_filter_covers_every_supported_type()
    {
        var filter = DocumentLoader.FileDialogFilter;

        Assert.Contains("*.pdf", filter);
        Assert.Contains("*.jpg", filter);
        Assert.Contains("*.docx", filter);
        Assert.Contains("*.xlsx", filter);
    }

    /// <summary>Gerçek bir PDF üreten, çağrıldığını kaydeden sahte dönüştürücü.</summary>
    private sealed class RecordingConverter : IOfficeConverter
    {
        public bool WasCalled { get; private set; }
        public void Reset() => WasCalled = false;

        public string Name => "sahte";
        public bool IsAvailable => true;

        public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
        {
            WasCalled = true;
            Directory.CreateDirectory(targetDirectory);

            var produced = PdfFixtures.Create((A4WidthPt, A4HeightPt));
            var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
            File.Move(produced, target, overwrite: true);

            return Task.FromResult(target);
        }
    }

    private sealed class FailingConverter : IOfficeConverter
    {
        public string Name => "bozuk";
        public bool IsAvailable => true;

        public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct) =>
            throw new OfficeConversionException("Dosya çevrilemedi: sahte hata");
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
