using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class WateringAwayTests
{
    private static DateOnly April(int day) => new(2026, 4, day);

    private static AwayStretch Stretch(DateOnly from, DateOnly until, bool deleted = false) =>
        new()
        {
            From = from, Until = until,
            DeletedAt = deleted ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : null
        };

    private static CareLog Log(Plant plant, DateOnly day, CareKind kind = CareKind.Watered, int? moisture = null) =>
        new() { PlantId = plant.Id, OccurredOn = day, Kind = kind, Moisture = moisture };

    private static List<CareLog> Waterings(Plant plant, params DateOnly[] days) => [.. days.Select(d => Log(plant, d))];

    private static IReadOnlyList<(DateOnly Start, int Days)> Gaps(
        Plant plant, IEnumerable<CareLog> logs, params AwayStretch[] stretches) =>
        WateringGuess.Gaps(plant, logs, stretches);

    [Fact]
    public void A_gap_across_a_stretch_is_left_out()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(8), April(15), April(22));

        var gaps = Gaps(plant, logs, Stretch(April(9), April(14)));

        Assert.Equal([(April(1), 7), (April(15), 7)], gaps);
    }

    [Fact]
    public void The_same_gap_is_kept_when_a_watering_was_logged_inside_the_stretch()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(8), April(11), April(15));

        var gaps = Gaps(plant, logs, Stretch(April(9), April(14)));

        Assert.Equal([(April(1), 7), (April(8), 3), (April(11), 4)], gaps);
    }

    [Fact]
    public void Any_kind_that_puts_water_in_counts_as_a_watering_inside_the_stretch()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(8), April(15));
        logs.Add(Log(plant, April(11), CareKind.ToppedUp));

        Assert.Equal(3, Gaps(plant, logs, Stretch(April(9), April(14))).Count);
    }

    [Fact]
    public void A_watering_on_the_edge_of_the_stretch_does_not_count_as_inside()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(9), April(14), April(20));

        // 9 and 14 are the first day and the day back, so (9, 14) has nothing strictly inside
        var gaps = Gaps(plant, logs, Stretch(April(9), April(14)));

        Assert.Equal([(April(1), 8), (April(14), 6)], gaps);
    }

    [Fact]
    public void A_gap_that_only_touches_a_stretch_is_kept()
    {
        var plant = new Plant { Nickname = "Hoya" };

        // The gap ends the day the stretch starts
        Assert.Single(Gaps(plant, Waterings(plant, April(1), April(8)), Stretch(April(8), April(12))));

        // The gap starts the day back
        Assert.Single(Gaps(plant, Waterings(plant, April(8), April(15)), Stretch(April(2), April(8))));
    }

    [Fact]
    public void Deleted_stretches_do_not_count()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(8), April(15));

        Assert.Equal(2, Gaps(plant, logs, Stretch(April(9), April(14), deleted: true)).Count);
    }

    [Fact]
    public void The_dormant_overload_leaves_them_out_too()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, April(1), April(8), April(15));

        var awake = WateringGuess.Gaps(plant, logs, DormantPeriods.Of(plant, [], new FixedTime(DateTimeOffset.UtcNow)), dormant: false,
            [Stretch(April(9), April(14))]);

        Assert.Equal([(April(1), 7)], awake);
    }

    [Fact]
    public void The_guess_learns_from_the_gaps_that_are_left()
    {
        var plant = new Plant { Nickname = "Hoya" };

        // Weekly, but a month away in the middle
        var logs = Waterings(plant, April(1), April(8), April(15), April(22), new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 27));
        var today = new DateOnly(2026, 5, 28);
        var time = new FixedTime(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));

        var with = WateringGuess.Of(plant, logs, [], WateringForm.TopWatered, false, Hemisphere.Northern, today, null,
            [Stretch(April(23), new DateOnly(2026, 5, 19))], time);
        var without = WateringGuess.Of(plant, logs, [], WateringForm.TopWatered, false, Hemisphere.Northern, today, null, [], time);

        Assert.Equal(7, with.UsualDays);
        Assert.Equal(WateringMode.Interval, with.Mode);
        Assert.Equal(5, with.LearnedFrom);
        Assert.Equal(WateringMode.Interval, without.Mode);
        Assert.Equal(6, without.LearnedFrom);
    }

    [Fact]
    public void A_dry_down_across_a_stretch_is_left_out()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = new List<CareLog>
        {
            Log(plant, April(1)),
            Log(plant, April(8), CareKind.MoistureReading, 3),
            Log(plant, April(15)),
            Log(plant, April(22), CareKind.MoistureReading, 2),
            Log(plant, April(29))
        };
        var today = April(30);
        var time = new FixedTime(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));

        WateringGuess Guess(params AwayStretch[] stretches) =>
            WateringGuess.Of(plant, logs, [], WateringForm.TopWatered, false, Hemisphere.Northern, today, null, stretches, time);

        Assert.Equal(2, Guess().LearnedFrom);

        // The first dry-down, 1 to 8 April, is across the stretch
        var left = Guess(Stretch(April(3), April(6)));
        Assert.Equal(WateringMode.Meter, left.Mode);
        Assert.Equal(1, left.LearnedFrom);

        // A watering inside the stretch keeps it
        logs.Add(Log(plant, April(4)));
        Assert.Equal(2, Guess(Stretch(April(3), April(6))).LearnedFrom);
    }

    [Fact]
    public void The_season_factor_ignores_the_gaps_across_a_stretch()
    {
        var time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
        var plants = new List<Plant>();
        var logs = new List<CareLog>();

        // Four plants watered weekly, except for two three-week gaps that started in April 2025
        for (var i = 0; i < 4; i++)
        {
            var plant = new Plant { Nickname = $"Old {i}" };
            plants.Add(plant);
            var days = new List<DateOnly>();
            for (var d = new DateOnly(2025, 2, 4); d <= new DateOnly(2025, 4, 1); d = d.AddDays(7))
                days.Add(d);
            days.AddRange([new DateOnly(2025, 4, 22), new DateOnly(2025, 5, 13), new DateOnly(2025, 5, 20)]);
            logs.AddRange(Waterings(plant, [.. days]));
        }

        // The plant to look at has only just begun, so it borrows the others' pace
        var subject = new Plant { Nickname = "New" };
        plants.Add(subject);
        logs.AddRange(Waterings(subject, April(30).AddDays(-31), April(6), April(13), April(20)));

        WateringGuess Guess(params AwayStretch[] stretches) =>
            WateringRound.GuessFor(subject, plants, logs, [], null, [], Places.None, stretches, Hemisphere.Northern, time);

        var slowed = Guess();
        Assert.True(slowed.SeasonAdjusted);
        Assert.Equal(21, slowed.UsualDays);

        var away = Guess(
            Stretch(new DateOnly(2025, 4, 2), new DateOnly(2025, 4, 21)),
            Stretch(new DateOnly(2025, 4, 23), new DateOnly(2025, 5, 12)));
        Assert.False(away.SeasonAdjusted);
        Assert.Equal(7, away.UsualDays);
    }
}
