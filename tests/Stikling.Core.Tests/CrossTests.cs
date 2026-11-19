using System.Text.Json;
using Stikling.Core.Crosses;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class CrossTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    private static Plant Hoya(string nickname) => new() { Nickname = nickname, Genus = "Hoya", Species = "carnosa" };

    private static Cross Pair(Plant pod, Plant pollen, string? name = null) => new()
    {
        Name = name,
        PodParentPlantId = pod.Id,
        PodParentName = pod.DisplayName,
        PollenParentPlantId = pollen.Id,
        PollenParentName = pollen.DisplayName,
        PollinatedOn = new DateOnly(2026, 6, 1)
    };

    private static CrossBook Book(IEnumerable<Cross> crosses, IEnumerable<SeedLot> lots, IEnumerable<Propagation> propagations, IEnumerable<Plant> plants) =>
        new(crosses, lots, propagations, plants);

    [Fact]
    public void A_cross_needs_both_parents_but_a_typed_name_is_enough()
    {
        var cross = new Cross { PollinatedOn = Today };
        Assert.Equal(2, cross.Validate(Today).Count);

        cross.PodParentName = "Mum";
        cross.PollenParentName = "Dad from a friend";
        Assert.Empty(cross.Validate(Today));
    }

    [Fact]
    public void A_cross_cannot_be_in_the_future_or_ripen_before_it_was_pollinated()
    {
        var cross = new Cross { PodParentName = "A", PollenParentName = "B", PollinatedOn = Today.AddDays(1) };
        Assert.NotEmpty(cross.Validate(Today));

        cross.PollinatedOn = Today;
        cross.SeedRipeOn = Today.AddDays(-1);
        Assert.NotEmpty(cross.Validate(Today));

        cross.SeedRipeOn = Today.AddDays(120);
        Assert.Empty(cross.Validate(Today));
    }

    [Fact]
    public void The_same_plant_on_both_sides_is_a_self()
    {
        var plant = Hoya("Mum");
        var book = Book([Pair(plant, plant)], [], [], [plant]);

        var view = book.ViewOf(book.Crosses[0]);
        Assert.True(view.IsSelf);
        Assert.Equal("Mum selfed", view.Title);
    }

    [Fact]
    public void The_same_typed_name_on_both_sides_is_a_self_but_two_names_are_not()
    {
        Assert.True(new Cross { PodParentName = "Mum", PollenParentName = " mum " }.IsSelf);
        Assert.False(new Cross { PodParentName = "Mum", PollenParentName = "Dad" }.IsSelf);
    }

    [Fact]
    public void The_title_names_the_pod_parent_first()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var view = Book([Pair(mum, dad)], [], [], [mum, dad]).ViewOf(Pair(mum, dad));

        Assert.Equal("Mum × Dad", view.Title);
        Assert.Equal("Mum × Dad", view.DisplayName);
        Assert.False(view.HasWorkingName);
    }

    [Fact]
    public void A_working_name_leads_and_the_title_follows()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var view = Book([], [], [], [mum, dad]).ViewOf(Pair(mum, dad, "Dark Star"));

        Assert.Equal("Dark Star", view.DisplayName);
        Assert.Equal("Dark Star (Mum × Dad)", view.FullName);
    }

    [Fact]
    public void A_parent_plant_that_is_gone_reads_as_the_name_kept_beside_it()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);
        dad.DeletedAt = DateTimeOffset.UtcNow;

        var view = Book([cross], [], [], [mum, dad]).ViewOf(cross);

        Assert.Equal("Dad", view.Pollen.Name);
        Assert.Null(view.Pollen.Plant);
        Assert.True(view.Pollen.Known);
    }

    [Fact]
    public void A_parent_that_is_gone_with_no_name_says_so()
    {
        var mum = Hoya("Mum");
        var cross = new Cross { PodParentPlantId = mum.Id, PollenParentPlantId = Guid.NewGuid(), PollinatedOn = Today };

        var view = Book([cross], [], [], [mum]).ViewOf(cross);

        Assert.False(view.Pollen.Known);
        Assert.Equal("a plant you no longer have", view.Pollen.Name);
    }

    [Fact]
    public void Seedlings_are_counted_from_what_was_sown_from_the_crosss_lots()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);
        var lot = new SeedLot { CrossId = cross.Id };
        var sown = new Propagation
        {
            Type = PropagationType.Seed,
            SeedLotId = lot.Id,
            StartedOn = new DateOnly(2026, 9, 1),
            InitialCount = 10,
            SeedsGerminated = 6,
            PottedUpCount = 2
        };
        var seedling = new Plant { Nickname = "S1", FromPropagationId = sown.Id, ParentPlantId = mum.Id };
        var gone = new Plant { Nickname = "S2", FromPropagationId = sown.Id, Status = PlantStatus.Died };
        // Another propagation of the same plant that is not from the cross
        var other = new Propagation { Type = PropagationType.Seed, StartedOn = new DateOnly(2026, 9, 1), InitialCount = 5, ParentPlantId = mum.Id };

        var seedlings = Book([cross], [lot], [sown, other], [mum, dad, seedling, gone]).SeedlingsOf(cross);

        Assert.Equal(10, seedlings.Sown);
        Assert.Equal(6, seedlings.CameUp);
        // 6 came up and 2 left to be potted up: 4 are still in the propagation
        Assert.Equal(4, seedlings.Growing);
        Assert.Equal(2, seedlings.Plants.Count);
        // One of the two plants died
        Assert.Equal(1 + 4, seedlings.Alive);
        Assert.Equal(new DateOnly(2026, 9, 1), seedlings.FirstSown);
    }

    [Fact]
    public void A_cross_with_no_lot_has_no_seedlings()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);

        var seedlings = Book([cross], [], [], [mum, dad]).SeedlingsOf(cross);

        Assert.False(seedlings.Any);
        Assert.Equal(0, seedlings.Alive);
    }

    [Fact]
    public void A_plant_and_a_propagation_find_both_parents_through_the_lot_and_the_cross()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad, "Dark Star");
        var lot = new SeedLot { CrossId = cross.Id };
        var sown = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id, ParentPlantId = mum.Id };
        var plant = new Plant { FromPropagationId = sown.Id, ParentPlantId = mum.Id };
        var book = Book([cross], [lot], [sown], [mum, dad, plant]);

        Assert.Equal(dad.Id, book.OriginOf(sown)!.Pollen.Plant!.Id);
        Assert.Equal(dad.Id, book.OriginOf(plant)!.Pollen.Plant!.Id);
        Assert.Equal(mum.Id, book.OriginOf(plant)!.Pod.Plant!.Id);
    }

    [Fact]
    public void A_propagation_not_from_a_lot_has_no_cross()
    {
        var mum = Hoya("Mum");
        var cutting = new Propagation { ParentPlantId = mum.Id };

        Assert.Null(Book([], [], [cutting], [mum]).OriginOf(cutting));
    }

    [Fact]
    public void The_working_name_shows_on_seedlings_until_they_have_a_nickname_or_cultivar()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad, "Dark Star");
        var lot = new SeedLot { CrossId = cross.Id };
        var plain = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id };
        var named = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id, Nickname = "Blackie" };
        var cultivar = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id, Cultivar = "Nightfall" };
        var plant = new Plant { FromPropagationId = plain.Id };
        var namedPlant = new Plant { FromPropagationId = plain.Id, Nickname = "Blackie" };
        var book = Book([cross], [lot], [plain, named, cultivar], [mum, dad, plant, namedPlant]);

        Assert.Equal("Dark Star", book.WorkingNameFor(plain));
        Assert.Null(book.WorkingNameFor(named));
        Assert.Null(book.WorkingNameFor(cultivar));
        Assert.Equal("Dark Star", book.WorkingNameFor(plant));
        Assert.Null(book.WorkingNameFor(namedPlant));
    }

    [Fact]
    public void A_cross_without_a_working_name_gives_none_to_its_seedlings()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);
        var lot = new SeedLot { CrossId = cross.Id };
        var sown = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id };

        Assert.Null(Book([cross], [lot], [sown], [mum, dad]).WorkingNameFor(sown));
    }

    [Fact]
    public void The_log_lists_the_newest_first_and_filters_by_outcome()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var old = Pair(mum, dad);
        old.PollinatedOn = new DateOnly(2026, 5, 1);
        old.Outcome = CrossOutcome.DidNotTake;
        var recent = Pair(dad, mum);
        recent.PollinatedOn = new DateOnly(2026, 7, 1);
        recent.Outcome = CrossOutcome.Took;
        var waiting = Pair(mum, mum);
        waiting.PollinatedOn = new DateOnly(2026, 8, 1);
        var book = Book([old, recent, waiting], [], [], [mum, dad]);

        Assert.Equal([waiting.Id, recent.Id, old.Id], book.Log().Select(r => r.View.Cross.Id));
        Assert.Equal([recent.Id], book.Log(CrossLogFilter.Took).Select(r => r.View.Cross.Id));
        Assert.Equal([old.Id], book.Log(CrossLogFilter.DidNotTake).Select(r => r.View.Cross.Id));
        Assert.Equal([waiting.Id], book.Log(CrossLogFilter.NotKnown).Select(r => r.View.Cross.Id));
    }

    [Fact]
    public void The_pollen_parents_seedlings_are_found_but_a_self_is_left_out()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);
        var self = Pair(dad, dad);
        var lot = new SeedLot { CrossId = cross.Id };
        var selfLot = new SeedLot { CrossId = self.Id };
        var sown = new Propagation { Type = PropagationType.Seed, SeedLotId = lot.Id, StartedOn = Today };
        var selfSown = new Propagation { Type = PropagationType.Seed, SeedLotId = selfLot.Id, StartedOn = Today };
        var plant = new Plant { FromPropagationId = sown.Id };
        var book = Book([cross, self], [lot, selfLot], [sown, selfSown], [mum, dad, plant]);

        var (plants, growing) = book.PollenChildrenOf(dad);

        Assert.Equal([plant.Id], plants.Select(p => p.Id));
        Assert.Equal([sown.Id], growing.Select(p => p.Id));
    }

    [Fact]
    public void Deleted_crosses_and_lots_are_left_out()
    {
        var mum = Hoya("Mum");
        var dad = Hoya("Dad");
        var cross = Pair(mum, dad);
        cross.DeletedAt = DateTimeOffset.UtcNow;
        var lot = new SeedLot { Source = "Swap", DeletedAt = DateTimeOffset.UtcNow };

        var book = Book([cross], [lot], [], [mum, dad]);

        Assert.False(book.Any);
    }

    [Fact]
    public void A_cross_reads_a_method_from_a_later_version_as_not_said()
    {
        var json = "{\"podParentName\":\"A\",\"pollenParentName\":\"B\",\"method\":\"Tweezers\",\"outcome\":\"Partly\"}";

        var cross = JsonSerializer.Deserialize<Cross>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(cross.Method);
        Assert.Null(cross.Outcome);
        Assert.Equal("A", cross.PodParentName);
    }

    [Fact]
    public void A_cross_is_written_with_its_enums_as_text()
    {
        var cross = new Cross { PodParentName = "A", PollenParentName = "B", Method = CrossMethod.ByHand, Outcome = CrossOutcome.Took, SeedAmountUnit = SeedUnit.Grams };

        var json = JsonSerializer.Serialize(cross, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"method\":\"ByHand\"", json);
        Assert.Contains("\"outcome\":\"Took\"", json);
        Assert.Contains("\"seedAmountUnit\":\"Grams\"", json);
    }

    [Fact]
    public void A_propagation_from_before_has_no_seed_lot()
    {
        var propagation = JsonSerializer.Deserialize<Propagation>("{\"nickname\":\"Old\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(propagation.SeedLotId);
    }
}

