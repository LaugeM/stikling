using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class CareCalendarTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);
    private readonly Guid plant = Guid.NewGuid();

    private CareLog Log(CareKind kind, DateOnly on, bool products = false, bool deleted = false, Guid? plantId = null)
    {
        var log = new CareLog { PlantId = plantId ?? plant, Kind = kind, OccurredOn = on };
        if (products)
            log.Products = [new ProductDose()];
        if (deleted)
            log.DeletedAt = DateTimeOffset.UtcNow;
        return log;
    }

    private CareCalendar Build(params CareLog[] logs) => CareCalendar.Build(logs, plant, Today);

    private static CareDayMark On(CareCalendar c, DateOnly d) =>
        c.Months.SelectMany(m => m.Days).Single(x => x.Date == d).Mark;

    [Fact]
    public void Nothing_logged_gives_no_calendar() => Assert.True(Build().IsEmpty);

    [Fact]
    public void Other_kinds_and_other_plants_give_no_calendar()
    {
        Assert.True(Build(Log(CareKind.Pruned, Today), Log(CareKind.Watered, Today, plantId: Guid.NewGuid())).IsEmpty);
    }

    [Fact]
    public void Fed_beats_watered_beats_reading()
    {
        var day = new DateOnly(2026, 9, 10);
        var c = Build(Log(CareKind.MoistureReading, day), Log(CareKind.Watered, day), Log(CareKind.Fertilised, day),
            Log(CareKind.MoistureReading, day.AddDays(1)), Log(CareKind.Watered, day.AddDays(1)),
            Log(CareKind.MoistureReading, day.AddDays(2)));
        Assert.Equal(CareDayMark.Fed, On(c, day));
        Assert.Equal(CareDayMark.Watered, On(c, day.AddDays(1)));
        Assert.Equal(CareDayMark.Reading, On(c, day.AddDays(2)));
        Assert.Equal(CareDayMark.None, On(c, day.AddDays(3)));
    }

    [Fact]
    public void Watering_with_products_or_a_feed_counts_as_fed()
    {
        var withFeed = Log(CareKind.ToppedUp, new DateOnly(2026, 9, 2));
        withFeed.FeedId = Guid.NewGuid();
        var c = Build(Log(CareKind.Watered, new DateOnly(2026, 9, 1), products: true), withFeed);
        Assert.Equal(CareDayMark.Fed, On(c, new DateOnly(2026, 9, 1)));
        Assert.Equal(CareDayMark.Fed, On(c, new DateOnly(2026, 9, 2)));
        Assert.Equal(2, c.Feeds);
    }

    [Fact]
    public void Deleted_logs_are_ignored() =>
        Assert.True(Build(Log(CareKind.Watered, Today, deleted: true)).IsEmpty);

    [Fact]
    public void Months_start_at_the_first_log_and_the_newest_is_first()
    {
        var c = Build(Log(CareKind.Watered, new DateOnly(2026, 7, 15)));
        Assert.Equal([(2026, 9), (2026, 8), (2026, 7)], c.Months.Select(m => (m.Year, m.Month)));
    }

    [Fact]
    public void Months_are_capped_at_twelve_and_older_logs_are_left_out()
    {
        var c = Build(Log(CareKind.Watered, new DateOnly(2024, 1, 1)), Log(CareKind.Watered, new DateOnly(2025, 10, 5)), Log(CareKind.Watered, Today));
        Assert.Equal(12, c.Months.Count);
        Assert.Equal((2025, 10), (c.Months[^1].Year, c.Months[^1].Month));
        Assert.Equal(2, c.Waterings);
    }

    [Fact]
    public void Only_logs_older_than_a_year_give_no_calendar() =>
        Assert.True(Build(Log(CareKind.Watered, new DateOnly(2024, 1, 1))).IsEmpty);

    [Fact]
    public void Months_have_their_own_number_of_days()
    {
        var c = Build(Log(CareKind.Watered, new DateOnly(2026, 2, 3)), Log(CareKind.Watered, new DateOnly(2026, 4, 3)));
        Assert.Equal(28, c.Months.Single(m => m.Month == 2).Days.Count);
        Assert.Equal(30, c.Months.Single(m => m.Month == 4).Days.Count);
        Assert.Equal(31, c.Months.Single(m => m.Month == 3).Days.Count);
    }

    [Fact]
    public void Days_after_today_are_marked_future()
    {
        var c = Build(Log(CareKind.Watered, Today));
        var days = c.Months[0].Days;
        Assert.All(days.Where(d => d.Date > Today), d => Assert.True(d.Future));
        Assert.All(days.Where(d => d.Date <= Today), d => Assert.False(d.Future));
    }

    [Fact]
    public void Summary_counts_the_year()
    {
        var c = Build(Log(CareKind.Watered, Today), Log(CareKind.Watered, Today.AddDays(-1)),
            Log(CareKind.Fertilised, Today.AddDays(-2)), Log(CareKind.MoistureReading, Today.AddDays(-3)));
        Assert.Equal("Last 12 months: watered 2 times, fed 1 time, 1 meter reading", c.Summary);
    }
}
