using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Core.Tests;

public class PrintSettingsTests
{
    [Fact]
    public void Default_fit_to_page_is_false()
    {
        // Gerçek boyut korunması kritiktir: kimlik fotosu ve ölçülü çıktı gibi uygulamalar
        // için fiziksel boyut önemlidir. Fit-to-page kapalı olunca içerik kendi boyutunda
        // basılır ve ancak küçülür. Varsayılan true olursa böyle çıktılar hatalı ölçüye sahip olur.
        var settings = new PrintSettings();

        Assert.False(settings.FitToPage);
    }

    [Fact]
    public void Default_auto_rotate_is_true()
    {
        var settings = new PrintSettings();

        Assert.True(settings.AutoRotate);
    }

    [Fact]
    public void Other_defaults_are_sensible()
    {
        var settings = new PrintSettings();

        Assert.Equal(PaperFormat.A4, settings.Paper);
        Assert.Equal(Orientation.Portrait, settings.Orientation);
        Assert.Equal(ColorMode.Monochrome, settings.Color);
        Assert.Equal(DuplexMode.Simplex, settings.Duplex);
        Assert.Equal(PagesPerSheet.One, settings.PagesPerSheet);
        Assert.Equal(1, settings.Copies);
        Assert.Null(settings.PageRange);
        Assert.Null(settings.MediaTypeId);
    }

    [Fact]
    public void Binding_is_long_edge_for_portrait()
    {
        var settings = new PrintSettings { Orientation = Orientation.Portrait };

        Assert.Equal(DuplexBinding.LongEdge, settings.Binding);
    }

    [Fact]
    public void Binding_is_short_edge_for_landscape()
    {
        var settings = new PrintSettings { Orientation = Orientation.Landscape };

        Assert.Equal(DuplexBinding.ShortEdge, settings.Binding);
    }
}
