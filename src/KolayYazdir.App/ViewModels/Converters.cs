using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace KolayYazdir.App.ViewModels;

/// <summary>Değer null ise görünür, doluysa gizli.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Ayar değerlerinin arayüzde görünen Türkçe karşılıkları. Kod içinde
/// tanımlayıcılar İngilizce kalır, kullanıcı Türkçe görür.
/// </summary>
public sealed class TurkishLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        PaperFormat paper => paper.ToString(),
        Orientation.Portrait => "Dikey",
        Orientation.Landscape => "Yatay",
        ColorMode.Color => "Renkli",
        ColorMode.Monochrome => "Siyah beyaz",
        DuplexMode.Simplex => "Tek yön",
        DuplexMode.Duplex => "Önlü arkalı",
        PagesPerSheet pages => ((int)pages).ToString(CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>GDI+ bitmap'ini WPF'in gösterebileceği biçime çevirir.</summary>
public static class BitmapConverter
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var source = BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgra32, null,
                data.Scan0, data.Stride * bitmap.Height, data.Stride);

            // Dondurmak, arka planda üretilip arayüz iş parçacığında
            // gösterilmesini güvenli kılar.
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
