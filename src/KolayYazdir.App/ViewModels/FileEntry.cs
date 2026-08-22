using CommunityToolkit.Mvvm.ComponentModel;

namespace KolayYazdir.App.ViewModels;

/// <summary>Dosya listesindeki bir satır.</summary>
public sealed partial class FileEntry(string path) : ObservableObject
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileName(path);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private int _pageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    private bool _isLoading;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>
    /// Satırın sağındaki küçük yazı. Word/Excel dönüşümü uzun sürebildiği için
    /// beklerken kullanıcıya ne olduğunu söylüyoruz.
    /// </summary>
    public string PageLabel => IsLoading
        ? "çevriliyor…"
        : HasError ? Error! : PageCount > 0 ? $"{PageCount} sf" : string.Empty;
}