public class SeedLotTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    [Fact]
    public void A_lot_needs_something_to_call_it_by()
    {
        Assert.NotEmpty(new SeedLot().Validate(Today));
        Assert.Empty(new SeedLot { Source = "Seed swap" }.Validate(Today));
        Assert.Empty(new SeedLot { Genus = "Hoya" }.Validate(Today));
        Assert.Empty(new SeedLot { CrossId = Guid.NewGuid() }.Validate(Today));
    }

    [Fact]
    public void An_amount_has_to_be_more_than_nothing()
    {
        Assert.NotEmpty(new SeedLot { Source = "x", Amount = 0 }.Validate(Today));
        Assert.NotEmpty(new SeedLot { Source = "x", Amount = -3 }.Validate(Today));
        Assert.Empty(new SeedLot { Source = "x", Amount = 100, AmountIsAbout = true }.Validate(Today));
    }

    [Theory]
    [InlineData(100, SeedUnit.Seeds, true, "about 100 seeds")]
    [InlineData(1, SeedUnit.Seeds, false, "1 seed")]
    [InlineData(1, SeedUnit.Grams, false, "1 g")]
    [InlineData(250, null, false, "250 seeds")]
    public void A_rough_amount_is_written_the_way_it_was_said(double amount, SeedUnit? unit, bool about, string expected)
    {
        Assert.Equal(expected, SeedAmounts.Describe((decimal)amount, unit, about));
    }

    [Fact]
    public void No_amount_is_written_as_nothing()
    {
        Assert.Null(SeedAmounts.Describe(null, null, false));
        Assert.Null(SeedAmounts.Describe(0, SeedUnit.Seeds, false));
    }

    [Fact]
    public void Only_seeds_give_a_number_to_sow_with()
    {
        Assert.Equal(100, SeedAmounts.SeedCount(100, SeedUnit.Seeds));
        Assert.Equal(100, SeedAmounts.SeedCount(100, null));
        Assert.Null(SeedAmounts.SeedCount(2, SeedUnit.Grams));
        Assert.Null(SeedAmounts.SeedCount(null, SeedUnit.Seeds));
        Assert.Null(SeedAmounts.SeedCount(0.5m, SeedUnit.Seeds));
    }

    [Fact]
    public void A_lot_reads_a_unit_from_a_later_version_as_none()
    {
        var lot = JsonSerializer.Deserialize<SeedLot>("{\"source\":\"x\",\"amount\":3,\"amountUnit\":\"Pinches\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Null(lot.AmountUnit);
        Assert.Equal(3, lot.Amount);
    }
}

