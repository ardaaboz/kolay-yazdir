using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;
using PDFtoImage;
using SkiaSharp;

namespace KolayYazdir.Documents;

/// <summary>
/// PDFium (PDFtoImage paketi üzerinden) ile PDF okur. Projedeki tek PDFtoImage
/// kullanıcısı burasıdır; paketin API'si değişirse etkisi bu dosyaya hapsolur.
/// </summary>
public sealed class PdfRasterizer : IPageRasterizer
{
    private readonly byte[] _bytes;
    private readonly IReadOnlyList<SizePt> _pageSizes;

    public PdfRasterizer(string path)
    {
        try
        {
            _bytes = File.ReadAllBytes(path);
            _pageSizes = Conversion.GetPageSizes(_bytes)
                .Select(size => new SizePt(size.Width, size.Height))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DocumentLoadException($"PDF açılamadı: {Path.GetFileName(path)}", ex);
        }

        if (_pageSizes.Count == 0)
            throw new DocumentLoadException($"PDF hiç sayfa içermiyor: {Path.GetFileName(path)}");
    }

    public int PageCount => _pageSizes.Count;

    public SizePt PageSize(int index) => _pageSizes[index];

    public RasterPage Render(int index, double dpi)
    {
        // Beyaz zemin isteniyor: PDFium saydam zemin üretir, kağıda basılan
        // sayfada saydamlığın karşılığı yoktur.
        var options = new RenderOptions(
            Dpi: (int)Math.Round(dpi),
            WithAnnotations: true,
            BackgroundColor: SKColors.White);

        using var bitmap = Conversion.ToImage(_bytes, index, password: null, options: options);

        return ToRasterPage(bitmap);
    }

    /// <summary>Skia bitmap'ini dolgusuz BGRA dizisine kopyalar.</summary>
    private static RasterPage ToRasterPage(SKBitmap bitmap)
    {
        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var buffer = new byte[info.Width * info.Height * 4];

        using var pixmap = bitmap.PeekPixels()
            ?? throw new DocumentLoadException("Sayfa pikselleri okunamadı.");

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            // Skia'nın kendi renk düzeni platforma göre değişebiliyor; hedef
            // biçimi açıkça vererek her makinede aynı BGRA çıktısını alıyoruz.
            if (!pixmap.ReadPixels(info, handle.AddrOfPinnedObject(), info.Width * 4))
                throw new DocumentLoadException("Sayfa BGRA biçimine çevrilemedi.");
        }
        finally
        {
            handle.Free();
        }

        return new RasterPage(info.Width, info.Height, buffer);
    }

    public void Dispose() { }
}
