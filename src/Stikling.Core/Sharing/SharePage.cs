using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Sharing;

/// <summary>A photo on the public page. The image is fetched by its id. Width and height are 0 when not known.</summary>
public sealed record SharePhoto(Guid Id, PhotoFrame? Frame, int Width = 0, int Height = 0);

/// <summary>What one item on the public page is.</summary>
public enum ShareItemKind
{
    /// <summary>Photos, on their own or from a note whose text is not shown.</summary>
    Photos,

    /// <summary>A note with its text. Only there when notes are shown.</summary>
    Note,

    /// <summary>Something that happened to it, like the first root.</summary>
    Milestone
}

/// <summary>One item on the page.</summary>
/// <param name="DayCount">What goes after the date, " · day 24", or empty.</param>
public sealed record SharePageItem(
    DateOnly Day,
    string DayCount,
    ShareItemKind Kind,
    string? Text,
    IReadOnlyList<SharePhoto> Photos);

/// <summary>Everything the public page says, and nothing it doesn't.</summary>
/// <param name="Updated">The newest change among what is shown.</param>
/// <param name="Items">Oldest first.</param>
public sealed record SharePageModel(
    string Name,
    string? Latin,
    string? Cultivar,
    string? Line,
    SharePhoto? Cover,
    DateTimeOffset Updated,
    int PhotoCount,
    int NoteCount,
    IReadOnlyList<SharePageItem> Items);

/// <summary>What a link has been set to show.</summary>
public sealed record ShareLinkSettings(bool ShowNotes, string? Name, string? Line, IReadOnlyCollection<Guid> LeftOutPhotoIds);

/// <summary>
/// Decides what the public page of a share link shows. The page is open to anyone with the link,
/// so everything is left out unless it is said here that it is shown. Status, rooms, prices, tags,
/// pots, care, the plant's notes field and what the Change entries say are never on it.
/// </summary>
public static class SharePage
{
    /// <summary>The page for a plant.</summary>
    /// <param name="parent">The plant it was propagated from.</param>
    /// <param name="from">The propagation it was potted up from.</param>
    /// <param name="taken">The propagations taken from it.</param>
    /// <param name="photos">Photos of any subject, deleted or not. Others than the plant's are ignored.</param>
    /// <param name="entries">Timeline entries of any subject, deleted or not. Others than the plant's are ignored.</param>
    /// <param name="propagations">Propagations by id, to say what kind of propagation a Propagated entry was about.</param>
    /// <param name="zone">The time zone days are counted in.</param>
    /// <param name="today">Today in that zone.</param>
    public static SharePageModel ForPlant(
        Plant plant, Plant? parent, Propagation? from, IReadOnlyCollection<Propagation> taken,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries, IReadOnlyDictionary<Guid, Propagation> propagations,
        ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var card = ShareCardText.ForPlant(
            plant,
            parent is { IsDeleted: false } ? parent : null,
            from is { IsDeleted: false } ? from : null,
            taken.Where(p => !p.IsDeleted).ToList(),
            today);

        var milestones = new List<Mark>();
        if (plant.AcquiredOn is { Precision: DatePrecision.Day } acquired)
            milestones.Add(new Mark(acquired.Start, 0, "Got it"));

        return Build(plant, SubjectType.Plant, plant.CoverPhotoId, card, PhotoDayCount.StartOf(plant), milestones,
            photos, entries, propagations, settings, zone);
    }

    /// <summary>The page for a propagation.</summary>
    /// <param name="parent">The plant it was taken from.</param>
    public static SharePageModel ForPropagation(
        Propagation propagation, Plant? parent,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries,
        ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var card = ShareCardText.ForPropagation(propagation, parent is { IsDeleted: false } ? parent : null, today);
        var p = propagation;
        var seed = p.Type == PropagationType.Seed;

        var milestones = new List<Mark>
        {
            new(p.StartedOn, 0, $"{(seed ? "Sown" : "Started")}{ShareCardText.MediumPhrase(p.Medium)}")
        };
        if (p.FirstRootOn is { } root)
            milestones.Add(new Mark(root, 1, "First root"));
        if (p.FirstLeafOn is { } leaf)
            milestones.Add(new Mark(leaf, 2, "First leaf"));
        if (p.FirstGerminatedOn is { } up)
            milestones.Add(new Mark(up, 3, "First seed germinated"));
        if (p.RootedOn is { } rooted)
            milestones.Add(new Mark(rooted, 4, "Rooted"));
        if (p.FinishedOn is { } finished && p.Stage is PropagationStage.Done or PropagationStage.Failed)
            milestones.Add(new Mark(finished, 5, p.Stage == PropagationStage.Done ? "Potted up" : "Didn't make it"));

        return Build(propagation, SubjectType.Propagation, propagation.CoverPhotoId, card, PhotoDayCount.StartOf(propagation), milestones,
            photos, entries, new Dictionary<Guid, Propagation>(), settings, zone);
    }

    // A milestone from the subject's own dates, in the order they come within one day
    private sealed record Mark(DateOnly Day, int Rank, string Text);

