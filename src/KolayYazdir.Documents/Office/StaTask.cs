namespace KolayYazdir.Documents.Office;

/// <summary>
/// COM otomasyonunu tek iş parçacıklı apartmanda, süre sınırıyla çalıştırır.
///
/// Sınır şart: eski Word, otomasyonla açıldığında görünmez bir kip penceresi
/// çıkarabiliyor ("Word varsayılan uygulama değil, değiştirilsin mi?"). Pencere
/// görünmez olduğu için kimse kapatamaz ve çağrı hiç dönmez. Sınır yokken bu,
/// zinciri olduğu yerde donduruyor ve LibreOffice'e hiç sıra gelmiyordu.
/// </summary>
public static class StaTask
{
    public static Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken ct)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            using var filter = OleMessageFilter.Install();

            try { completion.TrySetResult(work()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        })
        {
            // Asılı kalırsa uygulamanın kapanmasını engellememeli.
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return AwaitWithTimeout(completion.Task, timeout, ct);
    }

    /// <summary>
    /// Süre dolduğunda iş parçacığını öldürmeye çalışmıyoruz: COM çağrısının
    /// ortasında bir iş parçacığını kesmek Word'ü yarım durumda bırakır. Arka
    /// plan iş parçacığı olduğu için süreçle birlikte gider; biz sonucu
    /// beklemeyi bırakıp bir sonraki dönüştürücüye geçiyoruz.
    /// </summary>
    private static async Task<T> AwaitWithTimeout<T>(Task<T> pending, TimeSpan timeout, CancellationToken ct)
    {
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(ct);
        expiry.CancelAfter(timeout);

        try
        {
            return await pending.WaitAsync(expiry.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"İşlem {timeout.TotalSeconds:0} saniyede tamamlanmadı; yanıt vermiyor.");
        }
    }
}
