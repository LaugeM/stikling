using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class TodayNoticeTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];
    private readonly PutOffs putOffs = PutOffs.Empty();
    private readonly List<AwayStretch> stretches = [];
    private bool watering;

    private TodayData Data() => new()
    {
        Plants = plants,
        CareLogs = logs,
        PutOffs = putOffs,
        Stretches = stretches,
        Settings = new UserSettings { WateringReminder = watering }
    };

    private string? TextOn(int daysAhead) =>
        TodayNotice.Text(TodaySections.For(Data(), Today.AddDays(daysAhead), Time));

    private Plant Plant(string name)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        return plant;
    }

    private Plant Flagged(string name)
    {
        var plant = Plant(name);
        plant.Attention = new Attention("Yellow leaves", Today.AddDays(-1));
        return plant;
    }

    // Four weekly waterings ending on the given day, so it is due a week after
    private Plant Weekly(string name, DateOnly last)
    {
        var plant = Plant(name);
        watering = true;
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-7 * i) });
        return plant;
    }

    [Fact]
    public void A_day_clock_gives_the_shifted_day_and_keeps_the_time_zone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(2), "Test", "Test");
        var inner = new FixedTime(new DateTimeOffset(2026, 4, 20, 21, 30, 0, TimeSpan.Zero), zone: zone);

        var clock = new DayClock(inner, new DateOnly(2026, 4, 25));

        Assert.Equal(new DateOnly(2026, 4, 25), clock.Today());
        Assert.Same(zone, clock.LocalTimeZone);
        Assert.Equal(new DateOnly(2026, 4, 20), inner.Today());
    }

    [Fact]
    public void A_day_clock_for_today_is_the_clock_itself()
    {
        Assert.Same(Time, DayClock.On(Time, Today));
        Assert.Equal(Today.AddDays(3), DayClock.On(Time, Today.AddDays(3)).Today());
    }

    [Fact]
    public void A_quarantine_that_ends_in_five_days_shows_from_that_day()
    {
        var plant = Plant("Fern");
        plant.QuarantinedSince = Today.AddDays(-9);

        var days = TodayNotice.Days(Data(), Time);

        Assert.Equal(25, days.Count);
        Assert.Equal(Today.AddDays(5), days[0].Day);
        Assert.All(days, d => Assert.Equal("Quarantine is up for Fern", d.Text));
    }

    [Fact]
    public void One_row_names_it_and_several_are_counted()
    {
        Flagged("Fern");
        Assert.Equal("Fern needs attention", TextOn(0));

        Flagged("Ficus");
        Assert.Equal("2 need attention", TextOn(0));
    }

    [Fact]
    public void Nothing_due_gives_no_text()
    {
        Plant("Fern");

        Assert.Null(TextOn(0));
        Assert.Empty(TodayNotice.Days(Data(), Time));
    }

    [Fact]
    public void After_four_parts_the_rest_are_counted()
    {
        var plant = new Plant { Nickname = "Fern" };
        var sections = new TodaySections
        {
            FlaggedNow = [new AttentionItem(plant, "Fern", new Attention("Dry", Today))],
            LookAgainNow = [new Stikling.Core.Problems.LookAgain(new Problem(), plant, Today)],
            QuarantineNow = [plant],
            TransitionNow = [plant],
            RestNow = [(plant, Today)],
            PhotosDue = [new PhotoDue(plant, "Fern", null, null), new PhotoDue(plant, "Fern", null, null)],
            PhotoRoundShows = true
        };

        Assert.Equal(
            "Fern needs attention · Look again at Fern · Quarantine is up for Fern · Check Fern in semi-hydro · and 2 more",
            TodayNotice.Text(sections));
    }

    [Fact]
    public void Four_parts_are_all_shown()
    {
        var plant = new Plant { Nickname = "Fern" };
        var sections = new TodaySections
        {
            FlaggedNow = [new AttentionItem(plant, "Fern", new Attention("Dry", Today))],
            QuarantineNow = [plant],
            RestNow = [(plant, Today)],
            PhotosDue = [new PhotoDue(plant, "Fern", null, null), new PhotoDue(plant, "Fern", null, null)],
            PhotoRoundShows = true
        };

        Assert.Equal(
            "Fern needs attention · Quarantine is up for Fern · Has Fern gone dormant? · Take 2 photos",
            TodayNotice.Text(sections));
    }

    [Fact]
    public void A_top_watered_plant_that_is_due_gives_a_soil_check()
    {
        Weekly("Fern", Today.AddDays(-7));

        Assert.Equal("Check the soil of Fern", TextOn(0));

        Weekly("Ficus", Today.AddDays(-8));
        var text = TextOn(0);
        Assert.Equal("Check the soil of 2 plants", text);
        Assert.DoesNotContain("Water", text);
    }

    [Fact]
    public void A_reservoir_plant_that_is_due_is_left_out()
    {
        Weekly("Fern", Today.AddDays(-7)).WaterInOuterPot = true;

        Assert.Null(TextOn(0));
    }

    [Fact]
    public void A_put_off_item_is_left_out_until_its_day()
    {
        var plant = Flagged("Fern");
        putOffs.PutOff(PutOffs.Flag(plant.Id, plant.Attention!), Today.AddDays(2));

        var days = TodayNotice.Days(Data(), Time);

        Assert.Equal(Today.AddDays(2), days[0].Day);
        Assert.Equal(28, days.Count);
    }

    [Fact]
    public void Days_inside_an_away_stretch_have_no_text()
    {
        Flagged("Fern");
        stretches.Add(new AwayStretch { From = Today.AddDays(1), Until = Today.AddDays(4) });

        var days = TodayNotice.Days(Data(), Time, 6).Select(d => d.Day).ToList();

        Assert.Contains(Today, days);
        Assert.DoesNotContain(Today.AddDays(1), days);
        Assert.DoesNotContain(Today.AddDays(2), days);
        Assert.DoesNotContain(Today.AddDays(3), days);
        Assert.Contains(Today.AddDays(4), days);
    }

    [Fact]
    public void The_due_count_is_what_Today_lists()
    {
        Flagged("A");
        Plant("B").QuarantinedSince = Today.AddDays(-20);
        Weekly("C", Today.AddDays(-7));
        Weekly("D", Today.AddDays(-9));
        Weekly("E", Today.AddDays(-1));

        var sections = TodaySections.For(Data(), Today, Time);

        // The flag, the quarantine and the two plants due now, not the one coming up
        Assert.Equal(4, sections.DueCount);
        Assert.Equal(2, sections.WaterNow.Count);
    }

    [Fact]
    public void The_due_count_is_zero_while_away()
    {
        Flagged("A");
        stretches.Add(new AwayStretch { From = Today, Until = Today.AddDays(3) });

        var sections = TodaySections.For(Data(), Today, Time);

        Assert.Equal(0, sections.DueCount);
        Assert.NotNull(sections.AwayNow);
    }
}
