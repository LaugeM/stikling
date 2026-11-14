using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>What the logged water changes of a batch say, shared by the failed comparison and the results page.</summary>
public static class WaterChangeLog
{
    /// <summary>Whether the medium has water to change, which is where a change can be logged and a reminder is set.</summary>
    public static bool Takes(GrowingMedium medium) =>
        PropagationCareService.KindsFor(medium).Contains(PropagationCareKind.ChangedWater);

    /// <summary>The distinct days the water was changed, for each propagation that has any logged.</summary>
    public static Dictionary<Guid, List<DateOnly>> DaysByBatch(IEnumerable<PropagationCareLog> logs) =>
        logs.Where(l => !l.IsDeleted && l.Kind == PropagationCareKind.ChangedWater)
            .GroupBy(l => l.PropagationId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.OccurredOn).Distinct().ToList());

    /// <summary>
    /// The days it ran over the number of days the water was changed. Null without any changes or a
    /// stretch of time to go on. <paramref name="end"/> is when it rooted or finished, else the last change.
    /// </summary>
    public static double? DaysBetween(Propagation p, DateOnly? end, List<DateOnly>? days)
    {
        if (days is null || days.Count == 0)
            return null;
        var until = end ?? days.Max();
        var span = until.DayNumber - p.StartedOn.DayNumber;
        return span <= 0 ? null : span / (double)days.Count;
    }
}
