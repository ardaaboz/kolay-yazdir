using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Yerleşimin tamamı. Saf bir fonksiyondur: hiçbir çizim yapmaz, hiçbir dosya
/// okumaz, hiçbir yazıcıya dokunmaz. Bu yüzden tümüyle ve hızla test edilebilir.
/// </summary>
public static class LayoutEngine
{
    /// <param name="pages">Tüm dosyaların birleştirilmiş sayfa dizisi.</param>
    /// <param name="printableArea">Yazıcının basabildiği alan, kağıt koordinatlarında.</param>
    public static IReadOnlyList<Sheet> Build(
        IReadOnlyList<SourcePageInfo> pages,
        PrintSettings settings,
        RectPt printableArea)
    {
        var selected = PageRangeParser.Parse(settings.PageRange, pages.Count);
        if (selected.Count == 0) return [];

        var paper = Paper.SizeOf(settings.Paper, settings.Orientation);
        var grid = GridSpec.For(settings.PagesPerSheet, settings.Orientation);
        var cells = CellGrid.Build(paper, printableArea, grid);

        var sheets = new List<Sheet>();
        for (var offset = 0; offset < selected.Count; offset += grid.Capacity)
        {
            var placed = new List<PlacedPage>(grid.Capacity);
            var take = Math.Min(grid.Capacity, selected.Count - offset);

            for (var slot = 0; slot < take; slot++)
            {
                var page = pages[selected[offset + slot]];
                placed.Add(Placement.Fit(page.Index, page.Size, cells[slot], settings.FitToPage, settings.AutoRotate));
            }

            sheets.Add(new Sheet(sheets.Count, SheetSide.Front, paper, placed));
        }

        return sheets;
    }
}
