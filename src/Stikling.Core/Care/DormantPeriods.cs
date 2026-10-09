using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Care;

/// <summary>
/// The days a plant spent dormant, read off its history. A plant rests on a different pace than
/// it grows, so the watering reminder keeps the two apart.
/// </summary>
public sealed class DormantPeriods
{
    private readonly List<(DateOnly Start, DateOnly? End)> periods;

    private DormantPeriods(List<(DateOnly Start, DateOnly? End)> periods) => this.periods = periods;

    public static DormantPeriods None { get; } = new([]);

    /// <summary>
    /// A period starts at a "Went dormant" line in the plant's history and ends at a "Woke up from
    /// dormancy" line, each on the local day of its entry. While the plant is dormant now, the
    /// period it's in starts on <see cref="Plant.DormantSince"/>, which can have been set to
    /// another day than the entry says. A period left open on a plant that isn't dormant is dropped.
    /// </summary>
    public static DormantPeriods Of(Plant plant, IEnumerable<TimelineEntry> timeline, TimeProvider time)
    {
        var periods = new List<(DateOnly Start, DateOnly? End)>();
        DateOnly? open = null;

        var changes = timeline
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change
                && e.SubjectType == SubjectType.Plant && e.SubjectId == plant.Id)
            .OrderBy(e => e.OccurredAt);

        foreach (var entry in changes)
        {
            var day = time.LocalDay(entry.OccurredAt);
            foreach (var line in (entry.Text ?? "").Split('\n').Select(l => l.Trim()))
            {
                if (line == PlantChanges.DormancyText(true))
                    open ??= day;
                else if (line == PlantChanges.DormancyText(false) && open is { } start)
                {
                    periods.Add((start, day));
                    open = null;
                }
            }
        }

        if (plant.DormantSince is { } since)
            periods.Add((since, null));

        return new(periods);
    }

    /// <summary>True when the day falls in a period, counting the days it started and ended on.</summary>
    public bool Contains(DateOnly day) =>
        periods.Any(p => day >= p.Start && (p.End is null || day <= p.End));
}
