using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class PhotoDayCountTests
{
    private static readonly DateOnly Start = new(2026, 5, 1);

    [Fact]
    public void The_start_day_is_day_0()
    {
        Assert.Equal(0, PhotoDayCount.Day(Start, Start));
    }

    [Fact]
    public void Days_count_from_the_start()
    {
        Assert.Equal(24, PhotoDayCount.Day(Start, new DateOnly(2026, 5, 25)));
    }

    [Fact]
    public void A_photo_from_before_the_start_has_no_day()
    {
        Assert.Null(PhotoDayCount.Day(Start, Start.AddDays(-1)));
    }

    [Fact]
    public void Without_a_start_there_is_no_day()
    {
        Assert.Null(PhotoDayCount.Day(null, Start));
    }

    private static Plant Got(DateOnly d) => new() { AcquiredOn = LooseDate.Of(d) };

    [Theory]
    [InlineData("2026-03-15", "2026-03-15", "first day")]
    [InlineData("2026-03-15", "2026-03-16", "1 day")]
    [InlineData("2026-03-15", "2026-03-27", "12 days")]
    [InlineData("2026-03-15", "2026-04-14", "30 days")]
    [InlineData("2026-03-15", "2026-04-15", "1 mo")]
    [InlineData("2026-03-15", "2026-09-14", "5 mo")]
    [InlineData("2026-03-15", "2027-02-15", "11 mo")]
    [InlineData("2026-03-15", "2027-03-14", "11 mo")]
    [InlineData("2026-03-15", "2027-03-15", "1 yr")]
    [InlineData("2026-03-15", "2027-05-20", "1 yr 2 mo")]
    [InlineData("2026-03-15", "2029-03-15", "3 yr")]
    [InlineData("2026-01-31", "2026-02-28", "28 days")]
    [InlineData("2026-01-31", "2026-03-01", "1 mo")]
    [InlineData("2026-01-31", "2026-03-30", "1 mo")]
    [InlineData("2026-01-31", "2026-03-31", "2 mo")]
    public void A_plant_acquired_on_a_day_counts_whole_calendar_months(string got, string taken, string expected)
    {
        Assert.Equal(expected, PhotoDayCount.PlantAge(Got(DateOnly.Parse(got)), DateOnly.Parse(taken)));
    }

    [Fact]
    public void A_month_from_the_31st_is_complete_on_the_first_of_a_shorter_month()
    {
        // 31 Jan to 28 Feb is not yet a month, 1 Mar is
        var plant = Got(new DateOnly(2026, 1, 31));
        Assert.Equal("28 days", PhotoDayCount.PlantAge(plant, new DateOnly(2026, 2, 28)));
        Assert.Equal("1 mo", PhotoDayCount.PlantAge(plant, new DateOnly(2026, 3, 1)));
    }

    [Theory]
    [InlineData("2026-05-01", null)]
    [InlineData("2026-05-31", null)]
    [InlineData("2026-06-01", "1 mo")]
    [InlineData("2027-05-10", "1 yr")]
    [InlineData("2027-07-02", "1 yr 2 mo")]
    public void A_plant_acquired_in_a_month_counts_from_its_first(string taken, string? expected)
    {
        var plant = new Plant { AcquiredOn = LooseDate.Of(2026, 5) };
        Assert.Equal(expected, PhotoDayCount.PlantAge(plant, DateOnly.Parse(taken)));
    }

    [Fact]
    public void A_plant_without_a_usable_date_or_a_photo_from_before_it_has_no_age()
    {
        var taken = new DateOnly(2027, 1, 1);
        Assert.Null(PhotoDayCount.PlantAge(new Plant { AcquiredOn = LooseDate.Of(2026) }, taken));
        Assert.Null(PhotoDayCount.PlantAge(new Plant(), taken));
        Assert.Null(PhotoDayCount.PlantAge(Got(new DateOnly(2026, 5, 3)), new DateOnly(2026, 5, 2)));
        Assert.Null(PhotoDayCount.PlantAge(new Plant { AcquiredOn = LooseDate.Of(2026, 5) }, new DateOnly(2026, 4, 30)));
    }

    [Fact]
    public void A_propagation_photo_says_the_day_and_the_suffix_follows_the_date()
    {
        Assert.Equal("day 24", PhotoDayCount.DayLabel(Start, new DateOnly(2026, 5, 25)));
        Assert.Null(PhotoDayCount.DayLabel(Start, Start.AddDays(-1)));
        Assert.Equal(" · day 24", PhotoDayCount.Suffix("day 24"));
        Assert.Equal("", PhotoDayCount.Suffix(null));
    }

    [Fact]
    public void A_propagation_counts_from_the_day_it_started()
    {
        Assert.Equal(Start, PhotoDayCount.StartOf(new Propagation { StartedOn = Start }));
    }
}
