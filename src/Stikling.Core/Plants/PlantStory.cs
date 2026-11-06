using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Timeline;

namespace Stikling.Core.Plants;

/// <summary>One photo that can go on a plant's printed story.</summary>
public sealed record StoryPhoto(Guid Id, DateOnly Day);

/// <param name="Key">Tells the notes apart while choosing. A received note has no id of its own.</param>
public sealed record StoryNote(string Key, DateOnly Day, string Text);

/// <summary>
/// What can go on a plant's printed story: its own photos and notes, with those from the cutting it
/// grew from and from whoever had it before. Nothing here is stored, and the choice isn't kept.
/// </summary>
public static class PlantStory
{
    /// <summary>How many photos are ticked at first, which leaves room for the rest on one page.</summary>
    public const int PhotosAtFirst = 6;

    /// <summary>How many notes are ticked at first.</summary>
    public const int NotesAtFirst = 5;

    /// <summary>
    /// The photos, oldest first. Reference photos are left out, since they aren't of this plant, and
    /// those of the cutting only count up to <paramref name="cuttingUntil"/>, the day it was potted up.
    /// </summary>
    public static IReadOnlyList<StoryPhoto> Photos(
        IEnumerable<Photo> plantPhotos, IEnumerable<Photo> cuttingPhotos, DateOnly? cuttingUntil, TimeProvider time)
    {
        var earlier = cuttingUntil is { } until
            ? PhotoKinds.Own(cuttingPhotos).Where(p => time.LocalDay(p.TakenAt) <= until)
            : [];

        return PhotoKinds.Own(plantPhotos)
            .Concat(earlier)
            .DistinctBy(p => p.Id)
            .OrderBy(p => p.TakenAt)
            .ThenBy(p => p.Id)
            .Select(p => new StoryPhoto(p.Id, time.LocalDay(p.TakenAt)))
            .ToList();
    }

    /// <summary>
    /// What was written about the plant, oldest first: notes and photo captions from its history, the
    /// cutting's up to the day it was potted up, and those from before it was received. The lines the
    /// app writes itself aren't notes, so they stay out.
    /// </summary>
    public static IReadOnlyList<StoryNote> Notes(
        IEnumerable<TimelineEntry> plantEntries,
        IEnumerable<TimelineEntry> cuttingEntries,
        DateOnly? cuttingUntil,
        ReceivedHistory? received,
        TimeProvider time)
    {
        var own = plantEntries.Concat(cuttingUntil is { } until
            ? cuttingEntries.Where(e => time.LocalDay(e.OccurredAt) <= until)
            : []);

        var written = own
            .Where(e => !e.IsDeleted && e.Kind is TimelineKind.Note or TimelineKind.Photo && !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => (At: time.LocalDay(e.OccurredAt), Moment: e.OccurredAt, Note: new StoryNote(e.Id.ToString(), time.LocalDay(e.OccurredAt), e.Text!.Trim())));

        var before = (received?.Items ?? [])
            .Select((item, index) => (item, index))
            .Where(x => x.item.Kind == ShareItemKind.Note && !string.IsNullOrWhiteSpace(x.item.Text))
            .Select(x => (At: x.item.Day, Moment: DateTimeOffset.MinValue, Note: new StoryNote($"{received!.Id}:{x.index}", x.item.Day, x.item.Text!.Trim())));

        return before.Concat(written)
            .OrderBy(x => x.At)
            .ThenBy(x => x.Moment)
            .Select(x => x.Note)
            .ToList();
    }

    /// <summary>
    /// The ones ticked at first when there are more than fit: spread evenly from the first to the
    /// last, so the page shows the whole life of the plant and not only one end of it.
    /// </summary>
    public static IReadOnlyList<T> Spread<T>(IReadOnlyList<T> items, int count)
    {
        if (items.Count <= count)
            return [.. items];
        if (count <= 0)
            return [];
        if (count == 1)
            return [items[^1]];

        return [.. Enumerable.Range(0, count).Select(i => items[(int)Math.Round(i * (items.Count - 1) / (double)(count - 1))])];
    }
}
