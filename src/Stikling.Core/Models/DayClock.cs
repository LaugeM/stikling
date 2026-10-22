namespace Stikling.Core.Models;

/// <summary>
/// A clock shifted by whole days, so <c>Today()</c> is the given day. It lets the code that works
/// out Today answer for a day ahead.
/// </summary>
public sealed class DayClock : TimeProvider
{
    private readonly TimeProvider inner;
    private readonly int days;

    public DayClock(TimeProvider inner, DateOnly day)
    {
        this.inner = inner;
        days = day.DayNumber - inner.Today().DayNumber;
    }

    /// <summary>The clock itself when the day is today, otherwise one shifted to the day.</summary>
    public static TimeProvider On(TimeProvider time, DateOnly day) =>
        day == time.Today() ? time : new DayClock(time, day);

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override DateTimeOffset GetUtcNow()
    {
        var utc = inner.GetUtcNow();
        if (days == 0)
            return utc;

        // Shift the clock on the wall, so a day with summer time starting or ending still lands on the day
        var zone = inner.LocalTimeZone;
        var wall = TimeZoneInfo.ConvertTime(utc, zone).DateTime.AddDays(days);
        if (zone.IsInvalidTime(wall))
            wall = wall.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), zone), TimeSpan.Zero);
    }
}
