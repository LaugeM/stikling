using Stikling.Core.Models;

namespace Stikling.Core.Plants;

public enum RecentChangeKind
{
    Arrived,
    Repotted,
    Moved
}

/// <summary>Something that happened to a plant lately, and the local day it happened on.</summary>
public sealed record RecentChange(RecentChangeKind Kind, DateOnly Day);

/// <summary>What happened to a plant lately: it arrived, was repotted or was moved.</summary>
public static class RecentChanges
{
    /// <summary>The recent changes line shows two weeks back until the person picks another number.</summary>
    public const int DefaultDays = 14;

    /// <summary>The number of days to use for a setting that was never chosen.</summary>
    public static int DaysOf(int? setting) => Math.Max(0, setting ?? DefaultDays);

    /// <summary>
    /// The newest arrival, repot and move that fall within the last <paramref name="days"/> days
    /// (the day itself counts), oldest first. Zero days means the line is off. Only an acquired
    /// date known to the day counts as an arrival. A move is a "Moved to" or "Moved from" line in the
    /// plant's history, and only counts while the plant still has a place.
    /// </summary>
    public static IReadOnlyList<RecentChange> Of(
        Plant plant, IEnumerable<CareLog> care, IEnumerable<TimelineEntry> timeline,
        DateOnly today, int days, Func<DateTimeOffset, DateOnly> localDay)
    {
        if (days <= 0)
            return [];

        var found = new List<RecentChange>();

        if (plant.AcquiredOn is { Precision: DatePrecision.Day } acquired)
            found.Add(new(RecentChangeKind.Arrived, acquired.Start));

        var repotted = care
            .Where(c => !c.IsDeleted && c.PlantId == plant.Id && c.Kind == CareKind.Repotted)
            .Select(c => (DateOnly?)c.OccurredOn)
            .Max();
        if (repotted is { } repotDay)
            found.Add(new(RecentChangeKind.Repotted, repotDay));

        if (plant.PlaceId is not null)
        {
            var moved = timeline
                .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change
                    && e.SubjectType == SubjectType.Plant && e.SubjectId == plant.Id
                    && (e.Text ?? "").Split('\n').Select(l => l.Trim())
                        .Any(l => l.StartsWith("Moved to ", StringComparison.Ordinal)
                            || l.StartsWith("Moved from ", StringComparison.Ordinal)))
                .Select(e => (DateOnly?)localDay(e.OccurredAt))
                .Max();
            if (moved is { } moveDay)
                found.Add(new(RecentChangeKind.Moved, moveDay));
        }

        // Something dated later than today hasn't happened yet
        return found
            .Where(c => c.Day <= today && today.DayNumber - c.Day.DayNumber <= days)
            .OrderBy(c => c.Day)
            .ToList();
    }
}
