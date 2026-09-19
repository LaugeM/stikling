using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

public static class TimelineOrder
{
    /// <summary>Newest first; entries at the same moment keep the order they were recorded in (newest first).</summary>
    public static IReadOnlyList<TimelineEntry> NewestFirst(IEnumerable<TimelineEntry> entries) =>
        entries.Where(e => !e.IsDeleted)
               .OrderByDescending(e => e.OccurredAt)
               .ThenByDescending(e => e.CreatedAt)
               .ToList();

    /// <summary>Groups entries by calendar day (in the given offset), newest day first.</summary>
    public static IReadOnlyList<(DateOnly Day, IReadOnlyList<TimelineEntry> Entries)> ByDay(
        IEnumerable<TimelineEntry> entries, TimeSpan offset) =>
        NewestFirst(entries)
            .GroupBy(e => DateOnly.FromDateTime(e.OccurredAt.ToOffset(offset).DateTime))
            .Select(g => (g.Key, (IReadOnlyList<TimelineEntry>)g.ToList()))
            .ToList();
}
