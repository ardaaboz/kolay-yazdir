using System.Runtime.InteropServices;

namespace KolayYazdir.Printing.Interop;

/// <summary>
/// Windows yazdırma yığınına doğrudan erişim. .NET'in
/// <c>System.Drawing.Printing</c> katmanı kağıt cinsini ve çevirme kenarını
/// açmadığı için bu kadarını elle yapıyoruz.
/// </summary>
internal static class NativeMethods
{
    // DeviceCapabilities sorgu numaraları (wingdi.h)
    internal const int DC_DUPLEX = 7;
    internal const int DC_COLORDEVICE = 32;
    internal const int DC_MEDIATYPENAMES = 34;
    internal const int DC_MEDIATYPES = 35;

    /// <summary>Kağıt cinsi isimleri sabit 64 karakterlik alanlarda döner.</summary>
    internal const int MediaTypeNameLength = 64;

    [DllImport("winspool.drv", EntryPoint = "DeviceCapabilitiesW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int DeviceCapabilities(
        string device, string? port, int capability, IntPtr output, IntPtr deviceMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalUnlock(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GlobalFree(IntPtr handle);
}
