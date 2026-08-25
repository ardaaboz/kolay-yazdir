namespace KolayYazdir.Printing.Tests;

public class PaperTypeResolverTests
{
    /// <summary>Bir lazer yazıcının tipik kağıt cinsi listesi.</summary>
    private static readonly MediaType[] TypicalDriver =
    [
        new(1, "Düz"),
        new(257, "Kalın 1"),
        new(258, "Kalın 2"),
        new(259, "Kalın 3"),
        new(260, "Etiket"),
        new(261, "Asetat"),
        new(262, "Zarf"),
        new(263, "Geri Dönüşümlü"),
        new(264, "Parlak Foto")
    ];

    [Fact]
    public void Plain_picks_the_driver_entry_named_duz()
    {
        var media = PaperTypeResolver.Resolve(TypicalDriver, PaperType.Plain);

        Assert.Equal(1, media.Id);
        Assert.Equal("Düz", media.Name);
    }

    [Fact]
    public void Thick_picks_the_first_thick_step_not_the_second()
    {
        // Kullanıcı "Kalın 1 yeterli" dedi; Kalın 2/3 seçilirse yanlış ısı ayarı gider.
        var media = PaperTypeResolver.Resolve(TypicalDriver, PaperType.Thick);

        Assert.Equal(257, media.Id);
        Assert.Equal("Kalın 1", media.Name);
    }

    [Fact]
    public void Envelopes_and_labels_are_never_chosen()
    {
        var plain = PaperTypeResolver.Resolve(TypicalDriver, PaperType.Plain);
        var thick = PaperTypeResolver.Resolve(TypicalDriver, PaperType.Thick);

        Assert.DoesNotContain("Zarf", plain.Name);
        Assert.DoesNotContain("Etiket", plain.Name);
        Assert.DoesNotContain("Zarf", thick.Name);
        Assert.DoesNotContain("Asetat", thick.Name);
    }

    [Theory]
    [InlineData("Plain")]
    [InlineData("PLAIN PAPER")]
    [InlineData("Normal")]
    [InlineData("DÜZ")]
    [InlineData("duz")]
    public void English_and_uppercase_driver_names_still_match_plain(string name)
    {
        var media = PaperTypeResolver.Resolve([new MediaType(9, name), new MediaType(7, "Zarf")], PaperType.Plain);

        Assert.Equal(9, media.Id);
    }

    [Theory]
    [InlineData("Thick")]
    [InlineData("HEAVY")]
    [InlineData("KALIN 1")]
    [InlineData("Kalin")]
    [InlineData("Kart Stoğu")]
    public void English_and_uppercase_driver_names_still_match_thick(string name)
    {
        var media = PaperTypeResolver.Resolve([new MediaType(1, "Düz"), new MediaType(42, name)], PaperType.Thick);

        Assert.Equal(42, media.Id);
    }

    [Fact]
    public void An_empty_driver_list_falls_back_to_standard_constants()
    {
        Assert.Equal(1, PaperTypeResolver.Resolve([], PaperType.Plain).Id);
        Assert.Equal(3, PaperTypeResolver.Resolve([], PaperType.Thick).Id);
    }

    [Fact]
    public void A_driver_with_no_thick_option_falls_back_rather_than_guessing()
    {
        // Yanlış bir girdiyi kalın diye seçmek, kağıdı yakabilir veya sıkıştırabilir.
        var media = PaperTypeResolver.Resolve([new MediaType(1, "Düz"), new MediaType(7, "Zarf")], PaperType.Thick);

        Assert.Equal(3, media.Id);
    }

    [Fact]
    public void A_driver_with_no_plain_name_uses_its_first_entry()
    {
        var media = PaperTypeResolver.Resolve([new MediaType(88, "Otomatik"), new MediaType(7, "Zarf")], PaperType.Plain);

        Assert.Equal(88, media.Id);
    }

    [Fact]
    public void The_resolved_name_is_shown_to_the_user_so_the_mapping_is_visible()
    {
        // Eşleme yanlışsa kullanıcı arayüzde görebilmeli.
        Assert.Equal("Kalın 1", PaperTypeResolver.Resolve(TypicalDriver, PaperType.Thick).Name);
    }
}
