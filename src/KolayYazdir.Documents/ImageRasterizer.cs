using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Bir görsel dosyasını tek sayfalık belge gibi sunar. Render her zaman görselin
/// kendi piksellerini döner — büyütüp küçültme kararı yerleşim motorunun ve çizim
/// katmanının işidir, burada yeniden örnekleme yapılmaz.
/// </summary>
public sealed class ImageRasterizer : IPageRasterizer
{
    /// <summary>Çözünürlük bilgisi olmayan dosyalar için Windows'un varsayımı.</summary>
    private const float FallbackDpi = 96f;

    private readonly Bitmap _bitmap;
    private readonly SizePt _size;

    public ImageRasterizer(string path)
    {
        float horizontal;
        float vertical;

        try
        {
            // Dosya kilidini tutmamak için akıştan yükleyip kopyalıyoruz;
            // kullanıcı dosyayı silmek isterse uygulama engel olmasın.
            using var stream = File.OpenRead(path);
            using var loaded = new Bitmap(stream);

            // Çözünürlüğü kopyalamadan ÖNCE okuyoruz: new Bitmap(Image) yeni
            // bitmap'i ekran DPI'sıyla oluşturur ve kaynağın çözünürlük bilgisini
            // düşürür. Bu sessizce kaybolursa 300 DPI'lık bir fotoğraf gerçek
            // boyutunun üç katında basılır.
            horizontal = Sane(loaded.HorizontalResolution);
            vertical = Sane(loaded.VerticalResolution);

            _bitmap = new Bitmap(loaded);
            _bitmap.SetResolution(horizontal, vertical);
        }
        catch (Exception ex)
        {
            throw new DocumentLoadException($"Görsel açılamadı: {Path.GetFileName(path)}", ex);
        }

        _size = new SizePt(_bitmap.Width / horizontal * 72.0, _bitmap.Height / vertical * 72.0);
    }

    /// <summary>
    /// GDI+ çözünürlük bilgisi olmayan dosyalarda bazen 0 veya saçma bir değer
    /// verir; böyle bir sayı sayfa boyutunu sonsuza götürür.
    /// </summary>
    private static float Sane(float dpi) => dpi is > 1f and < 5000f ? dpi : FallbackDpi;

    public int PageCount => 1;

    public SizePt PageSize(int index) => _size;

    /// <param name="dpi">
    /// Yok sayılır: kaynağın kendi pikselleri en doğru veridir, ölçekleme çizim
    /// sırasında yapılır. İmza <see cref="IPageRasterizer"/> ortak olduğu için durur.
    /// </param>
    public RasterPage Render(int index, double dpi)
    {
        var rectangle = new Rectangle(0, 0, _bitmap.Width, _bitmap.Height);
        var data = _bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var rowBytes = _bitmap.Width * 4;
            var buffer = new byte[rowBytes * _bitmap.Height];

            // LockBits satır sonlarını hizalayabilir; hedef dizide dolgu istemiyoruz.
            for (var row = 0; row < _bitmap.Height; row++)
            {
                Marshal.Copy(data.Scan0 + row * data.Stride, buffer, row * rowBytes, rowBytes);
            }

            return new RasterPage(_bitmap.Width, _bitmap.Height, buffer);
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }
    }

    public void Dispose() => _bitmap.Dispose();
}
