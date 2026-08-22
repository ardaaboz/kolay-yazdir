using System.Diagnostics;
using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// LibreOffice'i başsız kipte çalıştırır. Dükkandaki her makinede kurulu olduğu
/// için garantili yedek yoldur.
/// </summary>
public sealed class LibreOfficeConverter : IOfficeConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly string? _executable = Locate();

    public string Name => "LibreOffice";

    public bool IsAvailable => _executable is not null;

    public async Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        if (_executable is null)
            throw new OfficeConversionException("LibreOffice bu bilgisayarda bulunamadı.");

        Directory.CreateDirectory(targetDirectory);

        // Kendi kullanıcı profilimizi veriyoruz: kullanıcının açık LibreOffice
        // penceresi varsa başsız süreç ona takılıp bize hiç cevap vermez.
        var profile = Path.Combine(Path.GetTempPath(), "KolayYazdir", "lo-profile");
        Directory.CreateDirectory(profile);

        var startInfo = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add($"-env:UserInstallation=file:///{profile.Replace('\\', '/')}");
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(targetDirectory);
        startInfo.ArgumentList.Add(sourcePath);

        using var process = Process.Start(startInfo)
            ?? throw new OfficeConversionException("LibreOffice başlatılamadı.");

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
        if (!File.Exists(expected))
        {
            var error = await process.StandardError.ReadToEndAsync(CancellationToken.None);
            throw new OfficeConversionException(
                $"LibreOffice dosyayı çeviremedi: {Path.GetFileName(sourcePath)}. {error}".Trim());
        }

        return expected;
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { }
    }

    /// <summary>Kayıt defterinden, sonra bilinen kurulum yollarından arar.</summary>
    private static string? Locate()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(@"SOFTWARE\LibreOffice\UNO\InstallPath");

                if (key?.GetValue(null) is string installPath)
                {
                    var candidate = Path.Combine(installPath, "soffice.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Kayıt defteri okunamadıysa dosya sistemine düşüyoruz.
            }
        }

        string[] fallbacks =
        [
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        ];

        return fallbacks.FirstOrDefault(File.Exists);
    }
}
