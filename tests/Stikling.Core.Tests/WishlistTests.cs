using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Wishlist;

namespace Stikling.Core.Tests;

public class WishlistTests
{
    private static readonly PlantNameData Data = new()
    {
        Genera =
        [
            new GenusNames
            {
                Name = "Monstera",
                Species = ["adansonii", "deliciosa"],
                Synonyms = new Dictionary<string, string> { ["borsigiana"] = "Monstera deliciosa" },
                Cultivars = [new("Thai Constellation", "deliciosa")]
            },
            new GenusNames { Name = "Thaumatophyllum", Now = "Philodendron" },
            new GenusNames { Name = "Philodendron", Species = ["bipinnatifidum"] }
        ]
    };

    private static readonly EverydayNameData Everyday = new()
    {
        Language = "en",
        Plants = [new EverydayNames("Monstera", "deliciosa", null, ["Swiss cheese plant"])]
    };

    private static readonly PlantDictionary Dictionary = new(Data, [Everyday], "en");

    private static readonly FixedTime Time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    private static Wish Wish(string? genus = null, string? species = null, string? cultivar = null, string? name = null) =>
        new() { Genus = genus, Species = species, Cultivar = cultivar, Name = name };

    private static Plant Plant(string? genus, string? species = null, string? cultivar = null, string? nickname = null,
        PlantStatus status = PlantStatus.Active) =>
        new() { Genus = genus, Species = species, Cultivar = cultivar, Nickname = nickname, Status = status };

    [Fact]
    public void A_wish_needs_a_genus_or_a_name_of_its_own()
    {
        var today = new DateOnly(2026, 10, 14);

        Assert.NotEmpty(Wish().Validate(today));
        Assert.Empty(Wish("Monstera").Validate(today));
        Assert.Empty(Wish(name: "that pink syngonium").Validate(today));
        Assert.NotEmpty(new Wish { Name = "x", MostToPay = -1 }.Validate(today));
        Assert.NotEmpty(new Wish { Name = "x", GotOn = today.AddDays(1) }.Validate(today));
    }

    [Fact]
    public void A_wish_is_called_by_its_botanical_name_else_the_name_typed()
    {
        Assert.Equal("Monstera deliciosa 'Thai Constellation'", Wish("monstera", "Deliciosa", "Thai Constellation", "my wish").DisplayName);
        Assert.Equal("that pink syngonium", Wish(name: " that pink syngonium ").DisplayName);
    }

    [Fact]
    public void You_have_counts_the_plants_with_the_same_names_without_case()
    {
        var plants = new[]
        {
            Plant("monstera", "DELICIOSA", "thai constellation"),
            Plant("Monstera", "deliciosa", "Thai Constellation"),
            Plant("Monstera", "deliciosa", "Thai Constellation", status: PlantStatus.Died),
            Plant("Monstera", "deliciosa")
        };

        var have = WishMatches.Have(Wish("Monstera", "deliciosa", "Thai Constellation"), plants);

        Assert.Equal(2, have.Same);
        Assert.Equal("You have 2", have.Describe());
    }

    [Fact]
    public void Another_cultivar_of_the_species_is_said_when_there_is_no_same_one()
    {
        var have = WishMatches.Have(Wish("Monstera", "deliciosa", "Thai Constellation"), [Plant("Monstera", "deliciosa", "Albo")]);

        Assert.Equal("You have another Monstera deliciosa", have.Describe());
        Assert.Null(WishMatches.Have(Wish("Monstera", "adansonii"), [Plant("Monstera", "deliciosa")]).Describe());
        Assert.Null(WishMatches.Have(Wish("Monstera"), [Plant("Monstera", "deliciosa")]).Describe());
    }

    [Fact]
    public void An_old_name_counts_as_the_one_it_goes_by_now()
    {
        var plants = new[] { Plant("Monstera", "deliciosa"), Plant("Philodendron", "bipinnatifidum") };

        Assert.Equal("You have 1", WishMatches.Have(Wish("Monstera", "borsigiana"), plants, Dictionary).Describe());
        Assert.Null(WishMatches.Have(Wish("Monstera", "borsigiana"), plants).Describe());
        Assert.Equal("You have 1", WishMatches.Have(Wish("Thaumatophyllum", "bipinnatifidum"), plants, Dictionary).Describe());
    }

    [Fact]
    public void A_wish_with_no_genus_is_matched_on_its_name_and_a_nickname()
    {
        var plants = new[] { Plant("Syngonium", nickname: "That Pink Syngonium"), Plant("Syngonium", "podophyllum") };

        Assert.Equal("You have 1", WishMatches.Have(Wish(name: "that pink syngonium"), plants).Describe());
        Assert.Null(WishMatches.Have(Wish(name: "the blue one"), plants).Describe());
    }

