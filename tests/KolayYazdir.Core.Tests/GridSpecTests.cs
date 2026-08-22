using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class GridSpecTests
{
    [Theory]
    [InlineData(PagesPerSheet.One, 1, 1)]
    [InlineData(PagesPerSheet.Two, 1, 2)]
    [InlineData(PagesPerSheet.Four, 2, 2)]
    [InlineData(PagesPerSheet.Nine, 3, 3)]
    [InlineData(PagesPerSheet.Sixteen, 4, 4)]
    [InlineData(PagesPerSheet.ThirtyFive, 5, 7)]
    public void Portrait_grids_match_the_spec(PagesPerSheet nUp, int columns, int rows)
    {
        var grid = GridSpec.For(nUp, Orientation.Portrait);

        Assert.Equal(columns, grid.Columns);
        Assert.Equal(rows, grid.Rows);
    }

    [Theory]
    [InlineData(PagesPerSheet.Two, 2, 1)]
    [InlineData(PagesPerSheet.ThirtyFive, 7, 5)]
    [InlineData(PagesPerSheet.Four, 2, 2)]
    public void Landscape_swaps_columns_and_rows(PagesPerSheet nUp, int columns, int rows)
    {
        var grid = GridSpec.For(nUp, Orientation.Landscape);

        Assert.Equal(columns, grid.Columns);
        Assert.Equal(rows, grid.Rows);
    }

    [Fact]
    public void Capacity_is_columns_times_rows()
    {
        Assert.Equal(35, GridSpec.For(PagesPerSheet.ThirtyFive, Orientation.Portrait).Capacity);
    }
}
