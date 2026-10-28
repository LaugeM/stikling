using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>
/// Round days: the weekdays someone goes round their plants. A check that is due comes up on the
/// round day before it, so they see them all at once. A check is not a watering, so checking a
/// bit early is safe.
/// </summary>
public static class RoundSchedule
{
    private static bool Off(IReadOnlyList<Weekday>? days) => days is null || days.Count == 0;

    public static bool IsRoundDay(IReadOnlyList<Weekday>? days, DateOnly date) =>
        !Off(days) && days!.Contains(date.DayOfWeek.ToWeekday());

    /// <summary>The first round day after the date, or null when round days are off.</summary>
    public static DateOnly? Next(IReadOnlyList<Weekday>? days, DateOnly after) =>
        OnOrAfter(days, after.AddDays(1));

    /// <summary>The date itself when it's a round day, else the next one. Null when off.</summary>
    public static DateOnly? OnOrAfter(IReadOnlyList<Weekday>? days, DateOnly date)
    {
        if (Off(days))
            return null;
        for (var i = 0; i < 7; i++)
        {
            if (IsRoundDay(days, date.AddDays(i)))
                return date.AddDays(i);
        }
        return null;
    }

    /// <summary>The latest round day from <paramref name="from"/> to <paramref name="to"/>, both included. Null when there is none.</summary>
    public static DateOnly? LatestIn(IReadOnlyList<Weekday>? days, DateOnly from, DateOnly to)
    {
        if (Off(days))
            return null;
        for (var day = to; day >= from && to.DayNumber - day.DayNumber < 7; day = day.AddDays(-1))
        {
            if (IsRoundDay(days, day))
                return day;
        }
        return null;
    }

    /// <summary>
    /// The longest gap in days between two round days, going round the week. One round day a
    /// week gives 7. Zero when round days are off.
    /// </summary>
    public static int LongestGap(IReadOnlyList<Weekday>? days)
    {
        if (Off(days))
            return 0;
        var sorted = days!.Select(d => (int)d).Distinct().Order().ToList();
        var longest = sorted[0] + 7 - sorted[^1];
        for (var i = 1; i < sorted.Count; i++)
            longest = Math.Max(longest, sorted[i] - sorted[i - 1]);
        return longest;
    }

    /// <summary>
    /// The day a check due on <paramref name="due"/> comes up. With no round days, or for a plant
    /// that dries out faster than the longest gap between round days, it's the due day itself.
    /// Otherwise the latest round day in the second half of the wait, from halfway between
    /// <paramref name="earliest"/> and the due day to the due day, so something seen to the day
    /// before a round day doesn't come back on it. When there is none it comes up on the due day,
    /// and when it can't come up before <paramref name="earliest"/> it waits for the next round
    /// day from then.
    /// </summary>
    /// <param name="earliest">The first day it may come up, after the last time it was seen to.</param>
    /// <param name="usualDays">How many days the plant usually goes between waterings, if known.</param>
    public static DateOnly ShowOn(IReadOnlyList<Weekday>? days, DateOnly due, DateOnly earliest, int? usualDays)
    {
        if (Off(days))
            return due;
        if (usualDays is { } usual && usual < LongestGap(days))
            return due;
        if (earliest > due)
            return OnOrAfter(days, earliest) ?? due;
        var halfway = earliest.AddDays((due.DayNumber - earliest.DayNumber) / 2);
        return LatestIn(days, halfway, due) ?? due;
    }
}
