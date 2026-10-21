namespace KolayYazdir.Core.Models;

public enum PaperFormat { A4, A5, A3 }

public enum Orientation { Portrait, Landscape }

/// <summary>Punto (1/72 inç) cinsinden bir genişlik-yükseklik çifti.</summary>
public readonly record struct SizePt(double Width, double Height);

public static class Paper
{
    private const double PointsPerInch = 72.0;
    private const double MmPerInch = 25.4;

    /// <summary>PDF üreticileri A4'ü 595x842 gibi yuvarlar; eşleşme bu kadar şaşabilir.</summary>
    private const double SizeToleranceMm = 3.0;

    /// <summary>
    /// Bakınca "bu bir belge sayfası" denebilecek ölçüler. Yönden bağımsız
    /// karşılaştırıldıkları için kısa kenar önce yazılı.
    /// </summary>
    private static readonly SizePt[] DocumentSizes =
    [
        new(MmToPt(105), MmToPt(148)), // A6
        new(MmToPt(148), MmToPt(210)), // A5
        new(MmToPt(176), MmToPt(250)), // B5
        new(MmToPt(210), MmToPt(297)), // A4
        new(MmToPt(216), MmToPt(279)), // Letter
        new(MmToPt(216), MmToPt(356)), // Legal
        new(MmToPt(297), MmToPt(420))  // A3
    ];

    public static double MmToPt(double mm) => mm / MmPerInch * PointsPerInch;

    /// <summary>ISO 216 kağıt boyutunu istenen yönde punto olarak verir.</summary>
    public static SizePt SizeOf(PaperFormat format, Orientation orientation)
    {
        var portrait = format switch
        {
            PaperFormat.A4 => new SizePt(MmToPt(210), MmToPt(297)),
            PaperFormat.A5 => new SizePt(MmToPt(148), MmToPt(210)),
            PaperFormat.A3 => new SizePt(MmToPt(297), MmToPt(420)),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return orientation == Orientation.Portrait
            ? portrait
            : new SizePt(portrait.Height, portrait.Width);
    }

    /// <summary>
    /// Verilen ölçü standart bir belge sayfası mı? Yerleşim bunu "içeriğin
    /// fiziksel boyutu önemli mi" sorusunun cevabı olarak kullanır: A4 bir
    /// belgenin kendi ölçüsünde kalmasının kimseye faydası yok, vesikalığın
    /// kalmasının var.
    /// </summary>
    public static bool IsStandardDocumentSize(SizePt size)
    {
        var tolerance = MmToPt(SizeToleranceMm);
        var shortSide = Math.Min(size.Width, size.Height);
        var longSide = Math.Max(size.Width, size.Height);

        return DocumentSizes.Any(page =>
            Math.Abs(shortSide - page.Width) <= tolerance
            && Math.Abs(longSide - page.Height) <= tolerance);
    }
}
