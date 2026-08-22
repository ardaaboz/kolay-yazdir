using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Render edilmiş bir sayfa. Piksel düzeni BGRA, satır dolgusu yoktur; hem WPF
/// <c>WriteableBitmap</c>'i hem GDI+ <c>Bitmap</c>'i bu düzeni doğrudan kabul eder.
/// </summary>
public sealed record RasterPage(int WidthPx, int HeightPx, byte[] Bgra);

/// <summary>
/// Bir kaynak belgenin sayfalarını okuyup istenen çözünürlükte piksele çevirir.
/// PDF, görsel ve Office dosyaları bu tek arayüzün arkasında birleşir; üst
/// katmanlar dosya türü diye bir şey bilmez.
/// </summary>
public interface IPageRasterizer : IDisposable
{
    int PageCount { get; }

    /// <summary>Sayfanın punto cinsinden gerçek boyutu.</summary>
    SizePt PageSize(int index);

    RasterPage Render(int index, double dpi);
}

/// <summary>Birleşik sayfa indeksinden piksel üretebilen kaynak.</summary>
public interface IPageImageSource
{
    RasterPage Render(int sourceIndex, double dpi);
}

/// <summary>Bir dosya açılamadığında veya okunamadığında atılır.</summary>
public sealed class DocumentLoadException(string message, Exception? inner = null)
    : Exception(message, inner);
