using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;
using ColorMode = KolayYazdir.Core.Models.ColorMode;

namespace KolayYazdir.Printing;

/// <summary>
/// Bir yaprağı çizer. Önizleme ve yazıcı aynı metodu farklı DPI ile çağırır;
/// ekranda görülen ile kağıda çıkan bu yüzden birbirinin aynısıdır.
/// </summary>
public sealed class SheetRenderer(IPageImageSource source)
{
    /// <summary>Renkli görüntüyü göze doğal gelen ağırlıklarla griye çevirir.</summary>
    private static readonly ColorMatrix GreyscaleMatrix = new(
    [
        [0.299f, 0.299f, 0.299f, 0, 0],
        [0.587f, 0.587f, 0.587f, 0, 0],
        [0.114f, 0.114f, 0.114f, 0, 0],
        [0, 0, 0, 1, 0],
        [0, 0, 0, 0, 1]
    ]);

    /// <summary>Yaprağı yeni bir bitmap'e çizer. Çağıran bitmap'i kapatmalıdır.</summary>
    public Bitmap RenderToBitmap(Sheet sheet, double dpi, ColorMode color)
    {
        var width = (int)Math.Round(sheet.Paper.Width / 72.0 * dpi);
        var height = (int)Math.Round(sheet.Paper.Height / 72.0 * dpi);

        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height), PixelFormat.Format32bppArgb);
        bitmap.SetResolution((float)dpi, (float)dpi);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            Draw(sheet, graphics, dpi, color);
        }

        return bitmap;
    }

    /// <summary>
    /// Yaprağı hazır bir yüzeye çizer. Koordinatlar punto cinsindendir; yüzeyin
    /// kendi ölçeğini <see cref="Graphics.PageUnit"/> ile ayarlıyoruz.
    /// </summary>
    public void Draw(Sheet sheet, Graphics graphics, double dpi, ColorMode color)
    {
        graphics.PageUnit = GraphicsUnit.Point;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        foreach (var placed in sheet.Pages)
        {
            if (placed.Destination.Width <= 0 || placed.Destination.Height <= 0) continue;

            using var image = ToBitmap(source.Render(placed.SourceIndex, SourceDpiFor(placed, dpi)));

            DrawPlaced(graphics, image, placed, color);
        }
    }

    /// <summary>
    /// Kaynağı hedef dikdörtgenine göre ölçeklenmiş çözünürlükte ister. Sayfa
    /// dörtte birine küçülüyorsa dörtte bir çözünürlük yeter; tam sayfa render
    /// edip küçültmek hem yavaş hem belleği boşuna şişirir.
    /// </summary>
    private double SourceDpiFor(PlacedPage placed, double sheetDpi)
    {
        var sourceSize = source.PageSize(placed.SourceIndex);

        // Döndürülmüş sayfada hedefin genişliği kaynağın yüksekliğine denk gelir.
        var sourceWidthPt = placed.RotationDegrees == 90 ? sourceSize.Height : sourceSize.Width;
        if (sourceWidthPt <= 0) return sheetDpi;

        var scale = placed.Destination.Width / sourceWidthPt;

        return Math.Max(RenderConstants.MinimumSourceDpi, sheetDpi * scale);
    }

    private static void DrawPlaced(Graphics graphics, Bitmap image, PlacedPage placed, ColorMode color)
    {
        var state = graphics.Save();
        try
        {
            var destination = placed.Destination;
            graphics.TranslateTransform((float)destination.X, (float)destination.Y);

            var width = (float)destination.Width;
            var height = (float)destination.Height;

            // 90° saat yönünde döndürme: önce sağ üst köşeye taşı, sonra döndür.
            // Böylece dönmüş içerik tam olarak hedef dikdörtgeni doldurur.
            if (placed.RotationDegrees == 90)
            {
                graphics.TranslateTransform(width, 0);
                graphics.RotateTransform(90);
                (width, height) = (height, width);
            }

            var target = new RectangleF(0, 0, width, height);

            if (color == ColorMode.Monochrome)
            {
                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(GreyscaleMatrix);
                graphics.DrawImage(image, Rectangle.Round(target),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
            else
            {
                graphics.DrawImage(image, target);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>Dolgusuz BGRA dizisini GDI+ bitmap'ine sarar.</summary>
    private static Bitmap ToBitmap(RasterPage page)
    {
        var bitmap = new Bitmap(page.WidthPx, page.HeightPx, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, page.WidthPx, page.HeightPx),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var rowBytes = page.WidthPx * 4;
            for (var row = 0; row < page.HeightPx; row++)
            {
                Marshal.Copy(page.Bgra, row * rowBytes, data.Scan0 + row * data.Stride, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
