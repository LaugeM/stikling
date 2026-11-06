using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class WateringSeasonsTests
{
    // From here, the first three gaps of a list start before 21 September and the rest after it
    private static readonly DateOnly First = new(2025, 8, 25);

    private static readonly int[] Both = [9, 10, 12, 15, 20, 24];

    private static TimeProvider Time { get; } = new FixedTime(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));

    private static CareLog Log(Plant plant, DateOnly day, CareKind kind = CareKind.Watered) =>
        new() { PlantId = plant.Id, OccurredOn = day, Kind = kind };

    private static List<CareLog> Waterings(Plant plant, DateOnly first, params int[] gaps)
    {
        var logs = new List<CareLog> { Log(plant, first) };
        var day = first;
        foreach (var gap in gaps)
        {
            day = day.AddDays(gap);
            logs.Add(Log(plant, day));
        }
        return logs;
    }

    private static WateringSeasons? Seasonal(
        Plant plant,
        IEnumerable<CareLog> logs,
        bool lit = false,
        Hemisphere hemisphere = Hemisphere.Northern,
        IEnumerable<TimelineEntry>? timeline = null,
        IReadOnlyList<AwayStretch>? stretches = null) =>
        WateringSeasons.Of(plant, logs, timeline ?? [], lit, hemisphere, stretches ?? [], Time);

    private static TimelineEntry Change(Plant plant, DateOnly day, bool dormant) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        Kind = TimelineKind.Change,
        OccurredAt = new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero),
        Text = dormant ? "Went dormant" : "Woke up from dormancy"
    };

    [Fact]
    public void Gives_a_range_for_each_half_once_both_have_enough()
    {
        var plant = new Plant { Nickname = "Hoya" };

        var seasons = Seasonal(plant, Waterings(plant, First, Both));

        Assert.Equal(new DayRange(9, 12), seasons?.Brighter);
        Assert.Equal(new DayRange(15, 24), seasons?.Darker);
    }

    [Fact]
    public void Says_nothing_with_too_few_waterings_in_the_darker_half()
    {
        var plant = new Plant { Nickname = "Hoya" };

        Assert.Null(Seasonal(plant, Waterings(plant, First, 9, 10, 12, 15, 20)));
    }

    [Fact]
    public void Says_nothing_with_too_few_waterings_in_the_brighter_half()
    {
        var plant = new Plant { Nickname = "Hoya" };

        Assert.Null(Seasonal(plant, Waterings(plant, First.AddDays(9), 10, 12, 15, 20, 24)));
    }

    [Fact]
    public void The_halves_swap_in_the_southern_hemisphere()
    {
        var plant = new Plant { Nickname = "Hoya" };

        var seasons = Seasonal(plant, Waterings(plant, First, Both), hemisphere: Hemisphere.Southern);

        Assert.Equal(new DayRange(15, 24), seasons?.Brighter);
        Assert.Equal(new DayRange(9, 12), seasons?.Darker);
    }

    [Fact]
    public void Says_nothing_under_a_grow_light()
    {
        var plant = new Plant { Nickname = "Hoya" };

        Assert.Null(Seasonal(plant, Waterings(plant, First, Both), lit: true));
    }

    [Fact]
    public void A_gap_a_rest_began_in_is_left_out()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, First, 9, 10, 12, 15, 20, 24, 90);
        var lastDay = logs[^2].OccurredOn;
        var timeline = new[] { Change(plant, lastDay.AddDays(3), true), Change(plant, lastDay.AddDays(60), false) };

        Assert.Equal(new DayRange(15, 24), Seasonal(plant, logs, timeline: timeline)?.Darker);
    }

    [Fact]
    public void Gaps_started_during_a_rest_are_left_out()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, First, Both);
        var timeline = new[] { Change(plant, logs[4].OccurredOn, true), Change(plant, logs[^1].OccurredOn, false) };

        // Only the first gap of the darker half is left, the one that ended on the day the rest began
        Assert.Null(Seasonal(plant, logs, timeline: timeline));
    }

    [Fact]
    public void A_rest_that_began_on_the_day_of_the_next_watering_leaves_the_gap_in()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, First, Both);
        var timeline = new[] { Change(plant, logs[^1].OccurredOn, true), Change(plant, logs[^1].OccurredOn.AddDays(5), false) };

        Assert.Equal(new DayRange(15, 24), Seasonal(plant, logs, timeline: timeline)?.Darker);
    }

    [Fact]
    public void A_plant_resting_now_still_shows_what_it_did_before()
    {
        var plant = new Plant { Nickname = "Hoya", DormantSince = new DateOnly(2026, 1, 1) };

        Assert.NotNull(Seasonal(plant, Waterings(plant, First, Both)));
    }

    [Fact]
    public void A_gap_across_a_stretch_away_is_left_out()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, First, Both);
        var away = new AwayStretch { From = logs[5].OccurredOn.AddDays(1), Until = logs[6].OccurredOn.AddDays(-1) };

        Assert.Null(Seasonal(plant, logs, stretches: [away]));
    }

    [Fact]
    public void One_long_gap_does_not_stretch_the_range()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, First, 9, 10, 12, 15, 16, 17, 18, 60);

        Assert.Equal(new DayRange(16, 18), Seasonal(plant, logs)?.Darker);
    }

    [Fact]
    public void Low_and_high_are_the_same_when_the_gaps_are()
    {
        var plant = new Plant { Nickname = "Hoya" };

        var seasons = Seasonal(plant, Waterings(plant, First.AddDays(7), 7, 7, 7, 14, 14, 14));

        Assert.Equal(new DayRange(7, 7), seasons?.Brighter);
        Assert.Equal(new DayRange(14, 14), seasons?.Darker);
    }

    [Fact]
    public void Only_counts_since_the_way_it_gets_water_changed()
    {
        var plant = new Plant { Nickname = "Hoya", WateringSince = First.AddDays(9) };

        Assert.Null(Seasonal(plant, Waterings(plant, First, Both)));
    }

    [Fact]
    public void Other_plants_and_deleted_entries_do_not_count()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var other = new Plant { Nickname = "Monstera" };
        var logs = Waterings(plant, First, 9, 10, 12, 15, 20);
        var deleted = Log(plant, logs[^1].OccurredOn.AddDays(24));
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;
        logs.Add(deleted);
        logs.AddRange(Waterings(other, First, Both));

        Assert.Null(Seasonal(plant, logs));
    }

    [Theory]
    [InlineData(3, 20, false)]
    [InlineData(3, 21, true)]
    [InlineData(9, 20, true)]
    [InlineData(9, 21, false)]
    public void The_brighter_half_runs_between_the_equinoxes(int month, int day, bool brighterInTheNorth)
    {
        var date = new DateOnly(2026, month, day);

        Assert.Equal(brighterInTheNorth, Seasons.InBrighterHalf(date, Hemisphere.Northern));
        Assert.Equal(!brighterInTheNorth, Seasons.InBrighterHalf(date, Hemisphere.Southern));
    }
}
