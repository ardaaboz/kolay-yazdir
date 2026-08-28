using System.Runtime.InteropServices;

namespace KolayYazdir.Documents.Office;

/// <summary>
/// COM çağrısı reddedildiğinde yeniden dener.
///
/// Word meşgulken (açılışta, bir pencere çıkarırken, kendi işini yaparken)
/// gelen otomasyon çağrılarını "RPC_E_CALL_REJECTED" ile geri çevirir. Süzgeç
/// olmadan bu, ilk saniyelerde anlamsız bir başarısızlık olarak görünür —
/// dükkandaki arıza tam olarak buydu. Süzgeçle kısa bir süre sabırla
/// beklenir; Word gerçekten kilitliyse yine de pes edilir ve hata yukarı
/// çıkar, çağrı sonsuza kadar asılı kalmaz.
/// </summary>
internal sealed class OleMessageFilter : IDisposable
{
    /// <summary>Sunucu meşgul, sonra tekrar dene.</summary>
    private const int ServerCallRetryLater = 2;

    /// <summary>Reddedilen çağrıyı bu aralıklarla tekrarla.</summary>
    private const int RetryDelayMs = 250;

    /// <summary>
    /// Bu süreden sonra pes edilir. Amaç gerçek hatayı görünür kılmak: sonsuz
    /// deneme, dıştaki süre sınırının "yanıt vermiyor" mesajına dönüşür ve
    /// asıl sebep kaybolur.
    /// </summary>
    private const int GiveUpAfterMs = 30_000;

    private readonly IOleMessageFilter? _previous;
    private bool _disposed;

    private OleMessageFilter(IOleMessageFilter? previous) => _previous = previous;

    /// <summary>STA iş parçacığında çağrılmalıdır.</summary>
    public static OleMessageFilter? Install()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return null;

        try
        {
            CoRegisterMessageFilter(new Filter(), out var previous);
            return new OleMessageFilter(previous);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Süzgeç bir iyileştirme; kurulamıyorsa dönüşüm yine denenmeli.
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { CoRegisterMessageFilter(_previous, out _); }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(
        IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

    private sealed class Filter : IOleMessageFilter
    {
        /// <summary>Gelen çağrılara izin ver (SERVERCALL_ISHANDLED).</summary>
        public int HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr interfaceInfo) => 0;

        public int RetryRejectedCall(IntPtr callee, int tickCount, int rejectType)
        {
            if (rejectType != ServerCallRetryLater) return -1;

            // tickCount: çağrının başlamasından bu yana geçen milisaniye.
            return tickCount < GiveUpAfterMs ? RetryDelayMs : -1;
        }

        /// <summary>Beklerken ileti kuyruğunu işlemeye devam et (PENDINGMSG_WAITDEFPROCESS).</summary>
        public int MessagePending(IntPtr callee, int tickCount, int pendingType) => 2;
    }

    [ComImport]
    [Guid("00000016-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleMessageFilter
    {
        [PreserveSig]
        int HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr interfaceInfo);

        [PreserveSig]
        int RetryRejectedCall(IntPtr callee, int tickCount, int rejectType);

        [PreserveSig]
        int MessagePending(IntPtr callee, int tickCount, int pendingType);
    }
}
