using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class SwapListTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 18, 0, 0, TimeSpan.Zero);

    private static string? Everyday(string? genus, string? species, string? cultivar) =>
        genus == "Epipremnum" ? "pothos" : null;

    private static Propagation Cutting(int count, PropagationStage stage = PropagationStage.Rooted, int? spare = null) => new()
    {
        Genus = "Epipremnum", Species = "aureum", Cultivar = "Marble Queen",
        InitialCount = count, Stage = stage, SpareCount = spare, StartedOn = new DateOnly(2026, 10, 1)
    };

    private static PropagationService ServiceFor(FakePropagationRepository propagations) =>
        new(propagations, new FakePlantRepository(), new FakeTimelineRepository(),
            new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FixedTime(Now));

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    [Fact]
    public void Spare_is_never_more_than_what_is_left()
    {
        var cuttings = Cutting(5, spare: 4);
        cuttings.FailedCount = 2;

        Assert.Equal(3, cuttings.SpareAvailable);
    }

    [Fact]
    public void A_finished_or_deleted_propagation_has_nothing_to_spare()
    {
        var done = Cutting(2, PropagationStage.Done, spare: 2);
        var gone = Cutting(2, spare: 2);
        gone.DeletedAt = Now;

        Assert.Equal(0, done.SpareAvailable);
        Assert.Equal(0, gone.SpareAvailable);
        Assert.Equal(0, Cutting(2).SpareAvailable);
    }

    [Fact]
    public void Giving_cuttings_away_takes_them_off_the_spare_count_first()
    {
        var cuttings = Cutting(5, spare: 2);

        cuttings.RecordGivenAway(1, new DateOnly(2026, 10, 14));
        Assert.Equal(1, cuttings.SpareCount);

        cuttings.RecordGivenAway(3, new DateOnly(2026, 10, 14));
        Assert.Equal(0, cuttings.SpareCount);
        Assert.Equal(0, cuttings.SpareAvailable);
    }

    [Fact]
    public async Task Giving_away_through_the_service_saves_the_spare_count_with_it()
    {
        var propagations = new FakePropagationRepository();
        var cuttings = Cutting(4, spare: 3);
        await propagations.SaveAsync(cuttings);

        await ServiceFor(propagations).GiveAwayAsync(cuttings, 2, "Anna");

        Assert.Equal(1, propagations.Propagations[cuttings.Id].SpareCount);
    }

    [Fact]
    public async Task Setting_the_spare_count_keeps_it_within_what_is_left()
    {
        var service = ServiceFor(new FakePropagationRepository());
        var cuttings = Cutting(3);

        await service.SetSpareAsync(cuttings, 9);
        Assert.Equal(3, cuttings.SpareCount);

        await service.SetSpareAsync(cuttings, 0);
        Assert.Null(cuttings.SpareCount);
    }

    [Fact]
    public void Only_plants_still_here_count_and_their_marks_are_left_alone()
    {
        var here = new Plant { Nickname = "A", Spare = SpareMark.UpForSwap };
        var left = new Plant { Nickname = "B", Spare = SpareMark.UpForSwap, Status = PlantStatus.GivenAway };

        var list = SwapLists.Build([here, left], []);

        Assert.Equal("A", Assert.Single(list.Plants).Name);
        Assert.Equal(SpareMark.UpForSwap, left.Spare);
    }

    [Fact]
    public void The_list_has_cuttings_cuttings_on_request_and_plants()
    {
        var spare = new Plant { Nickname = "Hoya", Spare = SpareMark.CuttingsToSpare };
        var swap = new Plant { Nickname = "Fern", Spare = SpareMark.UpForSwap };

        var list = SwapLists.Build([spare, swap], [Cutting(3, spare: 3), Cutting(2)]);

        var line = Assert.Single(list.Cuttings);
        Assert.Equal(3, line.Count);
        Assert.Equal(PropagationStage.Rooted, line.Stage);
        Assert.Equal("Hoya", Assert.Single(list.OnRequest).Name);
        Assert.Equal("Fern", Assert.Single(list.Plants).Name);
        Assert.False(list.IsEmpty);
        Assert.True(SwapLists.Build([], []).IsEmpty);
    }

    [Fact]
    public void The_swap_text_groups_adds_counts_and_ends_with_the_footer()
    {
        var text = SwapLists.WriteSwap(
            [new Plant { Genus = "Hoya", Species = "carnosa", Spare = SpareMark.CuttingsToSpare },
             new Plant { Nickname = "Fern", Spare = SpareMark.UpForSwap },
             new Plant { Nickname = "Fern", Spare = SpareMark.UpForSwap }],
            [Cutting(3, spare: 3), Cutting(2, PropagationStage.Rooting, spare: 1)],
            Everyday, nicknames: false, footer: true);

        Assert.Equal(Lines(
            "Available for swap",
            "",
            "Cuttings",
            "Epipremnum aureum 'Marble Queen' (pothos) ×1, rooting",
            "Epipremnum aureum 'Marble Queen' (pothos) ×3, rooted",
            "",
            "Cuttings on request",
            "Hoya carnosa",
            "",
            "Plants",
            "Fern ×2",
            "",
            "Kept in Stikling, stikling.app"), text);
    }

    [Fact]
    public void The_footer_can_be_left_off()
    {
        var text = SwapLists.WriteSwap([], [Cutting(1, spare: 1)], Everyday, false, footer: false);

        Assert.DoesNotContain("stikling.app", text);
    }

    [Fact]
    public void The_plant_text_counts_the_same_plants_once_and_sorts_by_name()
    {
        var plants = new[]
        {
            new Plant { Nickname = "Big", Genus = "Monstera", Species = "deliciosa" },
            new Plant { Nickname = "Small", Genus = "Monstera", Species = "deliciosa" },
            new Plant { Nickname = "Pothos", Genus = "Epipremnum", Species = "aureum" },
            new Plant { Nickname = "Kitchen basil" }
        };

        var text = SwapLists.WritePlants(plants, Everyday, nicknames: false, footer: false);

        Assert.Equal(Lines("Epipremnum aureum (pothos)", "Kitchen basil", "Monstera deliciosa ×2"), text);
    }

    [Fact]
    public void The_plant_text_shows_nicknames_only_when_asked()
    {
        var plants = new[] { new Plant { Nickname = "Big", Genus = "Monstera", Species = "deliciosa" } };

        Assert.Equal("Monstera deliciosa \"Big\"", SwapLists.WritePlants(plants, Everyday, true, false));
        Assert.Equal("Monstera deliciosa", SwapLists.WritePlants(plants, Everyday, false, false));
    }

    [Fact]
    public void The_plant_text_shows_the_spare_mark_and_nothing_private()
    {
        var plant = new Plant
        {
            Genus = "Hoya", Species = "carnosa", Spare = SpareMark.CuttingsToSpare, Tags = ["rare"],
            Source = "A shop", PricePaid = 99, Notes = "secret"
        };
        var left = new Plant { Genus = "Hoya", Species = "carnosa", Spare = SpareMark.UpForSwap, Status = PlantStatus.Sold };

        var text = SwapLists.WritePlants([plant, left], Everyday, false, true);

        Assert.Equal(Lines("Hoya carnosa", "Hoya carnosa, cuttings to spare", "", "Kept in Stikling, stikling.app"), text);
    }

    [Fact]
    public void Plants_tagged_for_swap_without_a_mark_are_found_in_any_case()
    {
        var tagged = new Plant { Nickname = "A", Tags = ["FOR SWAP"] };
        var marked = new Plant { Nickname = "B", Tags = ["For swap"], Spare = SpareMark.UpForSwap };
        var other = new Plant { Nickname = "C", Tags = ["Rare"] };

        Assert.Same(tagged, Assert.Single(PlantService.TaggedForSwap([tagged, marked, other])));
    }

    [Fact]
    public async Task Marking_plants_changes_only_the_ones_that_differ()
    {
        var service = new PlantService(new FakePlantRepository(), new FakeTimelineRepository(), new FixedTime(Now));
        var a = new Plant { Nickname = "A" };
        var b = new Plant { Nickname = "B", Spare = SpareMark.UpForSwap };

        Assert.Equal(1, await service.SetSpareAsync([a, b], SpareMark.UpForSwap));
        Assert.Equal(SpareMark.UpForSwap, a.Spare);
        Assert.Equal(2, await service.SetSpareAsync([a, b], null));
    }

    [Fact]
    public void Records_from_before_read_as_unmarked_and_the_mark_is_stored_as_text()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var oldPlant = JsonSerializer.Deserialize<Plant>("{\"nickname\":\"Old\"}", options);
        var oldCutting = JsonSerializer.Deserialize<Propagation>("{\"nickname\":\"Old\"}", options);
        var marked = JsonSerializer.Serialize(new Plant { Spare = SpareMark.UpForSwap }, options);

        Assert.Null(oldPlant!.Spare);
        Assert.Null(oldCutting!.SpareCount);
        Assert.Contains("\"UpForSwap\"", marked);
    }
}