    [Fact]
    public void The_add_form_finds_the_open_wishes_with_its_names()
    {
        var open = Wish("Monstera", "deliciosa", "Thai Constellation");
        var got = Wish("Monstera", "deliciosa", "Thai Constellation");
        got.GotOn = new DateOnly(2026, 10, 1);

        var found = WishMatches.OpenFor("monstera", "deliciosa", "thai constellation", null, [open, got, Wish("Monstera", "deliciosa")]);

        Assert.Same(open, Assert.Single(found));
        Assert.Empty(WishMatches.OpenFor(null, null, null, null, [Wish(name: "x")]));
    }

    [Fact]
    public void Search_finds_open_wishes_by_any_name_and_nothing_for_an_empty_search()
    {
        var thai = Wish("Monstera", "deliciosa", "Thai Constellation");
        var pink = Wish(name: "that pink syngonium");
        var got = Wish("Monstera", "deliciosa");
        got.GotOn = new DateOnly(2026, 10, 1);
        Wish[] all = [thai, pink, got];

        Assert.Equal([thai], WishSearch.OpenWishes(all, "thai monstera"));
        Assert.Equal([pink], WishSearch.OpenWishes(all, "pink"));
        Assert.Equal([thai], WishSearch.OpenWishes(all, "swiss cheese", Dictionary));
        var old = Wish("Monstera", "borsigiana");
        Assert.Equal([old], WishSearch.OpenWishes([old], "deliciosa", Dictionary));
        Assert.Empty(WishSearch.OpenWishes([old], "deliciosa"));
        Assert.Empty(WishSearch.OpenWishes(all, "  "));
    }

    [Fact]
    public void Search_finds_the_plants_and_propagations_you_already_have()
    {
        var plant = Plant("Monstera", "deliciosa");
        var gone = Plant("Monstera", "deliciosa", status: PlantStatus.Sold);
        var going = new Propagation { Genus = "Monstera", Species = "deliciosa", Stage = PropagationStage.Rooting };
        var failed = new Propagation { Genus = "Monstera", Species = "deliciosa", Stage = PropagationStage.Failed };

        Assert.Equal([plant], WishSearch.Plants([plant, gone], "monstera"));
        Assert.Equal([going], WishSearch.Propagations([going, failed], "deliciosa"));
        Assert.Equal([plant], WishSearch.Plants([plant, gone], "swiss", Dictionary));
    }

    [Fact]
    public async Task Got_it_keeps_the_wish_with_the_day_and_what_it_became()
    {
        var service = new WishService(new FakeWishRepository(), Time);
        var wish = Wish("Monstera");
        await service.SaveAsync(wish);
        var plant = Guid.NewGuid();

        Assert.True(await service.MarkGotAsync(wish.Id, plant, null));

        Assert.Equal(new DateOnly(2026, 10, 14), wish.GotOn);
        Assert.Equal(plant, wish.PlantId);
        Assert.Null(wish.PropagationId);
        Assert.False(await service.MarkGotAsync(wish.Id, Guid.NewGuid(), null));
        Assert.Equal(plant, wish.PlantId);
        Assert.False(await service.MarkGotAsync(Guid.NewGuid(), plant, null));
    }

    [Fact]
    public async Task A_got_wish_can_be_put_back()
    {
        var service = new WishService(new FakeWishRepository(), Time);
        var wish = Wish("Monstera");
        await service.SaveAsync(wish);
        await service.MarkGotAsync(wish.Id, null, Guid.NewGuid());

        await service.PutBackAsync(wish.Id);

        Assert.False(wish.IsGot);
        Assert.Null(wish.PropagationId);
    }

    private static string Text(WishlistTextOptions options, params Wish[] wishes) =>
        WishlistText.Write(wishes, Dictionary.EverydayNames, options);

    [Fact]
    public void The_text_lists_open_wishes_with_the_everyday_name_in_brackets()
    {
        var got = Wish("Ficus", "lyrata");
        got.GotOn = new DateOnly(2026, 10, 1);

        var text = Text(new WishlistTextOptions(AppLine: false),
            Wish("Monstera", "deliciosa"), Wish(name: "that pink syngonium"), Wish("Monstera", "adansonii", name: "the holey one"), got);

        Assert.Equal(
            "Plants I'm looking for\n- Monstera adansonii (the holey one)\n- Monstera deliciosa (Swiss cheese plant)\n- that pink syngonium",
            text);
    }

    [Fact]
    public void The_text_leaves_out_notes_and_price_unless_asked_and_ends_with_the_app_line()
    {
        var wish = Wish("Monstera", "deliciosa");
        wish.Notes = "Big fenestrations";
        wish.MostToPay = 250m;

        var plain = Text(new WishlistTextOptions(), wish);
        var full = Text(new WishlistTextOptions(Notes: true, MostToPay: true), wish);

        Assert.DoesNotContain("fenestrations", plain);
        Assert.DoesNotContain("250", plain);
        Assert.EndsWith("\n\nKept in Stikling, stikling.app", plain);
        Assert.Contains("\n  Big fenestrations\n  Would pay up to 250\n", full);
    }
}
