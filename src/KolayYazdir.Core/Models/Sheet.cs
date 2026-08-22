using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Models;

/// <summary>Yaprağın hangi yüzü.</summary>
public enum SheetSide { Front, Back }

/// <summary>
/// Basılacak tek bir kağıt yüzü. Çizim yapan taraf sadece bunu görür;
/// hangi ayarların bu sonucu doğurduğunu bilmesi gerekmez.
/// </summary>
/// <param name="Index">Kaçıncı fiziksel yaprak (sıfır tabanlı).</param>
/// <param name="Pages">Boş yüzlerde boş liste.</param>
public sealed record Sheet(int Index, SheetSide Side, SizePt Paper, IReadOnlyList<PlacedPage> Pages)
{
    public bool IsBlank => Pages.Count == 0;
}
