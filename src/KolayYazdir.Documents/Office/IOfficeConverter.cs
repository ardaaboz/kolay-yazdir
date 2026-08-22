namespace KolayYazdir.Documents.Office;

/// <summary>Word/Excel dosyasını PDF'e çeviren bir yol.</summary>
public interface IOfficeConverter
{
    /// <summary>Hata mesajlarında görünecek insan okunur ad.</summary>
    string Name { get; }

    /// <summary>Bu makinede kullanılabilir mi.</summary>
    bool IsAvailable { get; }

    /// <returns>Üretilen PDF'in tam yolu.</returns>
    Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct);
}

public sealed class OfficeConversionException(string message, Exception? inner = null)
    : Exception(message, inner);
