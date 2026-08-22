using System.Printing;

namespace KolayYazdir.Printing;

/// <summary>Yazıcının o anki durumu ve kullanıcıya gösterilecek kısa açıklaması.</summary>
/// <param name="IsHealthy">Baskı sorunsuz çıkacak gibi görünüyorsa true.</param>
/// <param name="Description">"hazır", "kağıt bitti" gibi tek kelimelik durum.</param>
public readonly record struct PrinterHealth(bool IsHealthy, string Description)
{
    private static readonly PrinterHealth Ready = new(true, "hazır");

    /// <summary>
    /// Yazıcı kuyruğunu sorgular. Durum okunamıyorsa "hazır" varsayılır:
    /// sorgu başarısız diye kullanıcıyı yazdırmaktan alıkoymanın anlamı yok,
    /// iş zaten kuyruğa girer.
    /// </summary>
    public static PrinterHealth Read(string printerName)
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(printerName);
            queue.Refresh();

            // Sıra önemli: en çok müdahale gerektiren durum önce söylensin.
            if (queue.IsPaperJammed) return new PrinterHealth(false, "kağıt sıkıştı");
            if (queue.IsOutOfPaper) return new PrinterHealth(false, "kağıt bitti");
            if (queue.IsDoorOpened) return new PrinterHealth(false, "kapağı açık");
            if (queue.IsOffline) return new PrinterHealth(false, "çevrimdışı");
            if (queue.IsNotAvailable) return new PrinterHealth(false, "ulaşılamıyor");
            if (queue.IsPaused) return new PrinterHealth(false, "duraklatıldı");
            if (queue.IsTonerLow) return new PrinterHealth(true, "toner azaldı");

            return Ready;
        }
        catch (Exception ex) when (ex is PrintQueueException or PrintServerException or InvalidOperationException)
        {
            return Ready;
        }
    }
}
