using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Layout;

/// <summary>
/// Otomatik dupleks olmayan yazıcılar için iki geçişlik baskı sırası. Önce tüm
/// ön yüzler basılır, kullanıcı desteyi çevirip tepsiye koyar, sonra arka yüzler
/// ters sırayla basılır.
/// </summary>
/// <remarks>
/// Ters sıra, yaprakları yüzü aşağı çıkaran yazıcılara göredir: bu yazıcılarda
/// çıkan deste ilk yaprak en altta olacak şekilde birikir, olduğu gibi çevrilip
/// geri konduğunda son yaprak en üste gelir. Yüzü yukarı çıkaran bir yazıcıda bu
/// sıra ters olur — dükkandaki yazıcıda yerinde doğrulanmalıdır.
/// </remarks>
public sealed record ManualDuplexPlan(IReadOnlyList<Sheet> FirstPass, IReadOnlyList<Sheet> SecondPass)
{
    public bool NeedsSecondPass => SecondPass.Count > 0;

    public static ManualDuplexPlan Split(IReadOnlyList<Sheet> sheets)
    {
        var fronts = sheets.Where(s => s.Side == SheetSide.Front).ToList();
        var backs = sheets.Where(s => s.Side == SheetSide.Back).Reverse().ToList();

        return new ManualDuplexPlan(fronts, backs);
    }
}
