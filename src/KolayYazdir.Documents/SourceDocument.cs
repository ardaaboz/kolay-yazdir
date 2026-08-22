using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Kullanıcının seçtiği tek bir dosya. Türü ne olursa olsun (görsel, PDF, Word,
/// Excel) dışarıya aynı yüzü gösterir.
/// </summary>
public sealed class SourceDocument(string path, IPageRasterizer rasterizer) : IDisposable
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileName(path);

    public int PageCount => rasterizer.PageCount;

    public SizePt PageSize(int index) => rasterizer.PageSize(index);

    public RasterPage Render(int index, double dpi) => rasterizer.Render(index, dpi);

    public void Dispose() => rasterizer.Dispose();
}
