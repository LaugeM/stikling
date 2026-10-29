using System.Globalization;
using Strings = Stikling.Core.Resources;
using System.Text.Json.Serialization;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Sharing;

/// <summary>A photo on the public page. The image is fetched by its id. Width and height are 0 when not known.</summary>
public sealed record SharePhoto(Guid Id, PhotoFrame? Frame, int Width = 0, int Height = 0);

/// <summary>What one item on the public page is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShareItemKind>))]
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
/// <param name="Label">The same without the separator, "day 24", or null when there is none.</param>
public sealed record SharePageItem(
    DateOnly Day,
    string DayCount,
    ShareItemKind Kind,
    string? Text,
    IReadOnlyList<SharePhoto> Photos,
    string? Label = null);

/// <summary>Everything the public page says, and nothing it doesn't.</summary>
/// <param name="Updated">The newest change among what is shown.</param>
/// <param name="Items">Oldest first.</param>
/// <param name="CanKeep">Whether someone who was given it can keep its history: a plant that has left, or a propagation with units given away.</param>
public sealed record SharePageModel(
    string Name,
    string? Latin,
    string? Cultivar,
    string? Line,
    SharePhoto? Cover,
    DateTimeOffset Updated,
    int PhotoCount,
    int NoteCount,
    IReadOnlyList<SharePageItem> Items,
    bool CanKeep = false);

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
    /// <param name="received">Histories received with the plant or the propagation it came from, deleted or not. Others than theirs are ignored.</param>
    /// <param name="propagations">Propagations by id, to say what kind of propagation a Propagated entry was about.</param>
    /// <param name="zone">The time zone days are counted in.</param>
    /// <param name="today">Today in that zone.</param>
    public static SharePageModel ForPlant(
        Plant plant, Plant? parent, Propagation? from, IReadOnlyCollection<Propagation> taken,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries, IEnumerable<ReceivedHistory> received,
        IReadOnlyDictionary<Guid, Propagation> propagations, ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var histories = received.ToList();
        var card = ShareCardText.ForPlant(
            plant,
            parent is { IsDeleted: false } ? parent : null,
            from is { IsDeleted: false } ? from : null,
            taken.Where(p => !p.IsDeleted).ToList(),
            today);

        // A plant potted up from a propagation starts its page with the rooting it came from
        var rooting = from is { IsDeleted: false } ? from : null;

        var milestones = new List<Mark>();
        if (plant.AcquiredOn is { Precision: DatePrecision.Day } acquired)
            milestones.Add(new Mark(acquired.Start, 0, rooting is null ? Strings.Propagations.MarkGotIt : Strings.Propagations.MarkPottedUp));

        var sections = new List<Section>();
        if (rooting is not null)
        {
            var history = HistoryOf(histories, rooting.Id, SubjectType.Propagation);
            sections.Add(new Section(rooting, SubjectType.Propagation, day => PhotoDayCount.DayLabel(PhotoDayCount.StartOf(rooting), day),
                PropagationMarks(rooting, includeFinish: false, history), CutOff(plant, zone), history));
        }
        // A plant's "Got it" mark is the day it arrived, so a history adds nothing of its own to it
        sections.Add(new Section(plant, SubjectType.Plant, day => PhotoDayCount.PlantAge(plant, day), milestones, null,
            HistoryOf(histories, plant.Id, SubjectType.Plant)));

        return Build(sections, plant, plant.CoverPhotoId, card, photos, entries, propagations, settings, zone, plant.HasLeft);
    }

    /// <summary>
    /// The photos of the propagation a plant was potted up from that its page shows: not deleted,
    /// and taken on or before the day it was potted up, oldest first. Empty when there is no
    /// propagation or it is deleted. Photos of the units left in a batch afterwards aren't its story.
    /// </summary>
    /// <param name="photos">Photos of any subject, deleted or not.</param>
    public static IReadOnlyList<Photo> RootingPhotos(Plant plant, Propagation? from, IEnumerable<Photo> photos, TimeZoneInfo zone)
    {
        if (from is not { IsDeleted: false })
            return [];
        var cutOff = CutOff(plant, zone);
        return photos
            .Where(p => !p.IsDeleted && !p.IsReference && p.SubjectType == SubjectType.Propagation && p.SubjectId == from.Id
                && Day(p.TakenAt, zone) <= cutOff)
            .GroupBy(p => p.Id).Select(g => g.First())
            .OrderBy(p => p.TakenAt)
            .ToList();
    }

    // The day it was potted up. When its date was changed to a month or a year, the day the plant was
    // added stands in, since the propagation's finish can be later and a batch may still be going
    private static DateOnly CutOff(Plant plant, TimeZoneInfo zone) =>
        plant.AcquiredOn is { Precision: DatePrecision.Day } acquired ? acquired.Start : Day(plant.CreatedAt, zone);

    /// <summary>The page for a propagation.</summary>
    /// <param name="parent">The plant it was taken from.</param>
    public static SharePageModel ForPropagation(
        Propagation propagation, Plant? parent,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries, IEnumerable<ReceivedHistory> received,
        ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var history = HistoryOf(received, propagation.Id, SubjectType.Propagation);
        var card = ShareCardText.ForPropagation(propagation, parent is { IsDeleted: false } ? parent : null, today);
        var sections = new List<Section>
        {
            new(propagation, SubjectType.Propagation, day => PhotoDayCount.DayLabel(PhotoDayCount.StartOf(propagation), day),
                PropagationMarks(propagation, includeFinish: true, history), null, history)
        };
        return Build(sections, propagation, propagation.CoverPhotoId, card, photos, entries, new Dictionary<Guid, Propagation>(), settings, zone,
            propagation.GivenAwayCount > 0);
    }

    // The newest history of the subject that isn't deleted
    private static ReceivedHistory? HistoryOf(IEnumerable<ReceivedHistory> received, Guid subjectId, SubjectType type) =>
        received
            .Where(h => !h.IsDeleted && h.SubjectId == subjectId && h.SubjectType == type)
            .OrderByDescending(h => h.UpdatedAt)
            .FirstOrDefault();

    // The milestones a propagation's dates give. Its finishing one is left out of the plant's page, where the plant is it.
    // With a received history, its items carry the dates up to the day it arrived, so those are left out and "Got it" marks the day.
    private static List<Mark> PropagationMarks(Propagation p, bool includeFinish, ReceivedHistory? history)
    {
        var marks = AllPropagationMarks(p, includeFinish);
        if (history is null)
            return marks;
        marks = marks.Where(m => m.Day > history.ArrivedOn).ToList();
        marks.Add(new Mark(history.ArrivedOn, 0, Strings.Propagations.MarkGotIt));
        return marks;
    }

    private static List<Mark> AllPropagationMarks(Propagation p, bool includeFinish)
    {
        var seed = p.Type == PropagationType.Seed;
        var milestones = new List<Mark>
        {
            new(p.StartedOn, 0, string.Format(CultureInfo.CurrentCulture, seed ? Strings.Propagations.MarkSown : Strings.Propagations.MarkStarted, ShareCardText.MediumPhrase(p.Medium)))
        };
        if (p.FirstRootOn is { } root)
            milestones.Add(new Mark(root, 1, Strings.Propagations.MarkFirstRoot));
        if (p.FirstLeafOn is { } leaf)
            milestones.Add(new Mark(leaf, 2, Strings.Propagations.MarkFirstLeaf));
        if (p.FirstGerminatedOn is { } up)
            milestones.Add(new Mark(up, 3, Strings.Propagations.MarkFirstGerminated));
        if (p.RootedOn is { } rooted)
            milestones.Add(new Mark(rooted, 4, Strings.Propagations.MarkRooted));
        if (includeFinish && p.FinishedOn is { } finished && p.Stage is PropagationStage.Done or PropagationStage.Failed)
            milestones.Add(new Mark(finished, 5, p.Stage == PropagationStage.Done ? Strings.Propagations.MarkPottedUp : Strings.Propagations.MarkDidntMakeIt));
        return milestones;
    }

    // A milestone from the subject's own dates, in the order they come within one day
    private sealed record Mark(DateOnly Day, int Rank, string Text);

    // One thing to place on the page, before it is sorted. The section keeps the rooting a plant came from ahead of the plant's own.
    // A received item keeps the label it had on the giver's page, so it carries its own.
    private sealed record Slot(DateOnly Day, int Section, int Order, DateTimeOffset At, ShareItemKind Kind, string? Text, List<Photo> Photos,
        bool Frozen = false, string? FrozenLabel = null);

    // The records of one subject that go on the page, with its own day counts and milestones.
    // Nothing after the cut-off day is shown.
    private sealed record Section(Entity Subject, SubjectType Type, Func<DateOnly, string?> Age, List<Mark> Marks, DateOnly? CutOff, ReceivedHistory? History);

    private static SharePageModel Build(
        IReadOnlyList<Section> sections, Entity subject, Guid? coverId, ShareCardText card,
        IEnumerable<Photo> allPhotos, IEnumerable<TimelineEntry> allEntries, IReadOnlyDictionary<Guid, Propagation> propagations,
        ShareLinkSettings settings, TimeZoneInfo zone, bool canKeep)
    {
        var leftOut = settings.LeftOutPhotoIds.ToHashSet();
        var photoList = allPhotos.ToList();
        var entryList = allEntries.ToList();

        var slots = new List<Slot>();
        var ages = new List<Func<DateOnly, string?>>();
        var photos = new Dictionary<Guid, Photo>();
        var placed = new HashSet<Guid>();
        var shownEntries = new List<TimelineEntry>();
        var shownHistories = new List<ReceivedHistory>();

        for (var rank = 0; rank < sections.Count; rank++)
        {
            var section = sections[rank];
            ages.Add(section.Age);
            var type = section.Type;
            bool Within(DateOnly day) => section.CutOff is not { } last || day <= last;

            var subjectPhotos = photoList
                .Where(p => !p.IsDeleted && !p.IsReference && p.SubjectType == type && p.SubjectId == section.Subject.Id && !leftOut.Contains(p.Id))
                .GroupBy(p => p.Id).Select(g => g.First())
                .ToDictionary(p => p.Id);
            // Photos copied with a history only show through its items
            var own = subjectPhotos.Values
                .Where(p => p.ReceivedHistoryId is null && Within(Day(p.TakenAt, zone)))
                .ToDictionary(p => p.Id);
            var entries = entryList
                .Where(e => !e.IsDeleted && e.SubjectType == type && e.SubjectId == section.Subject.Id && Within(Day(e.OccurredAt, zone)))
                .OrderBy(e => e.OccurredAt).ThenBy(e => e.CreatedAt)
                .ToList();

            foreach (var photo in own.Values)
                photos[photo.Id] = photo;

            foreach (var entry in entries)
            {
                switch (entry.Kind)
                {
                    case TimelineKind.Photo or TimelineKind.Note:
                        var entryPhotos = entry.PhotoIds.Where(id => own.ContainsKey(id) && placed.Add(id)).Select(id => own[id]).ToList();
                        var text = entry.Kind == TimelineKind.Note && settings.ShowNotes ? ShareLinkRules.Clean(entry.Text) : null;
                        if (entryPhotos.Count == 0 && text is null)
                            break;
                        shownEntries.Add(entry);
                        // A note whose text is not shown is only its photos
                        slots.Add(new Slot(Day(entry.OccurredAt, zone), rank, 1, entry.OccurredAt,
                            text is null ? ShareItemKind.Photos : ShareItemKind.Note, text, entryPhotos));
                        break;

                    case TimelineKind.Propagated when type == SubjectType.Plant:
                        shownEntries.Add(entry);
                        slots.Add(new Slot(Day(entry.OccurredAt, zone), rank, 0, entry.OccurredAt, ShareItemKind.Milestone,
                            PropagatedText(entry, propagations), []));
                        break;

                    // Created is covered by the milestones, and a Change names rooms, people, prices and the like
                }
            }

            // Photos that no shown entry points at still belong on the day they were taken
            var loose = own.Values.Where(p => !placed.Contains(p.Id)).ToList();
            foreach (var day in loose.GroupBy(p => Day(p.TakenAt, zone)))
                slots.Add(new Slot(day.Key, rank, 1, day.Max(p => p.TakenAt), ShareItemKind.Photos, null, [.. day]));
            foreach (var photo in loose)
                placed.Add(photo.Id);

            // What the subject had before it was received comes before everything of its own, even on the same day
            if (section.History is { } history)
            {
                var any = false;
                for (var i = 0; i < history.Items.Count; i++)
                {
                    var item = history.Items[i];
                    if (!Within(item.Day))
                        continue;
                    var kind = item.Kind;
                    var text = ShareLinkRules.Clean(item.Text);
                    List<Photo> itemPhotos;
                    if (kind == ShareItemKind.Milestone)
                    {
                        if (text is null)
                            continue;
                        itemPhotos = [];
                    }
                    else
                    {
                        text = kind == ShareItemKind.Note && settings.ShowNotes ? text : null;
                        itemPhotos = item.PhotoIds
                            .Where(id => subjectPhotos.ContainsKey(id) && placed.Add(id))
                            .Select(id => subjectPhotos[id]).ToList();
                        if (itemPhotos.Count == 0 && text is null)
                            continue;
                        kind = text is null ? ShareItemKind.Photos : ShareItemKind.Note;
                    }
                    foreach (var photo in itemPhotos)
                        photos[photo.Id] = photo;
                    any = true;
                    slots.Add(new Slot(item.Day, rank, -2, DateTimeOffset.MinValue.AddTicks(i), kind, text, itemPhotos,
                        Frozen: true, FrozenLabel: ShareLinkRules.Clean(item.DayCount)));
                }
                if (any)
                    shownHistories.Add(history);
            }

            // Milestones from dates come before everything else of the subject on their day, in a fixed order
            foreach (var mark in section.Marks.Where(m => Within(m.Day)))
                slots.Add(new Slot(mark.Day, rank, -1, DateTimeOffset.MinValue.AddTicks(mark.Rank), ShareItemKind.Milestone, mark.Text, []));
        }

        var items = slots
            .OrderBy(s => s.Day).ThenBy(s => s.Section).ThenBy(s => s.Order).ThenBy(s => s.At)
            .Select(s => new SharePageItem(
                s.Day,
                PhotoDayCount.Suffix(LabelOf(s)),
                s.Kind,
                s.Text,
                s.Photos.OrderBy(p => p.TakenAt).Select(p => new SharePhoto(p.Id, p.Frame, p.Width, p.Height)).ToList(),
                LabelOf(s)))
            .ToList();

        string? LabelOf(Slot s) => s.Frozen ? s.FrozenLabel : ages[s.Section](s.Day);

        var shown = photos.Values.Where(p => placed.Contains(p.Id)).ToList();
        // An explicit cover is only ever the subject's own photo, never one of the rooting it came from
        var cover = coverId is { } id && photos.TryGetValue(id, out var chosen) && chosen.SubjectId == subject.Id ? chosen : shown.MaxBy(p => p.TakenAt);
        var updates = shown.Select(p => p.UpdatedAt).Concat(shownEntries.Select(e => e.UpdatedAt)).Concat(shownHistories.Select(h => h.UpdatedAt)).ToList();

        return new SharePageModel(
            ShareLinkRules.Clean(settings.Name) ?? card.Name,
            card.Latin,
            card.Cultivar,
            ShareLinkRules.Clean(settings.Line) ?? card.Line,
            cover is null ? null : new SharePhoto(cover.Id, cover.Frame, cover.Width, cover.Height),
            updates.Count > 0 ? updates.Max() : subject.UpdatedAt,
            shown.Count,
            items.Count(i => i.Kind == ShareItemKind.Note),
            items,
            canKeep);
    }

    private static DateOnly Day(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    // Says what kind it was and nothing else: no name, no link
    private static string PropagatedText(TimelineEntry entry, IReadOnlyDictionary<Guid, Propagation> propagations)
    {
        if (entry.RelatedId is not { } id || !propagations.TryGetValue(id, out var propagation) || propagation.IsDeleted)
            return Strings.Propagations.PropagatedOther;
        return propagation.Type switch
        {
            PropagationType.Cutting => Strings.Propagations.PropagatedCutting,
            PropagationType.Corm => Strings.Propagations.PropagatedCorm,
            PropagationType.Offset => Strings.Propagations.PropagatedOffset,
            PropagationType.Seed => Strings.Propagations.PropagatedSeed,
            PropagationType.Division => Strings.Propagations.PropagatedDivision,
            PropagationType.AirLayer => Strings.Propagations.PropagatedAirLayer,
            PropagationType.Leaf => Strings.Propagations.PropagatedLeaf,
            _ => Strings.Propagations.PropagatedOther
        };
    }
}
