using System.Drawing;
using System.Drawing.Imaging;

namespace KolayYazdir.Documents.Tests;

public static class ImageFixtures
{
    /// <summary>Verilen piksel boyutunda ve çözünürlükte düz renkli bir görsel yazar.</summary>
    public static string Create(int widthPx, int heightPx, float dpi, ImageFormat? format = null)
    {
        format ??= ImageFormat.Png;
        var extension = Equals(format, ImageFormat.Png) ? "png" : "jpg";
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-{Guid.NewGuid():N}.{extension}");

        using var bitmap = new Bitmap(widthPx, heightPx);
        bitmap.SetResolution(dpi, dpi);
        using (var gfx = Graphics.FromImage(bitmap))
        {
            gfx.Clear(Color.Red);
        }

        bitmap.Save(path, format);
        return path;
    }
}
