namespace Stikling.Core.Models;

/// <summary>
/// Days as they are where the device is. Everything the app shows by day goes through here, so
/// "today" and the day an entry lands on can't come out differently in two places.
/// </summary>
public static class Clock
{
    public static DateOnly Today(this TimeProvider time) => DayOf(time.GetLocalNow());

    /// <summary>The local day a moment falls on.</summary>
    public static DateOnly LocalDay(this TimeProvider time, DateTimeOffset moment) =>
        DayOf(moment.ToOffset(time.GetLocalNow().Offset));

    /// <summary>
    /// The moment to record for something that happened on a given day. Today means now, and
    /// an earlier day has no time of day, so it is recorded at noon to land on the right day.
    /// </summary>
    public static DateTimeOffset MomentOn(this TimeProvider time, DateOnly day) =>
        MomentOn(day, time.GetLocalNow());

    /// <inheritdoc cref="MomentOn(TimeProvider, DateOnly)"/>
    public static DateTimeOffset MomentOn(DateOnly day, DateTimeOffset localNow) =>
        day == DayOf(localNow)
            ? localNow
            : new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), localNow.Offset);

    private static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.DateTime);
}
