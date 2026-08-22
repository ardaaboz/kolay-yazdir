using KolayYazdir.Core.Models;

namespace KolayYazdir.Documents;

/// <summary>
/// Seçilen dosyaların sayfalarını tek bir diziye dizer. Yerleşim motoru sadece
/// bu diziyi görür; hangi sayfanın hangi dosyadan geldiğini bilmez.
/// </summary>
public sealed class DocumentSet : IPageImageSource, IDisposable
{
    private readonly IReadOnlyList<SourceDocument> _documents;

    /// <summary>Birleşik indeksten (belge, o belgedeki sayfa) eşlemesi.</summary>
    private readonly List<(int Document, int Page)> _map = [];

    public DocumentSet(IReadOnlyList<SourceDocument> documents)
    {
        _documents = documents;

        var pages = new List<SourcePageInfo>();
        for (var d = 0; d < documents.Count; d++)
        {
            for (var p = 0; p < documents[d].PageCount; p++)
            {
                pages.Add(new SourcePageInfo(pages.Count, documents[d].PageSize(p)));
                _map.Add((d, p));
            }
        }

        Pages = pages;
    }

    public IReadOnlyList<SourcePageInfo> Pages { get; }

    public RasterPage Render(int sourceIndex, double dpi)
    {
        var (document, page) = Locate(sourceIndex);
        return _documents[document].Render(page, dpi);
    }

    public string FileNameOf(int sourceIndex) => _documents[Locate(sourceIndex).Document].FileName;

    private (int Document, int Page) Locate(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _map.Count)
            throw new ArgumentOutOfRangeException(nameof(sourceIndex), sourceIndex, "Böyle bir sayfa yok.");

        return _map[sourceIndex];
    }

    public void Dispose()
    {
        foreach (var document in _documents) document.Dispose();
    }
}
