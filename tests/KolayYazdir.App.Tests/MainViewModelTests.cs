using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.App.Tests;

/// <summary>
/// Görünüm modelinin gerçek dosyalarla uçtan uca çalıştığını doğrular: dosya
/// eklenince yerleşim kuruluyor mu, önizleme çiziliyor mu, ayar değişince
/// yeniden hesaplanıyor mu. Alt katmanların testleri bu kablolamayı görmez.
/// </summary>
public class MainViewModelTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string Pdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-vm-{Guid.NewGuid():N}.pdf");

        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(595.276);
            page.Height = XUnit.FromPoint(841.890);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.Black, 40, 40, 200, 200);
        }

        document.Save(path);
        _temporaryFiles.Add(path);
        return path;
    }

    private string Image(int widthPx, int heightPx, float dpi)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-vm-{Guid.NewGuid():N}.png");

        using var bitmap = new Bitmap(widthPx, heightPx);
        bitmap.SetResolution(dpi, dpi);
        using (var gfx = Graphics.FromImage(bitmap)) gfx.Clear(Color.Blue);
        bitmap.Save(path, ImageFormat.Png);

        _temporaryFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Önizleme çizimi arka planda koştuğu için sonucu beklemek gerekiyor.
    /// </summary>
    private static async Task<bool> WaitForPreview(MainViewModel viewModel, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (viewModel.PreviewImage is not null) return true;
            await Task.Delay(50);
        }

        return false;
    }

    [Fact]
    public async Task Adding_a_pdf_builds_the_job_summary()
    {
        using var viewModel = new MainViewModel();

        await viewModel.AddFilesAsync([Pdf(3)], CancellationToken.None);

        Assert.Single(viewModel.Files);
        Assert.Equal(3, viewModel.Files[0].PageCount);
        Assert.Equal("3 yaprak · 3 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task Adding_a_pdf_draws_a_preview()
    {
        using var viewModel = new MainViewModel();

        await viewModel.AddFilesAsync([Pdf(2)], CancellationToken.None);

        Assert.True(await WaitForPreview(viewModel, TimeSpan.FromSeconds(20)),
            "önizleme görüntüsü üretilmedi");
    }

    [Fact]
    public async Task Two_files_are_combined_into_one_job()
    {
        using var viewModel = new MainViewModel();

        await viewModel.AddFilesAsync([Pdf(2), Image(600, 400, 96)], CancellationToken.None);

        Assert.Equal(2, viewModel.Files.Count);
        Assert.Equal("3 yaprak · 3 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task Four_up_reduces_the_sheet_count()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(8)], CancellationToken.None);

        viewModel.PagesPerSheet = PagesPerSheet.Four;

        Assert.Equal("2 yaprak · 8 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task Duplex_pairs_the_sheets_and_labels_the_sides()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(4)], CancellationToken.None);

        viewModel.Duplex = DuplexMode.Duplex;

        Assert.Equal("2 yaprak · 4 sayfa", viewModel.JobSummary);
        Assert.Equal("Yüz 1 / 4 · ön", viewModel.SheetLabel);
    }

    [Fact]
    public async Task The_binding_hint_follows_the_orientation()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(2)], CancellationToken.None);

        viewModel.Duplex = DuplexMode.Duplex;
        Assert.Contains("uzun kenardan", viewModel.BindingHint);

        viewModel.Orientation = Orientation.Landscape;
        Assert.Contains("kısa kenardan", viewModel.BindingHint);
    }

    [Fact]
    public void The_binding_hint_is_empty_for_single_sided_jobs()
    {
        using var viewModel = new MainViewModel();

        Assert.Equal(string.Empty, viewModel.BindingHint);
    }

    [Fact]
    public async Task A_page_range_narrows_the_job()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(10)], CancellationToken.None);

        viewModel.PageRange = "2-4";

        Assert.Equal("3 yaprak · 10 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task A_broken_file_is_marked_and_the_rest_still_print()
    {
        var broken = Path.Combine(Path.GetTempPath(), $"bozuk-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(broken, "bu bir pdf değil");
        _temporaryFiles.Add(broken);

        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(2), broken], CancellationToken.None);

        var brokenEntry = viewModel.Files.Single(f => f.Path == broken);
        Assert.True(brokenEntry.HasError);

        // Bozuk dosya işi durdurmamalı; sağlam olan basılabilir kalmalı.
        Assert.Equal("2 yaprak · 2 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task Removing_a_file_updates_the_job()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(2), Pdf(3)], CancellationToken.None);

        await viewModel.RemoveFileAsync(viewModel.Files[0], CancellationToken.None);

        Assert.Single(viewModel.Files);
        Assert.Equal("3 yaprak · 3 sayfa", viewModel.JobSummary);
    }

    [Fact]
    public async Task Reordering_files_changes_which_page_comes_first()
    {
        using var viewModel = new MainViewModel();
        var first = Pdf(1);
        var second = Pdf(1);
        await viewModel.AddFilesAsync([first, second], CancellationToken.None);

        await viewModel.MoveFileAsync(1, 0, CancellationToken.None);

        Assert.Equal(second, viewModel.Files[0].Path);
    }

    [Fact]
    public async Task The_same_file_is_not_added_twice()
    {
        using var viewModel = new MainViewModel();
        var path = Pdf(1);

        await viewModel.AddFilesAsync([path], CancellationToken.None);
        await viewModel.AddFilesAsync([path], CancellationToken.None);

        Assert.Single(viewModel.Files);
    }

    [Fact]
    public async Task Unsupported_files_are_ignored_on_add()
    {
        var text = Path.Combine(Path.GetTempPath(), $"not-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(text, "merhaba");
        _temporaryFiles.Add(text);

        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([text], CancellationToken.None);

        Assert.Empty(viewModel.Files);
    }

    [Fact]
    public async Task Adding_a_file_remembers_its_folder()
    {
        using var viewModel = new MainViewModel();
        var path = Pdf(1);

        await viewModel.AddFilesAsync([path], CancellationToken.None);

        Assert.Equal(Path.GetDirectoryName(path), viewModel.DefaultFolder);
    }

    [Fact]
    public void Printing_with_no_files_asks_for_files_instead_of_failing()
    {
        using var viewModel = new MainViewModel();

        Assert.Equal(PrintOutcome.NothingToPrint, viewModel.Print());
    }

    [Fact]
    public async Task Copies_are_repeated_by_the_app_when_the_driver_cannot()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(2)], CancellationToken.None);
        viewModel.Copies = 3;

        var sheets = viewModel.SheetsForPrinting();

        // Sürücü kopyalıyorsa yapraklar tekrarlanmaz; kopyalamıyorsa 3 katı olur.
        Assert.Equal(viewModel.DriverHandlesCopies ? 2 : 6, sheets.Count);
    }

    [Fact]
    public async Task Settings_survive_a_round_trip_through_the_view_model()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(1)], CancellationToken.None);

        viewModel.PaperSize = PaperFormat.A3;
        viewModel.Color = ColorMode.Color;
        viewModel.FitToPage = true;

        var settings = viewModel.CurrentSettings;

        Assert.Equal(PaperFormat.A3, settings.Paper);
        Assert.Equal(ColorMode.Color, settings.Color);
        Assert.True(settings.FitToPage);
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
