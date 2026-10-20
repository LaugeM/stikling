using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Propagations;

/// <summary>
/// What a plant potted up from a propagation keeps of it: the cover, and the photos from when it
/// was a cutting. Nothing is copied. The photos stay on the propagation.
/// </summary>
public static class CarriedPhotos
{
    /// <summary>
    /// The cover for a plant potted up on <paramref name="potUpDay"/>: the propagation's cover when it
    /// is set and not a reference photo, otherwise its newest photo taken on or before that day, otherwise null.
    /// </summary>
    public static Guid? Cover(Propagation propagation, IEnumerable<Photo> photos, DateOnly potUpDay, TimeProvider time)
    {
        var own = PhotoKinds.Own(photos).ToList();
        if (propagation.CoverPhotoId is { } chosen && own.Any(p => p.Id == chosen))
            return chosen;
        return own.Where(p => time.LocalDay(p.TakenAt) <= potUpDay).MaxBy(p => p.TakenAt)?.Id;
    }

    /// <summary>
    /// The day a plant was potted up: the day of its "Created" entry that points at a propagation,
    /// or the day the plant was made when there is none.
    /// </summary>
    public static DateOnly PotUpDay(Plant plant, IEnumerable<TimelineEntry> plantEntries, TimeProvider time)
    {
        var created = plantEntries
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Created && e.RelatedType == SubjectType.Propagation)
            .OrderBy(e => e.OccurredAt)
            .FirstOrDefault();
        return time.LocalDay(created?.OccurredAt ?? plant.CreatedAt);
    }

    /// <summary>
    /// The propagation's entries with photos that belong on the plant's history: dated on or before
    /// <paramref name="until"/>, and not only reference photos.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> EarlierEntries(
        IEnumerable<TimelineEntry> propagationEntries, IReadOnlySet<Guid> referenceIds, DateOnly until, TimeProvider time) =>
        propagationEntries
            .Where(e => !e.IsDeleted
                && e.PhotoIds.Any(id => !referenceIds.Contains(id))
                && time.LocalDay(e.OccurredAt) <= until)
            .ToList();
}
