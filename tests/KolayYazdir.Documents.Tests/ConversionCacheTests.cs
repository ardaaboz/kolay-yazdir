using KolayYazdir.Documents.Office;

namespace KolayYazdir.Documents.Tests;

public class ConversionCacheTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kolayyazdir-cache").FullName;

    private string WriteSource(string content)
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.docx");
        File.WriteAllText(path, content);
        return path;
    }

    private string WritePdf()
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}-cikti.pdf");
        File.WriteAllText(path, "%PDF-1.4");
        return path;
    }

    [Fact]
    public void Unknown_source_is_a_miss()
    {
        var cache = new ConversionCache(_root);

        Assert.Null(cache.Lookup(WriteSource("merhaba")));
    }

    [Fact]
    public void Stored_conversion_is_found_again()
    {
        var cache = new ConversionCache(_root);
        var source = WriteSource("merhaba");

        var stored = cache.Store(source, WritePdf());

        Assert.Equal(stored, cache.Lookup(source));
        Assert.True(File.Exists(stored));
    }

    [Fact]
    public void Editing_the_source_invalidates_the_entry()
    {
        var cache = new ConversionCache(_root);
        var source = WriteSource("merhaba");
        cache.Store(source, WritePdf());

        File.WriteAllText(source, "değişti ve uzadı");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(5));

        Assert.Null(cache.Lookup(source));
    }

    [Fact]
    public void A_deleted_cache_file_is_reported_as_a_miss()
    {
        var cache = new ConversionCache(_root);
        var source = WriteSource("merhaba");
        var stored = cache.Store(source, WritePdf());

        File.Delete(stored);

        Assert.Null(cache.Lookup(source));
    }

    [Fact]
    public void Two_different_sources_do_not_share_an_entry()
    {
        var cache = new ConversionCache(_root);
        var first = WriteSource("birinci");
        var second = WriteSource("ikinci belge");

        cache.Store(first, WritePdf());

        Assert.NotNull(cache.Lookup(first));
        Assert.Null(cache.Lookup(second));
    }

    [Fact]
    public void Storing_twice_replaces_the_earlier_conversion()
    {
        var cache = new ConversionCache(_root);
        var source = WriteSource("merhaba");

        var first = cache.Store(source, WritePdf());
        var second = cache.Store(source, WritePdf());

        Assert.Equal(first, second);
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void The_stored_file_is_moved_not_copied()
    {
        var cache = new ConversionCache(_root);
        var source = WriteSource("merhaba");
        var produced = WritePdf();

        cache.Store(source, produced);

        // Geçici çalışma klasörü sonradan siliniyor; PDF orada kalmamalı.
        Assert.False(File.Exists(produced));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}
