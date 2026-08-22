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
        var sidesPerLeaf = settings.Duplex == DuplexMode.Duplex ? 2 : 1;
        var leafIndex = 0;
        var side = SheetSide.Front;

        for (var offset = 0; offset < selected.Count; offset += grid.Capacity)
        {
            var placed = new List<PlacedPage>(grid.Capacity);
            var take = Math.Min(grid.Capacity, selected.Count - offset);

            for (var slot = 0; slot < take; slot++)
            {
                var page = pages[selected[offset + slot]];
                placed.Add(Placement.Fit(page.Index, page.Size, cells[slot], settings.FitToPage, settings.AutoRotate));
            }

            sheets.Add(new Sheet(leafIndex, side, paper, placed));

            if (sidesPerLeaf == 1)
            {
                leafIndex++;
                continue;
            }

            if (side == SheetSide.Front)
            {
                side = SheetSide.Back;
            }
            else
            {
                side = SheetSide.Front;
                leafIndex++;
            }
        }

        // Son yaprağın arkası doldurulamadıysa boş bir yüz olarak eklenir;
        // yazıcı yaprağı çevirip boş basar, sıra kaymaz.
        if (sidesPerLeaf == 2 && side == SheetSide.Back)
        {
            sheets.Add(new Sheet(leafIndex, SheetSide.Back, paper, []));
        }

        return sheets;
    }

    /// <summary>
    /// Yaprak listesini harmanlanmış olarak çoğaltır (1,2,3 – 1,2,3). Sürücü
    /// kopyalamayı desteklemediğinde kullanılır. Yerleşim yeniden hesaplanmaz;
    /// yapraklar olduğu gibi tekrarlanır, sadece yaprak numaraları kayar.
    /// </summary>
    public static IReadOnlyList<Sheet> Repeat(IReadOnlyList<Sheet> sheets, int copies)
    {
        if (copies <= 1 || sheets.Count == 0) return sheets;

        // Bir kopyadaki farklı fiziksel yaprak sayısı. Listenin uzunluğu değil:
        // dupleks işlerde iki yüz tek yaprağa denk gelir, uzunluğu kullanmak
        // ikinci kopyanın numaralarını iki kat atlatırdı.
        var leavesPerCopy = sheets[^1].Index + 1;

        var result = new List<Sheet>(sheets.Count * copies);
        for (var copy = 0; copy < copies; copy++)
        {
            foreach (var sheet in sheets)
            {
                result.Add(sheet with { Index = sheet.Index + copy * leavesPerCopy });
            }
        }

        return result;
    }
}
