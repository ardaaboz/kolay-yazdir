using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing.Interop;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.Printing;

/// <summary>
/// Kullanıcının seçimlerini sürücünün anladığı DEVMODE yapısına yazar. .NET'in
/// <c>PrinterSettings</c> sınıfı kağıt cinsini ve çevirme kenarını açmadığı için
/// yapıya doğrudan dokunuyoruz.
/// </summary>
public static class DevModeConfigurator
{
    public static void Apply(PrinterSettings settings, PrintSettings print, bool driverHandlesCopies)
    {
        var handle = settings.GetHdevmode(settings.DefaultPageSettings);
        if (handle == IntPtr.Zero) return;

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(handle);
            return;
        }

        try
        {
            var mode = Marshal.PtrToStructure<DevMode>(pointer);

            mode.dmOrientation = print.Orientation == Orientation.Landscape
                ? DevModeFields.OrientLandscape
                : DevModeFields.OrientPortrait;
            mode.dmPaperSize = DevModeFields.PaperCode(print.Paper);
            mode.dmColor = print.Color == ColorMode.Color
                ? DevModeFields.ColorColour
                : DevModeFields.ColorMonochrome;
            mode.dmDuplex = print.Duplex == DuplexMode.Simplex
                ? DevModeFields.DuplexSimplex
                : print.Binding == DuplexBinding.LongEdge
                    ? DevModeFields.DuplexVertical
                    : DevModeFields.DuplexHorizontal;

            // Sürücü kopyalamayı üstlenmiyorsa yapraklar zaten çoğaltılmış gelir;
            // burada da çoğaltmak kopya sayısının karesini basardı.
            mode.dmCopies = (short)(driverHandlesCopies ? Math.Max(1, print.Copies) : 1);
            mode.dmCollate = DevModeFields.CollateTrue;

            mode.dmFields |= DevModeFields.Orientation
                | DevModeFields.PaperSize
                | DevModeFields.Color
                | DevModeFields.Duplex
                | DevModeFields.Copies
                | DevModeFields.Collate;

            if (print.MediaTypeId is { } mediaType)
            {
                mode.dmMediaType = mediaType;
                mode.dmFields |= DevModeFields.MediaType;
            }

            Marshal.StructureToPtr(mode, pointer, fDeleteOld: false);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }

        settings.SetHdevmode(handle);
        settings.DefaultPageSettings.SetHdevmode(handle);
        NativeMethods.GlobalFree(handle);
    }
}

/// <summary>Hazır yaprak listesini yazıcıya gönderir.</summary>
public sealed class PrintJobRunner(SheetRenderer renderer)
{
    /// <param name="driverHandlesCopies">
    /// Sürücü kopyalamayı üstleniyorsa true; kopya sayısı DEVMODE'a yazılır.
    /// False ise kopya 1'e sabitlenir ve çağıranın yaprakları
    /// <c>LayoutEngine.Repeat</c> ile çoğaltmış olması beklenir. Karar
    /// <see cref="PrinterCapabilities.SupportsMultipleCopies"/>'den gelir;
    /// burada yeniden hesaplanmaz ki iki yer farklı sonuca varmasın.
    /// </param>
    /// <param name="outputFile">Doluysa çıktı dosyaya yazılır (sanal yazıcı için).</param>
    public void Run(
        IReadOnlyList<Sheet> sheets,
        PrintSettings settings,
        string printerName,
        bool driverHandlesCopies,
        string? outputFile = null)
    {
        if (sheets.Count == 0) return;

        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;

        if (!document.PrinterSettings.IsValid)
            throw new InvalidPrinterException(document.PrinterSettings);

        DevModeConfigurator.Apply(document.PrinterSettings, settings, driverHandlesCopies);

        if (outputFile is not null)
        {
            document.PrinterSettings.PrintToFile = true;
            document.PrinterSettings.PrintFileName = outputFile;
        }

        document.DocumentName = "Kolay Yazdır";
        document.OriginAtMargins = false;

        var next = 0;
        document.PrintPage += (_, e) =>
        {
            var sheet = sheets[next++];

            // Boş arka yüz: kağıt yine de çıkmalı ki deste sırası kaymasın.
            if (!sheet.IsBlank && e.Graphics is { } graphics)
            {
                renderer.Draw(sheet, graphics, RenderConstants.PrintDpi, settings.Color);
            }

            e.HasMorePages = next < sheets.Count;
        };

        document.Print();
    }
}
