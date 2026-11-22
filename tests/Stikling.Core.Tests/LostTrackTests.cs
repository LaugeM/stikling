using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

/// <summary>I lost track of watering: when it is offered, and what it saves.</summary>
public class LostTrackTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 14);

    private readonly List<AwayStretch> stretches = [];

    private static List<CareLog> Waterings(Plant plant, params int[] daysAgo) =>
        [.. daysAgo.Select(d => new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-d) })];

    private bool Offered(Plant plant, List<CareLog> logs) => LostTrack.Offered(plant, logs, stretches, Today);

    [Fact]
    public void Not_offered_without_any_watering()
    {
        var plant = new Plant { Nickname = "Ficus" };

        Assert.False(Offered(plant, []));
    }

    [Fact]
    public void With_a_usual_gap_it_is_offered_after_more_than_twice_that_gap()
    {
        var plant = new Plant { Nickname = "Ficus" };
        // Weekly: the last watering 14 days ago is exactly twice the gap, 15 is more
        Assert.False(Offered(plant, Waterings(plant, 14, 21, 28, 35)));
        Assert.True(Offered(plant, Waterings(plant, 15, 22, 29, 36)));
    }

    [Fact]
    public void Without_a_usual_gap_it_takes_over_30_days()
    {
        var plant = new Plant { Nickname = "Ficus" };

        Assert.False(Offered(plant, Waterings(plant, 30)));
        Assert.True(Offered(plant, Waterings(plant, 31)));
        // Two gaps are too few to be a usual one
        Assert.False(Offered(plant, Waterings(plant, 20, 27, 34)));
        Assert.True(Offered(plant, Waterings(plant, 31, 38, 45)));
    }

    [Fact]
    public void Not_offered_for_a_dormant_plant()
    {
        var plant = new Plant { Nickname = "Ficus", DormantSince = Today.AddDays(-60) };

        Assert.False(Offered(plant, Waterings(plant, 90)));
    }

    [Fact]
    public void Waterings_before_the_day_it_counts_from_say_nothing_so_it_is_not_offered_again()
    {
        var plant = new Plant { Nickname = "Ficus", WateringSince = Today };

        Assert.False(Offered(plant, Waterings(plant, 90, 97, 104, 111)));
    }

    [Fact]
    public async Task Saving_sets_the_day_for_each_plant_and_leaves_the_history_alone()
    {
        var plants = new FakePlantRepository();
        var timeline = new FakeTimelineRepository();
        var first = new Plant { Nickname = "Ficus" };
        var second = new Plant { Nickname = "Aloe", WateringSince = Today.AddDays(-100) };
        await plants.SaveAsync(first);
        await plants.SaveAsync(second);
        var service = new PlantService(plants, timeline, new FixedTime(Now));

        Assert.Equal(2, await service.LostTrackAsync([first, second]));
        Assert.Equal(0, await service.LostTrackAsync([first, second]));

        Assert.Equal(Today, plants.Plants[first.Id].WateringSince);
        Assert.Equal(Today, plants.Plants[second.Id].WateringSince);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Counting_from_today_drops_the_old_waterings_from_what_is_learned_and_keeps_them_in_the_log()
    {
        var plant = new Plant { Nickname = "Ficus" };
        var logs = Waterings(plant, 90, 97, 104, 111);
        var plants = new FakePlantRepository();
        await plants.SaveAsync(plant);

        await new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now)).LostTrackAsync([plant]);

        Assert.Empty(WateringGuess.Waterings(plant, logs));
        Assert.Equal(4, logs.Count);
    }
}
