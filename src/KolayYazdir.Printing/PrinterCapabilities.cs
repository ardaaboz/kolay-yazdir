using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing.Interop;

namespace KolayYazdir.Printing;

/// <summary>Sürücünün tanıdığı bir kağıt cinsi.</summary>
public readonly record struct MediaType(int Id, string Name);

/// <summary>Bir yazıcının bizi ilgilendiren yetenekleri.</summary>
public sealed record PrinterCapabilities(
    string PrinterName,
    bool SupportsColor,
    bool SupportsAutomaticDuplex,
    bool SupportsMultipleCopies,
    IReadOnlyList<MediaType> MediaTypes,
    RectPt PrintableArea)
{
    /// <summary>
    /// Sürücü kağıt cinsi listesi vermediğinde kullanılan yedek eşleme.
    /// DMMEDIA_STANDARD = 1; kalın kağıt için sürücüye özel ilk değer 3'tür.
    /// </summary>
    private static readonly MediaType[] FallbackMediaTypes = [new(1, "Düz"), new(3, "Kalın")];

    public static string? DefaultPrinterName
    {
        get
        {
            try
            {
                var name = new PrinterSettings().PrinterName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (Exception ex) when (ex is InvalidPrinterException or ExternalException)
            {
                return null;
            }
        }
    }

    /// <returns>Yazıcı yoksa veya okunamıyorsa null.</returns>
    public static PrinterCapabilities? Read(string printerName, PaperFormat paper, Orientation orientation)
    {
        try
        {
            var settings = new PrinterSettings { PrinterName = printerName };
            if (!settings.IsValid) return null;

            return new PrinterCapabilities(
                printerName,
                settings.SupportsColor,
                ReadDuplexSupport(printerName),
                settings.MaximumCopies > 1,
                ReadMediaTypes(printerName),
                ReadPrintableArea(settings, paper, orientation));
        }
        catch (Exception ex) when (ex is InvalidPrinterException or ExternalException or ArgumentException)
        {
            return null;
        }
    }

    private static bool ReadDuplexSupport(string printerName) =>
        NativeMethods.DeviceCapabilities(printerName, null, NativeMethods.DC_DUPLEX, IntPtr.Zero, IntPtr.Zero) == 1;

    /// <summary>
    /// Sürücünün kendi kağıt cinsi isimlerini okur ("Düz", "Kalın 1"). Kullanıcı
    /// Windows'ta hangi ismi görüyorsa burada da onu görmeli.
    /// </summary>
    private static IReadOnlyList<MediaType> ReadMediaTypes(string printerName)
    {
        var count = NativeMethods.DeviceCapabilities(
            printerName, null, NativeMethods.DC_MEDIATYPENAMES, IntPtr.Zero, IntPtr.Zero);

        if (count <= 0) return FallbackMediaTypes;

        var namesBuffer = Marshal.AllocHGlobal(count * NativeMethods.MediaTypeNameLength * sizeof(char));
        var idsBuffer = Marshal.AllocHGlobal(count * sizeof(int));
        try
        {
            var namesWritten = NativeMethods.DeviceCapabilities(
                printerName, null, NativeMethods.DC_MEDIATYPENAMES, namesBuffer, IntPtr.Zero);
            var idsWritten = NativeMethods.DeviceCapabilities(
                printerName, null, NativeMethods.DC_MEDIATYPES, idsBuffer, IntPtr.Zero);

            if (namesWritten <= 0) return FallbackMediaTypes;

            var types = new List<MediaType>(namesWritten);
            for (var i = 0; i < namesWritten; i++)
            {
                var offset = namesBuffer + i * NativeMethods.MediaTypeNameLength * sizeof(char);
                var name = Marshal.PtrToStringUni(offset, NativeMethods.MediaTypeNameLength)?
                    .TrimEnd('\0')
                    .Trim();

                if (string.IsNullOrWhiteSpace(name)) continue;

                var id = i < idsWritten ? Marshal.ReadInt32(idsBuffer, i * sizeof(int)) : i + 1;
                types.Add(new MediaType(id, name));
            }

            return types.Count > 0 ? types : FallbackMediaTypes;
        }
        finally
        {
            Marshal.FreeHGlobal(namesBuffer);
            Marshal.FreeHGlobal(idsBuffer);
        }
    }

    /// <summary>
    /// Yazıcının basabildiği alanı punto olarak verir. .NET bu bilgiyi yüzde bir
    /// inç biriminde tutar, yani punto karşılığı 0.72 katıdır.
    /// </summary>
    private static RectPt ReadPrintableArea(PrinterSettings settings, PaperFormat paper, Orientation orientation)
    {
        var expected = Paper.SizeOf(paper, orientation);

        try
        {
            var page = settings.DefaultPageSettings;
            page.Landscape = orientation == Orientation.Landscape;

            var area = page.PrintableArea;

            // PrintableArea her zaman dikey kağıt koordinatlarında gelir;
            // yatayda eksenleri kendimiz çeviriyoruz.
            var (x, y, width, height) = orientation == Orientation.Landscape
                ? (area.Y, area.X, area.Height, area.Width)
                : (area.X, area.Y, area.Width, area.Height);

            var rect = new RectPt(x * 0.72, y * 0.72, width * 0.72, height * 0.72);

            // Sürücü saçmalarsa (sıfır alan veya kağıttan büyük) tüm kağıda düşüyoruz;
            // yanlış bir kenar payı yüzünden içeriği kırpmaktansa kenara kadar basmak yeğdir.
            if (rect.Width <= 0 || rect.Height <= 0
                || rect.Width > expected.Width + 1 || rect.Height > expected.Height + 1)
            {
                return new RectPt(0, 0, expected.Width, expected.Height);
            }

            return rect;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or ExternalException)
        {
            return new RectPt(0, 0, expected.Width, expected.Height);
        }
    }
}
