namespace KolayYazdir.Core.Models;

public enum PaperFormat { A4, A5, A3 }

public enum Orientation { Portrait, Landscape }

/// <summary>Punto (1/72 inç) cinsinden bir genişlik-yükseklik çifti.</summary>
public readonly record struct SizePt(double Width, double Height);

public static class Paper
{
    private const double PointsPerInch = 72.0;
    private const double MmPerInch = 25.4;

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
}
