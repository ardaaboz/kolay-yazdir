using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class CellGridTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);

    /// <summary>Basılabilir alan kısıtı olmayan, tüm kağıdı kaplayan bir yazıcı.</summary>
    private static RectPt FullBleed(SizePt paper) => new(0, 0, paper.Width, paper.Height);

    [Fact]
    public void Single_cell_is_the_page_inset_by_the_margin()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(1, 1));
        var margin = Paper.MmToPt(5);

        var cell = Assert.Single(cells);
        Assert.Equal(margin, cell.X, 3);
        Assert.Equal(margin, cell.Y, 3);
        Assert.Equal(A4.Width - 2 * margin, cell.Width, 3);
        Assert.Equal(A4.Height - 2 * margin, cell.Height, 3);
    }

    [Fact]
    public void Two_by_two_grid_splits_the_content_area_with_a_gutter()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(2, 2));
        var margin = Paper.MmToPt(5);
        var gutter = Paper.MmToPt(3);
        var expectedWidth = (A4.Width - 2 * margin - gutter) / 2;
        var expectedHeight = (A4.Height - 2 * margin - gutter) / 2;

        Assert.Equal(4, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal(expectedWidth, c.Width, 3);
            Assert.Equal(expectedHeight, c.Height, 3);
        });
    }

    [Fact]
    public void Cells_are_ordered_left_to_right_then_top_to_bottom()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), new GridSpec(2, 2));

        Assert.True(cells[0].X < cells[1].X, "birinci hücre ikincinin solunda olmalı");
        Assert.Equal(cells[0].Y, cells[1].Y, 3);
        Assert.True(cells[2].Y > cells[0].Y, "üçüncü hücre birincinin altında olmalı");
        Assert.Equal(cells[0].X, cells[2].X, 3);
    }

    [Fact]
    public void Hardware_margin_larger_than_five_millimetres_wins()
    {
        var hardMargin = Paper.MmToPt(12);
        var printable = new RectPt(hardMargin, hardMargin,
            A4.Width - 2 * hardMargin, A4.Height - 2 * hardMargin);

        var cell = Assert.Single(CellGrid.Build(A4, printable, new GridSpec(1, 1)));

        Assert.Equal(hardMargin, cell.X, 3);
        Assert.Equal(A4.Width - 2 * hardMargin, cell.Width, 3);
    }

    [Fact]
    public void Thirty_five_up_produces_thirty_five_cells_that_fit_the_page()
    {
        var cells = CellGrid.Build(A4, FullBleed(A4), GridSpec.For(PagesPerSheet.ThirtyFive, Orientation.Portrait));

        Assert.Equal(35, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.True(c.Width > 0 && c.Height > 0);
            Assert.True(c.X + c.Width <= A4.Width + 0.001);
            Assert.True(c.Y + c.Height <= A4.Height + 0.001);
        });
    }
}
