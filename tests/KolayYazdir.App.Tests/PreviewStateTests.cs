using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.App.Tests;

public class PreviewStateTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static IReadOnlyList<Sheet> Sheets(int pageCount, DuplexMode duplex = DuplexMode.Simplex) =>
        LayoutEngine.Build(
            Enumerable.Range(0, pageCount).Select(i => new SourcePageInfo(i, A4)).ToList(),
            new PrintSettings { Duplex = duplex },
            FullBleed);

    [Fact]
    public void A_fresh_state_shows_nothing()
    {
        var state = new PreviewState();

        Assert.Equal(0, state.SheetCount);
        Assert.Null(state.Current);
        Assert.Equal(string.Empty, state.Label);
    }

    [Fact]
    public void Loading_sheets_starts_on_the_first_one()
    {
        var state = new PreviewState();

        state.Load(Sheets(4));

        Assert.Equal(4, state.SheetCount);
        Assert.Equal(0, state.CurrentIndex);
    }

    [Fact]
    public void Next_moves_forward()
    {
        var state = new PreviewState();
        state.Load(Sheets(4));

        state.Next();

        Assert.Equal(1, state.CurrentIndex);
    }

    [Fact]
    public void Next_stops_at_the_last_sheet()
    {
        var state = new PreviewState();
        state.Load(Sheets(2));

        state.Next();
        state.Next();
        state.Next();

        Assert.Equal(1, state.CurrentIndex);
    }

    [Fact]
    public void Previous_stops_at_the_first_sheet()
    {
        var state = new PreviewState();
        state.Load(Sheets(2));

        state.Previous();

        Assert.Equal(0, state.CurrentIndex);
    }

    [Fact]
    public void Simplex_label_counts_sheets_without_naming_a_side()
    {
        var state = new PreviewState();
        state.Load(Sheets(3));
        state.Next();

        Assert.Equal("Yaprak 2 / 3", state.Label);
    }

    [Fact]
    public void Duplex_label_names_the_side()
    {
        var state = new PreviewState();
        state.Load(Sheets(3, DuplexMode.Duplex));

        Assert.Equal("Yaprak 1 / 4 · ön", state.Label);

        state.Next();

        Assert.Equal("Yaprak 2 / 4 · arka", state.Label);
    }

    [Fact]
    public void An_empty_load_clears_the_label()
    {
        var state = new PreviewState();
        state.Load(Sheets(3));

        state.Load([]);

        Assert.Equal(string.Empty, state.Label);
        Assert.Null(state.Current);
    }

    [Fact]
    public void Reloading_a_shorter_job_clamps_the_current_index()
    {
        var state = new PreviewState();
        state.Load(Sheets(5));
        state.Next();
        state.Next();
        state.Next();

        state.Load(Sheets(2));

        Assert.Equal(1, state.CurrentIndex);
        Assert.NotNull(state.Current);
    }

    [Fact]
    public void The_current_sheet_follows_the_index()
    {
        var sheets = Sheets(3);
        var state = new PreviewState();
        state.Load(sheets);

        state.Next();

        Assert.Same(sheets[1], state.Current);
    }
}
