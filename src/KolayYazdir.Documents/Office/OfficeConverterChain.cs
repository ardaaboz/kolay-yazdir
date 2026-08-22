namespace KolayYazdir.Documents.Office;

/// <summary>
/// Dönüştürücüleri sırayla dener. Varsayılan sıra Office → LibreOffice'tir:
/// Office kuruluysa biçim sadakati daha yüksektir, ama LibreOffice her makinede
/// bulunduğu için güvenilir yedektir.
/// </summary>
public sealed class OfficeConverterChain(IReadOnlyList<IOfficeConverter> converters) : IOfficeConverter
{
    public static OfficeConverterChain Default =>
        new([new OfficeComConverter(), new LibreOfficeConverter()]);

    public string Name => "Office dönüştürme zinciri";

    public bool IsAvailable => converters.Any(c => c.IsAvailable);

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        var failures = new List<string>();

        foreach (var converter in converters.Where(c => c.IsAvailable))
        {
            try
            {
                return await converter.ToPdfAsync(sourcePath, targetDirectory, ct);
            }
            catch (OfficeConversionException ex)
            {
                // Bir yol tıkandıysa diğerini deniyoruz; sebebi biriktirip
                // hepsi tükenirse kullanıcıya topluca gösteriyoruz.
                failures.Add($"{converter.Name}: {ex.Message}");
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
}
