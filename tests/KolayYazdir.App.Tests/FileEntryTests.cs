using KolayYazdir.App.ViewModels;

namespace KolayYazdir.App.Tests;

public class FileEntryTests
{
    [Fact]
    public void The_display_name_is_the_file_name_only()
    {
        var entry = new FileEntry(@"C:\Users\arda\Downloads\kitapcik.pdf");

        Assert.Equal("kitapcik.pdf", entry.FileName);
    }

    [Fact]
    public void The_full_path_is_kept_for_loading()
    {
        var entry = new FileEntry(@"C:\Users\arda\Downloads\kitapcik.pdf");

        Assert.Equal(@"C:\Users\arda\Downloads\kitapcik.pdf", entry.Path);
    }

    [Fact]
    public void A_fresh_entry_has_no_error()
    {
        Assert.False(new FileEntry(@"C:\a.pdf").HasError);
    }

    [Fact]
    public void Setting_an_error_raises_the_error_flag()
    {
        var entry = new FileEntry(@"C:\a.pdf") { Error = "Dosya bozuk" };

        Assert.True(entry.HasError);
    }

    [Fact]
    public void Clearing_the_error_lowers_the_flag()
    {
        var entry = new FileEntry(@"C:\a.pdf") { Error = "Dosya bozuk" };

        entry.Error = null;

        Assert.False(entry.HasError);
    }

    [Fact]
    public void Page_label_counts_pages()
    {
        var entry = new FileEntry(@"C:\a.pdf") { PageCount = 12 };

        Assert.Equal("12 sf", entry.PageLabel);
    }

    [Fact]
    public void Page_label_shows_the_error_instead_when_loading_failed()
    {
        var entry = new FileEntry(@"C:\a.pdf") { PageCount = 0, Error = "Dosya bozuk" };

        Assert.Equal("Dosya bozuk", entry.PageLabel);
    }

    [Fact]
    public void Page_label_is_blank_before_loading()
    {
        Assert.Equal(string.Empty, new FileEntry(@"C:\a.pdf").PageLabel);
    }

    [Fact]
    public void Page_label_reports_progress_while_loading()
    {
        var entry = new FileEntry(@"C:\a.docx") { IsLoading = true };

        Assert.Equal("çevriliyor…", entry.PageLabel);
    }

    [Fact]
    public void Progress_outranks_a_stale_page_count()
    {
        var entry = new FileEntry(@"C:\a.docx") { PageCount = 3, IsLoading = true };

        Assert.Equal("çevriliyor…", entry.PageLabel);
    }

    [Fact]
    public void Changing_the_page_count_announces_the_label()
    {
        var entry = new FileEntry(@"C:\a.pdf");
        var announced = new List<string?>();
        entry.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        entry.PageCount = 4;

        // Arayüz PageLabel'a bağlı; haber verilmezse satır güncellenmez.
        Assert.Contains(nameof(FileEntry.PageLabel), announced);
    }
}
