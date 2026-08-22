using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents;

/// <summary>
/// Dosya yolunu açılmış bir belgeye çevirir. Uzantı eşlemesi ve Office dönüşümü
/// burada saklanır; uygulamanın geri kalanı dosya türü diye bir şey bilmez.
/// </summary>
public sealed class DocumentLoader(IOfficeConverter converter, ConversionCache cache)
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    private static readonly string[] OfficeExtensions =
        [".doc", ".docx", ".docm", ".rtf", ".odt", ".xls", ".xlsx", ".xlsm", ".ods", ".csv"];

    public static DocumentLoader Default => new(OfficeConverterChain.Default, ConversionCache.Default);

    public static bool IsSupported(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".pdf" || ImageExtensions.Contains(extension) || OfficeExtensions.Contains(extension);
    }

    /// <summary>Dosya seçme penceresinin süzgeç metni.</summary>
    public static string FileDialogFilter =>
        "Yazdırılabilir dosyalar|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;" +
        "*.doc;*.docx;*.docm;*.rtf;*.odt;*.xls;*.xlsx;*.xlsm;*.ods;*.csv" +
        "|PDF|*.pdf" +
        "|Görseller|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp" +
        "|Word ve Excel|*.doc;*.docx;*.docm;*.rtf;*.odt;*.xls;*.xlsx;*.xlsm;*.ods;*.csv" +
        "|Tüm dosyalar|*.*";

    public async Task<SourceDocument> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new DocumentLoadException($"Dosya bulunamadı: {Path.GetFileName(path)}");

        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (extension == ".pdf")
            return new SourceDocument(path, new PdfRasterizer(path));

        if (ImageExtensions.Contains(extension))
            return new SourceDocument(path, new ImageRasterizer(path));

        if (OfficeExtensions.Contains(extension))
            return new SourceDocument(path, new PdfRasterizer(await ConvertAsync(path, ct)));

        throw new DocumentLoadException($"Bu dosya türü yazdırılamıyor: {extension}");
    }

    /// <summary>Office dosyasını PDF'e çevirir; daha önce çevrildiyse onu kullanır.</summary>
    private async Task<string> ConvertAsync(string path, CancellationToken ct)
    {
        if (cache.Lookup(path) is { } cached) return cached;

        var workspace = Path.Combine(Path.GetTempPath(), "KolayYazdir", "calisma", Guid.NewGuid().ToString("N"));
        try
        {
            var produced = await converter.ToPdfAsync(path, workspace, ct);
            return cache.Store(path, produced);
        }
        catch (OfficeConversionException ex)
        {
            // Üst katman tek bir hata türü görsün: dosya açılamadı.
            throw new DocumentLoadException(ex.Message, ex);
        }
        finally
        {
            try { Directory.Delete(workspace, recursive: true); }
            catch (Exception ex) when (ex is IOException or DirectoryNotFoundException or UnauthorizedAccessException) { }
        }
    }
}
