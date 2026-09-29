using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>When each plant last had something happen to it, for sorting the list by last activity.</summary>
public static class PlantActivity
{
    /// <summary>The newest of a plant's latest timeline entry and its latest care, by plant id.
    /// Care only has a day, so it counts from the start of that day.</summary>
    public static Dictionary<Guid, DateTimeOffset> Latest(
        IReadOnlyDictionary<Guid, TimelineEntry> latestEntries, IEnumerable<CareLog> careLogs)
    {
        var latest = latestEntries.ToDictionary(pair => pair.Key, pair => pair.Value.OccurredAt);
        foreach (var log in careLogs.Where(l => !l.IsDeleted))
        {
            var at = new DateTimeOffset(log.OccurredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            if (!latest.TryGetValue(log.PlantId, out var current) || at > current)
                latest[log.PlantId] = at;
        }
        return latest;
    }
}
