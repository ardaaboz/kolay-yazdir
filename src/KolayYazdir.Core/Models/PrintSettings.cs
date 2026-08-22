using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Models;

public enum ColorMode { Color, Monochrome }

public enum DuplexMode { Simplex, Duplex }

/// <summary>
/// Kullanıcının seçtiği her şey. Değişmez bir kayıt olduğu için önizleme
/// yeniden hesaplanırken yarı değişmiş bir ara duruma düşmek imkansızdır.
/// </summary>
public sealed record PrintSettings
{
    public PaperFormat Paper { get; init; } = PaperFormat.A4;
    public Orientation Orientation { get; init; } = Orientation.Portrait;
    public ColorMode Color { get; init; } = ColorMode.Monochrome;
    public DuplexMode Duplex { get; init; } = DuplexMode.Simplex;
    public PagesPerSheet PagesPerSheet { get; init; } = PagesPerSheet.One;

    /// <summary>Spec gereği varsayılan kapalı: gerçek boyut korunur.</summary>
    public bool FitToPage { get; init; }

    public bool AutoRotate { get; init; } = true;

    /// <summary>Boş veya null ise tüm sayfalar.</summary>
    public string? PageRange { get; init; }

    public int Copies { get; init; } = 1;

    /// <summary>Sürücüye gönderilecek kağıt cinsi kimliği; null ise dokunulmaz.</summary>
    public int? MediaTypeId { get; init; }

    /// <summary>
    /// Çevirme kenarı kullanıcıya sorulmaz, yönden türetilir: dikeyse uzun
    /// kenar, yataysa kısa kenar.
    /// </summary>
    public DuplexBinding Binding =>
        Orientation == Orientation.Portrait ? DuplexBinding.LongEdge : DuplexBinding.ShortEdge;
}

public enum DuplexBinding { LongEdge, ShortEdge }
