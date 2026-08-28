namespace KolayYazdir.Documents.Office;

/// <summary>
/// Dönüştürücüleri sırayla dener.
///
/// Sıra LibreOffice → Office'tir. Office'in biçim sadakati bir tık daha
/// yüksek olabilir, ama Word otomasyonu güvenilmez: eski sürümler görünmez
/// kip pencereleri açıp çağrıyı reddediyor ve dönüşüm hiç olmuyor. Dükkanda
/// yaşanan arıza buydu. LibreOffice başsız kipte her makinede aynı biçimde
/// davranır; belirlenimcilik, kenar durumdaki sadakatten değerli.
/// </summary>
public sealed class OfficeConverterChain(IReadOnlyList<IOfficeConverter> converters) : IOfficeConverter
{
    public static OfficeConverterChain Default =>
        new([new LibreOfficeConverter(), new OfficeComConverter()]);

    /// <summary>Zincirin halkaları, denenme sırasıyla.</summary>
    public IReadOnlyList<IOfficeConverter> Links => converters;

    public string Name => "Office dönüştürme zinciri";

    public bool IsAvailable => converters.Any(c => c.IsAvailable);

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        var failures = new List<string>();

        foreach (var converter in converters.Where(c => c.IsAvailable))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return await converter.ToPdfAsync(sourcePath, targetDirectory, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Kullanıcı vazgeçti; bu bir dönüşüm hatası değil.
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Bir yol tıkandıysa diğerini deniyoruz; sebebi biriktirip
                // hepsi tükenirse kullanıcıya topluca gösteriyoruz. COM katmanı
                // her türden istisna atabildiği için tür ayrımı yapmıyoruz —
                // beklenmedik bir hata da yedek yolu denemeyi engellememeli.
                failures.Add($"{converter.Name}: {Reason(ex)}");
            }
        }

        if (failures.Count == 0)
        {
            throw new OfficeConversionException(
                "Word ve Excel dosyalarını yazdırmak için LibreOffice veya Microsoft Office gerekiyor. " +
                "Bu bilgisayarda ikisi de bulunamadı.");
        }

        throw new OfficeConversionException(
            $"Dosya çevrilemedi: {Path.GetFileName(sourcePath)}. " + string.Join(" · ", failures));
    }

    /// <summary>
    /// Sarmalayıcı istisnalar sebebi gizlemesin. Zaman aşımı ve iç istisna
    /// mesajları da metne katılır: kullanıcı ekranda gördüğü mesajla sorunu
    /// anlatabilmeli, biz de dükkana gitmeden teşhis edebilmeliyiz.
    /// </summary>
    private static string Reason(Exception ex)
    {
        var messages = new List<string>();

        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message.Trim();
            if (message.Length > 0 && !messages.Contains(message)) messages.Add(message);
        }

        return string.Join(" ← ", messages);
    }
}
