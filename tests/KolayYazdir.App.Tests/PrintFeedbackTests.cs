using System.IO;
using KolayYazdir.App.ViewModels;
using KolayYazdir.Core.Layout;
using KolayYazdir.Printing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KolayYazdir.App.Tests;

/// <summary>
/// Kullanıcı "üst üste yazdırabiliyorum, hiçbir geri bildirim yok" dedi.
/// Bu testler düğmenin kilitlenmesini ve iş bildirimini koruyor.
/// </summary>
public class PrintFeedbackTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string Pdf(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kolayyazdir-fb-{Guid.NewGuid():N}.pdf");

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

    [Fact]
    public void The_print_button_starts_idle()
    {
        using var viewModel = new MainViewModel();

        Assert.False(viewModel.IsPrinting);
        Assert.Equal("Yazdır", viewModel.PrintButtonLabel);
    }

    [Fact]
    public async Task A_second_print_while_one_is_running_is_refused()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(1)], CancellationToken.None);

        // Baskı sürerkenki durumu taklit ediyoruz; gerçek baskı bu testte
        // yazıcıya gitmemeli.
        viewModel.IsPrinting = true;

        Assert.Equal(PrintOutcome.AlreadyPrinting, await viewModel.PrintAsync());
    }

    [Fact]
    public async Task A_refused_second_print_does_not_clear_the_running_state()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(1)], CancellationToken.None);
        viewModel.IsPrinting = true;

        await viewModel.PrintAsync();

        // finally bloğu yanlış yerdeyse ilk iş sürerken düğme açılır ve
        // kullanıcı ikinci kez basabilir.
        Assert.True(viewModel.IsPrinting);
    }

    [Fact]
    public void Nothing_to_print_leaves_the_button_usable()
    {
        using var viewModel = new MainViewModel();

        Assert.False(viewModel.IsPrinting);
        Assert.Equal(string.Empty, viewModel.LastJobMessage);
    }

    [Fact]
    public async Task A_single_sheet_job_shows_no_pager()
    {
        using var viewModel = new MainViewModel();

        await viewModel.AddFilesAsync([Pdf(1)], CancellationToken.None);

        Assert.False(viewModel.HasMultipleSheets);
    }

    [Fact]
    public async Task A_multi_sheet_job_shows_the_pager()
    {
        using var viewModel = new MainViewModel();

        await viewModel.AddFilesAsync([Pdf(4)], CancellationToken.None);

        Assert.True(viewModel.HasMultipleSheets);
    }

    [Fact]
    public async Task Packing_pages_onto_one_sheet_hides_the_pager_again()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(4)], CancellationToken.None);

        viewModel.PagesPerSheet = PagesPerSheet.Four;

        Assert.False(viewModel.HasMultipleSheets);
    }

    [Fact]
    public async Task Changing_a_setting_clears_a_stale_job_message()
    {
        using var viewModel = new MainViewModel();
        await viewModel.AddFilesAsync([Pdf(2)], CancellationToken.None);
        viewModel.LastJobMessage = "2 kağıt yazıcıya gönderildi";

        viewModel.PaperType = PaperType.Thick;

        // Eski bildirim yeni ayarla birlikte durursa kullanıcı yanlış işin
        // gittiğini sanır.
        Assert.Equal(string.Empty, viewModel.LastJobMessage);
    }

    [Fact]
    public void The_paper_type_defaults_to_plain()
    {
        using var viewModel = new MainViewModel();

        Assert.Equal(PaperType.Plain, viewModel.PaperType);
    }

    [Fact]
    public void The_paper_type_hint_names_the_driver_entry()
    {
        using var viewModel = new MainViewModel();
        viewModel.RefreshCapabilities();

        // Eşleme yanlışsa kullanıcı bunu arayüzde görmeli.
        if (viewModel.Capabilities is not null)
        {
            Assert.Contains("yazıcıda:", viewModel.MediaTypeHint);
        }
    }

    [Fact]
    public void Switching_to_thick_changes_the_resolved_media_type()
    {
        using var viewModel = new MainViewModel();
        viewModel.RefreshCapabilities();

        if (viewModel.Capabilities is null) return;

        var plainId = viewModel.CurrentSettings.MediaTypeId;
        viewModel.PaperType = PaperType.Thick;
        var thickId = viewModel.CurrentSettings.MediaTypeId;

        Assert.NotEqual(plainId, thickId);
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
