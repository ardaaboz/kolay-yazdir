namespace KolayYazdir.Printing;

public static class RenderConstants
{
    /// <summary>
    /// Baskı çözünürlüğü. A4 bir sayfa bu değerde yaklaşık 34 MB tutar; 600
    /// DPI'a çıkmak sayfayı şeritler halinde render etmeyi gerektirirdi ve lazer
    /// çıktıda gözle ayırt edilir bir kazanç sağlamazdı.
    /// </summary>
    public const double PrintDpi = 300;

    /// <summary>Ekran önizlemesi için yeterli çözünürlük.</summary>
    public const double PreviewDpi = 110;

    /// <summary>
    /// Kaynak sayfa istenirken inilebilecek en düşük çözünürlük. 35'li
    /// yerleşimde hücreler küçülür ama okunabilirlik büsbütün gitmemeli.
    /// </summary>
    public const double MinimumSourceDpi = 36;
}
