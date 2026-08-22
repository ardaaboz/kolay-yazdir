using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// Kurulu Microsoft Office'i geç bağlama (late binding) ile kullanır. Geç bağlama
/// sayesinde Office sürümüne özel bir birlikte çalışma derlemesine bağımlı
/// olmayız; dükkandaki makinelerde sürümler farklı.
/// </summary>
public sealed class OfficeComConverter : IOfficeConverter
{
    private const int WdFormatPdf = 17;
    private const int XlTypePdf = 0;

    public string Name => "Microsoft Office";

    public bool IsAvailable => IsRegistered("Word.Application") || IsRegistered("Excel.Application");

    public Task<string> ToPdfAsync(string sourcePath, string targetDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");

        // COM otomasyonu tek iş parçacıklı apartman gerektirir ve engelleyicidir;
        // arayüzü kilitlememek için ayrı bir STA iş parçacığında koşturuyoruz.
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                if (IsSpreadsheet(sourcePath)) ConvertWithExcel(sourcePath, target);
                else ConvertWithWord(sourcePath, target);

                completion.SetResult(target);
            }
            catch (Exception ex)
            {
                completion.SetException(new OfficeConversionException(
                    $"Office dosyayı çeviremedi: {Path.GetFileName(sourcePath)}", ex));
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.WaitAsync(ct);
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
            application.Visible = false;
            application.DisplayAlerts = 0;

            document = application.Documents.Open(sourcePath, ReadOnly: true, AddToRecentFiles: false);
            document.SaveAs2(target, WdFormatPdf);
        }
        finally
        {
            if (document is not null) { document.Close(0); Marshal.FinalReleaseComObject(document); }
            if (application is not null) { application.Quit(0); Marshal.FinalReleaseComObject(application); }
        }
    }

    private static void ConvertWithExcel(string sourcePath, string target)
    {
        dynamic? application = null;
        dynamic? workbook = null;
        try
        {
            application = CreateInstance("Excel.Application");
            application.Visible = false;
            application.DisplayAlerts = false;

            workbook = application.Workbooks.Open(sourcePath, ReadOnly: true, AddToMru: false);
            workbook.ExportAsFixedFormat(XlTypePdf, target);
        }
        finally
        {
            if (workbook is not null) { workbook.Close(false); Marshal.FinalReleaseComObject(workbook); }
            if (application is not null) { application.Quit(); Marshal.FinalReleaseComObject(application); }
        }
    }

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
