using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// Kurulu Microsoft Office'i geç bağlama (late binding) ile kullanır. Geç
/// bağlama sayesinde Office sürümüne özel bir birlikte çalışma derlemesine
/// bağımlı olmayız; dükkandaki makinelerde sürümler farklı.
///
/// Zincirin son halkasıdır. Word otomasyonu güvenilmezdir: görünmez kip
/// pencereleri açar, meşgulken çağrıları reddeder, sürümden sürüme farklı
/// kaydetme yöntemleri sunar. Buradaki savunmalar bunların her birine karşılık
/// gelir.
/// </summary>
public sealed class OfficeComConverter : IOfficeConverter
{
    private const int WdFormatPdf = 17;
    private const int XlTypePdf = 0;

    /// <summary>Word açılışı eski makinelerde yavaştır; ama sonsuza kadar beklenmez.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public string Name => "Microsoft Office";

    public bool IsAvailable => IsRegistered("Word.Application") || IsRegistered("Excel.Application");

    public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");

        // COM otomasyonu tek iş parçacıklı apartman gerektirir ve engelleyicidir;
        // arayüzü kilitlememek ve asılı kalırsa kurtulabilmek için StaTask'ta.
        return StaTask.RunAsync(() =>
        {
            if (IsSpreadsheet(sourcePath)) ConvertWithExcel(sourcePath, target);
            else ConvertWithWord(sourcePath, target);

            return target;
        }, Timeout, ct);
    }

    private static bool IsSpreadsheet(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".xls" or ".xlsx" or ".xlsm" or ".ods" or ".csv";

    private static void ConvertWithWord(string sourcePath, string target)
    {
        dynamic? application = null;
        dynamic? document = null;
        try
        {
            application = CreateInstance("Word.Application");

            Quieten(application);

            document = application.Documents.Open(
                sourcePath,
                ConfirmConversions: false,
                ReadOnly: true,
                AddToRecentFiles: false,
                Revert: false,
                Visible: false);

            SaveWordAsPdf(document, target);
        }
        catch (Exception ex)
        {
            throw Describe(sourcePath, ex);
        }
        finally
        {
            Release((object?)document, d => d.Close(0));
            Release((object?)application, a => a.Quit(0));
        }
    }

    /// <summary>
    /// Word sürümleri PDF'e farklı yollardan kaydeder. SaveAs2 Word 2010 ile
    /// geldi; daha eskisinde yoktur ve geç bağlamada üye bulunamadı hatası
    /// verir. Sırayla denenir, ilki tutan kazanır.
    /// </summary>
    private static void SaveWordAsPdf(dynamic document, string target)
    {
        var attempts = new List<(string Name, Action Save)>
        {
            ("SaveAs2", () => document.SaveAs2(target, WdFormatPdf)),
            ("ExportAsFixedFormat", () => document.ExportAsFixedFormat(target, WdFormatPdf)),
            ("SaveAs", () => document.SaveAs(target, WdFormatPdf))
        };

        var failures = new List<string>();

        foreach (var (name, save) in attempts)
        {
            try
            {
                save();
                return;
            }
            catch (Exception ex) when (ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or COMException)
            {
                failures.Add($"{name}: {ex.Message}");
            }
        }

        throw new OfficeConversionException(
            "Word bu sürümde PDF'e kaydedemedi. " + string.Join(" · ", failures));
    }

    private static void ConvertWithExcel(string sourcePath, string target)
    {
        dynamic? application = null;
        dynamic? workbook = null;
        try
        {
            application = CreateInstance("Excel.Application");

            Quieten(application);

            workbook = application.Workbooks.Open(sourcePath, ReadOnly: true, AddToMru: false);
            workbook.ExportAsFixedFormat(XlTypePdf, target);
        }
        catch (Exception ex)
        {
            throw Describe(sourcePath, ex);
        }
        finally
        {
            Release((object?)workbook, w => w.Close(false));
            Release((object?)application, a => a.Quit());
        }
    }

    /// <summary>
    /// Otomasyonu bölebilecek pencereleri kapatır.
    ///
    /// DisplayAlerts yalnızca belge uyarılarını susturur; "Word varsayılan
    /// uygulama değil" penceresini kapatmaz. Onu engelleyen ayar yok, ama
    /// makro sorgusunu, bağlantı güncellemesini ve dosya doğrulama uyarısını
    /// kapatmak açılışta çıkan pencere sayısını belirgin biçimde azaltır.
    ///
    /// Her biri ayrı ayrı denenir: eski sürümlerde bazı özellikler yok ve tek
    /// bir eksik üye tüm dönüşümü düşürmemeli.
    /// </summary>
    private static void Quieten(dynamic application)
    {
        Try(() => application.Visible = false);
        Try(() => application.DisplayAlerts = 0);
        Try(() => application.ScreenUpdating = false);

        // msoAutomationSecurityForceDisable: makro sorma, çalıştırma.
        Try(() => application.AutomationSecurity = 3);

        // msoFileValidationSkip: eski veya ağdan gelen dosyalarda korumalı
        // görünüm sorgusunu atlar. Dükkandaki dosyalar USB ve e-postadan geliyor.
        Try(() => application.FileValidation = 0);

        Try(() => application.Options.UpdateLinksAtOpen = false);
        Try(() => application.Options.ConfirmConversions = false);
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or COMException) { }
    }

    /// <summary>
    /// Nesneyi kapatıp bırakır. Temizlik hatası dönüşümün kendi hatasını
    /// gölgelememeli: PDF üretildiyse Word'ün kapanışta takılması işi bozmaz.
    /// </summary>
    private static void Release(object? instance, Action<dynamic> close)
    {
        if (instance is null) return;

        try { close(instance); }
        catch (Exception ex) when (ex is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or COMException or InvalidOperationException) { }

        try { Marshal.FinalReleaseComObject(instance); }
        catch (Exception ex) when (ex is ArgumentException or InvalidComObjectException) { }
    }

    /// <summary>
    /// Asıl sebebi mesaja taşır. Eskiden iç istisna atılıp yalnızca "Office
    /// dosyayı çeviremedi" gösteriliyordu; hatanın ne olduğu koda bakmadan
    /// anlaşılamıyordu.
    /// </summary>
    private static OfficeConversionException Describe(string sourcePath, Exception ex)
    {
        if (ex is OfficeConversionException already) return already;

        var name = Path.GetFileName(sourcePath);
        var reason = ex is COMException com ? Explain(com) : ex.Message;

        return new OfficeConversionException($"Office dosyayı çeviremedi: {name}. {reason}", ex);
    }

    private static string Explain(COMException ex) => (uint)ex.HResult switch
    {
        // Word meşgul ya da görünmez bir kip penceresi açık.
        0x80010001 => "Word yanıt vermedi; açık bir uyarı penceresi olabilir. " +
                      "Word'ü elle açıp bekleyen uyarıları kapatmak sorunu giderir. " +
                      $"(RPC_E_CALL_REJECTED) {ex.Message}",
        0x8001010A => "Word meşgul olduğu için isteği geri çevirdi. " +
                      $"(RPC_E_SERVERCALL_RETRYLATER) {ex.Message}",
        0x800706BA => $"Word süreci beklenmedik biçimde kapandı. (RPC_S_SERVER_UNAVAILABLE) {ex.Message}",
        _ => $"{ex.Message} (0x{(uint)ex.HResult:X8})"
    };

    private static dynamic CreateInstance(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new OfficeConversionException($"{progId} bu bilgisayarda kayıtlı değil.");

        return Activator.CreateInstance(type)
            ?? throw new OfficeConversionException($"{progId} başlatılamadı.");
    }

    private static bool IsRegistered(string progId)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(progId);
            return key is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
