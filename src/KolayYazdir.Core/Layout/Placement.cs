using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Bir kaynak sayfanın yaprak üzerindeki nihai yeri. <see cref="RotationDegrees"/>
/// 0 veya 90'dır ve saat yönünde dönüşü ifade eder; <see cref="Destination"/>
/// döndürülmüş hâlin kapladığı dikdörtgendir.
/// </summary>
public readonly record struct PlacedPage(int SourceIndex, RectPt Destination, int RotationDegrees);

public static class Placement
{
    /// <summary>
    /// Spec'teki beş adımlı kural: gerekiyorsa döndür, oranı koruyarak ölçekle,
    /// sığdırma kapalıysa büyütme, hücrenin ortasına yerleştir.
    /// </summary>
    public static PlacedPage Fit(int sourceIndex, SizePt source, RectPt cell, bool fitToPage, bool autoRotate)
    {
        if (source.Width <= 0 || source.Height <= 0 || cell.Width <= 0 || cell.Height <= 0)
        {
            return new PlacedPage(sourceIndex, cell with { Width = 0, Height = 0 }, 0);
        }

        var rotate = autoRotate && WouldRotationHelp(source, cell);
        var effective = rotate ? new SizePt(source.Height, source.Width) : source;

        var scale = Math.Min(cell.Width / effective.Width, cell.Height / effective.Height);

        // Sığdırma kapalıyken gerçek boyut korunur: sadece taşıyorsa küçültülür,
        // asla büyütülmez. Vesikalık gibi ölçüsü önemli işler bozulmasın.
        if (!fitToPage) scale = Math.Min(scale, 1.0);

        var width = effective.Width * scale;
        var height = effective.Height * scale;

        var destination = new RectPt(
            cell.X + (cell.Width - width) / 2,
            cell.Y + (cell.Height - height) / 2,
            width,
            height);

        return new PlacedPage(sourceIndex, destination, rotate ? 90 : 0);
    }

    /// <summary>
    /// Kaynak ile hücrenin yön oranları zıt işaretliyse döndürmek içeriği
    /// büyütür. Kare hücrede (veya kare içerikte) kazanç yoktur, döndürülmez.
    /// </summary>
    private static bool WouldRotationHelp(SizePt source, RectPt cell)
    {
        var sourceIsWide = source.Width > source.Height;
        var cellIsWide = cell.Width > cell.Height;

        var sourceIsSquare = Math.Abs(source.Width - source.Height) < 1e-9;
        var cellIsSquare = Math.Abs(cell.Width - cell.Height) < 1e-9;

        if (sourceIsSquare || cellIsSquare) return false;

        return sourceIsWide != cellIsWide;
    }
}
