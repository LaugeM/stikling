using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class TimelapsePlanTests
{
    private static TimelapsePhoto Photo(int day) =>
        new(Guid.NewGuid(), new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero).AddDays(day));

    private static TimelapsePlan Plan(int count, TimelapseSpeed speed) =>
        TimelapsePlan.Create(Enumerable.Range(0, count).Select(Photo), speed);

    [Theory]
    [InlineData(TimelapseSpeed.Slow, 1.0)]
    [InlineData(TimelapseSpeed.Normal, 0.6)]
    [InlineData(TimelapseSpeed.Fast, 0.35)]
    public void Each_photo_is_held_for_the_speed(TimelapseSpeed speed, double hold)
    {
        var plan = Plan(4, speed);

        Assert.Equal(hold, plan.Shots[0].Hold, 9);
        Assert.Equal(hold, plan.Shots[1].Hold, 9);
        Assert.Equal(hold, plan.Shots[2].Hold, 9);
    }

    [Fact]
    public void Each_photo_starts_when_the_one_before_it_ends()
    {
        var plan = Plan(4, TimelapseSpeed.Normal);

        Assert.Equal([0.0, 0.6, 1.2, 1.8], plan.Shots.Select(s => s.Start).ToArray(), new Within(1e-9));
    }

    [Fact]
    public void The_last_photo_stays_up_longer()
    {
        var plan = Plan(3, TimelapseSpeed.Normal);

        Assert.Equal(0.6 + 1.5, plan.Shots[^1].Hold, 9);
    }

    [Theory]
    [InlineData(TimelapseSpeed.Slow, 0.2)]
    [InlineData(TimelapseSpeed.Normal, 0.2)]
    public void The_crossfade_is_a_fifth_of_a_second(TimelapseSpeed speed, double crossfade)
    {
        Assert.Equal(crossfade, Plan(3, speed).Crossfade, 9);
    }

    [Fact]
    public void A_short_hold_gets_a_crossfade_of_at_most_a_third_of_it()
    {
        var plan = Plan(3, TimelapseSpeed.Fast);

        Assert.Equal(0.35 / 3, plan.Crossfade, 9);
        Assert.True(plan.Crossfade <= plan.Shots[0].Hold / 3 + 1e-9);
    }

    [Theory]
    [InlineData(TimelapseSpeed.Slow, 5, 6.5)]
    [InlineData(TimelapseSpeed.Normal, 5, 4.5)]
    [InlineData(TimelapseSpeed.Fast, 10, 5.0)]
    public void The_total_is_every_hold_plus_the_extra_for_the_last_photo(TimelapseSpeed speed, int count, double total)
    {
        Assert.Equal(total, Plan(count, speed).Total, 9);
    }

    [Fact]
    public void The_last_photo_ends_where_the_video_does()
    {
        var plan = Plan(7, TimelapseSpeed.Slow);
        var last = plan.Shots[^1];

        Assert.Equal(plan.Total, last.Start + last.Hold, 9);
    }

    [Fact]
    public void Photos_are_put_in_the_order_they_were_taken()
    {
        var newest = Photo(30);
        var oldest = Photo(0);
        var middle = Photo(10);

        var plan = TimelapsePlan.Create([newest, oldest, middle], TimelapseSpeed.Normal);

        Assert.Equal([oldest.Id, middle.Id, newest.Id], plan.Shots.Select(s => s.PhotoId).ToArray());
    }

    [Fact]
    public void Photos_from_the_same_moment_keep_the_same_order_every_time()
    {
        var moment = Photo(3).TakenAt;
        var photos = Enumerable.Range(0, 5).Select(_ => new TimelapsePhoto(Guid.NewGuid(), moment)).ToList();

        var first = TimelapsePlan.Create(photos, TimelapseSpeed.Normal).Shots.Select(s => s.PhotoId);
        var second = TimelapsePlan.Create(photos.AsEnumerable().Reverse(), TimelapseSpeed.Normal).Shots.Select(s => s.PhotoId);

        Assert.Equal(first, second);
    }

    [Fact]
    public void No_photos_make_an_empty_plan()
    {
        var plan = TimelapsePlan.Create([], TimelapseSpeed.Normal);

        Assert.Empty(plan.Shots);
        Assert.Equal(0, plan.Total);
    }

    [Theory]
    [InlineData("slow", TimelapseSpeed.Slow)]
    [InlineData("Fast", TimelapseSpeed.Fast)]
    [InlineData("normal", TimelapseSpeed.Normal)]
    [InlineData("quick", TimelapseSpeed.Normal)]
    [InlineData("7", TimelapseSpeed.Normal)]
    [InlineData(null, TimelapseSpeed.Normal)]
    public void A_saved_speed_is_read_back_and_anything_else_is_normal(string? saved, TimelapseSpeed expected)
    {
        Assert.Equal(expected, TimelapsePlan.ParseSpeed(saved));
    }

    [Fact]
    public void A_history_up_to_the_cap_is_kept_whole_and_in_order()
    {
        var photos = Enumerable.Range(0, TimelapsePlan.MaxPhotos).Select(Photo).Reverse().ToList();

        var spread = TimelapsePlan.Spread(photos);

        Assert.Equal(photos.OrderBy(p => p.TakenAt).ToList(), spread);
    }

    [Fact]
    public void A_longer_history_is_cut_to_the_cap_keeping_the_first_and_the_newest()
    {
        var photos = Enumerable.Range(0, 214).Select(Photo).ToList();

        var spread = TimelapsePlan.Spread(photos);

        Assert.Equal(TimelapsePlan.MaxPhotos, spread.Count);
        Assert.Equal(photos[0], spread[0]);
        Assert.Equal(photos[^1], spread[^1]);
        Assert.Equal(spread.Count, spread.Select(p => p.Id).Distinct().Count());
        Assert.Equal(spread.OrderBy(p => p.TakenAt).ToList(), spread);
    }

    [Fact]
    public void A_longer_history_is_spread_evenly()
    {
        var photos = Enumerable.Range(0, 300).Select(Photo).ToList();

        var days = TimelapsePlan.Spread(photos).Select(p => (p.TakenAt - photos[0].TakenAt).TotalDays).ToList();
        var gaps = days.Zip(days.Skip(1), (a, b) => b - a).ToList();

        Assert.InRange(gaps.Max() - gaps.Min(), 0, 1);
    }

    [Fact]
    public void A_cap_of_one_keeps_the_first_photo_only()
    {
        var photos = Enumerable.Range(0, 5).Select(Photo).ToList();

        Assert.Equal([photos[0]], TimelapsePlan.Spread(photos, 1));
    }

    private sealed class Within(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double value) => 0;
    }
}
