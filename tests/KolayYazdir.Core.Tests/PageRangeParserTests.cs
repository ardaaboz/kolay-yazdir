using KolayYazdir.Core.Layout;

namespace KolayYazdir.Core.Tests;

public class PageRangeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_selects_every_page(string? text)
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, PageRangeParser.Parse(text, 4));
    }

    [Fact]
    public void Single_page_selects_that_page()
    {
        Assert.Equal(new[] { 2 }, PageRangeParser.Parse("3", 10));
    }

    [Fact]
    public void Range_is_inclusive_on_both_ends()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, PageRangeParser.Parse("1-5", 10));
    }

    [Fact]
    public void Mixed_list_keeps_document_order()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 7, 10, 11, 12 },
            PageRangeParser.Parse("1-5, 8, 11-13", 20));
    }

    [Fact]
    public void Overlapping_ranges_are_deduplicated()
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, PageRangeParser.Parse("1-3, 2-4", 10));
    }

    [Fact]
    public void Descending_range_is_read_as_ascending()
    {
        Assert.Equal(new[] { 2, 3, 4 }, PageRangeParser.Parse("5-3", 10));
    }

    [Fact]
    public void Pages_past_the_end_are_dropped()
    {
        Assert.Equal(new[] { 8, 9 }, PageRangeParser.Parse("9-40", 10));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-")]
    [InlineData("5-")]
    [InlineData("99")]
    public void Unusable_text_selects_nothing(string text)
    {
        Assert.Empty(PageRangeParser.Parse(text, 10));
    }

    [Fact]
    public void Semicolon_works_as_a_separator_too()
    {
        Assert.Equal(new[] { 0, 4 }, PageRangeParser.Parse("1; 5", 10));
    }
}
