using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class CollectionSummaryTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static CollectionSummary Summary(IEnumerable<Plant> plants, IEnumerable<Propagation>? propagations = null, Places? places = null) =>
        CollectionSummary.From(plants, propagations ?? [], places ?? Places.None, Today);

    [Fact]
    public void Plants_are_counted_by_how_many_each_pot_holds()
    {
        var summary = Summary([new Plant { Genus = "Hoya" }, new Plant { Genus = "Hoya", Count = 4 }]);

        Assert.Equal(5, summary.Plants);
    }

    [Fact]
    public void Gone_deleted_and_potted_in_plants_are_left_out()
    {
        Plant kept = new() { Genus = "Hoya" };
        var plants = new[]
        {
            kept,
            new Plant { Genus = "Ficus", Status = PlantStatus.Died },
            new Plant { Genus = "Ficus", DeletedAt = DateTimeOffset.UnixEpoch },
            new Plant { Genus = "Ficus", PottedWithPlantId = kept.Id }
        };

        var summary = Summary(plants);

        Assert.Equal(1, summary.Plants);
        Assert.Equal(1, summary.Genera);
        Assert.Equal(["Hoya"], summary.ByGenus.Select(r => r.Name));
    }

    [Fact]
    public void A_genus_written_in_another_case_or_with_spaces_is_the_same_genus()
    {
        var summary = Summary([new Plant { Genus = "Hoya" }, new Plant { Genus = " hoya " }, new Plant { Genus = "HOYA" }]);

        Assert.Equal(1, summary.Genera);
        var row = Assert.Single(summary.ByGenus);
        Assert.Equal("Hoya", row.Name);
        Assert.Equal(3, row.Count);
    }

    [Fact]
    public void Species_count_a_genus_and_species_pair_once()
    {
        var summary = Summary(
        [
            new Plant { Genus = "Hoya", Species = "carnosa" },
            new Plant { Genus = "hoya", Species = "Carnosa", Cultivar = "Krimson Queen" },
            new Plant { Genus = "Hoya", Species = "kerrii" },
            new Plant { Genus = "Ficus", Species = "carnosa" },
            new Plant { Genus = "Hoya" },
            new Plant { Nickname = "Nameless", Species = "alba" }
        ]);

        Assert.Equal(3, summary.Species);
    }

    [Fact]
    public void Sp_and_names_that_are_lost_do_not_add_a_species()
    {
        var summary = Summary(
        [
            new Plant { Genus = "Hoya", NameQualifier = NameQualifier.Sp },
            new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Unknown }
        ]);

        Assert.Equal(0, summary.Species);
        Assert.Equal(1, summary.Genera);
    }

    [Fact]
    public void An_unsure_species_with_cf_still_counts_as_the_species_it_is_near()
    {
        // KnownSpecies leaves out only sp. and a lost name, so "Hoya cf. carnosa" counts as carnosa
        var summary = Summary([new Plant { Genus = "Hoya", Species = "carnosa", NameQualifier = NameQualifier.Cf }]);

        Assert.Equal(1, summary.Species);
    }

    [Fact]
    public void By_genus_is_biggest_first_then_by_name_with_no_genus_last()
    {
        var summary = Summary(
        [
            new Plant { Nickname = "Nameless" },
            new Plant { Genus = "Ficus" },
            new Plant { Genus = "Hoya" },
            new Plant { Genus = "Hoya" },
            new Plant { Genus = "Calathea" },
            new Plant { Nickname = "Also nameless", Genus = "  " }
        ]);

        Assert.Equal(["Hoya", "Calathea", "Ficus", null], summary.ByGenus.Select(r => r.Name));
        Assert.Equal([2, 1, 1, 2], summary.ByGenus.Select(r => r.Count));
        Assert.All(summary.ByGenus, r => Assert.Null(r.Id));
    }

    [Fact]
    public void There_is_no_no_genus_row_when_every_plant_has_one()
    {
        var summary = Summary([new Plant { Genus = "Hoya" }]);

        Assert.DoesNotContain(summary.ByGenus, r => r.Name is null);
    }

    [Fact]
    public void Spots_count_toward_their_room_and_plants_without_a_place_come_last()
    {
        var places = new FakePlaceRepository();
        var kitchen = places.Add("Kitchen");
        var living = places.Add("Living room");
        var windowsill = places.Add("Windowsill", living);

        var summary = Summary(
        [
            new Plant { Genus = "A", PlaceId = kitchen.Id },
            new Plant { Genus = "A", PlaceId = living.Id },
            new Plant { Genus = "A", PlaceId = windowsill.Id },
            new Plant { Genus = "A" }
        ], places: places.Current);

        Assert.Equal(["Living room", "Kitchen", null], summary.ByRoom.Select(r => r.Name));
        Assert.Equal([2, 1, 1], summary.ByRoom.Select(r => r.Count));
        Assert.Equal([living.Id, kitchen.Id, (Guid?)null], summary.ByRoom.Select(r => r.Id));
    }

    [Fact]
    public void A_spot_whose_room_was_deleted_counts_as_a_room_of_its_own()
    {
        var places = new FakePlaceRepository();
        var living = places.Add("Living room");
        var windowsill = places.Add("Windowsill", living);
        living.DeletedAt = DateTimeOffset.UnixEpoch;

        var summary = Summary([new Plant { Genus = "A", PlaceId = windowsill.Id }], places: places.Current);

        var row = Assert.Single(summary.ByRoom);
        Assert.Equal("Windowsill", row.Name);
        Assert.Equal(windowsill.Id, row.Id);
    }

    [Fact]
    public void Longest_held_goes_by_the_date_it_was_got_when_there_is_one()
    {
        Plant added = new() { Nickname = "Added", CreatedAt = new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero) };
        Plant got = new() { Nickname = "Got", AcquiredOn = LooseDate.Of(2019), CreatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };

        var longest = Summary([added, got]).Longest;

        Assert.NotNull(longest);
        Assert.Same(got, longest.Plant);
        Assert.Equal(new DateOnly(2019, 1, 1), longest.Since);
        Assert.True(longest.YearOnly);
        Assert.False(longest.MonthOnly);
    }

    [Fact]
    public void Longest_held_says_how_exact_the_date_is()
    {
        var month = Summary([new Plant { Nickname = "M", AcquiredOn = LooseDate.Of(2020, 3) }]).Longest!;
        var day = Summary([new Plant { Nickname = "D", AcquiredOn = LooseDate.Of(new DateOnly(2020, 3, 14)) }]).Longest!;
        var added = Summary([new Plant { Nickname = "C", CreatedAt = new DateTimeOffset(2021, 6, 15, 12, 0, 0, TimeSpan.Zero) }]).Longest!;

        Assert.True(month.MonthOnly);
        Assert.False(month.YearOnly);
        Assert.False(day.MonthOnly);
        Assert.False(day.YearOnly);
        Assert.False(added.YearOnly);
        Assert.False(added.MonthOnly);
        Assert.Equal(2021, added.Since.Year);
    }

    [Fact]
    public void Longest_held_ties_go_to_the_name_first_in_the_alphabet()
    {
        Plant zebra = new() { Nickname = "Zebra", AcquiredOn = LooseDate.Of(2020) };
        Plant aloe = new() { Nickname = "Aloe", AcquiredOn = LooseDate.Of(2020) };

        Assert.Same(aloe, Summary([zebra, aloe]).Longest!.Plant);
    }

    [Fact]
    public void Longest_held_ignores_plants_that_are_gone()
    {
        Plant gone = new() { Nickname = "Gone", Status = PlantStatus.Died, AcquiredOn = LooseDate.Of(2010) };
        Plant here = new() { Nickname = "Here", AcquiredOn = LooseDate.Of(2020) };

        Assert.Same(here, Summary([gone, here]).Longest!.Plant);
    }

    [Fact]
    public void An_empty_collection_has_nothing_to_count()
    {
        var summary = Summary([]);

        Assert.Equal(0, summary.Plants);
        Assert.Null(summary.Longest);
        Assert.Empty(summary.ByGenus);
        Assert.Empty(summary.ByRoom);
        Assert.False(summary.AnyPropagations);
    }

    [Fact]
    public void Propagations_count_the_ones_rooting_now_and_those_rooted_this_year()
    {
        var propagations = new[]
        {
            new Propagation { Nickname = "A", Stage = PropagationStage.Started },
            new Propagation { Nickname = "B", Stage = PropagationStage.Rooting },
            new Propagation { Nickname = "C", Stage = PropagationStage.Rooted, RootedOn = new DateOnly(2026, 3, 1) },
            new Propagation { Nickname = "D", Stage = PropagationStage.Done, RootedOn = new DateOnly(2026, 2, 1) },
            new Propagation { Nickname = "E", Stage = PropagationStage.Done, RootedOn = new DateOnly(2025, 12, 1) },
            new Propagation { Nickname = "F", Stage = PropagationStage.Rooting, DeletedAt = DateTimeOffset.UnixEpoch, RootedOn = new DateOnly(2026, 4, 1) }
        };

        var summary = Summary([], propagations);

        Assert.Equal(2, summary.RootingNow);
        Assert.Equal(2, summary.RootedThisYear);
        Assert.True(summary.AnyPropagations);
    }

    [Fact]
    public void Deleted_propagations_alone_are_no_propagations()
    {
        var summary = Summary([], [new Propagation { Nickname = "Gone", DeletedAt = DateTimeOffset.UnixEpoch }]);

        Assert.False(summary.AnyPropagations);
    }

    [Theory]
    [InlineData(2019, 3, 14, "Since 14 March 2019, 7 years")]
    [InlineData(2025, 10, 22, "Since 22 October 2025, 1 year")]
    [InlineData(2026, 5, 23, "Since 23 May 2026, 4 months")]
    [InlineData(2026, 9, 22, "Since 22 September 2026, 1 month")]
    [InlineData(2026, 10, 10, "Since 10 October 2026, less than a month")]
    public void Longest_held_text_gives_the_date_and_how_long(int year, int month, int day, string expected)
    {
        var held = new LongestHeld(new Plant(), new DateOnly(year, month, day), false, false);

        Assert.Equal(expected, held.Text(Today));
    }

    [Fact]
    public void Longest_held_text_shows_only_what_is_known_of_the_date()
    {
        Plant plant = new();

        Assert.Equal("Since March 2019, 7 years", new LongestHeld(plant, new DateOnly(2019, 3, 1), false, true).Text(Today));
        Assert.Equal("Since 2019, 7 years", new LongestHeld(plant, new DateOnly(2019, 1, 1), true, false).Text(Today));
    }
}
