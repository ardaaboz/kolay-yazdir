using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class ManualDuplexPlanTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    private static List<SourcePageInfo> Pages(int count) =>
        Enumerable.Range(0, count).Select(i => new SourcePageInfo(i, A4)).ToList();

    private static IReadOnlyList<Sheet> DuplexSheets(int pageCount) =>
        LayoutEngine.Build(Pages(pageCount), new PrintSettings { Duplex = DuplexMode.Duplex }, FullBleed);

    [Fact]
    public void Fronts_go_in_the_first_pass()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.All(plan.FirstPass, s => Assert.Equal(SheetSide.Front, s.Side));
        Assert.Equal(3, plan.FirstPass.Count);
    }

    [Fact]
    public void Fronts_keep_their_natural_order()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.Equal([0, 1, 2], plan.FirstPass.Select(s => s.Index));
    }

    [Fact]
    public void Backs_are_printed_in_reverse_leaf_order()
    {
        // Yüzü aşağı çıkaran yazıcılarda deste ters birikir; çevirip geri
        // koyunca son yaprak en üste gelir.
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.Equal([2, 1, 0], plan.SecondPass.Select(s => s.Index));
    }

    [Fact]
    public void Backs_are_all_back_sides()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(6));

        Assert.All(plan.SecondPass, s => Assert.Equal(SheetSide.Back, s.Side));
    }

    [Fact]
    public void Every_sheet_ends_up_in_exactly_one_pass()
    {
        var sheets = DuplexSheets(7);
        var plan = ManualDuplexPlan.Split(sheets);

        Assert.Equal(sheets.Count, plan.FirstPass.Count + plan.SecondPass.Count);
    }

    [Fact]
    public void A_blank_back_is_still_printed_to_keep_the_stack_aligned()
    {
        var plan = ManualDuplexPlan.Split(DuplexSheets(3));

        Assert.Equal(2, plan.FirstPass.Count);
        Assert.Equal(2, plan.SecondPass.Count);
        Assert.Contains(plan.SecondPass, s => s.IsBlank);
    }

    [Fact]
    public void A_simplex_job_needs_no_second_pass()
    {
        var simplex = LayoutEngine.Build(Pages(3), new PrintSettings(), FullBleed);

        var plan = ManualDuplexPlan.Split(simplex);

        Assert.False(plan.NeedsSecondPass);
        Assert.Equal(3, plan.FirstPass.Count);
        Assert.Empty(plan.SecondPass);
    }

    [Fact]
    public void A_duplex_job_needs_a_second_pass()
    {
        Assert.True(ManualDuplexPlan.Split(DuplexSheets(2)).NeedsSecondPass);
    }

    [Fact]
    public void An_empty_job_produces_empty_passes()
    {
        var plan = ManualDuplexPlan.Split([]);

        Assert.Empty(plan.FirstPass);
        Assert.Empty(plan.SecondPass);
        Assert.False(plan.NeedsSecondPass);
    }

    [Fact]
    public void The_two_passes_pair_up_leaf_by_leaf_when_reversed()
    {
        // İkinci geçiş ters sırada; tersine çevrilince ön yüzlerle birebir
        // aynı yaprak sırasını vermeli, yoksa arka yüzler yanlış kağıda basılır.
        var plan = ManualDuplexPlan.Split(DuplexSheets(8));

        Assert.Equal(
            plan.FirstPass.Select(s => s.Index),
            plan.SecondPass.Reverse().Select(s => s.Index));
    }
}
