using Stikling.Core.Compare;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Wishlist;
using static Stikling.Core.Tests.CompareTestData;

namespace Stikling.Core.Tests;

public class ListTextReaderTests
{
    private static ListTextResult Read(string? text) => ListTextReader.Read(text, Dictionary);

    private static string SwapText() => SwapLists.WriteSwap(
        [
            new Plant { Genus = "Monstera", Species = "deliciosa", Cultivar = "Albo", Spare = SpareMark.UpForSwap, CollectionNumber = "117" },
            new Plant { Genus = "Hoya", Species = "carnosa", Spare = SpareMark.CuttingsToSpare },
            new Plant { Nickname = "Big fern", Spare = SpareMark.UpForSwap },
            new Plant { Genus = "Alocasia", Species = "zebrina" },
        ],
        [new Propagation { Genus = "Hoya", Species = "kerrii", InitialCount = 3, SpareCount = 2, Stage = PropagationStage.Rooted, StartedOn = new DateOnly(2026, 10, 1) }],
        (g, s, c) => g == "Monstera" ? "Swiss cheese plant" : null, nicknames: false, footer: true);

    private static void CheckSwap(ListTextResult read)
    {
        Assert.Equal(ListTextKind.Plants, read.Kind);
        Assert.Equal(4, read.Plants.Count);

        var cutting = Assert.Single(read.Plants, p => p.Spare == FriendSpare.Cuttings);
        Assert.Equal(("Hoya", "kerrii"), (cutting.Genus, cutting.Species));
        Assert.Equal(2, cutting.SpareCount);
        Assert.Equal(PropagationStage.Rooted, cutting.Stage);
        Assert.Equal(SubjectType.Propagation, cutting.Type);

        var onRequest = Assert.Single(read.Plants, p => p.Spare == FriendSpare.CuttingsToSpare);
        Assert.Equal(("Hoya", "carnosa"), (onRequest.Genus, onRequest.Species));

        var albo = Assert.Single(read.Plants, p => p.Genus == "Monstera");
        Assert.Equal(FriendSpare.UpForSwap, albo.Spare);
        Assert.Equal(("deliciosa", "Albo"), (albo.Species, albo.Cultivar));
        Assert.Contains("Swiss cheese plant", albo.Name);
        Assert.DoesNotContain("117", albo.Name);

        var fern = Assert.Single(read.Plants, p => p.Genus is null);
        Assert.Equal("Big fern", fern.Name);
        Assert.Equal(FriendSpare.UpForSwap, fern.Spare);
        Assert.Empty(read.UnreadLines);
        Assert.Empty(read.Wishes);
    }

    [Fact]
    public void Reads_the_swap_list_it_writes_in_English() => CheckSwap(Read(SwapText()));

    [Fact]
    public void Reads_the_swap_list_it_writes_in_Danish() => CheckSwap(In("da", () => Read(SwapText())));

