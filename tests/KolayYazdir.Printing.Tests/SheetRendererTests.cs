using System.Drawing;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;

namespace KolayYazdir.Printing.Tests;

public class SheetRendererTests
{
    private static readonly SizePt A4 = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);
    private static readonly RectPt FullBleed = new(0, 0, A4.Width, A4.Height);

    /// <summary>
    /// Her sayfayı düz renkli bir kare olarak veren sahte kaynak. İstenen DPI
    /// değerlerini kaydeder, böylece çözünürlük seçimi de test edilebilir.
    /// </summary>
    private sealed class SolidColourSource(Color colour, double pageSidePt = 200, int sizePx = 64) : IPageImageSource
    {
        public List<double> RequestedDpi { get; } = [];

        public SizePt PageSize(int sourceIndex) => new(pageSidePt, pageSidePt);

        public RasterPage Render(int sourceIndex, double dpi)
        {
            RequestedDpi.Add(dpi);

            var bytes = new byte[sizePx * sizePx * 4];
            for (var i = 0; i < bytes.Length; i += 4)
            {
                bytes[i] = colour.B;
                bytes[i + 1] = colour.G;
                bytes[i + 2] = colour.R;
                bytes[i + 3] = 255;
            }

            return new RasterPage(sizePx, sizePx, bytes);
        }
    }

    private static IReadOnlyList<Sheet> Sheets(int pageCount, PagesPerSheet nUp, bool fitToPage = true)
    {
        var pages = Enumerable.Range(0, pageCount)
            .Select(i => new SourcePageInfo(i, new SizePt(200, 200)))
            .ToList();

        return LayoutEngine.Build(
            pages,
            new PrintSettings { PagesPerSheet = nUp, FitToPage = fitToPage },
            FullBleed);
    }

    /// <summary>Bir noktadaki pikselin koyu olup olmadığı.</summary>
    private static bool IsDark(Bitmap bitmap, double fractionX, double fractionY)
    {
        var pixel = bitmap.GetPixel((int)(bitmap.Width * fractionX), (int)(bitmap.Height * fractionY));
        return pixel.R < 128 && pixel.G < 128 && pixel.B < 128;
    }

    [Fact]
    public void Rendered_bitmap_matches_the_paper_aspect_ratio()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Color);

        Assert.InRange(bitmap.Width, 594, 597);
        Assert.InRange(bitmap.Height, 840, 843);
    }

    [Fact]
    public void A_blank_sheet_renders_all_white()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var blank = new Sheet(0, SheetSide.Back, A4, []);

        using var bitmap = renderer.RenderToBitmap(blank, dpi: 72, ColorMode.Color);

        Assert.False(IsDark(bitmap, 0.5, 0.5));
        Assert.False(IsDark(bitmap, 0.25, 0.25));
    }

    [Fact]
    public void One_up_content_lands_in_the_middle_of_the_page()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.5, 0.5), "sayfanın ortası dolu olmalı");
    }

    [Fact]
    public void Four_up_fills_all_four_quadrants()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));

        using var bitmap = renderer.RenderToBitmap(Sheets(4, PagesPerSheet.Four)[0], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.25, 0.25), "sol üst hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.75, 0.25), "sağ üst hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.25, 0.75), "sol alt hücre dolu olmalı");
        Assert.True(IsDark(bitmap, 0.75, 0.75), "sağ alt hücre dolu olmalı");
    }

    [Fact]
    public void Cells_left_empty_by_a_partial_sheet_stay_white()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var sheets = Sheets(5, PagesPerSheet.Four);

        using var bitmap = renderer.RenderToBitmap(sheets[1], dpi: 72, ColorMode.Color);

        Assert.True(IsDark(bitmap, 0.25, 0.25), "ikinci yaprakta ilk hücre dolu olmalı");
        Assert.False(IsDark(bitmap, 0.75, 0.75), "kalan hücreler boş kalmalı");
    }

    [Fact]
    public void Monochrome_turns_colour_into_grey()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Red));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Monochrome);

        var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);

        Assert.Equal(pixel.R, pixel.G);
        Assert.Equal(pixel.G, pixel.B);
    }

    [Fact]
    public void Colour_mode_keeps_the_original_hue()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Red));

        using var bitmap = renderer.RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], dpi: 72, ColorMode.Color);

        var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);

        Assert.True(pixel.R > 200 && pixel.G < 80 && pixel.B < 80, $"beklenen kırmızı, gelen {pixel}");
    }

    [Fact]
    public void Higher_dpi_produces_a_proportionally_larger_bitmap()
    {
        var renderer = new SheetRenderer(new SolidColourSource(Color.Black));
        var sheet = Sheets(1, PagesPerSheet.One)[0];

        using var low = renderer.RenderToBitmap(sheet, dpi: 72, ColorMode.Color);
        using var high = renderer.RenderToBitmap(sheet, dpi: 144, ColorMode.Color);

        Assert.InRange(high.Width, low.Width * 2 - 2, low.Width * 2 + 2);
    }

    [Fact]
    public void A_page_shrunk_into_a_small_cell_is_requested_at_a_lower_resolution()
    {
        // 35'li yerleşimde her sayfayı tam A4 çözünürlüğünde render etmek boşa iş.
        var oneUp = new SolidColourSource(Color.Black);
        var manyUp = new SolidColourSource(Color.Black);

        new SheetRenderer(oneUp).RenderToBitmap(Sheets(1, PagesPerSheet.One)[0], 300, ColorMode.Color);
        new SheetRenderer(manyUp).RenderToBitmap(Sheets(35, PagesPerSheet.ThirtyFive)[0], 300, ColorMode.Color);

        Assert.True(manyUp.RequestedDpi[0] < oneUp.RequestedDpi[0] / 3,
            $"35'li yerleşimde çok daha düşük çözünürlük beklenir; " +
            $"1'li {oneUp.RequestedDpi[0]:F0}, 35'li {manyUp.RequestedDpi[0]:F0}");
    }

    [Fact]
    public void The_source_resolution_never_drops_below_the_floor()
    {
        var source = new SolidColourSource(Color.Black, pageSidePt: 20000);

        new SheetRenderer(source).RenderToBitmap(Sheets(35, PagesPerSheet.ThirtyFive)[0], 300, ColorMode.Color);

        Assert.All(source.RequestedDpi, dpi => Assert.True(dpi >= RenderConstants.MinimumSourceDpi));
    }

    [Fact]
    public void Every_page_on_the_sheet_is_drawn()
    {
        var source = new SolidColourSource(Color.Black);

        new SheetRenderer(source).RenderToBitmap(Sheets(9, PagesPerSheet.Nine)[0], 72, ColorMode.Color);

        Assert.Equal(9, source.RequestedDpi.Count);
    }

    /// <summary>
    /// Sol üst çeyreği siyah, kalanı beyaz bir sayfa. Döndürmenin yönünü
    /// ayırt etmek için asimetrik olması şart: kare ve düz renkli bir kaynak
    /// yanlış yöne dönse bile aynı görünürdü.
    /// </summary>
    private sealed class CornerMarkedSource(SizePt pageSize, int sizePx = 80) : IPageImageSource
    {
        public SizePt PageSize(int sourceIndex) => pageSize;

        public RasterPage Render(int sourceIndex, double dpi)
        {
            var bytes = new byte[sizePx * sizePx * 4];
            for (var y = 0; y < sizePx; y++)
            for (var x = 0; x < sizePx; x++)
            {
                var i = (y * sizePx + x) * 4;
                var black = x < sizePx / 2 && y < sizePx / 2;
                var value = (byte)(black ? 0 : 255);

                bytes[i] = value;
                bytes[i + 1] = value;
                bytes[i + 2] = value;
                bytes[i + 3] = 255;
            }

            return new RasterPage(sizePx, sizePx, bytes);
        }
    }

    [Fact]
    public void An_unrotated_page_keeps_its_marked_corner_at_the_top_left()
    {
        var sheet = new Sheet(0, SheetSide.Front, A4,
            [new PlacedPage(0, new RectPt(0, 0, 400, 400), 0)]);

        using var bitmap = new SheetRenderer(new CornerMarkedSource(new SizePt(400, 400)))
            .RenderToBitmap(sheet, dpi: 72, ColorMode.Color);

        Assert.True(IsDarkAtPoint(bitmap, 100, 100), "işaretli köşe sol üstte kalmalı");
        Assert.False(IsDarkAtPoint(bitmap, 300, 100), "sağ üst beyaz olmalı");
    }

    [Fact]
    public void A_rotated_page_turns_clockwise_and_stays_inside_its_destination()
    {
        // Hedef 400x200 yatay, kaynak 200x400 dikey: 90° saat yönünde dönünce
        // kaynağın sol üst köşesi hedefin SAĞ üstüne gelir.
        var sheet = new Sheet(0, SheetSide.Front, A4,
            [new PlacedPage(0, new RectPt(0, 0, 400, 200), 90)]);

        using var bitmap = new SheetRenderer(new CornerMarkedSource(new SizePt(200, 400)))
            .RenderToBitmap(sheet, dpi: 72, ColorMode.Color);

        Assert.True(IsDarkAtPoint(bitmap, 300, 50), "işaretli köşe sağ üste gelmeli");
        Assert.False(IsDarkAtPoint(bitmap, 100, 50), "sol üst beyaz olmalı");

        // Dönmüş içerik hedef dikdörtgenin dışına taşmamalı.
        Assert.False(IsDarkAtPoint(bitmap, 300, 260), "hedefin altı boş kalmalı");
    }

    /// <summary>Punto koordinatındaki noktanın koyu olup olmadığı (72 DPI'da 1 punto = 1 piksel).</summary>
    private static bool IsDarkAtPoint(Bitmap bitmap, int xPt, int yPt)
    {
        var pixel = bitmap.GetPixel(xPt, yPt);
        return pixel.R < 128 && pixel.G < 128 && pixel.B < 128;
    }
}
