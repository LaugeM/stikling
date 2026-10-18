using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Care;

/// <summary>One plant due for one interval.</summary>
/// <param name="Interval">The interval that makes it due. With overlapping ones, the one due first.</param>
/// <param name="Last">The day it was last done, or null when it never has been.</param>
/// <param name="Due">The day it is due.</param>
/// <param name="ShowOn">The day it comes up on Today: the due day, or with round days the round day before.</param>
/// <param name="FeedId">The feed to log with, only when it still exists.</param>
/// <param name="Label">What it is called, like "Fertilise with Aroid feed".</param>
public sealed record IntervalItem(
    CareInterval Interval, Plant Plant, DateOnly? Last, DateOnly Due, DateOnly ShowOn, Guid? FeedId, string Label);

/// <summary>The plants due for the same thing, shown as one card on Today.</summary>
/// <param name="Interval">The interval due first, for the "every 2 weeks" text.</param>
public sealed record IntervalGroup(
    CareKind Kind, Guid? OwnKindId, Guid? FeedId, string Label, CareInterval Interval, IReadOnlyList<IntervalItem> Items)
{
    public DateOnly EarliestDue => Items.Min(i => i.Due);
}

/// <summary>
/// Care that comes back: who an interval covers, when each plant is due, and what Today shows.
/// An interval never covers watering, which the watering reminder learns from each plant.
/// </summary>
public static class CareIntervals
{
    /// <summary>How many days ahead Coming up looks.</summary>
    public const int ComingDays = 2;

    /// <summary>
    /// The plants an interval covers right now. Rooms and tags are worked out from the plants as
    /// they are, so a plant moved into the room or given the tag is covered from then on.
    /// </summary>
    public static IReadOnlyList<Plant> PlantsIn(CareInterval interval, IEnumerable<Plant> plants, Places places)
    {
        var here = plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active);

        var covered = interval.Scope switch
        {
            CareScope.Everywhere => here,
            CareScope.Room => here.Where(p => places.IsIn(p.PlaceId, interval.PlaceId)),
            CareScope.Tag => here.Where(p => PlantTags.Has(p, interval.Tag)),
            _ => here.Where(p => interval.PlantIds.Contains(p.Id))
        };

        return covered
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The intervals covering one plant, the one due first first.</summary>
    public static IReadOnlyList<CareInterval> CoveringPlant(Plant plant, IEnumerable<CareInterval> intervals, Places places) =>
        intervals
            .Where(i => !i.IsDeleted && PlantsIn(i, [plant], places).Count > 0)
            .OrderBy(i => i.CreatedAt)
            .ToList();

    /// <summary>The day plus the interval. AddMonths stops at the end of a short month, so 31 January plus a month is 28 February.</summary>
    public static DateOnly Add(DateOnly day, int every, IntervalUnit unit) => unit switch
    {
        IntervalUnit.Days => day.AddDays(every),
        IntervalUnit.Weeks => day.AddDays(every * 7),
        IntervalUnit.Months => day.AddMonths(every),
        _ => day.AddYears(every)
    };

    /// <summary>Whether the month is inside the interval's part of the year. Always true without one.</summary>
    public static bool InSeason(CareInterval interval, int month) =>
        interval is not { FromMonth: { } from, ToMonth: { } to } ? true
        : from <= to ? month >= from && month <= to
        : month >= from || month <= to;

    /// <summary>
    /// The day moved into the interval's part of the year: itself when it is inside, else the 1st
    /// of the next first month.
    /// </summary>
    public static DateOnly InWindow(CareInterval interval, DateOnly day)
    {
        if (InSeason(interval, day.Month) || interval.FromMonth is not { } from)
            return day;
        return new DateOnly(day.Month < from ? day.Year : day.Year + 1, from, 1);
    }

    /// <summary>
    /// Whether a care entry counts as this interval having been done. An own kind only counts its
    /// own entries. A feeding counts every feeding, also one logged with a watering or an own kind
    /// that counts as a feed. The other kinds count entries of the kind that aren't an own kind.
    /// </summary>
    public static bool Matches(CareInterval interval, CareLog entry)
    {
        if (entry.IsDeleted)
            return false;
        if (interval.OwnKindId is { } own)
            return entry.OwnKindId == own;
        if (interval.Kind == CareKind.Fertilised)
            return entry.Kind == CareKind.Fertilised;
        return entry.Kind == interval.Kind && entry.OwnKindId is null;
    }

