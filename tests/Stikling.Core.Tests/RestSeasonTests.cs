using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class RestSeasonTests
{
    private static TimeProvider TimeOn(DateOnly day) =>
        new FixedTime(new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero));

    private static Plant Grower(GrowingSeason? season, string name = "Fern") =>
        new() { Nickname = name, GrowingSeason = season };

    private static TimelineEntry Change(Plant plant, DateOnly day, string text) => new()
    {
        SubjectType = SubjectType.Plant,
        SubjectId = plant.Id,
        Kind = TimelineKind.Change,
        OccurredAt = new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero),
        Text = text
    };

    private static DateOnly? Start(Plant plant, DateOnly today, Hemisphere hemisphere = Hemisphere.Northern) =>
        RestSeason.WindowStart(plant, DormantPeriods.None, hemisphere, today);

    [Theory]
    [InlineData(GrowingSeason.Summer, Hemisphere.Northern, 11, 1)]
    [InlineData(GrowingSeason.Summer, Hemisphere.Southern, 5, 1)]
    [InlineData(GrowingSeason.Winter, Hemisphere.Northern, 6, 1)]
    [InlineData(GrowingSeason.Winter, Hemisphere.Southern, 12, 1)]
    public void The_default_window_starts_on_the_day_for_the_season_and_hemisphere(
        GrowingSeason season, Hemisphere hemisphere, int month, int day)
    {
        var plant = Grower(season);
        var expected = new DateOnly(2026, month, day);

        Assert.Null(Start(plant, expected.AddDays(-1), hemisphere));
        Assert.Equal(expected, Start(plant, expected, hemisphere));
        Assert.Equal(expected, Start(plant, expected.AddDays(RestSeason.WindowDays - 1), hemisphere));
        Assert.Null(Start(plant, expected.AddDays(RestSeason.WindowDays), hemisphere));
    }

    [Fact]
    public void A_window_can_cross_new_year()
    {
        var plant = Grower(GrowingSeason.Summer);

        Assert.Equal(new DateOnly(2025, 11, 1), Start(plant, new DateOnly(2026, 1, 15)));
        Assert.Equal(new DateOnly(2026, 11, 1), Start(plant, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Outside_a_window_there_is_none()
    {
        Assert.Null(Start(Grower(GrowingSeason.Summer), new DateOnly(2026, 7, 1)));
        Assert.Null(Start(Grower(GrowingSeason.Winter), new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Plants_that_do_not_rest_or_have_no_season_are_never_asked()
    {
        var day = new DateOnly(2026, 11, 20);
        Assert.Null(Start(Grower(GrowingSeason.NoRest), day));
        Assert.Null(Start(Grower(null), day));
    }

    [Fact]
    public void An_early_rest_moves_next_years_window_earlier()
    {
        var plant = Grower(GrowingSeason.Summer);
        var periods = WentDormant(plant, new DateOnly(2025, 10, 20));

        // 20 October minus 14 days
        Assert.Null(RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 10, 5)));
        Assert.Equal(new DateOnly(2026, 10, 6), RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 10, 6)));
    }

    [Fact]
    public void A_late_rest_does_not_move_the_window_later()
    {
        var plant = Grower(GrowingSeason.Summer);
        var periods = WentDormant(plant, new DateOnly(2026, 1, 20));

        Assert.Equal(new DateOnly(2026, 11, 1), RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void A_rest_far_from_its_season_is_left_out()
    {
        var plant = Grower(GrowingSeason.Summer);
        var periods = WentDormant(plant, new DateOnly(2026, 7, 10));

        Assert.Null(RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 10, 20)));
        Assert.Equal(new DateOnly(2026, 11, 1), RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Only_the_latest_rest_counts()
    {
        var plant = Grower(GrowingSeason.Summer);
        var timeline = new[]
        {
            Change(plant, new DateOnly(2024, 10, 20), "Went dormant"),
            Change(plant, new DateOnly(2025, 3, 1), "Woke up from dormancy")
        };
        plant.DormantSince = new DateOnly(2025, 12, 15);
        var periods = DormantPeriods.Of(plant, timeline, TimeOn(new DateOnly(2026, 10, 1)));

        Assert.Equal([new DateOnly(2024, 10, 20), new DateOnly(2025, 12, 15)], periods.Starts);
        Assert.Equal(new DateOnly(2026, 11, 1), RestSeason.WindowStart(plant, periods, Hemisphere.Northern, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void A_window_starting_on_29_February_uses_the_28th_in_other_years()
    {
        var plant = Grower(GrowingSeason.Summer);
        var periods = WentDormant(plant, new DateOnly(2024, 3, 14));

        Assert.Equal(new DateOnly(2026, 2, 28), RestSeason.WindowStart(plant, periods, Hemisphere.Southern, new DateOnly(2026, 3, 1)));
        Assert.Equal(new DateOnly(2028, 2, 29), RestSeason.WindowStart(plant, periods, Hemisphere.Southern, new DateOnly(2028, 2, 29)));
    }

    private static DormantPeriods WentDormant(Plant plant, DateOnly day)
    {
        plant.DormantSince = day;
        return DormantPeriods.Of(plant, [], TimeOn(day));
    }

    [Fact]
    public void Ask_now_lists_plants_in_their_window_by_name()
    {
        var today = new DateOnly(2026, 11, 20);
        var b = Grower(GrowingSeason.Summer, "Bravo");
        var a = Grower(GrowingSeason.Summer, "alpha");
        var summer = Grower(GrowingSeason.Winter, "Summer grower");
        var none = Grower(null, "No season");

        var asked = RestSeason.AskNow([b, summer, none, a], _ => DormantPeriods.None, Hemisphere.Northern, today);

        Assert.Equal([a, b], asked.Select(r => r.Plant));
        Assert.All(asked, r => Assert.Equal(new DateOnly(2026, 11, 1), r.WindowStart));
    }

    [Fact]
    public void Ask_now_leaves_out_plants_that_are_dormant_gone_or_deleted()
    {
        var today = new DateOnly(2026, 11, 20);
        var dormant = Grower(GrowingSeason.Summer, "Dormant");
        dormant.DormantSince = new DateOnly(2026, 11, 10);
        var gone = Grower(GrowingSeason.Summer, "Gone");
        gone.Status = PlantStatus.GivenAway;
        var deleted = Grower(GrowingSeason.Summer, "Deleted");
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        var kept = Grower(GrowingSeason.Summer, "Kept");

        var asked = RestSeason.AskNow([dormant, gone, deleted, kept], _ => DormantPeriods.None, Hemisphere.Northern, today);

        Assert.Equal([kept], asked.Select(r => r.Plant));
    }

    [Fact]
    public void Ask_now_skips_a_plant_that_already_went_dormant_this_season()
    {
        var today = new DateOnly(2026, 11, 20);
        var plant = Grower(GrowingSeason.Summer);
        var timeline = new[]
        {
            Change(plant, new DateOnly(2026, 11, 5), "Went dormant"),
            Change(plant, new DateOnly(2026, 11, 12), "Woke up from dormancy")
        };
        var periods = DormantPeriods.Of(plant, timeline, TimeOn(today));

        Assert.Empty(RestSeason.AskNow([plant], _ => periods, Hemisphere.Northern, today));
    }

    [Fact]
    public void Ask_now_asks_again_when_the_last_dormancy_was_before_the_window()
    {
        var today = new DateOnly(2026, 11, 20);
        var plant = Grower(GrowingSeason.Summer);
        var timeline = new[]
        {
            Change(plant, new DateOnly(2025, 10, 20), "Went dormant"),
            Change(plant, new DateOnly(2026, 3, 1), "Woke up from dormancy")
        };
        var periods = DormantPeriods.Of(plant, timeline, TimeOn(today));

        // Learned from 20 October minus 14 days: 6 October
        var asked = RestSeason.AskNow([plant], _ => periods, Hemisphere.Northern, today);

        Assert.Equal(new DateOnly(2026, 10, 6), Assert.Single(asked).WindowStart);
    }

    [Fact]
    public void The_put_off_key_holds_the_plant_and_the_window_start()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"rest:{id}:2026-11-01", PutOffs.Rest(id, new DateOnly(2026, 11, 1)));
        Assert.NotEqual(PutOffs.Rest(id, new DateOnly(2026, 11, 1)), PutOffs.Rest(id, new DateOnly(2027, 11, 1)));
    }
}