    private static string PlantsText() => SwapLists.WritePlants(
        [
            new Plant { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation", CollectionNumber = "42" },
            new Plant { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation", CollectionNumber = "42" },
            new Plant { Genus = "Hoya", Species = "carnosa", Cultivar = "Krimson Queen", Spare = SpareMark.UpForSwap },
            new Plant { Genus = "Alocasia", Species = "zebrina", Nickname = "Zeb", Spare = SpareMark.CuttingsToSpare },
            new Plant { Nickname = "Grandma's fern" },
        ],
        (g, s, c) => g == "Monstera" ? "Swiss cheese plant" : null, nicknames: true, footer: true);

    private static void CheckPlants(ListTextResult read)
    {
        Assert.Equal(ListTextKind.Plants, read.Kind);
        Assert.Equal(4, read.Plants.Count);
        Assert.Empty(read.UnreadLines);

        var thai = Assert.Single(read.Plants, p => p.Cultivar == "Thai Constellation");
        Assert.Equal(("Monstera", "deliciosa"), (thai.Genus, thai.Species));
        Assert.Equal(FriendSpare.None, thai.Spare);
        Assert.DoesNotContain("×", thai.Name);
        Assert.DoesNotContain("#42", thai.Name);

        Assert.Equal(FriendSpare.UpForSwap, Assert.Single(read.Plants, p => p.Genus == "Hoya").Spare);
        var zeb = Assert.Single(read.Plants, p => p.Genus == "Alocasia");
        Assert.Equal(FriendSpare.CuttingsToSpare, zeb.Spare);
        Assert.Equal("zebrina", zeb.Species);
        Assert.Equal("Grandma's fern", Assert.Single(read.Plants, p => p.Genus is null).Name);
    }

    [Fact]
    public void Reads_the_plant_list_it_writes_in_English() => CheckPlants(Read(PlantsText()));

    [Fact]
    public void Reads_the_plant_list_it_writes_in_Danish() => CheckPlants(In("da", () => Read(PlantsText())));

    private static string WishlistTextOf() => WishlistText.Write(
        [
            new Wish { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation", Notes = "Variegated, please", MostToPay = 40m },
            new Wish { Genus = "Hoya", Species = "carnosa" },
            new Wish { Name = "that pink syngonium" },
        ],
        Dictionary.EverydayNames, new WishlistTextOptions(Notes: true, MostToPay: true));

    private static void CheckWishlist(ListTextResult read)
    {
        Assert.Equal(ListTextKind.Wishlist, read.Kind);
        Assert.Empty(read.Plants);
        Assert.Equal(3, read.Wishes.Count);
        Assert.Empty(read.UnreadLines);

        var thai = Assert.Single(read.Wishes, w => w.Cultivar == "Thai Constellation");
        Assert.Equal(("Monstera", "deliciosa"), (thai.Genus, thai.Species));
        var hoya = Assert.Single(read.Wishes, w => w.Genus == "Hoya");
        Assert.Equal("carnosa", hoya.Species);
        Assert.Equal("that pink syngonium", Assert.Single(read.Wishes, w => w.Genus is null).Name);
    }

    [Fact]
    public void Reads_the_wishlist_it_writes_in_English_and_skips_notes_and_prices()
    {
        var text = WishlistTextOf();
        Assert.Contains("Would pay up to", text);
        CheckWishlist(Read(text));
    }

    [Fact]
    public void Reads_the_wishlist_it_writes_in_Danish() => CheckWishlist(In("da", () => Read(WishlistTextOf())));

    [Fact]
    public void A_plain_list_is_in_both_lists_and_split_on_commas_and_semicolons()
    {
        var read = Read("Monstera deliciosa, Hoya carnosa 'Krimson Queen'; Alocasia\nbig fern");

        Assert.Equal(ListTextKind.Plain, read.Kind);
        Assert.Equal(4, read.Plants.Count);
        Assert.Equal(4, read.Wishes.Count);
        Assert.Equal("Krimson Queen", read.Plants[1].Cultivar);
        Assert.Equal("Alocasia", read.Wishes[2].Genus);
        Assert.Null(read.Plants[3].Genus);
        Assert.Equal("big fern", read.Wishes[3].Name);
    }

    [Fact]
    public void Commas_only_split_a_plain_list()
    {
        var read = Read("Monstera deliciosa\nHoya, Alocasia\nKept in Stikling, stikling.app");

        // The footer makes it a plant list, so a line is a plant and the commas stay inside it
        Assert.Equal(ListTextKind.Plants, read.Kind);
        Assert.Equal(2, read.Plants.Count);
    }

    [Fact]
    public void Nothing_typed_is_empty()
    {
        Assert.Equal(ListTextKind.Empty, Read(null).Kind);
        Assert.Equal(ListTextKind.Empty, Read(" \n  \r\n").Kind);
    }

    [Fact]
    public void Numbers_counts_everyday_names_and_nicknames_are_kept_out_of_the_match()
    {
        var read = Read("#117 Monstera deliciosa (Swiss cheese plant) \"Big one\" ×2, up for swap\n\nKept in Stikling, stikling.app");

        var plant = Assert.Single(read.Plants);
        Assert.Equal(("Monstera", "deliciosa"), (plant.Genus, plant.Species));
        Assert.Equal(FriendSpare.UpForSwap, plant.Spare);
        Assert.Equal("Monstera deliciosa (Swiss cheese plant) \"Big one\"", plant.Name);
    }

    [Fact]
    public void A_quoted_cultivar_is_taken_even_when_the_dictionary_does_not_know_it()
    {
        var read = Read("Hoya kerrii 'Variegata'\nPhilodendron 'Pink Princess'\nKept in Stikling, stikling.app");

        Assert.Equal(("Hoya", "kerrii", "Variegata"), (read.Plants[0].Genus, read.Plants[0].Species, read.Plants[0].Cultivar));
        Assert.Equal(("Philodendron", null, "Pink Princess"), (read.Plants[1].Genus, read.Plants[1].Species, read.Plants[1].Cultivar));
    }

    [Fact]
    public void Lines_with_no_letters_or_too_long_are_unread()
    {
        var long130 = new string('a', 130);
        var read = Read($"Plants I'm looking for\n- Monstera\n- 12345\n- {long130}\n\n  a note under it");

        Assert.Equal(ListTextKind.Wishlist, read.Kind);
        Assert.Single(read.Wishes);
        Assert.Equal(["12345", long130], read.UnreadLines);
    }

    [Fact]
    public void Headings_are_matched_without_case()
    {
        Assert.Equal(ListTextKind.Wishlist, Read("PLANTS I'M LOOKING FOR\n- Hoya").Kind);
        Assert.Equal(ListTextKind.Wishlist, Read("planter jeg leder efter\n- Hoya").Kind);
    }
}
