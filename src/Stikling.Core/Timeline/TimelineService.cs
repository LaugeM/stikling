using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

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
    /// Call after a photo moved from the reference photos to the history. A photo that an entry
    /// still lists is back where it was. One that was added as a reference photo has no entry, so it
    /// gets a Photo entry on the day it was taken, the same as the Photo button makes. Returns
    /// whether an entry was added.
    /// </summary>
    public async Task<bool> EnsureOnHistoryAsync(Photo photo)
    {
        if (photo.IsReference || photo.IsDeleted || photo.SubjectType is not (SubjectType.Plant or SubjectType.Propagation))
            return false;

        var listed = (await timeline.GetForAsync(photo.SubjectId)).Any(e => !e.IsDeleted && e.PhotoIds.Contains(photo.Id));
        if (listed)
            return false;

        foreach (var entry in PhotoEntries.For(photo.SubjectType, photo.SubjectId, [photo], time))
            await timeline.AddAsync(entry);
        return true;
    }

    /// <summary>
    /// Writes that a plant or propagation was looked over, with nothing else to log. It's a
    /// change entry with an event rather than a new kind, so older versions still load it.
    /// </summary>
    public async Task<TimelineEntry> LookedOverAsync(SubjectType type, Guid subjectId)
    {
        var entry = new TimelineEntry
        {
            SubjectType = type,
            SubjectId = subjectId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.LookedOver,
            OccurredAt = time.GetLocalNow()
        };
        HistoryText.Set(entry, [HistoryText.Line("LookedOver")]);
        await timeline.AddAsync(entry);
        return entry;
    }

    /// <summary>Takes an entry off the history again (soft delete), e.g. a look over tapped by mistake.</summary>
    public Task RemoveAsync(Guid entryId) => timeline.DeleteAsync(entryId);

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
            throw new InvalidOperationException(Strings.Propagations.ErrOnlyNotesCorrected);

        if (day > time.Today())
            throw new ArgumentException(Strings.Propagations.ErrEntryFuture, nameof(day));

        var entryPhotos = new List<Photo>();
        foreach (var id in entry.PhotoIds)
        {
            if (await photos.GetAsync(id) is { } photo)
                entryPhotos.Add(photo);
        }

        var cleanText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (cleanText is null && entryPhotos.Count == 0)
            throw new ArgumentException(Strings.Propagations.ErrNoteNeedsText, nameof(text));

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
    /// Sets the day a note comes back on Today, or null to take it off. It isn't a correction, so the
    /// entry's edits stay as they are. A new day makes the note come back even if it was ticked Done before.
    /// </summary>
    public async Task SetComesBackAsync(TimelineEntry entry, DateOnly? day)
    {
        if (entry.IsDeleted)
            throw new InvalidOperationException(Strings.Propagations.ErrEntryDeleted);
        if (entry.Kind != TimelineKind.Note)
            day = null;
        if (entry.ComesBackOn == day)
            return;
        if (day is { } d && d <= time.Today())
            throw new ArgumentException(Strings.Propagations.ErrPickDayAfterToday, nameof(day));

        entry.ComesBackOn = day;
        entry.CameBackDoneOn = null;
        await timeline.UpdateAsync(entry);
    }

    /// <summary>Ticks a note that came back as Done on Today.</summary>
    public async Task MarkCameBackDoneAsync(TimelineEntry entry)
    {
        entry.CameBackDoneOn = time.Today();
        await timeline.UpdateAsync(entry);
    }

    /// <summary>Sends a note that came back on for another year.</summary>
    public async Task BringBackAgainAsync(TimelineEntry entry)
    {
        if (entry.ComesBackOn is not { } day)
            return;
        entry.ComesBackOn = NotesComingBack.NextYear(day, time.Today());
        entry.CameBackDoneOn = null;
        await timeline.UpdateAsync(entry);
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
