using System.Diagnostics;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// LibreOffice'i başsız kipte çalıştırır. Word'ün aksine belirlenimcidir: kip
/// penceresi açmaz, sürüm farkı gözetmez, hep aynı biçimde davranır. Bu yüzden
/// zincirin ilk halkasıdır.
/// </summary>
public sealed class LibreOfficeConverter(LibreOfficeLocator? locator = null) : IOfficeConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly string? _executable = (locator ?? LibreOfficeLocator.Default).Locate();

    public string Name => "LibreOffice";

    public bool IsAvailable => _executable is not null;

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        if (_executable is null)
            throw new OfficeConversionException("LibreOffice bu bilgisayarda bulunamadı.");

        Directory.CreateDirectory(targetDirectory);

        var startInfo = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add($"-env:UserInstallation={FileUrl(ProfileDirectory())}");
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--nolockcheck");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(targetDirectory);
        startInfo.ArgumentList.Add(sourcePath);

        using var process = Process.Start(startInfo)
            ?? throw new OfficeConversionException("LibreOffice başlatılamadı.");

        // Akışlar süreç koşarken boşaltılmalı. Beklemeden önce okumazsak boru
        // arabelleği dolduğunda LibreOffice yazmaya çalışırken kilitlenir ve
        // dönüşüm üç dakikalık süre sınırına kadar sessizce asılı kalır.
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new OfficeConversionException("LibreOffice dönüşümü zaman aşımına uğradı.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var expected = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
        if (File.Exists(expected)) return expected;

        throw new OfficeConversionException(Explain(sourcePath, process.ExitCode,
            await standardOutput, await standardError));
    }

    /// <summary>
    /// Kendi kullanıcı profilimizi veriyoruz: kullanıcının açık LibreOffice
    /// penceresi varsa başsız süreç ona takılıp bize hiç cevap vermez.
    ///
    /// Yol bir dosya URL'sine çevrilirken kaçışlama şart — kırtasiyedeki
    /// kullanıcı adlarında boşluk ve Türkçe harf var, ham hâlleri URL'yi bozar.
    /// </summary>
    private static string ProfileDirectory()
    {
        var profile = Path.Combine(Path.GetTempPath(), "KolayYazdir", "lo-profile");
        Directory.CreateDirectory(profile);

        return profile;
    }

    /// <summary>
    /// Yerel yolu dosya URL'sine çevirir, kaçışlamayı doğru yaparak.
    ///
    /// Eskiden ters bölü işaretleri elle değiştiriliyordu; kullanıcı adında
    /// boşluk ya da Türkçe harf olan her makinede ("C:/Users/Arda Boz/...")
    /// URL bozuluyor, LibreOffice profili kuramıyordu.
    /// </summary>
    internal static string FileUrl(string path) => new Uri(path).AbsoluteUri;

    /// <summary>
    /// Hata mesajı sebebi söylemeli. LibreOffice başarısızlığı çoğu zaman
    /// standart çıktıya yazar, çıkış kodu yine de sıfır olur.
    /// </summary>
    private static string Explain(string sourcePath, int exitCode, string standardOutput, string standardError)
    {
        var detail = string.Join(" ",
            new[] { standardError, standardOutput }
                .Select(text => text.Trim())
                .Where(text => text.Length > 0));

        var message = $"LibreOffice dosyayı çeviremedi: {Path.GetFileName(sourcePath)}.";

        if (detail.Length > 0) return $"{message} {detail}";

        return $"{message} LibreOffice {exitCode} koduyla çıktı, sebep bildirmedi.";
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { }
    }
}
