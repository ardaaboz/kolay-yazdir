using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

/// <summary>
/// Dükkandaki bilgisayarda LibreOffice kuruluydu ama uygulama onu bulamadı:
/// arama yalnızca tek bir kayıt defteri anahtarına ve iki sabit C:\ yoluna
/// bakıyordu. Bu testler aramanın gerçek kurulum yerlerini kapsadığını
/// doğruluyor; hiçbiri gerçek dosya sistemine dokunmuyor.
/// </summary>
public class LibreOfficeLocatorTests
{
    private static LibreOfficeLocator Locator(
        Dictionary<string, string>? environment = null,
        IEnumerable<string>? existingFiles = null,
        string? registryInstallPath = null)
    {
        var env = environment ?? [];
        var files = new HashSet<string>(existingFiles ?? [], StringComparer.OrdinalIgnoreCase);

        return new LibreOfficeLocator(
            name => env.TryGetValue(name, out var value) ? value : null,
            files.Contains,
            () => registryInstallPath);
    }

    [Fact]
    public void Registry_install_path_is_used_when_present()
    {
        var locator = Locator(
            registryInstallPath: @"E:\Programlar\LibreOffice\program",
            existingFiles: [@"E:\Programlar\LibreOffice\program\soffice.com"]);

        Assert.Equal(@"E:\Programlar\LibreOffice\program\soffice.com", locator.Locate());
    }

    /// <summary>
    /// soffice.com konsol sarmalayıcısı dönüşüm bitene kadar bekler; soffice.exe
    /// kendini ayırıp hemen dönebilir ve PDF daha ortada yokken "bitti" deriz.
    /// İkisi de varsa .com tercih edilmeli.
    /// </summary>
    [Fact]
    public void Console_wrapper_is_preferred_over_the_windowed_executable()
    {
        var locator = Locator(
            registryInstallPath: @"C:\LO\program",
            existingFiles:
            [
                @"C:\LO\program\soffice.exe",
                @"C:\LO\program\soffice.com"
            ]);

        Assert.Equal(@"C:\LO\program\soffice.com", locator.Locate());
    }

    [Fact]
    public void The_windowed_executable_is_accepted_when_the_wrapper_is_missing()
    {
        var locator = Locator(
            registryInstallPath: @"C:\LO\program",
            existingFiles: [@"C:\LO\program\soffice.exe"]);

        Assert.Equal(@"C:\LO\program\soffice.exe", locator.Locate());
    }

    /// <summary>
    /// Program Files her makinede C: sürücüsünde değil. Ortam değişkeni ne
    /// diyorsa oraya bakılmalı.
    /// </summary>
    [Fact]
    public void Program_files_is_read_from_the_environment_not_a_hardcoded_drive()
    {
        var locator = Locator(
            environment: new Dictionary<string, string> { ["ProgramFiles"] = @"E:\Programlar" },
            existingFiles: [@"E:\Programlar\LibreOffice\program\soffice.com"]);

        Assert.Equal(@"E:\Programlar\LibreOffice\program\soffice.com", locator.Locate());
    }

    [Fact]
    public void The_32_bit_program_files_folder_is_searched()
    {
        var locator = Locator(
            environment: new Dictionary<string, string> { ["ProgramFiles(x86)"] = @"C:\Program Files (x86)" },
            existingFiles: [@"C:\Program Files (x86)\LibreOffice\program\soffice.com"]);

        Assert.Equal(@"C:\Program Files (x86)\LibreOffice\program\soffice.com", locator.Locate());
    }

    /// <summary>Yönetici şifresi olmayan kullanıcılar LibreOffice'i kendi profiline kurar.</summary>
    [Fact]
    public void A_per_user_installation_is_found()
    {
        var locator = Locator(
            environment: new Dictionary<string, string> { ["LOCALAPPDATA"] = @"C:\Users\Kirtasiye\AppData\Local" },
            existingFiles: [@"C:\Users\Kirtasiye\AppData\Local\Programs\LibreOffice\program\soffice.com"]);

        Assert.Equal(
            @"C:\Users\Kirtasiye\AppData\Local\Programs\LibreOffice\program\soffice.com",
            locator.Locate());
    }

    [Fact]
    public void Directories_on_the_path_are_searched()
    {
        var locator = Locator(
            environment: new Dictionary<string, string>
            {
                ["PATH"] = @"C:\Windows;D:\Tasinabilir\LibreOffice\program;C:\Windows\System32"
            },
            existingFiles: [@"D:\Tasinabilir\LibreOffice\program\soffice.com"]);

        Assert.Equal(@"D:\Tasinabilir\LibreOffice\program\soffice.com", locator.Locate());
    }

    [Fact]
    public void Blank_path_entries_do_not_break_the_search()
    {
        var locator = Locator(
            environment: new Dictionary<string, string> { ["PATH"] = @"C:\Windows;;;D:\LO\program;" },
            existingFiles: [@"D:\LO\program\soffice.com"]);

        Assert.Equal(@"D:\LO\program\soffice.com", locator.Locate());
    }

    [Fact]
    public void Nothing_is_returned_when_libre_office_is_absent()
    {
        var locator = Locator(
            environment: new Dictionary<string, string> { ["ProgramFiles"] = @"C:\Program Files" });

        Assert.Null(locator.Locate());
    }

    /// <summary>
    /// Kayıt defteri okunamadığında (kilitli makine, bozuk kovan) arama
    /// çökmemeli, dosya sistemine düşmeli.
    /// </summary>
    /// <summary>
    /// LibreOffice'e kendi profil klasörümüzü bir URL olarak veriyoruz.
    /// Kırtasiyedeki kullanıcı adlarında boşluk ve Türkçe harf var; ters bölüyü
    /// elle değiştirmek bu yolları bozuk bir URL'ye çeviriyordu.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Users\Arda Boz\Temp\lo", "file:///C:/Users/Arda%20Boz/Temp/lo")]
    [InlineData(@"C:\Users\Kirtasiye\Temp\lo", "file:///C:/Users/Kirtasiye/Temp/lo")]
    public void The_profile_path_becomes_a_properly_escaped_file_url(string path, string expected)
    {
        Assert.Equal(expected, LibreOfficeConverter.FileUrl(path));
    }

    [Fact]
    public void Turkish_characters_in_the_profile_path_are_encoded()
    {
        var url = LibreOfficeConverter.FileUrl(@"C:\Users\Şükrü Çağlar\lo");

        // Ham hâlleri URL'yi bozardı; kaçışlanmış olmaları yeterli.
        Assert.StartsWith("file:///C:/Users/", url);
        Assert.DoesNotContain(" ", url);
        Assert.EndsWith("/lo", url);
    }

    [Fact]
    public void A_failing_registry_read_falls_back_to_the_file_system()
    {
        var locator = new LibreOfficeLocator(
            name => name == "ProgramFiles" ? @"C:\Program Files" : null,
            path => path == @"C:\Program Files\LibreOffice\program\soffice.com",
            () => throw new UnauthorizedAccessException("kayıt defteri kilitli"));

        Assert.Equal(@"C:\Program Files\LibreOffice\program\soffice.com", locator.Locate());
    }
}
