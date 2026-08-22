using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class LayoutEngineTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    /// <summary>A4 boyutunda <paramref name="count"/> adet kaynak sayfa.</summary>
    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    [Fact]
    public void One_up_produces_one_sheet_per_page()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.Equal(3, sheets.Count);
        Assert.All(sheets, s => Assert.Single(s.Pages));
    }

    [Fact]
    public void Four_up_packs_four_pages_onto_each_sheet()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(8), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.All(sheets, s => Assert.Equal(4, s.Pages.Count));
    }

    [Fact]
    public void Last_sheet_is_partially_filled_when_pages_run_out()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(6), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(4, sheets[0].Pages.Count);
        Assert.Equal(2, sheets[1].Pages.Count);
    }

    [Fact]
    public void Pages_are_placed_in_document_order()
    {
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };

        var sheets = LayoutEngine.Build(Pages(4), settings, FullBleed);

        Assert.Equal([0, 1, 2, 3], sheets[0].Pages.Select(p => p.SourceIndex));
    }

    [Fact]
    public void Page_range_narrows_the_job()
    {
        var settings = new PrintSettings { PageRange = "2-3" };

        var sheets = LayoutEngine.Build(Pages(10), settings, FullBleed);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(1, sheets[0].Pages[0].SourceIndex);
        Assert.Equal(2, sheets[1].Pages[0].SourceIndex);
    }

    [Fact]
    public void Landscape_sheets_use_swapped_paper_dimensions()
    {
        var settings = new PrintSettings { Orientation = Orientation.Landscape };

        var sheet = LayoutEngine.Build(Pages(1), settings, FullBleed).Single();

        Assert.Equal(A4.Height, sheet.Paper.Width, 3);
        Assert.Equal(A4.Width, sheet.Paper.Height, 3);
    }

    [Fact]
    public void Empty_input_produces_no_sheets()
    {
        Assert.Empty(LayoutEngine.Build([], new PrintSettings(), FullBleed));
    }

    [Fact]
    public void Every_simplex_sheet_is_a_front()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.All(sheets, s => Assert.Equal(SheetSide.Front, s.Side));
    }

    [Fact]
    public void Sheet_indexes_are_sequential_from_zero()
    {
        var sheets = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        Assert.Equal([0, 1, 2], sheets.Select(s => s.Index));
    }
}
