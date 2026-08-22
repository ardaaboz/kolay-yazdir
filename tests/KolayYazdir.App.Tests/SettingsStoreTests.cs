using System.IO;
using KolayYazdir.App.Services;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kolayyazdir-settings").FullName;

    private SettingsStore Store() => new(Path.Combine(_root, "ayarlar.json"));

    [Fact]
    public void A_missing_file_yields_the_defaults()
    {
        var settings = Store().Load();

        Assert.Equal(PaperFormat.A4, settings.Paper);
        Assert.Equal(ColorMode.Monochrome, settings.Color);
        Assert.Equal(DuplexMode.Simplex, settings.Duplex);
        Assert.Equal(PagesPerSheet.One, settings.PagesPerSheet);
        Assert.False(settings.FitToPage);
        Assert.True(settings.AutoRotate);
        Assert.Equal(1, settings.Copies);
    }

    [Fact]
    public void Saved_settings_come_back()
    {
        Store().Save(new StoredSettings
        {
            Paper = PaperFormat.A3,
            Orientation = Orientation.Landscape,
            Color = ColorMode.Color,
            Duplex = DuplexMode.Duplex,
            PagesPerSheet = PagesPerSheet.Nine,
            FitToPage = true,
            AutoRotate = false,
            Copies = 5,
            MediaTypeId = 3,
            DefaultFolder = @"D:\Islerim"
        });

        var loaded = Store().Load();

        Assert.Equal(PaperFormat.A3, loaded.Paper);
        Assert.Equal(Orientation.Landscape, loaded.Orientation);
        Assert.Equal(ColorMode.Color, loaded.Color);
        Assert.Equal(DuplexMode.Duplex, loaded.Duplex);
        Assert.Equal(PagesPerSheet.Nine, loaded.PagesPerSheet);
        Assert.True(loaded.FitToPage);
        Assert.False(loaded.AutoRotate);
        Assert.Equal(5, loaded.Copies);
        Assert.Equal(3, loaded.MediaTypeId);
        Assert.Equal(@"D:\Islerim", loaded.DefaultFolder);
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_the_defaults()
    {
        File.WriteAllText(Path.Combine(_root, "ayarlar.json"), "{ bu json değil");

        // Bozuk ayar dosyası yüzünden uygulamanın açılmaması kabul edilemez.
        Assert.Equal(PaperFormat.A4, Store().Load().Paper);
    }

    [Fact]
    public void Saving_creates_the_folder_when_it_is_missing()
    {
        var nested = new SettingsStore(Path.Combine(_root, "yeni", "klasor", "ayarlar.json"));

        nested.Save(new StoredSettings { Copies = 2 });

        Assert.Equal(2, nested.Load().Copies);
    }

    [Fact]
    public void Enums_are_written_as_names_not_numbers()
    {
        Store().Save(new StoredSettings { Paper = PaperFormat.A3 });

        var json = File.ReadAllText(Path.Combine(_root, "ayarlar.json"));

        // İsimle yazmak, enum sırası değişirse ayarların kaymasını önler.
        Assert.Contains("A3", json);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
