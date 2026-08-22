using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>Bir yaprağa kaç kaynak sayfa yerleşeceği.</summary>
public enum PagesPerSheet
{
    One = 1,
    Two = 2,
    Four = 4,
    Nine = 9,
    Sixteen = 16,
    ThirtyFive = 35
}

/// <summary>Yaprak üzerindeki hücre ızgarasının sütun ve satır sayısı.</summary>
public readonly record struct GridSpec(int Columns, int Rows)
{
    public int Capacity => Columns * Rows;

    /// <summary>
    /// Dikey kağıtta tablo spec'teki gibidir; yatay kağıtta sütun ve satır
    /// yer değiştirir, böylece hücre oranı kağıt oranını takip eder.
    /// </summary>
    public static GridSpec For(PagesPerSheet pagesPerSheet, Orientation orientation)
    {
        var portrait = pagesPerSheet switch
        {
            PagesPerSheet.One => new GridSpec(1, 1),
            PagesPerSheet.Two => new GridSpec(1, 2),
            PagesPerSheet.Four => new GridSpec(2, 2),
            PagesPerSheet.Nine => new GridSpec(3, 3),
            PagesPerSheet.Sixteen => new GridSpec(4, 4),
            PagesPerSheet.ThirtyFive => new GridSpec(5, 7),
            _ => throw new ArgumentOutOfRangeException(nameof(pagesPerSheet))
        };

        return orientation == Orientation.Portrait
            ? portrait
            : new GridSpec(portrait.Rows, portrait.Columns);
    }
}
