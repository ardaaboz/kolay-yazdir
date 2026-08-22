using System.Runtime.InteropServices;
using KolayYazdir.Core.Models;

namespace KolayYazdir.Printing.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DevMode
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
    public short dmSpecVersion;
    public short dmDriverVersion;
    public short dmSize;
    public short dmDriverExtra;
    public int dmFields;
    public short dmOrientation;
    public short dmPaperSize;
    public short dmPaperLength;
    public short dmPaperWidth;
    public short dmScale;
    public short dmCopies;
    public short dmDefaultSource;
    public short dmPrintQuality;
    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
    public short dmLogPixels;
    public int dmBitsPerPel;
    public int dmPelsWidth;
    public int dmPelsHeight;
    public int dmDisplayFlags;
    public int dmDisplayFrequency;
    public int dmICMMethod;
    public int dmICMIntent;
    public int dmMediaType;
    public int dmDitherType;
    public int dmReserved1;
    public int dmReserved2;
    public int dmPanningWidth;
    public int dmPanningHeight;
}

/// <summary>DEVMODE alan bayrakları ve değerleri (wingdi.h).</summary>
internal static class DevModeFields
{
    public const int Orientation = 0x00000001;
    public const int PaperSize = 0x00000002;
    public const int Copies = 0x00000100;
    public const int Color = 0x00000800;
    public const int Duplex = 0x00001000;
    public const int Collate = 0x00008000;
    public const int MediaType = 0x02000000;

    public const short OrientPortrait = 1;
    public const short OrientLandscape = 2;

    public const short ColorMonochrome = 1;
    public const short ColorColour = 2;

    public const short DuplexSimplex = 1;

    /// <summary>Uzun kenardan çevir — dikey kağıtta kitap gibi açılır.</summary>
    public const short DuplexVertical = 2;

    /// <summary>Kısa kenardan çevir — yatay kağıtta bloknot gibi açılır.</summary>
    public const short DuplexHorizontal = 3;

    public const short CollateTrue = 1;

    public const short PaperA3 = 8;
    public const short PaperA4 = 9;
    public const short PaperA5 = 11;

    public static short PaperCode(PaperFormat format) => format switch
    {
        PaperFormat.A4 => PaperA4,
        PaperFormat.A5 => PaperA5,
        PaperFormat.A3 => PaperA3,
        _ => PaperA4
    };
}
