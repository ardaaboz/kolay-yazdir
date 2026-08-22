using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineDuplexTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    private static PrintSettings Duplex(PagesPerSheet nUp = PagesPerSheet.One) =>
        new() { Duplex = DuplexMode.Duplex, PagesPerSheet = nUp };

    [Fact]
    public void Sides_alternate_front_then_back()
    {
        var sheets = LayoutEngine.Build(Pages(4), Duplex(), FullBleed);

        Assert.Equal(
            [SheetSide.Front, SheetSide.Back, SheetSide.Front, SheetSide.Back],
            sheets.Select(s => s.Side));
    }

    [Fact]
    public void Both_sides_of_a_leaf_share_a_sheet_index()
    {
        var sheets = LayoutEngine.Build(Pages(4), Duplex(), FullBleed);

        Assert.Equal([0, 0, 1, 1], sheets.Select(s => s.Index));
    }

    [Fact]
    public void Four_up_duplex_puts_pages_one_to_four_on_the_front()
    {
        var sheets = LayoutEngine.Build(Pages(8), Duplex(PagesPerSheet.Four), FullBleed);

        Assert.Equal([0, 1, 2, 3], sheets[0].Pages.Select(p => p.SourceIndex));
        Assert.Equal([4, 5, 6, 7], sheets[1].Pages.Select(p => p.SourceIndex));
    }

    [Fact]
    public void Odd_page_count_leaves_a_blank_back()
    {
        var sheets = LayoutEngine.Build(Pages(3), Duplex(), FullBleed);

        Assert.Equal(4, sheets.Count);
        Assert.True(sheets[3].IsBlank);
        Assert.Equal(SheetSide.Back, sheets[3].Side);
    }

    [Fact]
    public void A_single_page_still_gets_a_blank_back()
    {
        var sheets = LayoutEngine.Build(Pages(1), Duplex(), FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.False(sheets[0].IsBlank);
        Assert.True(sheets[1].IsBlank);
    }

    [Fact]
    public void Blank_back_carries_the_paper_size()
    {
        var sheets = LayoutEngine.Build(Pages(1), Duplex(), FullBleed);

        Assert.Equal(A4.Width, sheets[1].Paper.Width, 3);
    }

    [Fact]
    public void Blank_back_shares_the_leaf_index()
    {
        var sheets = LayoutEngine.Build(Pages(3), Duplex(), FullBleed);

        Assert.Equal([0, 0, 1, 1], sheets.Select(s => s.Index));
        Assert.True(sheets[3].IsBlank);
    }

    [Fact]
    public void Simplex_never_produces_a_blank_side()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.DoesNotContain(sheets, s => s.IsBlank);
    }

    [Fact]
    public void Portrait_binds_on_the_long_edge()
    {
        Assert.Equal(DuplexBinding.LongEdge, new PrintSettings().Binding);
    }

    [Fact]
    public void Landscape_binds_on_the_short_edge()
    {
        var settings = new PrintSettings { Orientation = Orientation.Landscape };

        Assert.Equal(DuplexBinding.ShortEdge, settings.Binding);
    }
}
