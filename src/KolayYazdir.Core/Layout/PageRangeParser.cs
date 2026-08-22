using System.Globalization;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Kullanıcının yazdığı "1-5, 8, 11-13" biçimindeki metni sıfır tabanlı sayfa
/// indekslerine çevirir. Anlaşılmayan parçalar sessizce atlanır; kullanıcı
/// yazarken her tuş vuruşunda hata göstermek yerine önizleme boş kalır.
/// </summary>
public static class PageRangeParser
{
    private static readonly char[] Separators = [',', ';'];

    public static IReadOnlyList<int> Parse(string? text, int pageCount)
    {
        if (pageCount <= 0) return [];
        if (string.IsNullOrWhiteSpace(text)) return Enumerable.Range(0, pageCount).ToList();

        var selected = new SortedSet<int>();

        foreach (var part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            if (token.Length == 0) continue;

            var dash = token.IndexOf('-');
            if (dash < 0)
            {
                // Tek sayfa: belge sonunu aşıyorsa kırpma yapılmaz, atılır.
                if (TryBound(token, pageCount, clampToEnd: false, out var single)) selected.Add(single);
                continue;
            }

            var span = token.AsSpan();
            if (!TryBound(span[..dash], pageCount, clampToEnd: false, out var from)) continue;
            if (!TryBound(span[(dash + 1)..], pageCount, clampToEnd: true, out var to)) continue;

            if (from > to) (from, to) = (to, from);
            for (var i = from; i <= to; i++) selected.Add(i);
        }

        return selected.ToList();
    }

    /// <summary>
    /// 1 tabanlı sayfa numarasını sıfır tabanlı indekse çevirir.
    /// <paramref name="clampToEnd"/> açıkken belge sonunu aşan değer hata
    /// sayılmaz, son sayfaya kırpılır — "9-40" yazan kullanıcı çoğu zaman
    /// "sonuna kadar" demek istemiştir. Tek sayfa ve aralığın alt ucu için
    /// kapalıdır: "99" yazan kullanıcı son sayfayı kastetmiyordur.
    /// </summary>
    private static bool TryBound(ReadOnlySpan<char> token, int pageCount, bool clampToEnd, out int index)
    {
        index = -1;
        if (!int.TryParse(token.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var oneBased))
            return false;
        if (oneBased < 1) return false;

        if (oneBased > pageCount)
        {
            if (!clampToEnd) return false;
            oneBased = pageCount;
        }

        index = oneBased - 1;
        return true;
    }
}
