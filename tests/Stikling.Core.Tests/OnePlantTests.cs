using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>Just one plant: which plant comes up, and which are left out.</summary>
public class OnePlantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 14);

    private readonly FakeTimelineRepository timeline = new();
    private readonly TimelineService service;
    private readonly List<Plant> plants = [];
    private readonly List<AwayStretch> stretches = [];

    public OnePlantTests() =>
        service = new TimelineService(timeline, new FakePhotoRepository(), new FakeMeasurementRepository(), new FixedTime(Now));

    private Plant Add(string name, Action<Plant>? change = null)
    {
        var plant = new Plant { Nickname = name };
        change?.Invoke(plant);
        plants.Add(plant);
        return plant;
    }

    private async Task<Plant?> PickAsync(params Guid[] seen) =>
        OnePlant.Pick(plants, await timeline.GetLastActivityBeforeAsync(Now.AddDays(1)), stretches, Today, new FixedTime(Now), seen.ToHashSet());

    private async Task LookedAsync(Plant plant, int daysAgo)
    {
        var entry = await service.LookedOverAsync(SubjectType.Plant, plant.Id);
        entry.OccurredAt = Now.AddDays(-daysAgo);
    }

    [Fact]
    public async Task A_flagged_plant_comes_first_the_oldest_flag_before_the_rest()
    {
        Add("Calm");
        Add("Newer flag", p => p.Attention = new("Drooping", Today.AddDays(-1)));
        var oldest = Add("Older flag", p => p.Attention = new("Yellow leaves", Today.AddDays(-9)));

        Assert.Equal(oldest.Id, (await PickAsync())?.Id);
    }

    [Fact]
    public async Task Otherwise_the_plant_longest_since_a_look_comes_up_and_one_never_looked_at_counts_as_longest()
    {
        var recent = Add("Recent");
        var old = Add("Old");
        var never = Add("Never");
        await LookedAsync(recent, 1);
        await LookedAsync(old, 20);

        Assert.Equal(never.Id, (await PickAsync())?.Id);
        Assert.Equal(old.Id, (await PickAsync(never.Id))?.Id);
    }

    [Fact]
    public async Task A_plant_looked_over_today_is_skipped_even_when_flagged()
    {
        var looked = Add("Looked", p => p.Attention = new("Drooping", Today.AddDays(-5)));
        var other = Add("Other", p => p.Attention = new("Yellow leaves", Today.AddDays(-1)));
        await LookedAsync(looked, 0);

        Assert.Equal(other.Id, (await PickAsync())?.Id);

        await LookedAsync(other, 0);
        Assert.Null(await PickAsync());
    }

    [Fact]
    public async Task A_tie_goes_to_the_name()
    {
        Add("Zamioculcas");
        var first = Add("Aloe");

        Assert.Equal(first.Id, (await PickAsync())?.Id);
    }

    [Fact]
    public async Task Dormant_plants_and_plants_that_have_left_are_left_out()
    {
        Add("Asleep", p => { p.DormantSince = Today.AddDays(-30); p.Attention = new("Check", Today); });
        Add("Gone", p => p.Status = PlantStatus.Died);
        var here = Add("Here");

        Assert.Equal(here.Id, (await PickAsync())?.Id);
    }

    [Fact]
    public async Task Plants_already_seen_in_the_visit_are_skipped_and_none_is_left_at_the_end()
    {
        var only = Add("Only");

        Assert.Null(await PickAsync(only.Id));
    }

    [Fact]
    public async Task Nothing_is_picked_in_away_time_or_when_there_are_no_plants()
    {
        Add("Here", p => p.Attention = new("Check", Today));
        stretches.Add(new AwayStretch { From = Today.AddDays(-1), Until = Today.AddDays(3) });

        Assert.Null(await PickAsync());

        stretches.Clear();
        plants.Clear();
        Assert.Null(await PickAsync());
    }
}
