using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class OfficeConverterTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("kolayyazdir-office").FullName;
    private readonly List<string> _temporaryFiles = [];

    private string Docx(string text = "Merhaba kırtasiye")
    {
        var path = OfficeFixtures.CreateDocx(text);
        _temporaryFiles.Add(path);
        return path;
    }

    [Fact]
    public void Chain_is_available_when_any_link_is_available()
    {
        var chain = new OfficeConverterChain([
            new StubConverter("yok", available: false),
            new StubConverter("var", available: true)
        ]);

        Assert.True(chain.IsAvailable);
    }

    [Fact]
    public void Chain_is_unavailable_when_every_link_is_missing()
    {
        var chain = new OfficeConverterChain([new StubConverter("yok", available: false)]);

        Assert.False(chain.IsAvailable);
    }

    [Fact]
    public async Task Chain_uses_the_first_available_converter()
    {
        var first = new StubConverter("birinci", available: true);
        var second = new StubConverter("ikinci", available: true);
        var chain = new OfficeConverterChain([first, second]);

        await chain.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(first.WasCalled);
        Assert.False(second.WasCalled);
    }

    [Fact]
    public async Task Chain_falls_through_to_the_next_converter_on_failure()
    {
        var second = new StubConverter("ikinci", available: true);
        var chain = new OfficeConverterChain([
            new StubConverter("birinci", available: true, throws: true),
            second
        ]);

        await chain.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(second.WasCalled);
    }

    [Fact]
    public async Task Chain_skips_unavailable_converters_without_calling_them()
    {
        var unavailable = new StubConverter("yok", available: false);
        var chain = new OfficeConverterChain([unavailable, new StubConverter("var", available: true)]);

        await chain.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.False(unavailable.WasCalled);
    }

    [Fact]
    public async Task Chain_with_no_available_converter_names_libre_office()
    {
        var chain = new OfficeConverterChain([new StubConverter("yok", available: false)]);

        var error = await Assert.ThrowsAsync<OfficeConversionException>(
            () => chain.ToPdfAsync(Docx(), _output, CancellationToken.None));

        // Kullanıcı ne kuracağını bilmeli; mesaj çözümü söylemeli.
        Assert.Contains("LibreOffice", error.Message);
    }

    [Fact]
    public async Task When_every_converter_fails_the_reasons_are_reported()
    {
        var chain = new OfficeConverterChain([
            new StubConverter("birinci", available: true, throws: true),
            new StubConverter("ikinci", available: true, throws: true)
        ]);

        var error = await Assert.ThrowsAsync<OfficeConversionException>(
            () => chain.ToPdfAsync(Docx(), _output, CancellationToken.None));

        Assert.Contains("birinci", error.Message);
        Assert.Contains("ikinci", error.Message);
    }

    [SkippableFact]
    public async Task LibreOffice_converts_a_docx_to_pdf()
    {
        var converter = new LibreOfficeConverter();
        Skip.IfNot(converter.IsAvailable, "Bu makinede LibreOffice kurulu değil.");

        var pdf = await converter.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(File.Exists(pdf));
        using var rasterizer = new PdfRasterizer(pdf);
        Assert.True(rasterizer.PageCount >= 1);
    }

    [SkippableFact]
    public async Task Office_com_converts_a_docx_to_pdf()
    {
        var converter = new OfficeComConverter();
        Skip.IfNot(converter.IsAvailable, "Bu makinede Microsoft Word kurulu değil.");

        var pdf = await converter.ToPdfAsync(Docx(), _output, CancellationToken.None);

        Assert.True(File.Exists(pdf));
        using var rasterizer = new PdfRasterizer(pdf);
        Assert.True(rasterizer.PageCount >= 1);
    }

    // "Bu makinede Office veya LibreOffice kurulu mu" bir ortam koşulu, kod
    // davranışı değil — testte yeri yok. Kurulu değilse kullanıcı Word dosyası
    // eklediğinde satırda LibreOffice'i adıyla söyleyen bir hata görür;
    // Chain_with_no_available_converter_names_libre_office bunu doğruluyor.

    /// <summary>Gerçek bir dosya üreten, çağrıldığını kaydeden sahte dönüştürücü.</summary>
    private sealed class StubConverter(string name, bool available, bool throws = false) : IOfficeConverter
    {
        public bool WasCalled { get; private set; }
        public string Name => name;
        public bool IsAvailable => available;

        public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
        {
            WasCalled = true;
            if (throws) throw new OfficeConversionException($"{name} başarısız");

            Directory.CreateDirectory(targetDirectory);
            var path = Path.Combine(targetDirectory, $"{Guid.NewGuid():N}.pdf");
            File.WriteAllText(path, "%PDF-1.4");
            return Task.FromResult(path);
        }
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
        try { Directory.Delete(_output, recursive: true); } catch (IOException) { }
    }
}
