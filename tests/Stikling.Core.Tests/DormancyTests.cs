using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class DormancyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();

    private static string Label(Enum value) => value.ToString();

    [Fact]
    public void Days_dormant_count_from_the_day_it_went_dormant()
    {
        Assert.Equal(40, new Plant { DormantSince = Today.AddDays(-40) }.DaysDormant(Today));
        Assert.Null(new Plant().DaysDormant(Today));
        Assert.Equal(0, new Propagation { DormantSince = Today }.DaysDormant(Today));
    }

    [Fact]
    public void Dormancy_cannot_start_in_the_future()
    {
        const string error = "It can't go dormant in the future.";
        Assert.Contains(error, new Plant { Nickname = "A", DormantSince = Today.AddDays(1) }.Validate(Today));
        Assert.Contains(error, new Propagation { Nickname = "A", StartedOn = Today, DormantSince = Today.AddDays(1) }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "A", DormantSince = Today }.Validate(Today));
    }

    [Fact]
    public void Going_dormant_and_waking_up_are_described()
    {
        var plant = new Plant { Nickname = "Alocasia" };

        var resting = plant.Copy();
        resting.DormantSince = Today;
        Assert.Equal(["Went dormant"], PlantChanges.Describe(plant, resting, Label));

        var awake = resting.Copy();
        awake.DormantSince = null;
        Assert.Equal(["Woke up from dormancy"], PlantChanges.Describe(resting, awake, Label));

        var redated = resting.Copy();
        redated.DormantSince = Today.AddDays(-10);
        Assert.Empty(PlantChanges.Describe(resting, redated, Label));
    }

    [Fact]
    public async Task Waking_a_plant_up_is_recorded_on_the_history()
    {
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var plant = new Plant { Nickname = "Alocasia", DormantSince = Today.AddDays(-60) };

        await service.SetDormantAsync(plant, null, Label);
        await service.SetDormantAsync(plant, null, Label); // already awake, nothing more to say

        Assert.False(plant.IsDormant);
        Assert.Equal("Woke up from dormancy", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public async Task A_dormant_plant_that_dies_stops_being_dormant_without_saying_it_woke_up()
    {
        var service = new PlantService(plants, timeline, new FixedTime(Now));
        var plant = new Plant { Nickname = "Alocasia", DormantSince = Today.AddDays(-90) };

        await service.SetStatusAsync([plant], PlantStatus.Died, Label);

        Assert.False(plant.IsDormant);
        Assert.Equal("Status: Died (was Active)", Assert.Single(timeline.Entries).Text);
    }

    [Fact]
    public void The_dormant_filter_shows_only_dormant_plants()
    {
        var resting = new Plant { Nickname = "Alocasia", DormantSince = Today };
        var growing = new Plant { Nickname = "Monstera" };

        Assert.Equal([resting], new PlantFilter(Dormant: true).Apply([resting, growing]));
        Assert.Equal(2, new PlantFilter().Apply([resting, growing]).Count());
    }

    [Fact]
    public void A_dormant_propagation_is_left_off_Today()
    {
        var time = new FixedTime(Now);
        var resting = new Propagation { Nickname = "Corms", StartedOn = Today.AddDays(-60), DormantSince = Today.AddDays(-30) };
        var stuck = new Propagation { Nickname = "Cutting", StartedOn = Today.AddDays(-60) };

        var check = Assert.Single(TodayBoard.NeedsChecking([resting, stuck], new Dictionary<Guid, TimelineEntry>(), time));
        Assert.Same(stuck, check.Propagation);
    }

    [Fact]
    public async Task Waking_a_propagation_up_is_recorded_and_counts_as_looking_at_it()
    {
        var time = new FixedTime(Now);
        var service = new PropagationService(propagations, plants, timeline, time);
        var corms = new Propagation { Nickname = "Corms", StartedOn = Today.AddDays(-60), DormantSince = Today.AddDays(-30) };
        propagations.Propagations[corms.Id] = corms;

        await service.SetDormantAsync(corms, null, Label);

        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Woke up from dormancy", entry.Text);
        Assert.Empty(TodayBoard.NeedsChecking([corms], new Dictionary<Guid, TimelineEntry> { [corms.Id] = entry }, time));
    }

    [Fact]
    public void A_finished_propagation_stops_being_dormant()
    {
        var corms = new Propagation { Nickname = "Corms", StartedOn = Today.AddDays(-60), InitialCount = 2, DormantSince = Today.AddDays(-30) };

        corms.RecordFailed(1, new DateOnly(2026, 9, 20));
        Assert.True(corms.IsDormant);

        corms.RecordFailed(1, new DateOnly(2026, 9, 20));
        Assert.Equal(PropagationStage.Failed, corms.Stage);
        Assert.False(corms.IsDormant);
    }

    [Fact]
    public void Finishing_says_so_through_the_stage_and_not_as_waking_up()
    {
        var before = new Propagation { Nickname = "Corms", StartedOn = Today, DormantSince = Today };
        var after = before.Copy();
        after.RecordFailed(1, new DateOnly(2026, 9, 20));

        Assert.Equal(["Stage: Failed (was Started)"], PropagationChanges.Describe(before, after, Label));
    }

    [Fact]
    public void Corm_size_is_described_when_it_changes()
    {
        var before = new Propagation { Nickname = "Corm", StartedOn = Today, Type = PropagationType.Corm };
        var after = before.Copy();
        after.CormSizeMm = 8;
        Assert.Equal(["Corm size: 8 mm"], PropagationChanges.Describe(before, after, Label));
        Assert.Equal(["Corm size cleared"], PropagationChanges.Describe(after, before, Label));
    }

    [Fact]
    public void Seed_counts_are_described_when_they_change()
    {
        var before = new Propagation { Nickname = "Coleus", StartedOn = Today, Type = PropagationType.Seed, InitialCount = 20 };
        var after = before.Copy();
        after.SeedsGerminated = 12;

        Assert.Equal(["Seeds: 12 of 20 came up"], PropagationChanges.Describe(before, after, Label));
    }
}
