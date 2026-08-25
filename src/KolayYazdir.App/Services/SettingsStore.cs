using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App.Services;

/// <summary>Uygulama kapanırken saklanan, açılırken geri yüklenen ayarlar.</summary>
public sealed record StoredSettings
{
    public string? DefaultFolder { get; init; }
    public PaperFormat Paper { get; init; } = PaperFormat.A4;
    public Orientation Orientation { get; init; } = Orientation.Portrait;
    public ColorMode Color { get; init; } = ColorMode.Monochrome;
    public DuplexMode Duplex { get; init; } = DuplexMode.Simplex;
    public PagesPerSheet PagesPerSheet { get; init; } = PagesPerSheet.One;
    public bool FitToPage { get; init; }
    public bool AutoRotate { get; init; } = true;
    public int Copies { get; init; } = 1;
    /// <summary>
    /// Kağıt cinsi ham sürücü numarası olarak değil, anlam olarak saklanır:
    /// aynı numara başka bir yazıcıda bambaşka bir kağıdı gösterebilir.
    /// </summary>
    public PaperType PaperType { get; init; } = PaperType.Plain;
}

/// <summary>
/// Ayarları kullanıcının profilinde bir JSON dosyasında tutar. Dosya bozulursa
/// varsayılanlara dönülür — kullanıcıya hata gösterip yolunu kesmenin faydası yok.
/// </summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SettingsStore Default => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KolayYazdir",
        "ayarlar.json"));

    public StoredSettings Load()
    {
        try
        {
            if (!File.Exists(filePath)) return new StoredSettings();

            return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(filePath), Options)
                ?? new StoredSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new StoredSettings();
        }
    }

    public void Save(StoredSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ayar saklanamadıysa uygulama yine çalışır; sessizce geçiyoruz.
        }
    }
}
