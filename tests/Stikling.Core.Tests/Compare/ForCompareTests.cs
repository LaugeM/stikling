using Stikling.Core.Compare;
using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class ForCompareTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static PublicPageSettings Settings(bool cuttings = true, bool swap = false, bool wishlist = false, bool stories = true) =>
        new(null, null, PublicPageLayout.Shelves, PublicPageOrder.Name, cuttings, swap, wishlist, stories);

    private static Plant APlant(string? nickname, string? genus = null, string? species = null, string? cultivar = null, Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = nickname, Genus = genus, Species = species, Cultivar = cultivar, CreatedAt = Now, UpdatedAt = Now };
        change?.Invoke(plant);
        return plant;
    }

    private static Propagation ACutting(string? nickname, string? genus = null, Action<Propagation>? change = null)
    {
        var cutting = new Propagation { Nickname = nickname, Genus = genus, StartedOn = Today, CreatedAt = Now, UpdatedAt = Now };
        change?.Invoke(cutting);
        return cutting;
    }

    private static Photo APhoto(Entity subject, SubjectType type, Action<Photo>? change = null)
    {
        var photo = new Photo { SubjectType = type, SubjectId = subject.Id, TakenAt = Now, UpdatedAt = Now };
        change?.Invoke(photo);
        return photo;
    }

    private static FriendList Compare(PublicPageSettings settings, IEnumerable<Plant>? plants = null, IEnumerable<Propagation>? cuttings = null,
        IEnumerable<Wish>? wishes = null, IEnumerable<Photo>? photos = null, bool swapRequests = false, string? contact = null) =>
        PublicCollectionPage.ForCompare(
            PublicCollectionPage.Subjects(settings, plants ?? [], cuttings ?? [], wishes ?? []), photos ?? [], swapRequests, contact);

    [Fact]
    public void Ids_are_left_out_when_the_page_has_no_stories_to_link_to()
    {
        var plant = APlant(null, "Monstera", "deliciosa");
        Assert.Null(Assert.Single(Compare(Settings(stories: false), [plant]).Plants).Id);
        Assert.Equal(plant.Id, Assert.Single(Compare(Settings(stories: true), [plant]).Plants).Id);
    }

    [Fact]
    public void Plants_come_with_the_botanical_parts_the_tile_shows_only_when_they_have_a_genus()
    {
        var named = APlant(null, "Monstera", "deliciosa", "Albo");
        var nicknamed = APlant("Big one", "Monstera", "deliciosa");
        var bare = APlant("Grandma's fern");

        var list = Compare(Settings(), [named, nicknamed, bare]);

        var albo = Assert.Single(list.Plants, p => p.Id == named.Id);
        Assert.Equal(("Monstera deliciosa 'Albo'", "Monstera", "deliciosa", "Albo"), (albo.Name, albo.Genus, albo.Species, albo.Cultivar));
        var big = Assert.Single(list.Plants, p => p.Id == nicknamed.Id);
        Assert.Equal(("Big one", "Monstera", "deliciosa", null), (big.Name, big.Genus, big.Species, big.Cultivar));
        var fern = Assert.Single(list.Plants, p => p.Id == bare.Id);
        Assert.Equal(("Grandma's fern", null, null, null), (fern.Name, fern.Genus, fern.Species, fern.Cultivar));
    }

    [Fact]
    public void A_species_that_is_not_known_is_not_passed_on()
    {
        var sp = APlant(null, "Hoya", "carnosa", change: p => p.NameQualifier = NameQualifier.Sp);

        Assert.Null(Assert.Single(Compare(Settings(), [sp]).Plants).Species);
    }

    [Fact]
    public void Hidden_deleted_and_gone_plants_and_cuttings_and_hidden_photos_are_never_included()
    {
        var shown = APlant("Shown");
        var hidden = APlant("Hidden", change: p => p.HiddenFromPublicPage = true);
        var deleted = APlant("Deleted", change: p => p.DeletedAt = Now);
        var gave = APlant("Gave", change: p => p.Status = PlantStatus.GivenAway);
        var demo = APlant("Demo", change: p => p.Id = ExampleFamily.MotherId);
        var going = ACutting("Going");
        var hiddenCutting = ACutting("Hidden cutting", change: c => { c.HiddenFromPublicPage = true; c.SpareCount = 1; c.InitialCount = 1; });
        var doneCutting = ACutting("Done cutting", change: c => c.Stage = PropagationStage.Done);
        var hiddenPhoto = APhoto(shown, SubjectType.Plant, p => p.HiddenFromPublicPage = true);
        var shownPhoto = APhoto(shown, SubjectType.Plant, p => p.TakenAt = Now.AddDays(-5));
        var reference = APhoto(going, SubjectType.Propagation, p => p.IsReference = true);

        var list = Compare(Settings(swap: true), [shown, hidden, deleted, gave, demo], [going, hiddenCutting, doneCutting],
            photos: [hiddenPhoto, shownPhoto, reference]);

        Assert.Equal(["Shown", "Going"], list.Plants.Select(p => p.Name));
        Assert.Equal(shownPhoto.Id, list.Plants[0].Cover?.Id);
        Assert.Null(list.Plants[1].Cover);
    }

    [Fact]
    public void Cuttings_show_only_while_the_part_is_on_or_while_they_have_some_to_swap()
    {
        var going = ACutting("Going");
        var spare = ACutting("Spare", change: c => { c.InitialCount = 3; c.SpareCount = 2; c.Stage = PropagationStage.Rooted; });

        Assert.Empty(Compare(Settings(cuttings: false), cuttings: [going, spare]).Plants);
        Assert.Equal(["Going", "Spare"], Compare(Settings(cuttings: true), cuttings: [going, spare]).Plants.Select(p => p.Name).Order());
        // Under Swap only, the cutting with some to spare is listed and the other is not
        Assert.Equal(["Spare"], Compare(Settings(cuttings: false, swap: true), cuttings: [going, spare]).Plants.Select(p => p.Name));
    }

    [Fact]
    public void A_cutting_on_the_page_twice_is_listed_once()
    {
        var spare = ACutting("Spare", change: c => { c.InitialCount = 3; c.SpareCount = 2; });

        Assert.Single(Compare(Settings(cuttings: true, swap: true), cuttings: [spare]).Plants);
    }

    [Fact]
    public void Spare_info_only_comes_with_the_swap_part_and_spare_ones_come_first()
    {
        var plain = APlant("A plain");
        var swap = APlant("B swap", change: p => p.Spare = SpareMark.UpForSwap);
        var onRequest = APlant("C request", change: p => p.Spare = SpareMark.CuttingsToSpare);
        var cutting = ACutting("D cutting", change: c => { c.InitialCount = 3; c.SpareCount = 2; c.Stage = PropagationStage.Rooted; });

        var on = Compare(Settings(swap: true), [plain, swap, onRequest], [cutting]);
        Assert.Equal(["D cutting", "C request", "B swap", "A plain"], on.Plants.Select(p => p.Name));
        Assert.Equal([FriendSpare.Cuttings, FriendSpare.CuttingsToSpare, FriendSpare.UpForSwap, FriendSpare.None], on.Plants.Select(p => p.Spare));
        Assert.Equal((2, PropagationStage.Rooted), (on.Plants[0].SpareCount, on.Plants[0].Stage));

        var off = Compare(Settings(swap: false), [plain, swap, onRequest], [cutting]);
        Assert.All(off.Plants, p => Assert.Equal(FriendSpare.None, p.Spare));
        Assert.All(off.Plants, p => Assert.Equal(0, p.SpareCount));
        Assert.All(off.Plants, p => Assert.Null(p.Stage));
    }

    [Fact]
    public void A_plant_that_left_keeps_no_mark()
    {
        var left = APlant("Left", change: p => { p.Spare = SpareMark.UpForSwap; p.Status = PlantStatus.Sold; });

        Assert.Empty(Compare(Settings(swap: true), [left]).Plants);
    }

    [Fact]
    public void Wishes_come_only_with_the_wishlist_part_and_only_by_name()
    {
        var wishes = new[]
        {
            new Wish { Genus = "Monstera", Species = "deliciosa", Cultivar = "Albo", Notes = "secret", FoundAt = "a shop", MostToPay = 99m, UpdatedAt = Now },
            new Wish { Name = "that pink syngonium", Notes = "secret", UpdatedAt = Now },
            new Wish { Name = "got it", GotOn = Today, UpdatedAt = Now },
            new Wish { Name = "deleted", DeletedAt = Now, UpdatedAt = Now },
        };

        var on = Compare(Settings(wishlist: true), wishes: wishes);
        Assert.True(on.WishlistShared);
        Assert.Equal(2, on.Wishes.Count);
        var albo = Assert.Single(on.Wishes, w => w.Genus == "Monstera");
        Assert.Equal(("deliciosa", "Albo"), (albo.Species, albo.Cultivar));
        var named = Assert.Single(on.Wishes, w => w.Genus is null);
        Assert.Equal(("that pink syngonium", null, null), (named.Name, named.Species, named.Cultivar));
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(on));

        var off = Compare(Settings(wishlist: false), wishes: wishes);
        Assert.False(off.WishlistShared);
        Assert.Empty(off.Wishes);
    }

    [Fact]
    public void The_contact_is_only_passed_on_when_swap_requests_are_on()
    {
        Assert.Equal("fern@example.com", Compare(Settings(), swapRequests: true, contact: "fern@example.com").SwapContact);
        Assert.Null(Compare(Settings(), swapRequests: false, contact: "fern@example.com").SwapContact);
        Assert.Null(Compare(Settings(), swapRequests: true, contact: null).SwapContact);
    }

    [Fact]
    public void The_json_is_text_for_the_enums()
    {
        var swap = APlant("Swap", change: p => p.Spare = SpareMark.UpForSwap);
        var json = System.Text.Json.JsonSerializer.Serialize(Compare(Settings(swap: true), [swap]), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Contains("\"spare\":\"UpForSwap\"", json);
        Assert.Contains("\"type\":\"Plant\"", json);
    }

    [Fact]
    public void Compare_ids_are_the_plants_and_cuttings_listed()
    {
        var plant = APlant("P");
        var hidden = APlant("H", change: p => p.HiddenFromPublicPage = true);
        var cutting = ACutting("C", change: c => { c.InitialCount = 1; c.SpareCount = 1; });

        var ids = PublicCollectionPage.CompareIds(PublicCollectionPage.Subjects(Settings(swap: true), [plant, hidden], [cutting], []));

        Assert.Equal(new[] { plant.Id, cutting.Id }.Order(), ids.Order());
    }
}
