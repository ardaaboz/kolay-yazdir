using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

/// <summary>
/// COM otomasyonunun süre sınırı. Eski Word, görünmez bir kip penceresi
/// açtığında (varsayılan uygulama uyarısı gibi) çağrı hiç dönmez; sınır yoksa
/// zincir orada asılı kalır ve LibreOffice'e hiç sıra gelmez.
/// </summary>
public class StaTaskTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task A_result_is_returned_from_the_sta_thread()
    {
        var value = await StaTask.RunAsync(() => "bitti", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal("bitti", value);
    }

    [Fact]
    public async Task The_work_runs_on_a_single_threaded_apartment()
    {
        var state = await StaTask.RunAsync(
            () => Thread.CurrentThread.GetApartmentState(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.Equal(ApartmentState.STA, state);
    }

    [Fact]
    public async Task A_failure_keeps_its_original_exception()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => StaTask.RunAsync<string>(
                () => throw new InvalidOperationException("içerideki gerçek sebep"),
                TimeSpan.FromSeconds(30),
                CancellationToken.None));

        Assert.Equal("içerideki gerçek sebep", error.Message);
    }

    [Fact]
    public async Task Work_that_never_finishes_times_out()
    {
        var released = new ManualResetEventSlim(false);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => StaTask.RunAsync(() => { released.Wait(); return "asla"; }, Short, CancellationToken.None));
        }
        finally
        {
            released.Set();
        }
    }

    [Fact]
    public async Task Cancellation_is_observed_while_the_work_hangs()
    {
        var released = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();

        var pending = StaTask.RunAsync(
            () => { released.Wait(); return "asla"; },
            TimeSpan.FromSeconds(30),
            cancellation.Token);

        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            released.Set();
        }
    }
}
