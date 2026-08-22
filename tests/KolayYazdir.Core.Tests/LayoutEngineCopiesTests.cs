using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineCopiesTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    private static IReadOnlyList<Sheet> ThreeSheets() =>
        LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

    [Fact]
    public void One_copy_returns_the_list_unchanged()
    {
        var sheets = ThreeSheets();

        Assert.Equal(3, LayoutEngine.Repeat(sheets, 1).Count);
    }

    [Fact]
    public void Copies_are_collated_not_grouped_by_page()
    {
        var repeated = LayoutEngine.Repeat(ThreeSheets(), 2);

        var sourceOrder = repeated.Select(s => s.Pages[0].SourceIndex).ToList();

        // Harmanlı: 1,2,3 - 1,2,3. Harmansız olsaydı 1,1,2,2,3,3 gelirdi.
        Assert.Equal([0, 1, 2, 0, 1, 2], sourceOrder);
    }

    [Fact]
    public void Sheet_count_multiplies_by_the_copy_count()
    {
        Assert.Equal(9, LayoutEngine.Repeat(ThreeSheets(), 3).Count);
    }

    [Fact]
    public void Sheet_indexes_are_renumbered_across_copies()
    {
        var repeated = LayoutEngine.Repeat(ThreeSheets(), 2);

        Assert.Equal([0, 1, 2, 3, 4, 5], repeated.Select(s => s.Index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Non_positive_copy_counts_are_treated_as_one(int copies)
    {
        Assert.Equal(3, LayoutEngine.Repeat(ThreeSheets(), copies).Count);
    }

    [Fact]
    public void An_empty_job_stays_empty()
    {
        Assert.Empty(LayoutEngine.Repeat([], 5));
    }

    [Fact]
    public void Duplex_leaf_pairing_survives_repetition()
    {
        var duplex = LayoutEngine.Build(
            Pages(1),
            new PrintSettings { Duplex = DuplexMode.Duplex },
            FullBleed);

        var repeated = LayoutEngine.Repeat(duplex, 2);

        Assert.Equal([0, 0, 1, 1], repeated.Select(s => s.Index));
        Assert.Equal(
            [SheetSide.Front, SheetSide.Back, SheetSide.Front, SheetSide.Back],
            repeated.Select(s => s.Side));
    }

    /// <summary>
    /// Kopya sınırı, bir kopyadaki farklı fiziksel yaprak sayısından türetilmeli.
    /// Yaprak listesinin uzunluğunu kullanan bir uygulama dupleks işlerde
    /// numaraları iki kat atlatır ve ikinci kopya birinciyle çakışmaz görünse de
    /// yaprak sayısı yalan söyler.
    /// </summary>
    [Fact]
    public void Duplex_copies_continue_leaf_numbering_without_gaps()
    {
        var duplex = LayoutEngine.Build(
            Pages(4),
            new PrintSettings { Duplex = DuplexMode.Duplex },
            FullBleed);

        var repeated = LayoutEngine.Repeat(duplex, 2);

        // Bir kopya iki fiziksel yaprak (0,0,1,1); ikinci kopya 2,2,3,3 olmalı.
        Assert.Equal([0, 0, 1, 1, 2, 2, 3, 3], repeated.Select(s => s.Index));
    }

    [Fact]
    public void The_blank_back_is_repeated_with_its_copy()
    {
        var duplex = LayoutEngine.Build(
            Pages(1),
            new PrintSettings { Duplex = DuplexMode.Duplex },
            FullBleed);

        var repeated = LayoutEngine.Repeat(duplex, 2);

        Assert.Equal(4, repeated.Count);
        Assert.True(repeated[1].IsBlank);
        Assert.True(repeated[3].IsBlank);
    }

    [Fact]
    public void Repeating_does_not_disturb_the_original_list()
    {
        var original = ThreeSheets();

        LayoutEngine.Repeat(original, 3);

        Assert.Equal(3, original.Count);
        Assert.Equal([0, 1, 2], original.Select(s => s.Index));
    }

    [Fact]
    public void Placed_pages_survive_repetition_intact()
    {
        var repeated = LayoutEngine.Repeat(ThreeSheets(), 2);

        // Çoğaltma yerleşimi yeniden hesaplamamalı; sayfa aynı yerde durmalı.
        Assert.Equal(repeated[0].Pages[0].Destination, repeated[3].Pages[0].Destination);
        Assert.Equal(repeated[0].Paper, repeated[3].Paper);
    }
}
