namespace KolayYazdir.App.Controls;

/// <summary>
/// Sol sütunun ölçüleri. Ayar bloğunun kaydırmadan görünmesi bir tercih değil,
/// yapısal bir kural: pencerenin en küçük boyu burada tarif edilen yükseklikten
/// türetiliyor, böylece yazı tipi, DPI ya da yeni bir ayar satırı bloğu
/// büyüttüğünde pencere de onunla büyüyor.
/// </summary>
public static class SidebarMetrics
{
    public const double Width = 340;

    /// <summary>Sağdaki ayırıcı çizgi.</summary>
    public const double BorderThickness = 1;

    public const double PaddingLeft = 12;
    public const double PaddingRight = 14;

    /// <summary>Üst ve alt boşluğun toplamı.</summary>
    public const double VerticalPadding = 24;

    /// <summary>Bloklar arasındaki dikey boşluk.</summary>
    public const double Gap = 12;

    /// <summary>Dosya listesinin esnerken inebileceği en küçük boy.</summary>
    public const double FileListMinHeight = 70;

    /// <summary>
    /// Ayar bloğuna ayrılan tavan. Blok bunu aşarsa pencerenin tabanı da yükselir
    /// ve uygulama 1366x768 gibi tezgahta yaygın ekranlara sığmamaya başlar.
    /// </summary>
    public const double SettingsHeightBudget = 430;

    public static double ContentWidth => Width - BorderThickness - PaddingLeft - PaddingRight;

    /// <summary>
    /// Dosya seç düğmesi, en küçük hâliyle dosya listesi ve ayar bloğu birlikte
    /// kaydırmadan görünsün diye sol sütunun ihtiyaç duyduğu yükseklik.
    /// </summary>
    public static double RequiredHeight(double pickButtonHeight, double settingsHeight) =>
        VerticalPadding + pickButtonHeight + Gap + FileListMinHeight + Gap + settingsHeight;
}
