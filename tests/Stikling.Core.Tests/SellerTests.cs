using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sellers;

namespace Stikling.Core.Tests;

public class SellerTests
{
    private readonly FakeSellerRepository sellers = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();

    private SellerService Service => new(sellers, plants, propagations);

    private Plant AddPlant(string? source, PlantOrigin origin = PlantOrigin.Purchased, Guid? sellerId = null)
    {
        var plant = new Plant { Nickname = "Plant", Source = source, Origin = origin, SellerId = sellerId };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Propagation AddPropagation(string? source)
    {
        var propagation = new Propagation { Nickname = "Cutting", Source = source };
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    private Seller AddSeller(string name, SellerKind kind = SellerKind.Shop)
    {
        var seller = new Seller { Name = name, Kind = kind };
        sellers.SaveAsync(seller);
        return seller;
    }

    [Fact]
    public async Task Adopting_makes_one_seller_per_name_ignoring_case_and_spaces()
    {
        var a = AddPlant("Plantorama");
        var b = AddPlant("  plantorama ");
        var c = AddPlant("Plant   Orama");
        var d = AddPropagation("PLANTORAMA");

        Assert.Equal(2, await Service.AdoptSourcesAsync());

        Assert.Equal(2, sellers.Sellers.Count);
        var list = await sellers.GetSellersAsync();
        Assert.Equal(list.For(null, a.Source)?.Id, list.For(null, b.Source)?.Id);
        Assert.Equal(list.For(null, a.Source)?.Id, list.For(null, d.Source)?.Id);
        Assert.NotEqual(list.For(null, a.Source)?.Id, list.For(null, c.Source)?.Id);
        Assert.Equal("Plantorama", list.For(null, a.Source)!.Name);
        Assert.Equal("Plant Orama", list.For(null, c.Source)!.Name);
    }

    [Fact]
    public async Task Adopting_never_saves_a_plant_or_propagation_and_is_idempotent()
    {
        var plant = AddPlant("  plantorama ");
        var propagation = AddPropagation("Anna");
        AddPlant(null);
        AddPlant("   ");

        Assert.Equal(2, await Service.AdoptSourcesAsync());
        Assert.Equal("  plantorama ", plant.Source);
        Assert.Null(plant.SellerId);
        Assert.Null(propagation.SellerId);
        Assert.Equal((0, 0), (plants.Saves, propagations.Saves));

        var ids = sellers.Sellers.Keys.Order().ToList();
        Assert.Equal(0, await Service.AdoptSourcesAsync());
        Assert.Equal(ids, sellers.Sellers.Keys.Order().ToList());
        Assert.Equal((0, 0), (plants.Saves, propagations.Saves));
    }

    [Fact]
    public async Task Two_devices_make_the_same_seller()
    {
        var other = new FakeSellerRepository();
        var otherPlants = new FakePlantRepository();
        var plantOnOther = new Plant { Nickname = "Plant", Source = "Anna" };
        otherPlants.Plants[plantOnOther.Id] = plantOnOther;
        AddPlant("anna");

        await Service.AdoptSourcesAsync();
        await new SellerService(other, otherPlants, new FakePropagationRepository()).AdoptSourcesAsync();

        Assert.Equal(Assert.Single(sellers.Sellers).Key, Assert.Single(other.Sellers).Key);
    }

    [Fact]
    public async Task A_seller_renamed_elsewhere_is_still_found_by_the_id_the_old_name_makes()
    {
        var renamed = new Seller { Id = SellerName.IdFor("Plantorama"), Name = "Plantorama Odense" };
        await sellers.SaveAsync(renamed);
        var plant = AddPlant("Plantorama");

        Assert.Equal(0, await Service.AdoptSourcesAsync());

        Assert.Single(sellers.Sellers);
        Assert.Equal(renamed.Id, (await sellers.GetSellersAsync()).For(null, plant.Source)?.Id);
    }

    [Fact]
    public async Task The_lookup_follows_a_merge_and_a_deleted_seller_stays_deleted()
    {
        var into = AddSeller("Plantorama Odense");
        var merged = new Seller { Id = SellerName.IdFor("Plantorama"), Name = "Plantorama", MergedIntoId = into.Id, DeletedAt = DateTimeOffset.UtcNow };
        await sellers.SaveAsync(merged);
        var deleted = new Seller { Id = SellerName.IdFor("Anna"), Name = "Anna", DeletedAt = DateTimeOffset.UtcNow };
        await sellers.SaveAsync(deleted);
        var a = AddPlant("Plantorama");
        var b = AddPlant("Anna");

        Assert.Equal(0, await Service.AdoptSourcesAsync());

        var list = await sellers.GetSellersAsync();
        Assert.Equal(into.Id, list.For(null, a.Source)?.Id);
        Assert.Null(list.For(null, b.Source));
        Assert.Equal(3, sellers.Sellers.Count);
    }

    [Fact]
    public async Task The_lookup_resolves_by_id_then_by_name_then_by_the_name_based_id()
    {
        var byId = AddSeller("Plantorama");
        var renamed = new Seller { Id = SellerName.IdFor("Anna"), Name = "Anna B" };
        await sellers.SaveAsync(renamed);
        var list = await sellers.GetSellersAsync();

        Assert.Equal(byId.Id, list.For(byId.Id, "something else")?.Id);
        Assert.Equal(byId.Id, list.For(null, " plantorama ")?.Id);
        Assert.Equal(renamed.Id, list.For(null, "Anna")?.Id);
        Assert.Null(list.For(null, "Nobody"));
        Assert.Null(list.For(null, "  "));
        Assert.Null(list.For(Guid.NewGuid(), "Plantorama"));
    }

    [Fact]
    public async Task Adopting_makes_nothing_when_a_live_seller_has_the_name()
    {
        AddSeller("Plantorama", SellerKind.OnlineShop);
        AddPlant("plantorama");

        Assert.Equal(0, await Service.AdoptSourcesAsync());

        Assert.Single(sellers.Sellers);
    }

    [Fact]
    public async Task The_kind_is_guessed_from_the_origin()
    {
        AddPlant("Shop", PlantOrigin.Purchased);
        AddPlant("Anna", PlantOrigin.Gift);
        AddPlant("Swap day", PlantOrigin.Swap);
        AddPlant("Garden", PlantOrigin.Propagated);
        AddPropagation("Seed bank");

        await Service.AdoptSourcesAsync();

        SellerKind KindOf(string name) => sellers.Sellers.Values.Single(s => s.Name == name).Kind;
        Assert.Equal(SellerKind.Shop, KindOf("Shop"));
        Assert.Equal(SellerKind.Person, KindOf("Anna"));
        Assert.Equal(SellerKind.Person, KindOf("Swap day"));
        Assert.Equal(SellerKind.Other, KindOf("Garden"));
        Assert.Equal(SellerKind.Other, KindOf("Seed bank"));
    }

    [Fact]
    public async Task Renaming_links_the_records_that_only_had_the_old_name()
    {
        var seller = AddSeller("Plantorama");
        var nameOnly = AddPlant("plantorama");
        var linked = AddPlant("Plantorama", sellerId: seller.Id);

        await Service.UpdateAsync(seller.Id, "Plantorama Odense", SellerKind.Shop, null);

        Assert.Equal((seller.Id, "Plantorama Odense"), (nameOnly.SellerId, nameOnly.Source));
        Assert.Equal((seller.Id, "Plantorama Odense"), (linked.SellerId, linked.Source));
    }

    [Fact]
    public async Task Merging_links_the_records_that_only_had_the_name()
    {
        var from = AddSeller("Plantorama");
        var into = AddSeller("Plantorama Odense");
        var nameOnly = AddPropagation("Plantorama");

        await Service.MergeAsync(from.Id, into.Id);

        Assert.Equal((into.Id, "Plantorama Odense"), (nameOnly.SellerId, nameOnly.Source));
    }

    [Fact]
    public async Task Choosing_a_name_finds_or_makes_the_seller_and_clearing_it_clears_both()
    {
        var existing = AddSeller("Plantorama");

        var found = await Service.ChooseAsync("  plantorama", SellerKind.Shop);
        Assert.Equal(new SellerChoice(existing.Id, "Plantorama"), found);

        var made = await Service.ChooseAsync("Anna", SellerKind.Person);
        Assert.Equal("Anna", made.Source);
        Assert.Equal(SellerKind.Person, sellers.Sellers[made.SellerId!.Value].Kind);
        Assert.Equal(made.SellerId, (await Service.ChooseAsync("anna", SellerKind.Shop)).SellerId);

        Assert.Equal(new SellerChoice(null, null), await Service.ChooseAsync("  ", SellerKind.Shop));
    }

    [Fact]
    public async Task Renaming_writes_the_name_into_Source()
    {
        var seller = AddSeller("Plantorama");
        var plant = AddPlant("Plantorama", sellerId: seller.Id);
        var propagation = AddPropagation("Plantorama");
        propagation.SellerId = seller.Id;
        var other = AddPlant("Anna", sellerId: AddSeller("Anna").Id);

        await Service.UpdateAsync(seller.Id, "  Plantorama   Odense ", SellerKind.OnlineShop, "  Good  ");

        Assert.Equal("Plantorama Odense", seller.Name);
        Assert.Equal(SellerKind.OnlineShop, seller.Kind);
        Assert.Equal("Good", seller.Notes);
        Assert.Equal("Plantorama Odense", plant.Source);
        Assert.Equal("Plantorama Odense", propagation.Source);
        Assert.Equal("Anna", other.Source);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.UpdateAsync(seller.Id, " ", SellerKind.Shop, null));
    }

    [Fact]
    public async Task Merging_moves_everything_and_keeps_the_merged_seller_pointing_on()
    {
        var from = AddSeller("Plantorama");
        var into = AddSeller("Plantorama Odense");
        var plant = AddPlant("Plantorama", sellerId: from.Id);
        var propagation = AddPropagation("Plantorama");
        propagation.SellerId = from.Id;

        await Service.MergeAsync(from.Id, into.Id);

        Assert.Equal(into.Id, plant.SellerId);
        Assert.Equal("Plantorama Odense", plant.Source);
        Assert.Equal(into.Id, propagation.SellerId);
        Assert.True(from.IsDeleted);
        Assert.Equal(into.Id, from.MergedIntoId);

        // A plant saved on another device before the merge arrived still lands right
        var late = AddPlant("Plantorama", sellerId: from.Id);
        var list = await sellers.GetSellersAsync();
        Assert.Equal(into.Id, list.Find(late.SellerId)?.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => Service.MergeAsync(into.Id, into.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.MergeAsync(into.Id, from.Id));
    }

    [Fact]
    public async Task Deleting_clears_the_link_and_keeps_the_Source_text()
    {
        var seller = AddSeller("Plantorama");
        var plant = AddPlant("Plantorama", sellerId: seller.Id);
        var propagation = AddPropagation("Plantorama");
        propagation.SellerId = seller.Id;

        await Service.DeleteAsync(seller.Id);

        Assert.True(seller.IsDeleted);
        Assert.Null(plant.SellerId);
        Assert.Null(propagation.SellerId);
        Assert.Equal("Plantorama", plant.Source);

        // And it isn't made again from the text, even though this seller had another id than its name makes
        Assert.Equal(0, await Service.AdoptSourcesAsync());
        Assert.Null(plant.SellerId);
        Assert.Equal(2, sellers.Sellers.Count);
        Assert.Null((await sellers.GetSellersAsync()).For(plant.SellerId, plant.Source));
    }

    [Fact]
    public void Two_merged_the_opposite_way_leave_the_oldest()
    {
        var first = new Seller { Name = "A", CreatedAt = DateTimeOffset.UnixEpoch, DeletedAt = DateTimeOffset.UnixEpoch };
        var second = new Seller { Name = "A", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(1), DeletedAt = DateTimeOffset.UnixEpoch };
        first.MergedIntoId = second.Id;
        second.MergedIntoId = first.Id;

        Assert.Equal(first.Id, new SellerList([first, second]).Find(second.Id)?.Id);
    }

    [Fact]
    public void The_summary_adds_up_what_came_from_the_seller()
    {
        var seller = new Seller { Name = "Plantorama" };
        var list = new SellerList([seller]);
        var day = new DateOnly(2026, 3, 1);

        Plant Make(PlantStatus status, ArrivalRating? rating, decimal? price, DateOnly? got, params ArrivalProblem[] problems) => new()
        {
            Nickname = "P", SellerId = seller.Id, Status = status, ArrivedIn = rating, PricePaid = price,
            AcquiredOn = got is { } d ? LooseDate.Of(d) : null, ArrivalProblems = [.. problems]
        };

        var withPests = Make(PlantStatus.Active, ArrivalRating.Poor, 100m, day, ArrivalProblem.Pests, ArrivalProblem.RootRot);
        var late = Make(PlantStatus.Died, ArrivalRating.Good, 50m, day);
        var plants = new List<Plant>
        {
            withPests, late,
            Make(PlantStatus.GivenAway, ArrivalRating.Ok, null, null),
            Make(PlantStatus.Sold, null, null, null, ArrivalProblem.Pests),
            new() { Nickname = "Elsewhere" },
            new() { Nickname = "Deleted", SellerId = seller.Id, DeletedAt = DateTimeOffset.UtcNow }
        };
        var propagation = new Propagation { Nickname = "Cutting", SellerId = seller.Id, ArrivedIn = ArrivalRating.Poor };

        var cases = new[]
        {
            new PestCase { Scope = PestScope.PickedPlants, PlantIds = [withPests.Id], StartedOn = day.AddDays(30) },
            new PestCase { Scope = PestScope.PickedPlants, PlantIds = [late.Id], StartedOn = day.AddDays(31) },
            new PestCase { Scope = PestScope.PickedPlants, PlantIds = [late.Id], StartedOn = day.AddDays(-1) },
            new PestCase { Scope = PestScope.PickedPlants, PlantIds = [withPests.Id, late.Id], StartedOn = day.AddDays(5) },
            new PestCase { Scope = PestScope.Everywhere, StartedOn = day.AddDays(5) }
        };

        var summary = SellerSummary.For(seller, list, plants, [propagation], cases);

        Assert.Equal(4, summary.Plants.Count);
        Assert.Single(summary.Propagations);
        Assert.Equal((1, 1, 1, 1), (summary.StillHere, summary.Died, summary.GivenAway, summary.Sold));
        Assert.Equal((2, 2), (summary.PestCases, summary.PlantsWithPests));
        Assert.Equal((1, 1, 2), (summary.ArrivedGood, summary.ArrivedOk, summary.ArrivedPoor));
        Assert.Equal(2, summary.Problems[ArrivalProblem.Pests]);
        Assert.Equal(1, summary.Problems[ArrivalProblem.RootRot]);
        Assert.False(summary.Problems.ContainsKey(ArrivalProblem.Damaged));
        Assert.Equal((150m, 2, 75m), (summary.PaidTotal, summary.PaidCount, summary.PaidAverage));

        var counts = SellerSummary.CountsBySeller(list, plants, [propagation]);
        Assert.Equal(5, counts[seller.Id]);
    }

    [Fact]
    public void The_summary_of_a_seller_with_nothing_has_no_average()
    {
        var seller = new Seller { Name = "Anna" };

        var summary = SellerSummary.For(seller, new SellerList([seller]), [], [], []);

        Assert.Null(summary.PaidAverage);
        Assert.False(summary.HasArrivals);
        Assert.Equal(0, summary.Total);
    }

    [Fact]
    public void Unknown_text_reads_as_other_and_unknown_problems_drop_out()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var seller = JsonSerializer.Deserialize<Seller>("""{"name":"X","kind":"FleaMarket"}""", options)!;
        var numeric = JsonSerializer.Deserialize<Seller>("""{"name":"X","kind":7}""", options)!;
        var plant = JsonSerializer.Deserialize<Plant>(
            """{"nickname":"P","arrivedIn":"Terrible","arrivalProblems":["Pests","Mould",{"a":1},"Pests","RootRot",3]}""", options)!;
        var good = JsonSerializer.Deserialize<Plant>("""{"nickname":"P","arrivedIn":"Ok","arrivalProblems":null}""", options)!;
        var missing = JsonSerializer.Deserialize<Propagation>("""{"nickname":"P"}""", options)!;

        Assert.Equal(SellerKind.Other, seller.Kind);
        Assert.Equal(SellerKind.Other, numeric.Kind);
        Assert.Null(plant.ArrivedIn);
        Assert.Equal([ArrivalProblem.Pests, ArrivalProblem.RootRot], plant.ArrivalProblems);
        Assert.Equal(ArrivalRating.Ok, good.ArrivedIn);
        Assert.Empty(missing.ArrivalProblems);
        Assert.Null(missing.ArrivedIn);
    }

    [Fact]
    public void Enums_are_written_as_text()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var plant = new Plant { Nickname = "P", ArrivedIn = ArrivalRating.Ok, ArrivalProblems = [ArrivalProblem.SmallerThanShown] };

        var json = JsonSerializer.Serialize(plant, options);

        Assert.Contains("\"arrivedIn\":\"Ok\"", json);
        Assert.Contains("\"arrivalProblems\":[\"SmallerThanShown\"]", json);
        Assert.Contains("\"kind\":\"SwapEvent\"", JsonSerializer.Serialize(new Seller { Name = "X", Kind = SellerKind.SwapEvent }, options));
    }

    [Fact]
    public void Duplicating_a_plant_copies_the_seller_but_not_how_it_arrived()
    {
        var seller = Guid.NewGuid();
        var plant = new Plant { Nickname = "P", SellerId = seller, ArrivedIn = ArrivalRating.Poor, ArrivalProblems = [ArrivalProblem.Pests], ArrivalNote = "x" };

        var copy = plant.Duplicate();

        Assert.Equal(seller, copy.SellerId);
        Assert.Null(copy.ArrivedIn);
        Assert.Empty(copy.ArrivalProblems);
        Assert.Null(copy.ArrivalNote);
    }

    [Fact]
    public void Poor_arrivals_are_described_and_good_ones_are_not()
    {
        Assert.Equal("arrived in poor shape", Arrival.PoorText(ArrivalRating.Poor, []));
        Assert.Equal("arrived in poor shape: pests, root rot", Arrival.PoorText(ArrivalRating.Poor, [ArrivalProblem.Pests, ArrivalProblem.RootRot]));
        Assert.Null(Arrival.PoorText(ArrivalRating.Ok, [ArrivalProblem.Pests]));
        Assert.Null(Arrival.PoorText(null, []));
    }
}
