using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>
/// What counts as someone having been at a plant or propagation, for the daily round's choice of
/// room. Automatic change lines (a new room, a stage) don't, since nobody was there.
/// </summary>
public static class TimelineActivity
{
    /// <summary>
    /// True for an entry on a plant or propagation that isn't deleted and is a creation, a note,
    /// photos, a propagation taken, or a look over. Care logs count too, but they aren't timeline
    /// entries, so the caller adds those.
    /// </summary>
    public static bool Counts(TimelineEntry entry)
    {
        if (entry.IsDeleted || entry.SubjectType is not (SubjectType.Plant or SubjectType.Propagation))
            return false;

        return entry.Kind switch
        {
            TimelineKind.Created or TimelineKind.Note or TimelineKind.Photo or TimelineKind.Propagated => true,
            TimelineKind.Change => TimelineEvents.Of(entry) == TimelineEvent.LookedOver,
            _ => false
        };
    }

    /// <summary>For each subject, the newest moment before <paramref name="before"/> of an entry that counts.</summary>
    public static IReadOnlyDictionary<Guid, DateTimeOffset> LastBefore(
        IEnumerable<TimelineEntry> entries, DateTimeOffset before)
    {
        var last = new Dictionary<Guid, DateTimeOffset>();
        foreach (var entry in entries)
        {
            if (!Counts(entry) || entry.OccurredAt >= before)
                continue;
            if (!last.TryGetValue(entry.SubjectId, out var seen) || entry.OccurredAt > seen)
                last[entry.SubjectId] = entry.OccurredAt;
        }
        return last;
    }
}
