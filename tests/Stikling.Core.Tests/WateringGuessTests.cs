using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class WateringGuessTests
{
    private static readonly DateOnly Spring = new(2026, 4, 15);
    private static readonly DateOnly Autumn = new(2026, 10, 9);

    private static TimeProvider TimeOn(DateOnly day) =>
        new FixedTime(new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero));

    private static CareLog Log(Plant plant, DateOnly day, CareKind kind = CareKind.Watered, int? moisture = null) =>
        new() { PlantId = plant.Id, OccurredOn = day, Kind = kind, Moisture = moisture };

    // Waterings starting on a day and then each gap after the one before
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

    private static WateringGuess Guess(
        Plant plant,
        IEnumerable<CareLog> logs,
        DateOnly today,
        bool lit = false,
        double? factor = null,
        IEnumerable<TimelineEntry>? timeline = null,
        WateringForm form = WateringForm.TopWatered,
        Hemisphere hemisphere = Hemisphere.Northern) =>
        WateringGuess.Of(plant, logs, timeline ?? [], form, lit, hemisphere, today, factor, TimeOn(today));

    [Fact]
    public void It_says_nothing_until_there_are_four_waterings()
    {
        var plant = new Plant { Nickname = "Hoya" };

        var none = Guess(plant, [], Spring);
        Assert.Equal(WateringMode.Learning, none.Mode);
        Assert.Equal(4, none.WateringsNeeded);
        Assert.Null(none.CheckOn);
        Assert.Null(none.LastWatered);

        var three = Guess(plant, Waterings(plant, Spring.AddDays(-20), 7, 7), Spring);
        Assert.Equal(WateringMode.Learning, three.Mode);
        Assert.Equal(1, three.WateringsNeeded);
        Assert.Equal(Spring.AddDays(-6), three.LastWatered);
        Assert.Equal(6, three.DaysSince);

        var four = Guess(plant, Waterings(plant, Spring.AddDays(-21), 7, 7, 7), Spring);
        Assert.Equal(WateringMode.Interval, four.Mode);
        Assert.Equal(0, four.WateringsNeeded);
    }

    [Fact]
    public void Steady_gaps_give_that_gap_and_the_day_it_is_due()
    {
        var plant = new Plant { Nickname = "Hoya" };

        var guess = Guess(plant, Waterings(plant, Spring.AddDays(-23), 7, 7, 7), Spring);

        Assert.Equal(7, guess.UsualDays);
        Assert.Equal(4, guess.LearnedFrom);
        Assert.Equal(Spring.AddDays(-2).AddDays(7), guess.CheckOn);
        Assert.False(guess.SeasonAdjusted);
        Assert.Null(guess.LatestReading);
    }

    [Fact]
    public void A_newer_longer_gap_pulls_the_estimate_later()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var steady = Guess(plant, Waterings(plant, Spring.AddDays(-60), 7, 7, 7, 7, 7), Spring);
        var slowing = Guess(plant, Waterings(plant, Spring.AddDays(-70), 7, 7, 7, 14, 14), Spring);

        Assert.Equal(7, steady.UsualDays);
        Assert.Equal(14, slowing.UsualDays);
    }

    [Fact]
    public void Shortening_days_lean_late_in_the_first_year()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, new DateOnly(2026, 8, 1), 6, 7, 8, 9, 10);

        var lit = Guess(plant, logs, Autumn, lit: true);
        var unlit = Guess(plant, logs, Autumn);
        var southern = Guess(plant, logs, Autumn, hemisphere: Hemisphere.Southern);

        Assert.Equal(9, lit.UsualDays);
        Assert.False(lit.SeasonAdjusted);
        Assert.Equal(10, unlit.UsualDays);
        Assert.True(unlit.SeasonAdjusted);
        Assert.True(unlit.DaysShortening);
        // Days are getting longer in the south in October
        Assert.Equal(9, southern.UsualDays);
        Assert.False(southern.DaysShortening);
    }

    [Fact]
    public void A_lit_plant_ignores_the_collections_season_factor()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, Spring.AddDays(-40), 7, 7, 7, 7, 7);

        Assert.Equal(7, Guess(plant, logs, Spring, lit: true, factor: 1.5).UsualDays);

        var unlit = Guess(plant, logs, Spring, factor: 1.5);
        Assert.Equal(11, unlit.UsualDays);
        Assert.True(unlit.SeasonAdjusted);
    }

    [Fact]
    public void The_factor_never_goes_below_the_recent_pace_while_days_shorten()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, new DateOnly(2026, 8, 1), 7, 7, 7, 7, 7);

        var guess = Guess(plant, logs, Autumn, factor: 0.5);

        Assert.Equal(7, guess.UsualDays);
    }

    [Fact]
    public void The_same_weeks_of_last_year_lead_when_there_are_enough()
    {
        var plant = new Plant { Nickname = "Hoya" };
        // Every two weeks round October last year, weekly this summer
        var logs = Waterings(plant, new DateOnly(2025, 9, 20), 14, 14, 33);
        for (var weekly = new DateOnly(2026, 6, 1); weekly <= new DateOnly(2026, 9, 28); weekly = weekly.AddDays(7))
            logs.Add(Log(plant, weekly));

        var lit = Guess(plant, logs, Autumn, lit: true);
        var unlit = Guess(plant, logs, Autumn);

        Assert.Equal(7, lit.UsualDays);
        Assert.Equal(14, unlit.UsualDays);
        Assert.True(unlit.SeasonAdjusted);
    }

    [Fact]
    public void Fertilising_flushing_and_topping_up_count_as_watering_and_a_day_counts_once()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var start = Spring.AddDays(-30);
        var logs = new List<CareLog>
        {
            Log(plant, start, CareKind.Watered),
            Log(plant, start, CareKind.Fertilised),
            Log(plant, start.AddDays(7), CareKind.Fertilised),
            Log(plant, start.AddDays(14), CareKind.Flushed),
            Log(plant, start.AddDays(21), CareKind.ToppedUp),
            Log(plant, start.AddDays(21), CareKind.Watered),
            Log(plant, start.AddDays(22), CareKind.Pruned),
            Log(plant, start.AddDays(23), CareKind.Rotated),
            new CareLog { PlantId = plant.Id, OccurredOn = start.AddDays(24), Kind = CareKind.Watered, DeletedAt = DateTimeOffset.UtcNow },
            Log(new Plant(), start.AddDays(25))
        };

        Assert.Equal(4, WateringGuess.Waterings(plant, logs).Count);
        var guess = Guess(plant, logs, Spring);
        Assert.Equal(WateringMode.Interval, guess.Mode);
        Assert.Equal(7, guess.UsualDays);
        Assert.Equal(start.AddDays(21), guess.LastWatered);
    }

    [Fact]
    public void Waterings_before_the_way_it_is_watered_changed_are_ignored()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var logs = Waterings(plant, Spring.AddDays(-60), 7, 7, 7, 7, 7, 7, 7, 7);

        Assert.Equal(WateringMode.Interval, Guess(plant, logs, Spring).Mode);

        plant.WateringSince = Spring.AddDays(-12);
        var after = Guess(plant, logs, Spring);
        Assert.Equal(WateringMode.Learning, after.Mode);
        Assert.Equal(2, after.WateringsNeeded);
        Assert.Equal(Spring.AddDays(-4), after.LastWatered);
    }

    [Fact]
    public void A_dormant_plant_with_no_dormant_history_is_still_learning()
    {
        var plant = new Plant { Nickname = "Alocasia", DormantSince = Spring.AddDays(-3) };
        var logs = Waterings(plant, Spring.AddDays(-80), 7, 7, 7, 7, 7, 7, 7, 7, 7);

        var guess = Guess(plant, logs, Spring);

        Assert.Equal(WateringMode.Learning, guess.Mode);
        Assert.Equal(Spring.AddDays(-17), guess.LastWatered);
    }

    [Fact]
    public void A_dormant_plant_goes_by_how_it_was_watered_in_earlier_dormancy()
    {
        var plant = new Plant { Nickname = "Alocasia", DormantSince = new DateOnly(2026, 10, 1) };
        var winter = new DateOnly(2025, 11, 1);
        var logs = Waterings(plant, winter, 30, 30, 30, 30, 30);
        // Awake again and watered weekly, then dormant now
        for (var day = new DateOnly(2026, 4, 10); day < new DateOnly(2026, 10, 1); day = day.AddDays(7))
            logs.Add(Log(plant, day));
        logs.Add(Log(plant, new DateOnly(2026, 10, 2)));
        var timeline = new[]
        {
            Change(plant, winter.AddDays(-1), "Went dormant"),
            Change(plant, winter.AddDays(160), "Woke up from dormancy"),
            Change(plant, new DateOnly(2026, 10, 1), "Went dormant")
        };

        var resting = Guess(plant, logs, Autumn, timeline: timeline);
        Assert.Equal(WateringMode.Interval, resting.Mode);
        Assert.True(resting.UsualDays >= 30, $"was {resting.UsualDays}");

        // Awake, the same log is read from the weekly waterings alone
        plant.DormantSince = null;
        var awake = Guess(plant, logs, Autumn, timeline: timeline);
        Assert.Equal(WateringMode.Interval, awake.Mode);
        Assert.Equal(7, awake.UsualDays);
    }

    private static TimelineEntry Change(Plant plant, DateOnly day, string text) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        Kind = TimelineKind.Change,
        OccurredAt = new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero),
        Text = text
    };

    [Fact]
    public void Dormant_periods_come_from_the_history_lines()
    {
        var plant = new Plant { Nickname = "Alocasia" };
        var timeline = new[]
        {
            Change(plant, new DateOnly(2025, 11, 1), "Moved to Kitchen\nWent dormant"),
            Change(plant, new DateOnly(2026, 3, 1), "Woke up from dormancy"),
            // Another plant's entry says nothing about this plant
            Change(new Plant(), new DateOnly(2026, 5, 1), "Went dormant")
        };

        var periods = DormantPeriods.Of(plant, timeline, TimeOn(Autumn));

        Assert.False(periods.Contains(new DateOnly(2025, 10, 31)));
        Assert.True(periods.Contains(new DateOnly(2025, 11, 1)));
        Assert.True(periods.Contains(new DateOnly(2026, 1, 15)));
        Assert.True(periods.Contains(new DateOnly(2026, 3, 1)));
        Assert.False(periods.Contains(new DateOnly(2026, 3, 2)));
        Assert.False(periods.Contains(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void The_meter_gives_the_pace_when_it_has_been_read_after_waterings()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = new List<CareLog>
        {
            Log(plant, new DateOnly(2026, 4, 1)),
            Log(plant, new DateOnly(2026, 4, 8), CareKind.MoistureReading, 3),
            Log(plant, new DateOnly(2026, 4, 15)),
            Log(plant, new DateOnly(2026, 4, 22), CareKind.MoistureReading, 2),
            Log(plant, new DateOnly(2026, 4, 29))
        };
        var today = new DateOnly(2026, 4, 30);

        var guess = Guess(plant, logs, today);

        Assert.Equal(WateringMode.Meter, guess.Mode);
        Assert.Equal(7, guess.UsualDays);
        Assert.Equal(2, guess.LearnedFrom);
        Assert.Equal(new DateOnly(2026, 5, 6), guess.CheckOn);
        Assert.Null(guess.LatestReading);
    }

    [Fact]
    public void A_reservoir_plant_does_not_use_the_meter()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = new List<CareLog>
        {
            Log(plant, new DateOnly(2026, 4, 1)),
            Log(plant, new DateOnly(2026, 4, 8), CareKind.MoistureReading, 3),
            Log(plant, new DateOnly(2026, 4, 15)),
            Log(plant, new DateOnly(2026, 4, 29))
        };

        var guess = Guess(plant, logs, new DateOnly(2026, 4, 30), form: WateringForm.Reservoir);

        Assert.Equal(WateringMode.Learning, guess.Mode);
    }

    private static List<CareLog> MeterCycles(Plant plant) =>
    [
        Log(plant, new DateOnly(2026, 4, 1)),
        Log(plant, new DateOnly(2026, 4, 8), CareKind.MoistureReading, 3),
        Log(plant, new DateOnly(2026, 4, 15)),
        Log(plant, new DateOnly(2026, 4, 22), CareKind.MoistureReading, 2),
        Log(plant, new DateOnly(2026, 4, 29))
    ];

    [Fact]
    public void A_wet_reading_this_round_moves_the_day_later()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = MeterCycles(plant);
        var today = new DateOnly(2026, 5, 5);
        var without = Guess(plant, logs, today).CheckOn;

        logs.Add(Log(plant, today, CareKind.MoistureReading, 8));
        var wet = Guess(plant, logs, today);

        Assert.Equal(new DateOnly(2026, 5, 6), without);
        Assert.Equal(new DateOnly(2026, 5, 7), wet.CheckOn);
        Assert.Equal(8, wet.LatestReading?.Value);
    }

    [Fact]
    public void A_reading_at_the_watering_level_makes_it_due_that_day()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = MeterCycles(plant);
        logs.Add(Log(plant, new DateOnly(2026, 5, 3), CareKind.MoistureReading, 3));

        var guess = Guess(plant, logs, new DateOnly(2026, 5, 5));

        Assert.Equal(new DateOnly(2026, 5, 3), guess.CheckOn);
    }

    [Fact]
    public void The_watering_level_can_be_set_per_plant()
    {
        var plant = new Plant { Nickname = "Monstera", WaterAt = 5 };
        var logs = new List<CareLog>
        {
            Log(plant, new DateOnly(2026, 4, 1)),
            Log(plant, new DateOnly(2026, 4, 5), CareKind.MoistureReading, 5),
            Log(plant, new DateOnly(2026, 4, 15)),
            Log(plant, new DateOnly(2026, 4, 19), CareKind.MoistureReading, 4)
        };

        var guess = Guess(plant, logs, new DateOnly(2026, 4, 20));

        Assert.Equal(WateringMode.Meter, guess.Mode);
        Assert.Equal(4, guess.UsualDays);
        // The reading on the 19th is at or under 5 as well
        Assert.Equal(new DateOnly(2026, 4, 19), guess.CheckOn);
    }

    [Fact]
    public void Falling_readings_predict_the_day_they_reach_the_level_and_never_a_day_before_the_next()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var logs = MeterCycles(plant);
        logs.Add(Log(plant, new DateOnly(2026, 5, 2), CareKind.MoistureReading, 9));
        logs.Add(Log(plant, new DateOnly(2026, 5, 4), CareKind.MoistureReading, 7));

        var guess = Guess(plant, logs, new DateOnly(2026, 5, 4));

        // One point a day, from 7 on the 4th down to 3 on the 8th
        Assert.Equal(new DateOnly(2026, 5, 8), guess.CheckOn);

        logs.Add(Log(plant, new DateOnly(2026, 5, 5), CareKind.MoistureReading, 4));
        // A steep fall can't put it before the day after the last reading
        var steep = Guess(plant, logs, new DateOnly(2026, 5, 5));
        Assert.True(steep.CheckOn >= new DateOnly(2026, 5, 6));
    }

    [Fact]
    public void Still_wet_waits_a_quarter_of_the_usual_days()
    {
        Assert.Equal(2, WateringGuess.StillWetWait(7));
        Assert.Equal(3, WateringGuess.StillWetWait(10));
        Assert.Equal(1, WateringGuess.StillWetWait(1));
    }

    private static (Plant, IReadOnlyList<(DateOnly, int)>) PlantWithGaps(int aprilGaps, int otherGaps)
    {
        var gaps = new List<(DateOnly, int)>();
        for (var i = 0; i < aprilGaps; i++)
            gaps.Add((new DateOnly(2025 + i % 2, 4, 1 + i), 12));
        for (var i = 0; i < otherGaps; i++)
            gaps.Add((new DateOnly(2025, 8, 1 + i), 10));
        return (new Plant { Nickname = "P" }, gaps);
    }

    [Fact]
    public void The_season_factor_needs_eight_ratios_from_two_plants()
    {
        var four = Enumerable.Range(0, 4).Select(_ => PlantWithGaps(2, 4)).ToList();

        // 4 plants x 2 gaps in April, each 12 days against a usual of 10
        Assert.Equal(1.2, WateringSeasonFactor.For(four, 4)!.Value, 6);
        // Three plants make only six
        Assert.Null(WateringSeasonFactor.For(four.Take(3), 4));
        // Plants with fewer than six gaps are left out
        Assert.Null(WateringSeasonFactor.For([.. four.Take(3), PlantWithGaps(2, 3), PlantWithGaps(2, 1)], 4));
        // One plant with plenty of April gaps is not enough
        Assert.Null(WateringSeasonFactor.For([PlantWithGaps(8, 4)], 4));
        // No ratios for a month nobody watered in
        Assert.Null(WateringSeasonFactor.For(four, 1));
    }
}
