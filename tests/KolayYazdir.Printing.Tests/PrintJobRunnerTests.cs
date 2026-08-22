using System.Drawing.Printing;
using KolayYazdir.Core.Layout;
using KolayYazdir.Core.Models;
using KolayYazdir.Documents;
using ColorMode = KolayYazdir.Core.Models.ColorMode;
using Orientation = KolayYazdir.Core.Models.Orientation;

namespace KolayYazdir.Printing.Tests;

public class PrintJobRunnerTests : IDisposable
{
    private const string VirtualPrinter = "Microsoft Print to PDF";

    private readonly string _output = Directory.CreateTempSubdirectory("kolayyazdir-print").FullName;

    private static bool HasVirtualPrinter =>
        PrinterSettings.InstalledPrinters.Cast<string>().Contains(VirtualPrinter);

    /// <summary>Her sayfayı siyah bir kare olarak veren kaynak.</summary>
    private sealed class BlackSquareSource : IPageImageSource
    {
        public SizePt PageSize(int sourceIndex) => new(200, 200);

        public RasterPage Render(int sourceIndex, double dpi)
        {
            var bytes = new byte[64 * 64 * 4];
            for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            return new RasterPage(64, 64, bytes);
        }
    }

    private static PrintJobRunner Runner() => new(new SheetRenderer(new BlackSquareSource()));

    private static IReadOnlyList<Sheet> Sheets(int pageCount, PrintSettings settings)
    {
        var paper = Paper.SizeOf(settings.Paper, settings.Orientation);
        var pages = Enumerable.Range(0, pageCount).Select(i => new SourcePageInfo(i, paper)).ToList();

        return LayoutEngine.Build(pages, settings, new RectPt(0, 0, paper.Width, paper.Height));
    }

    [SkippableFact]
    public void Devmode_carries_the_paper_size_and_orientation()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(
            settings,
            new PrintSettings { Paper = PaperFormat.A3, Orientation = Orientation.Landscape },
            driverHandlesCopies: true);

        Assert.True(settings.DefaultPageSettings.Landscape);
        Assert.Equal(PaperKind.A3, settings.DefaultPageSettings.PaperSize.Kind);
    }

    [SkippableFact]
    public void Devmode_carries_the_copy_count_when_the_driver_handles_copies()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(settings, new PrintSettings { Copies = 4 }, driverHandlesCopies: true);

        Assert.Equal(4, settings.Copies);
    }

    [SkippableFact]
    public void Devmode_leaves_copies_at_one_when_the_app_repeats_sheets()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        // Aksi halde kopya sayısının karesi kadar kağıt çıkardı.
        DevModeConfigurator.Apply(settings, new PrintSettings { Copies = 4 }, driverHandlesCopies: false);

        Assert.Equal(1, settings.Copies);
    }

    [SkippableFact]
    public void Devmode_switches_the_paper_size()
    {
        var settings = new PrinterSettings();
        Skip.IfNot(settings.IsValid, "Varsayılan yazıcı yok.");

        DevModeConfigurator.Apply(settings, new PrintSettings { Paper = PaperFormat.A5 }, driverHandlesCopies: true);

        Assert.Equal(PaperKind.A5, settings.DefaultPageSettings.PaperSize.Kind);
    }

    [SkippableFact]
    public void A_job_reaches_the_virtual_printer_and_produces_a_file()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "cikti.pdf");
        var settings = new PrintSettings();

        Runner().Run(Sheets(3, settings), settings, VirtualPrinter, driverHandlesCopies: false, target);

        Assert.True(File.Exists(target), "sanal yazıcı dosya üretmedi");
        Assert.True(new FileInfo(target).Length > 0);
    }

    [SkippableFact]
    public void The_produced_file_has_one_page_per_sheet()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "sayfa-sayisi.pdf");
        var settings = new PrintSettings { PagesPerSheet = PagesPerSheet.Four };
        var sheets = Sheets(8, settings);

        Runner().Run(sheets, settings, VirtualPrinter, driverHandlesCopies: false, target);

        using var produced = new PdfRasterizer(target);
        Assert.Equal(sheets.Count, produced.PageCount);
    }

    [SkippableFact]
    public void Blank_backs_still_come_out_as_paper()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "bos-arka.pdf");
        var settings = new PrintSettings { Duplex = DuplexMode.Duplex };
        var sheets = Sheets(3, settings);

        Runner().Run(sheets, settings, VirtualPrinter, driverHandlesCopies: false, target);

        // 3 sayfa dupleks = 4 yüz (son arka yüz boş). Boş yüz atlanırsa deste kayar.
        using var produced = new PdfRasterizer(target);
        Assert.Equal(4, produced.PageCount);
    }

    [SkippableFact]
    public void App_side_copies_multiply_the_printed_pages()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var target = Path.Combine(_output, "kopya.pdf");
        var settings = new PrintSettings { Copies = 3 };
        var sheets = LayoutEngine.Repeat(Sheets(2, settings), 3);

        Runner().Run(sheets, settings, VirtualPrinter, driverHandlesCopies: false, target);

        using var produced = new PdfRasterizer(target);
        Assert.Equal(6, produced.PageCount);
    }

    [Fact]
    public void An_empty_job_is_a_no_op()
    {
        // Yazıcıya hiç gitmeden dönmeli; geçersiz yazıcı adı bunu kanıtlar.
        Runner().Run([], new PrintSettings(), "Böyle Bir Yazıcı Yok 12345", driverHandlesCopies: true);
    }

    [Fact]
    public void An_unknown_printer_is_reported()
    {
        var settings = new PrintSettings();

        Assert.Throws<InvalidPrinterException>(() =>
            Runner().Run(Sheets(1, settings), settings, "Böyle Bir Yazıcı Yok 12345", driverHandlesCopies: true));
    }

    public void Dispose()
    {
        try { Directory.Delete(_output, recursive: true); } catch (IOException) { }
    }
}
