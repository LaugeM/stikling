namespace Stikling.Core.Models;

/// <summary>
/// Days as they are where the device is. Everything the app shows by day goes through here, so
/// "today" and the day an entry lands on can't come out differently in two places.
/// </summary>
public static class Clock
{
    public static DateOnly Today(this TimeProvider time) => DayOf(time.GetLocalNow());

    /// <summary>
    /// The local day a moment falls on, by the time zone's rules on that day, so a moment from
    /// summer is read with summer time even in winter.
    /// </summary>
    public static DateOnly LocalDay(this TimeProvider time, DateTimeOffset moment) =>
        DayOf(TimeZoneInfo.ConvertTime(moment, time.LocalTimeZone));

    /// <summary>
    /// The moment to record for something that happened on a given day. Today means now, and
    /// an earlier day has no time of day, so it is recorded at noon to land on the right day.
    /// </summary>
    public static DateTimeOffset MomentOn(this TimeProvider time, DateOnly day) =>
        day == time.Today() ? time.GetLocalNow() : time.MomentAt(day.ToDateTime(new TimeOnly(12, 0)));

    /// <summary>
    /// A time read off a clock where the device is, e.g. the one a camera saves in a photo. The
    /// offset is the one the time zone had on that day, so an old photo keeps the time it says.
    /// </summary>
    public static DateTimeOffset MomentAt(this TimeProvider time, DateTime localTime)
    {
        var local = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return new(local, time.LocalTimeZone.GetUtcOffset(local));
    }

    private static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.DateTime);
}