    // One thing to place on the page, before it is sorted
    private sealed record Slot(DateOnly Day, int Order, DateTimeOffset At, ShareItemKind Kind, string? Text, List<Photo> Photos);

    private static SharePageModel Build(
        Entity subject, SubjectType type, Guid? coverId, ShareCardText card, DateOnly? start, List<Mark> marks,
        IEnumerable<Photo> allPhotos, IEnumerable<TimelineEntry> allEntries, IReadOnlyDictionary<Guid, Propagation> propagations,
        ShareLinkSettings settings, TimeZoneInfo zone)
    {
        var leftOut = settings.LeftOutPhotoIds.ToHashSet();
        var photos = allPhotos
            .Where(p => !p.IsDeleted && p.SubjectType == type && p.SubjectId == subject.Id && !leftOut.Contains(p.Id))
            .GroupBy(p => p.Id).Select(g => g.First())
            .ToDictionary(p => p.Id);
        var entries = allEntries
            .Where(e => !e.IsDeleted && e.SubjectType == type && e.SubjectId == subject.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt)
            .ToList();

        var slots = new List<Slot>();
        var placed = new HashSet<Guid>();
        var shownEntries = new List<TimelineEntry>();

        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case TimelineKind.Photo or TimelineKind.Note:
                    var own = entry.PhotoIds.Where(id => photos.ContainsKey(id) && placed.Add(id)).Select(id => photos[id]).ToList();
                    var text = entry.Kind == TimelineKind.Note && settings.ShowNotes ? ShareLinkRules.Clean(entry.Text) : null;
                    if (own.Count == 0 && text is null)
                        break;
                    shownEntries.Add(entry);
                    // A note whose text is not shown is only its photos
                    slots.Add(new Slot(Day(entry.OccurredAt, zone), 1, entry.OccurredAt,
                        text is null ? ShareItemKind.Photos : ShareItemKind.Note, text, own));
                    break;

                case TimelineKind.Propagated when type == SubjectType.Plant:
                    shownEntries.Add(entry);
                    slots.Add(new Slot(Day(entry.OccurredAt, zone), 0, entry.OccurredAt, ShareItemKind.Milestone,
                        PropagatedText(entry, propagations), []));
                    break;

                // Created is covered by the milestones, and a Change names rooms, people, prices and the like
            }
        }

        // Photos that no shown entry points at still belong on the day they were taken
        var loose = photos.Values.Where(p => !placed.Contains(p.Id)).ToList();
        foreach (var day in loose.GroupBy(p => Day(p.TakenAt, zone)))
            slots.Add(new Slot(day.Key, 1, day.Max(p => p.TakenAt), ShareItemKind.Photos, null, [.. day]));

        // Milestones from dates come before everything else on their day, in a fixed order
        foreach (var mark in marks)
            slots.Add(new Slot(mark.Day, -1, DateTimeOffset.MinValue.AddTicks(mark.Rank), ShareItemKind.Milestone, mark.Text, []));

        var items = slots
            .OrderBy(s => s.Day).ThenBy(s => s.Order).ThenBy(s => s.At)
            .Select(s => new SharePageItem(
                s.Day,
                PhotoDayCount.Suffix(start, s.Day),
                s.Kind,
                s.Text,
                s.Photos.OrderBy(p => p.TakenAt).Select(p => new SharePhoto(p.Id, p.Frame, p.Width, p.Height)).ToList()))
            .ToList();

        var shown = photos.Values.Where(p => placed.Contains(p.Id) || loose.Contains(p)).ToList();
        var cover = coverId is { } id && photos.TryGetValue(id, out var chosen) ? chosen : shown.MaxBy(p => p.TakenAt);
        var updates = shown.Select(p => p.UpdatedAt).Concat(shownEntries.Select(e => e.UpdatedAt)).ToList();

        return new SharePageModel(
            ShareLinkRules.Clean(settings.Name) ?? card.Name,
            card.Latin,
            card.Cultivar,
            ShareLinkRules.Clean(settings.Line) ?? card.Line,
            cover is null ? null : new SharePhoto(cover.Id, cover.Frame, cover.Width, cover.Height),
            updates.Count > 0 ? updates.Max() : subject.UpdatedAt,
            shown.Count,
            items.Count(i => i.Kind == ShareItemKind.Note),
            items);
    }

    private static DateOnly Day(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    // Says what kind it was and nothing else: no name, no link
    private static string PropagatedText(TimelineEntry entry, IReadOnlyDictionary<Guid, Propagation> propagations)
    {
        if (entry.RelatedId is not { } id || !propagations.TryGetValue(id, out var propagation) || propagation.IsDeleted)
            return "A propagation was started";
        return propagation.Type switch
        {
            PropagationType.Cutting => "A cutting was taken",
            PropagationType.Corm => "A corm was taken",
            PropagationType.Offset => "An offset was taken",
            PropagationType.Seed => "Seeds were collected",
            PropagationType.Division => "A division was made",
            PropagationType.AirLayer => "An air layer was started",
            PropagationType.Leaf => "A leaf cutting was taken",
            _ => "A propagation was started"
        };
    }
}
