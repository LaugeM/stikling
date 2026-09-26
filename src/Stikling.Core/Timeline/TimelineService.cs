using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>Correcting the notes and photos on a plant's or propagation's history.</summary>
public sealed class TimelineService(ITimelineRepository timeline, IPhotoRepository photos, TimeProvider time)
{
    /// <summary>
    /// Only what the user wrote can be corrected. Automatic entries copy something stored
    /// elsewhere (a stage date, a care log), so they are fixed there instead.
    /// </summary>
    public static bool CanCorrect(TimelineEntry entry) =>
        !entry.IsDeleted && entry.Kind is TimelineKind.Note or TimelineKind.Photo;

    /// <summary>
    /// The moment to record for something that happened on a given day. Today means now, and
    /// an earlier day has no time of day, so it is recorded at noon to land on the right day.
    /// </summary>
    public static DateTimeOffset MomentOn(DateOnly day, DateTimeOffset localNow) =>
        day == DateOnly.FromDateTime(localNow.DateTime)
            ? localNow
            : new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), localNow.Offset);

    /// <summary>
    /// Changes an entry's text and day, keeping the version it replaces. Photos on the entry
    /// move to the new day with it. Returns false when nothing changed.
    /// </summary>
    public async Task<bool> CorrectAsync(TimelineEntry entry, string? text, DateOnly day)
    {
        if (!CanCorrect(entry))
            throw new InvalidOperationException("Only notes and photos can be corrected.");

        var localNow = time.GetLocalNow();
        if (day > DateOnly.FromDateTime(localNow.DateTime))
            throw new ArgumentException("An entry can't be dated in the future.", nameof(day));

        var entryPhotos = new List<Photo>();
        foreach (var id in entry.PhotoIds)
        {
            if (await photos.GetAsync(id) is { } photo)
                entryPhotos.Add(photo);
        }

        var cleanText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (cleanText is null && entryPhotos.Count == 0)
            throw new ArgumentException("A note needs some text.", nameof(text));

        // The same day keeps the time it was recorded at
        var sameDay = DateOnly.FromDateTime(entry.OccurredAt.ToOffset(localNow.Offset).DateTime) == day;
        var occurredAt = sameDay ? entry.OccurredAt : MomentOn(day, localNow);
        if (cleanText == entry.Text && occurredAt == entry.OccurredAt)
            return false;

        entry.Edits.Add(new TimelineEdit
        {
            Text = entry.Text,
            OccurredAt = entry.OccurredAt,
            ReplacedAt = time.GetUtcNow()
        });
        entry.Text = cleanText;
        entry.OccurredAt = occurredAt;
        entry.Kind = cleanText is null ? TimelineKind.Photo : TimelineKind.Note;
        await timeline.UpdateAsync(entry);

        if (!sameDay)
        {
            foreach (var photo in entryPhotos)
            {
                photo.TakenAt = occurredAt;
                await photos.UpdateAsync(photo);
            }
        }

        return true;
    }
}
