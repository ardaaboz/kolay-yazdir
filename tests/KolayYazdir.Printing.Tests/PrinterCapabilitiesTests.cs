using System.Drawing.Printing;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Printing.Tests;

public class PrinterCapabilitiesTests
{
    private const string VirtualPrinter = "Microsoft Print to PDF";

    private static bool HasVirtualPrinter =>
        PrinterSettings.InstalledPrinters.Cast<string>().Contains(VirtualPrinter);

    [SkippableFact]
    public void A_known_printer_can_be_read()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait);

        Assert.NotNull(capabilities);
        Assert.Equal(VirtualPrinter, capabilities.PrinterName);
    }

    [Fact]
    public void An_unknown_printer_returns_null_instead_of_throwing()
    {
        Assert.Null(PrinterCapabilities.Read("Böyle Bir Yazıcı Yok 12345", PaperFormat.A4, Orientation.Portrait));
    }

    [SkippableFact]
    public void Media_types_are_never_empty()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait)!;

        // Sürücü liste vermezse yedek "Düz / Kalın" eşlemesi devreye girer;
        // kullanıcı her hâlükârda bir seçenek görmeli.
        Assert.NotEmpty(capabilities.MediaTypes);
        Assert.All(capabilities.MediaTypes, m => Assert.False(string.IsNullOrWhiteSpace(m.Name)));
    }

    [SkippableFact]
    public void Printable_area_fits_inside_the_paper()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Portrait)!;
        var paper = Paper.SizeOf(PaperFormat.A4, Orientation.Portrait);

        Assert.True(capabilities.PrintableArea.Width > 0);
        Assert.True(capabilities.PrintableArea.Width <= paper.Width + 1);
        Assert.True(capabilities.PrintableArea.Height <= paper.Height + 1);
    }

    [SkippableFact]
    public void Landscape_printable_area_is_wider_than_it_is_tall()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var capabilities = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A4, Orientation.Landscape)!;

        Assert.True(capabilities.PrintableArea.Width > capabilities.PrintableArea.Height,
            "yatay kağıtta basılabilir alan da yatay olmalı");
    }

    [SkippableFact]
    public void A_bigger_paper_reports_a_bigger_printable_area()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var a5 = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A5, Orientation.Portrait)!;
        var a3 = PrinterCapabilities.Read(VirtualPrinter, PaperFormat.A3, Orientation.Portrait)!;

        Assert.True(a3.PrintableArea.Width > a5.PrintableArea.Width);
    }

    [SkippableFact]
    public void A_default_printer_name_is_reported_when_one_exists()
    {
        Skip.If(PrinterSettings.InstalledPrinters.Count == 0, "Bu makinede hiç yazıcı yok.");

        Assert.False(string.IsNullOrWhiteSpace(PrinterCapabilities.DefaultPrinterName));
    }
}
