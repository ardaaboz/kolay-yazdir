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

    /// <summary>"Yaprak 2 / 4 · arka" biçiminde gösterge metni.</summary>
    public string Label
    {
        get
        {
            if (Current is not { } sheet) return string.Empty;

            var position = $"Yaprak {CurrentIndex + 1} / {_sheets.Count}";

            // Tek yönlü baskıda "ön" demek gereksiz gürültü.
            if (!_sheets.Any(s => s.Side == SheetSide.Back)) return position;

            return $"{position} · {(sheet.Side == SheetSide.Front ? "ön" : "arka")}";
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
