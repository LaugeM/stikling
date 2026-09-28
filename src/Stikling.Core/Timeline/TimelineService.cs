using Stikling.Core.Models;

namespace Stikling.Core.Timeline;

/// <summary>Adding and correcting the notes and photos on a plant's or propagation's history.</summary>
public sealed class TimelineService(ITimelineRepository timeline, IPhotoRepository photos, TimeProvider time)
{
    /// <summary>
    /// Puts newly saved photos on the history, split by the day each was taken unless there is a
    /// note (see <see cref="PhotoEntries.For"/>). A <paramref name="day"/> picked by hand dates
    /// all of them, and they go on one entry.
    /// </summary>
    public async Task<IReadOnlyList<TimelineEntry>> AddPhotosAsync(
        SubjectType subjectType, Guid subjectId, IReadOnlyList<Photo> added, string? note = null, DateOnly? day = null)
    {
        if (day is { } d)
        {
            var moment = time.MomentOn(d);
            foreach (var photo in added)
            {
                photo.TakenAt = moment;
                await photos.UpdateAsync(photo);
            }
        }

        var entries = PhotoEntries.For(subjectType, subjectId, added, time, note);
        foreach (var entry in entries)
            await timeline.AddAsync(entry);
        return entries;
    }

    /// <summary>
    /// Only what the user wrote can be corrected. Automatic entries copy something stored
    /// elsewhere (a stage date, a care log), so they are fixed there instead.
    /// </summary>
    public static bool CanCorrect(TimelineEntry entry) =>
        !entry.IsDeleted && entry.Kind is TimelineKind.Note or TimelineKind.Photo;

    /// <summary>
    /// Changes an entry's text and day, keeping the version it replaces. Photos on the entry
    /// move to the new day with it. Returns false when nothing changed.
    /// </summary>
    public async Task<bool> CorrectAsync(TimelineEntry entry, string? text, DateOnly day)
    {
        if (!CanCorrect(entry))
            throw new InvalidOperationException("Only notes and photos can be corrected.");

        if (day > time.Today())
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
        var sameDay = time.LocalDay(entry.OccurredAt) == day;
        var occurredAt = sameDay ? entry.OccurredAt : time.MomentOn(day);
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

    /// <summary>
    /// Call after deleting a photo. An entry that only held photos goes too once none of them
    /// are left, so the history doesn't keep an empty "Photo" line. An entry with text stays.
    /// </summary>
    public async Task RemoveEmptiedEntriesAsync(Guid subjectId, Guid photoId)
    {
        foreach (var entry in await timeline.GetForAsync(subjectId))
        {
            if (!entry.PhotoIds.Contains(photoId) || !string.IsNullOrWhiteSpace(entry.Text))
                continue;

            var anyLeft = false;
            foreach (var id in entry.PhotoIds)
                anyLeft |= await photos.GetAsync(id) is not null;

            if (!anyLeft)
                await timeline.DeleteAsync(entry.Id);
        }
    }
}
