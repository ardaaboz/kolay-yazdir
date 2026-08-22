using KolayYazdir.Core.Models;

namespace KolayYazdir.App.ViewModels;

/// <summary>
/// Önizlemede hangi yaprağın gösterildiğini tutar. WPF'e bağlı olmadığı için
/// gezinme mantığı doğrudan test edilebilir.
/// </summary>
public sealed class PreviewState
{
    private IReadOnlyList<Sheet> _sheets = [];

    public int SheetCount => _sheets.Count;

    public int CurrentIndex { get; private set; }

    public Sheet? Current => CurrentIndex < _sheets.Count ? _sheets[CurrentIndex] : null;

    /// <summary>
    /// Gezinme göstergesi. Tek yönlü baskıda her yaprak bir kağıt olduğu için
    /// "Yaprak 2 / 3"; önlü arkalıda gezinilen şey kağıt değil yüz olduğundan
    /// "Yüz 2 / 4 · arka" denir. Aynı kelimeyi iki farklı sayı için kullanmak —
    /// alttaki özet fiziksel kağıdı sayarken — kullanıcıyı yanıltıyordu.
    /// </summary>
    public string Label
    {
        get
        {
            if (Current is not { } sheet) return string.Empty;

            if (!_sheets.Any(s => s.Side == SheetSide.Back))
                return $"Yaprak {CurrentIndex + 1} / {_sheets.Count}";

            return $"Yüz {CurrentIndex + 1} / {_sheets.Count} · {(sheet.Side == SheetSide.Front ? "ön" : "arka")}";
        }
    }

    /// <summary>
    /// Yeni yaprak listesini yükler. Ayar değişince liste kısalabilir; bu durumda
    /// görünen yaprak son yaprağa kırpılır, önizleme boşa düşmez.
    /// </summary>
    public void Load(IReadOnlyList<Sheet> sheets)
    {
        _sheets = sheets;
        CurrentIndex = sheets.Count == 0 ? 0 : Math.Min(CurrentIndex, sheets.Count - 1);
    }

    public void Next() => CurrentIndex = Math.Min(CurrentIndex + 1, Math.Max(0, _sheets.Count - 1));

    public void Previous() => CurrentIndex = Math.Max(0, CurrentIndex - 1);
}
