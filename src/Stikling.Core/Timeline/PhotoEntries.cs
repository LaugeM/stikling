using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>How photos added in one go are put on a plant's or propagation's history.</summary>
public static class PhotoEntries
{
    /// <summary>
    /// Photos on their own get one entry per day they were taken, so old photos picked from the
    /// gallery land on their own days. With a note they stay together as one entry, because the
    /// note is about one moment. An entry is dated by its newest photo.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> For(
        SubjectType subjectType, Guid subjectId, IReadOnlyList<Photo> photos, TimeProvider time, string? note = null)
    {
        if (photos.Count == 0)
            return [];

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var groups = text is null
            ? photos.GroupBy(p => time.LocalDay(p.TakenAt)).Select(g => g.ToList())
            : [photos.ToList()];

        return groups
            .Select(group => new TimelineEntry
            {
                SubjectType = subjectType,
                SubjectId = subjectId,
                Kind = text is null ? TimelineKind.Photo : TimelineKind.Note,
                OccurredAt = group.Max(p => p.TakenAt),
                Text = text,
                PhotoIds = group.Select(p => p.Id).ToList()
            })
            .ToList();
    }

    /// <summary>The day the oldest photo was taken, or null without photos.</summary>
    public static DateOnly? OldestDay(IReadOnlyList<Photo> photos, TimeProvider time) =>
        photos.Count == 0 ? null : photos.Min(p => time.LocalDay(p.TakenAt));

    /// <summary>The photo to use as the cover: the newest, since it shows the plant as it is now.</summary>
    public static Photo Cover(IReadOnlyList<Photo> photos) => photos.MaxBy(p => p.TakenAt)!;

    /// <summary>
    /// When the first entry of a new plant or propagation is dated. A day without a time is
    /// recorded at noon, or now for today, so a photo from earlier that day would come before it.
    /// The first entry comes in just before its first photo then, but never on the day before.
    /// </summary>
    public static DateTimeOffset StartedAt(DateTimeOffset started, IReadOnlyList<Photo> photos, TimeProvider time)
    {
        var day = time.LocalDay(started);
        var first = photos.Where(p => time.LocalDay(p.TakenAt) == day).Select(p => p.TakenAt).DefaultIfEmpty(started).Min();
        if (first >= started)
            return started;
        var startOfDay = time.MomentAt(day.ToDateTime(TimeOnly.MinValue));
        return first.AddSeconds(-1) < startOfDay ? first : first.AddSeconds(-1);
    }
}