    /// <summary>The day the plant came into the collection.</summary>
    public static DateOnly InCollectionSince(Plant plant, TimeProvider time) =>
        plant.AcquiredOn?.Start ?? time.LocalDay(plant.CreatedAt);

    /// <summary>
    /// When one plant was last done and is next due for the interval, whether or not it is
    /// dormant. With nothing logged the first one is due an interval after the later of the day it
    /// was set and the day the plant came in, so setting it up doesn't flood Today on day one.
    /// </summary>
    public static (DateOnly? Last, DateOnly Due) DueFor(
        CareInterval interval, Plant plant, IEnumerable<CareLog> logs, TimeProvider time)
    {
        var last = logs
            .Where(l => l.PlantId == plant.Id && Matches(interval, l))
            .OrderByDescending(l => l.OccurredOn)
            .Select(l => (DateOnly?)l.OccurredOn)
            .FirstOrDefault();

        var from = last ?? Later(interval.StartsOn, InCollectionSince(plant, time));
        return (last, InWindow(interval, Add(from, interval.Every, interval.Unit)));
    }

    /// <summary>
    /// Everything due for the intervals, one item per plant and thing. Dormant plants are left out
    /// of feedings and nothing else. When two intervals cover a plant for the same thing it is
    /// listed once, under the one due first. An interval of an own kind that has been removed is left out.
    /// </summary>
    public static IReadOnlyList<IntervalItem> Plan(
        IEnumerable<CareInterval> intervals,
        IEnumerable<Plant> plants,
        Places places,
        IEnumerable<CareLog> logs,
        IEnumerable<OwnCareKind> ownKinds,
        IEnumerable<Feed> feeds,
        TimeProvider time,
        IReadOnlyList<Weekday>? roundDays,
        PutOffs putOffs)
    {
        var plantList = plants.ToList();
        var logList = logs.Where(l => !l.IsDeleted).ToList();
        var logsByPlant = logList.GroupBy(l => l.PlantId).ToDictionary(g => g.Key, g => g.ToList());
        var own = CareLabels.Lookup(ownKinds);
        var liveFeeds = feeds.Where(f => !f.IsDeleted).GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());

        var all = new List<IntervalItem>();
        foreach (var interval in intervals.Where(i => !i.IsDeleted && i.Validate().Count == 0))
        {
            OwnCareKind? ownKind = null;
            if (interval.OwnKindId is { } ownId && !own.TryGetValue(ownId, out ownKind))
                continue;

            Feed? feed = interval.FeedId is { } feedId && liveFeeds.TryGetValue(feedId, out var found) ? found : null;
            var label = Label(interval.Kind, ownKind?.Name, feed?.Name);

            foreach (var plant in PlantsIn(interval, plantList, places))
            {
                if (interval.IsFeed && plant.IsDormant)
                    continue;

                var mine = logsByPlant.GetValueOrDefault(plant.Id) ?? [];
                var (last, due) = DueFor(interval, plant, mine, time);

                var earliest = (last ?? Later(interval.StartsOn, InCollectionSince(plant, time))).AddDays(last is null ? 0 : 1);
                if (roundDays is { Count: > 0 }
                    && putOffs.EndedOn(PutOffs.Interval(interval.Id, plant.Id, last)) is { } ended)
                    earliest = Later(earliest, ended);

                all.Add(new IntervalItem(interval, plant, last, due,
                    RoundSchedule.ShowOn(roundDays, due, earliest, null), feed?.Id, label));
            }
        }

