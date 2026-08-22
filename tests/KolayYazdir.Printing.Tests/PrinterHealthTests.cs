using System.Drawing.Printing;

namespace KolayYazdir.Printing.Tests;

public class PrinterHealthTests
{
    private const string VirtualPrinter = "Microsoft Print to PDF";

    private static bool HasVirtualPrinter =>
        PrinterSettings.InstalledPrinters.Cast<string>().Contains(VirtualPrinter);

    [SkippableFact]
    public void A_working_printer_reports_ready()
    {
        Skip.IfNot(HasVirtualPrinter, $"'{VirtualPrinter}' bu makinede kurulu değil.");

        var health = PrinterHealth.Read(VirtualPrinter);

        Assert.True(health.IsHealthy);
        Assert.Equal("hazır", health.Description);
    }

    [Fact]
    public void An_unknown_printer_is_assumed_ready_rather_than_blocking_the_user()
    {
        // Durum sorgusu başarısız diye yazdırmayı engellemenin anlamı yok.
        var health = PrinterHealth.Read("Böyle Bir Yazıcı Yok 12345");

        Assert.True(health.IsHealthy);
    }

    [Fact]
    public void The_description_is_never_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(PrinterHealth.Read("Böyle Bir Yazıcı Yok 12345").Description));
    }
}
