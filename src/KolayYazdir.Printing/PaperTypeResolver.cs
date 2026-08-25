namespace KolayYazdir.Printing;

/// <summary>
/// Kullanıcının gördüğü kağıt cinsi. Sürücüler onlarca seçenek sunuyor
/// (zarf, etiket, asetat, parlak foto, geri dönüşümlü…) ama kırtasiyede
/// kullanılan iki tane: normal fotokopi kağıdı ve kalın gramajlı kağıt.
/// Uygulamanın var oluş sebebi bu listeyi budamak.
/// </summary>
public enum PaperType
{
    Plain,
    Thick
}

/// <summary>
/// İki basit seçeneği sürücünün kendi kağıt cinsi numarasına çevirir.
/// </summary>
public static class PaperTypeResolver
{
    /// <summary>Sürücü hiç isim vermezse DEVMODE'un standart sabiti.</summary>
    private const int PlainFallbackId = 1;

    /// <summary>Sürücüye özel kağıt cinsleri 256'dan başlar; 3 yaygın bir "kalın" değeri.</summary>
    private const int ThickFallbackId = 3;

    /// <summary>
    /// Türkçe "ı" harfi yüzünden büyük/küçük dönüşümü güvenilmez ("KALIN"
    /// küçültülünce "kalin" olur), bu yüzden iki yazımı da arıyoruz.
    /// </summary>
    private static readonly string[] PlainWords = ["düz", "duz", "plain", "normal", "standart", "standard"];

    private static readonly string[] ThickWords = ["kalın", "kalin", "thick", "heavy", "card", "kart"];

    /// <summary>
    /// Sürücünün listesinden istenen cinse en iyi uyan girdiyi seçer.
    /// Tam isim eşleşmesi önce denenir ("Kalın 1"), sonra kelime araması.
    /// </summary>
    public static MediaType Resolve(IReadOnlyList<MediaType> driverTypes, PaperType wanted)
    {
        if (driverTypes.Count == 0) return Fallback(wanted);

        var words = wanted == PaperType.Plain ? PlainWords : ThickWords;

        // "Kalın 1" gibi numaralı isimlerde en küçük numaralı olan, sürücünün
        // ilk kalın kademesidir; kullanıcının kastettiği de o.
        var matches = driverTypes
            .Where(t => words.Any(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Name.Length)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count > 0) return matches[0];

        // Eşleşme yoksa düz için listenin ilki makul bir tahmin; kalın için
        // uydurmak yerine sabite düşüyoruz.
        return wanted == PaperType.Plain ? driverTypes[0] : Fallback(wanted);
    }

    private static MediaType Fallback(PaperType wanted) => wanted == PaperType.Plain
        ? new MediaType(PlainFallbackId, "Düz")
        : new MediaType(ThickFallbackId, "Kalın");
}