public class CrossServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeCrossRepository crosses = new();
    private readonly FakeSeedLotRepository lots = new();

    private CrossService Service => new(crosses, lots, plants, propagations);

    private async Task<(Plant Mum, Plant Dad, Cross Cross)> SetUpAsync()
    {
        var mum = new Plant { Nickname = "Mum", Genus = "Hoya", Species = "carnosa" };
        var dad = new Plant { Nickname = "Dad", Genus = "Hoya", Species = "pubicalyx" };
        await plants.SaveAsync(mum);
        await plants.SaveAsync(dad);
        var cross = new Cross
        {
            PodParentPlantId = mum.Id,
            PodParentName = "Mum",
            PollenParentPlantId = dad.Id,
            PollenParentName = "Dad",
            PollinatedOn = new DateOnly(2026, 6, 1),
            SeedRipeOn = new DateOnly(2026, 9, 1),
            SeedAmount = 40,
            SeedAmountIsAbout = true
        };
        await Service.SaveAsync(cross);
        return (mum, dad, cross);
    }

    [Fact]
    public async Task A_lot_from_a_cross_takes_the_pod_parents_name_the_ripe_day_and_the_amount()
    {
        var (_, _, cross) = await SetUpAsync();

        var lot = await Service.LotFromCrossAsync(cross);

        Assert.Equal(cross.Id, lot.CrossId);
        Assert.Equal("Hoya", lot.Genus);
        Assert.Equal("carnosa", lot.Species);
        Assert.Equal(new DateOnly(2026, 9, 1), lot.CollectedOn);
        Assert.Equal(40, lot.Amount);
        Assert.True(lot.AmountIsAbout);
    }

    [Fact]
    public async Task Making_a_lot_twice_gives_the_same_lot_not_a_second()
    {
        var (_, _, cross) = await SetUpAsync();

        var first = await Service.LotFromCrossAsync(cross);
        await Service.SaveLotAsync(first);
        var second = await Service.LotFromCrossAsync(cross);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(CrossService.LotIdFor(cross.Id), first.Id);
        Assert.Single(lots.Lots);
    }

    [Fact]
    public async Task Sowing_from_a_lot_keeps_the_lot_and_makes_the_pod_parent_the_parent_plant()
    {
        var (mum, _, cross) = await SetUpAsync();
        var lot = await Service.LotFromCrossAsync(cross);
        lot.Amount = 25;
        lot.AmountIsAbout = false;
        await Service.SaveLotAsync(lot);

        var sowing = await Service.SowingFromAsync(lot, Today);

        Assert.Equal(PropagationType.Seed, sowing.Type);
        Assert.Equal(lot.Id, sowing.SeedLotId);
        Assert.Equal(mum.Id, sowing.ParentPlantId);
        Assert.Equal(25, sowing.InitialCount);
        Assert.Equal("Hoya", sowing.Genus);
        Assert.Equal(Today, sowing.StartedOn);
    }

    [Fact]
    public async Task Sowing_a_lot_counted_in_grams_starts_with_one_unit_to_be_counted_later()
    {
        var lot = new SeedLot { Source = "Shop", Genus = "Basil", Amount = 2, AmountUnit = SeedUnit.Grams };
        await Service.SaveLotAsync(lot);

        var sowing = await Service.SowingFromAsync(lot, Today);

        Assert.Equal(1, sowing.InitialCount);
        Assert.Null(sowing.ParentPlantId);
    }

    [Fact]
    public async Task A_pod_parent_that_is_only_a_name_gives_no_parent_plant()
    {
        var cross = new Cross { PodParentName = "A friend's hoya", PollenParentName = "Another", PollinatedOn = new DateOnly(2026, 6, 1) };
        await Service.SaveAsync(cross);
        var lot = await Service.LotFromCrossAsync(cross);
        await Service.SaveLotAsync(lot);

        var sowing = await Service.SowingFromAsync(lot, Today);

        Assert.Null(sowing.ParentPlantId);
        Assert.Equal(lot.Id, sowing.SeedLotId);
    }

    [Fact]
    public async Task A_lot_bought_with_a_typed_cross_still_names_its_parents()
    {
        var cross = new Cross { PodParentName = "H. carnosa", PollenParentName = "H. pubicalyx", PollinatedOn = new DateOnly(2025, 6, 1) };
        await Service.SaveAsync(cross);
        var lot = new SeedLot { Source = "Seller", CrossId = cross.Id, Amount = 10 };
        await Service.SaveLotAsync(lot);

        var book = await Service.GetBookAsync();

        Assert.Equal("H. carnosa × H. pubicalyx", book.LotView(lot).Cross!.Title);
        Assert.Equal("H. carnosa × H. pubicalyx", book.LotView(lot).Title);
    }

    [Fact]
    public async Task Starting_a_cross_from_a_plant_fills_in_that_side_only()
    {
        var (mum, _, _) = await SetUpAsync();

        var asPod = CrossService.StartFrom(mum, asPollenParent: false, Today);
        var asPollen = CrossService.StartFrom(mum, asPollenParent: true, Today);

        Assert.Equal(mum.Id, asPod.PodParentPlantId);
        Assert.Null(asPod.PollenParentPlantId);
        Assert.Equal(mum.Id, asPollen.PollenParentPlantId);
        Assert.Null(asPollen.PodParentPlantId);
        Assert.Equal(Today, asPod.PollinatedOn);
    }

    [Fact]
    public async Task A_cross_with_no_pollen_parent_is_not_saved()
    {
        var cross = new Cross { PodParentName = "A", PollinatedOn = Today };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SaveAsync(cross));
    }
}
