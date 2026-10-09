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

    [Fact]
    public void A_plant_counts_from_a_whole_day_it_was_acquired()
    {
        var plant = new Plant { AcquiredOn = LooseDate.Of(new DateOnly(2026, 5, 3)) };
        Assert.Equal(new DateOnly(2026, 5, 3), PhotoDayCount.StartOf(plant));
    }

    [Fact]
    public void A_plant_with_only_a_month_year_or_no_date_has_no_start()
    {
        Assert.Null(PhotoDayCount.StartOf(new Plant { AcquiredOn = LooseDate.Of(2026, 5) }));
        Assert.Null(PhotoDayCount.StartOf(new Plant { AcquiredOn = LooseDate.Of(2026) }));
        Assert.Null(PhotoDayCount.StartOf(new Plant()));
    }

    [Fact]
    public void A_propagation_counts_from_the_day_it_started()
    {
        Assert.Equal(Start, PhotoDayCount.StartOf(new Propagation { StartedOn = Start }));
    }
}
