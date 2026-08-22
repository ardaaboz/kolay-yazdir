using System.Security.Cryptography;
using System.Text;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// Çevrilmiş PDF'leri dosya yolu + değişiklik tarihi + boyut anahtarıyla saklar.
/// Aynı Word dosyası ikinci kez seçildiğinde dönüşüm beklenmez; kırtasiyede aynı
/// belge gün içinde defalarca basılıyor.
/// </summary>
public sealed class ConversionCache(string rootDirectory)
{
    public static ConversionCache Default =>
        new(Path.Combine(Path.GetTempPath(), "KolayYazdir", "donusum"));

    /// <returns>Önbellekteki PDF'in yolu, yoksa null.</returns>
    public string? Lookup(string sourcePath)
    {
        try
        {
            var path = PathFor(sourcePath);
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Üretilen PDF'i önbelleğe taşır ve yeni yolunu döner.</summary>
    public string Store(string sourcePath, string pdfPath)
    {
        var target = PathFor(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        File.Move(pdfPath, target, overwrite: true);

        return target;
    }

    /// <summary>
    /// Anahtar dosyanın içeriğini değil kimliğini yakalar: yol, son değişiklik
    /// zamanı ve boyut. Dosya düzenlenirse anahtar değişir ve önbellek ıskalar.
    /// </summary>
    private string PathFor(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        var key = $"{sourcePath.ToLowerInvariant()}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24];

        return Path.Combine(rootDirectory, $"{hash}.pdf");
    }
}
