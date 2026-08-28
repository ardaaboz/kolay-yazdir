using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// LibreOffice'in çalıştırılabilir dosyasını arar.
///
/// Arama bilerek geniş: dükkandaki bir bilgisayarda LibreOffice kuruluydu ama
/// uygulama onu bulamadığı için Word'e mahkum kaldı ve dönüşüm hiç olmadı.
/// Eskiden yalnızca tek bir kayıt defteri anahtarına ve iki sabit "C:\Program
/// Files..." yoluna bakılıyordu; artık ortam değişkenleri, kullanıcı bazlı
/// kurulumlar ve PATH de taranıyor.
///
/// Dosya sistemine ve kayıt defterine erişim dışarıdan verilir, böylece arama
/// sırası gerçek bir kurulum gerekmeden test edilebilir.
/// </summary>
public sealed class LibreOfficeLocator(
    Func<string, string?> environmentVariable,
    Func<string, bool> fileExists,
    Func<string?> registryInstallPath)
{
    /// <summary>
    /// soffice.com konsol sarmalayıcısıdır ve dönüşüm bitene kadar bekler.
    /// soffice.exe pencereli sürümdür; Windows'ta kendini ayırıp dönüşüm hâlâ
    /// sürerken çıkabilir, biz de PDF'i yokken ararız. Bu yüzden .com öncelikli.
    /// </summary>
    private static readonly string[] Executables = ["soffice.com", "soffice.exe"];

    public static LibreOfficeLocator Default =>
        new(Environment.GetEnvironmentVariable, File.Exists, ReadInstallPathFromRegistry);

    public string? Locate()
    {
        foreach (var directory in CandidateDirectories())
        {
            foreach (var executable in Executables)
            {
                var candidate = Path.Combine(directory, executable);
                if (fileExists(candidate)) return candidate;
            }
        }

        return null;
    }

    /// <summary>Bakılacak klasörler, olasılık sırasına göre.</summary>
    private IEnumerable<string> CandidateDirectories()
    {
        // Kurulumun kendi bildirdiği yer; özel bir klasöre kurulmuşsa tek doğru kaynak.
        var registered = TryReadRegistry();
        if (!string.IsNullOrWhiteSpace(registered)) yield return registered;

        // Program Files her makinede C: sürücüsünde değil; sürücü harfini
        // varsaymak yerine ortam değişkenini soruyoruz.
        foreach (var variable in (string[])["ProgramFiles", "ProgramFiles(x86)", "ProgramW6432"])
        {
            var root = environmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root))
                yield return Path.Combine(root, "LibreOffice", "program");
        }

        // Yönetici şifresi olmayan kullanıcı LibreOffice'i kendi profiline kurar.
        var localAppData = environmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "Programs", "LibreOffice", "program");

        // Taşınabilir kurulumlar genelde yalnızca PATH üzerinden bulunur.
        foreach (var entry in (environmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(entry)) yield return entry.Trim();
        }
    }

    private string? TryReadRegistry()
    {
        try
        {
            return registryInstallPath();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Kilitli veya bozuk bir kovan aramayı bitirmemeli; dosya sistemine düşüyoruz.
            return null;
        }
    }

    private static string? ReadInstallPathFromRegistry()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(@"SOFTWARE\LibreOffice\UNO\InstallPath");

            if (key?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path)) return path;
        }

        return null;
    }
}
