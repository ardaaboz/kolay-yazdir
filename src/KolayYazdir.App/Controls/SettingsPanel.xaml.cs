using System.Windows.Controls;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Printing;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App.Controls;

/// <summary>
/// Yazdırma ayarlarının tamamı. Kendi seçeneklerini kendisi doldurur, böylece
/// yükseklik ölçüsü pencereye bağlı kalmadan sınanabiliyor.
/// </summary>
public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();

        PaperSelector.ItemsSource = new object[] { PaperFormat.A4, PaperFormat.A5, PaperFormat.A3 };
        OrientationSelector.ItemsSource = new object[] { Orientation.Portrait, Orientation.Landscape };
        ColorSelector.ItemsSource = new object[] { ColorMode.Color, ColorMode.Monochrome };
        DuplexSelector.ItemsSource = new object[] { DuplexMode.Simplex, DuplexMode.Duplex };
        PaperTypeSelector.ItemsSource = new object[] { PaperType.Plain, PaperType.Thick };
        NUpSelector.ItemsSource = new object[]
        {
            PagesPerSheet.One, PagesPerSheet.Two, PagesPerSheet.Four,
            PagesPerSheet.Nine, PagesPerSheet.Sixteen, PagesPerSheet.ThirtyFive
        };
    }
}
