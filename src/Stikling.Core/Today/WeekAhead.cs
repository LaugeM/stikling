using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>What is due on one day of the week ahead.</summary>
/// <param name="Water">The plants worth a check for water that day.</param>
/// <param name="Care">What care intervals have due that day, one group for each kind of care.</param>
/// <param name="Away">An away or quiet stretch covers the day, so nothing is asked for.</param>
public sealed record WeekDay(DateOnly Day, IReadOnlyList<WateringDue> Water, IReadOnlyList<IntervalGroup> Care, bool Away)
{
    public int Count => Water.Count + Care.Sum(g => g.Items.Count);
}

/// <summary>
/// What comes up over the next days, worked out by the same logic as Today. Each thing is listed
/// once, on the next day it comes up, since what follows depends on when it is done. The days are
/// estimates, not a schedule: a plant's day moves with what is logged.
/// </summary>
public static class WeekAhead
{
    /// <summary>How many days after today it looks.</summary>
    public const int Days = 7;

    /// <summary>
    /// Today and the next seven days, leaving out days with nothing on them. Anything overdue is on
    /// today, and something put off is on the day it comes back.
    /// </summary>
    public static IReadOnlyList<WeekDay> For(TodayData data, DateOnly today, TimeProvider time)
    {
        var clock = DayClock.On(time, today);
        var settings = data.Settings;
        var putOffs = data.PutOffs;
        var last = today.AddDays(Days);

        var water = settings.WateringReminder
            ? WateringRound.Due(data.Plants, data.CareLogs, data.PlantChanges, id => data.PotsById.GetValueOrDefault(id),
                data.Lights, data.Places, data.Stretches, Seasons.HemisphereOf(clock.LocalTimeZone), clock, putOffs,
                settings.RoundDays, Days)
            : [];

        var care = data.CareIntervals.Count == 0
            ? []
            : CareIntervals.Plan(data.CareIntervals, data.Plants, data.Places, data.CareLogs, data.OwnCareKinds, data.Feeds,
                clock, settings.RoundDays, putOffs);

        var days = new List<WeekDay>();
        for (var day = today; day <= last; day = day.AddDays(1))
        {
            if (AwayTime.IsOn(data.Stretches, day))
            {
                days.Add(new WeekDay(day, [], [], true));
                continue;
            }

            var waterOn = water.Where(d => Later(d.ShowOn, today) == day).ToList();
            var careOn = CareIntervals.Group(care.Where(i => CareDay(i, putOffs, today) == day));
            if (waterOn.Count > 0 || careOn.Count > 0)
                days.Add(new WeekDay(day, waterOn, careOn, false));
        }

        return days;
    }

    private static DateOnly CareDay(IntervalItem item, PutOffs putOffs, DateOnly today)
    {
        var day = Later(item.ShowOn, today);
        return CareIntervals.IsPutOff(item, putOffs, today)
            ? Later(day, putOffs.EndedOn(PutOffs.Interval(item.Interval.Id, item.Plant.Id, item.Last))!.Value)
            : day;
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
}

/// <summary>The entries made by marking a day as done, so they can be taken back.</summary>
public sealed record WeekDone(IReadOnlyList<Guid> Waterings, IReadOnlyList<Guid> Care)
{
    public int Count => Waterings.Count + Care.Count;
}

/// <summary>Marks what was picked from a day of the week ahead as done, with ordinary care entries dated today.</summary>
public sealed class WeekAheadService(WateringAnswers answers, CareService care, CareIntervalService intervals)
{
    public async Task<WeekDone> MarkDoneAsync(
        IEnumerable<WateringDue> water, IEnumerable<IntervalItem> items, Func<Enum, string> label)
    {
        var waterings = new List<Guid>();
        foreach (var due in water)
            waterings.Add((await answers.WaterAsync(due.Plant.Id, due.Guess.Form, label)).Id);

        var logged = new List<Guid>();
        foreach (var group in CareIntervals.Group(items))
            logged.AddRange(await intervals.LogAsync(group, group.Items.Select(i => i.Plant.Id), label));

        return new WeekDone(waterings, logged);
    }

    /// <summary>Takes it all back, with any feed that came along with a watering.</summary>
    public async Task UndoAsync(WeekDone done)
    {
        foreach (var id in done.Waterings)
            await care.UndoWateringAsync(id);
        await intervals.UndoAsync(done.Care);
    }
}
