namespace KolayYazdir.Core.Models;

/// <summary>
/// Yerleşim motorunun bir kaynak sayfa hakkında bilmesi gereken her şey.
/// Motor sayfanın içeriğini görmez, sadece sırasını ve boyutunu bilir.
/// </summary>
/// <param name="Index">Birleştirilmiş sayfa dizisindeki sıfır tabanlı yeri.</param>
/// <param name="Size">Sayfanın punto cinsinden boyutu.</param>
public readonly record struct SourcePageInfo(int Index, SizePt Size);