        return all
            .GroupBy(i => (i.Plant.Id, i.Interval.Kind, i.Interval.OwnKindId))
            .Select(g => g.OrderBy(i => i.Due).ThenBy(i => i.Interval.CreatedAt).ThenBy(i => i.Interval.Id).First())
            .ToList();
    }

    /// <summary>What is due now: it comes up today or earlier and isn't put off.</summary>
    public static IReadOnlyList<IntervalGroup> Now(IEnumerable<IntervalItem> items, DateOnly today, PutOffs putOffs) =>
        Group(items.Where(i => i.ShowOn <= today && !IsPutOff(i, putOffs, today)));

    /// <summary>What is due in the next couple of days and doesn't come up today.</summary>
    public static IReadOnlyList<IntervalGroup> Coming(IEnumerable<IntervalItem> items, DateOnly today, PutOffs putOffs) =>
        Group(items.Where(i => i.ShowOn > today && i.Due <= today.AddDays(ComingDays) && !IsPutOff(i, putOffs, today)));

    /// <summary>What waits for the round day, for the line saying so on a day that isn't one.</summary>
    public static IReadOnlyList<IntervalItem> WaitingForRound(IEnumerable<IntervalItem> items, DateOnly roundDay, DateOnly today, PutOffs putOffs) =>
        [.. items.Where(i => i.ShowOn == roundDay && i.ShowOn > today && !IsPutOff(i, putOffs, today))];

    public static bool IsPutOff(IntervalItem item, PutOffs putOffs, DateOnly today) =>
        putOffs.IsPutOff(PutOffs.Interval(item.Interval.Id, item.Plant.Id, item.Last), today);

    /// <summary>The cards: by kind, own kind and feed, the one due first at the top, plants by name.</summary>
    public static IReadOnlyList<IntervalGroup> Group(IEnumerable<IntervalItem> items) =>
        items
            .GroupBy(i => (i.Interval.Kind, i.Interval.OwnKindId, i.FeedId))
            .Select(g =>
            {
                var ordered = g.OrderBy(i => i.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
                var first = ordered.OrderBy(i => i.Due).First();
                return new IntervalGroup(g.Key.Kind, g.Key.OwnKindId, g.Key.FeedId, first.Label, first.Interval, ordered);
            })
            .OrderBy(g => g.EarliestDue)
            .ThenBy(g => g.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>"Fertilise with Aroid feed", "Repot", or the own kind's name.</summary>
    public static string Label(CareKind kind, string? ownKindName, string? feedName)
    {
        var feed = string.IsNullOrWhiteSpace(feedName) ? null : feedName.Trim();
        var what = !string.IsNullOrWhiteSpace(ownKindName)
            ? ownKindName.Trim()
            : kind switch
            {
                CareKind.Fertilised => "Fertilise",
                CareKind.Flushed => "Flush",
                CareKind.Repotted => "Repot",
                CareKind.Pruned => "Prune",
                CareKind.Rotated => "Rotate",
                CareKind.LeavesCleaned => "Clean the leaves",
                CareKind.Harvested => "Harvest",
                _ => "Care"
            };
        return feed is null ? what : $"{what} with {feed}";
    }

    /// <summary>"every 2 weeks", "every week", "every day".</summary>
    public static string EveryText(CareInterval interval)
    {
        var (one, many) = interval.Unit switch
        {
            IntervalUnit.Days => ("day", "days"),
            IntervalUnit.Weeks => ("week", "weeks"),
            IntervalUnit.Months => ("month", "months"),
            _ => ("year", "years")
        };
        return interval.Every == 1 ? $"every {one}" : $"every {interval.Every} {many}";
    }

    /// <summary>"April to October", or null for the whole year.</summary>
    public static string? MonthsText(CareInterval interval) =>
        interval is { FromMonth: { } from, ToMonth: { } to }
            ? $"{MonthName(from)} to {MonthName(to)}"
            : null;

    public static string MonthName(int month) =>
        System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(Math.Clamp(month, 1, 12));

    /// <summary>
    /// When the first plants are due: the earliest and the latest day among the plants it covers,
    /// or null when none is covered. Plants that are dormant don't count for a feeding.
    /// </summary>
    public static (DateOnly Earliest, DateOnly Latest)? FirstDue(
        CareInterval interval, IEnumerable<Plant> plants, Places places, IEnumerable<CareLog> logs, TimeProvider time)
    {
        var logList = logs.ToList();
        var dues = PlantsIn(interval, plants, places)
            .Where(p => !(interval.IsFeed && p.IsDormant))
            .Select(p => DueFor(interval, p, logList, time).Due)
            .ToList();
        return dues.Count == 0 ? null : (dues.Min(), dues.Max());
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
}
