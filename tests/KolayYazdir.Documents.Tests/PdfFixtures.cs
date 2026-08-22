using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KolayYazdir.Documents.Tests;

/// <summary>
/// Testler için PDF üretir. Depoya ikili dosya koymak yerine her çalıştırmada
/// yeniden üretmek, fixture'ın içeriğini testin yanında görünür kılar.
/// </summary>
public static class PdfFixtures
{
    /// <summary>
    /// Verilen punto boyutlarında, her sayfasında siyah bir dikdörtgen olan PDF.
    /// Dikdörtgen render testlerinin "sayfa boş çıkmadı" kontrolü için gerekli.
    /// </summary>
    public static string Create(params (double WidthPt, double HeightPt)[] pages)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-{Guid.NewGuid():N}.pdf");

        using var document = new PdfDocument();
        foreach (var (width, height) in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(height);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.Black, 10, 10, width - 20, height - 20);
        }

        document.Save(path);
        return path;
    }
}
