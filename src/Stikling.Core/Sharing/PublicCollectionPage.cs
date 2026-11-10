using System.Globalization;
using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Text;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Sharing;

/// <summary>What the owner has set the public collection page to show.</summary>
public sealed record PublicPageSettings(
    string? Title,
    string? Line,
    PublicPageLayout Layout,
    PublicPageOrder Order,
    bool ShowPropagations,
    bool ShowSwap,
    bool ShowWishlist,
    bool ShowStories);

/// <summary>The parts of the page.</summary>
public enum PublicPartKind
{
    Plants,
    Cuttings,
    Swap,
    Wishlist
}

/// <summary>A plant or a cutting on the page: its name, and its cover photo when it has one.</summary>
/// <param name="Latin">Genus and species, set in italic under the name. Null when the name already is the botanical name.</param>
/// <param name="Cultivar">The cultivar without its quotes.</param>
/// <param name="Caption">What goes under the name, like "3 · rooted" or "Up for swap".</param>
public sealed record PublicTile(
    Guid Id,
    SubjectType Type,
    string Name,
    string? Latin,
    string? Cultivar,
    SharePhoto? Cover,
    string? Caption = null);

/// <summary>One part of the front page.</summary>
/// <param name="Total">How many there are in all, also when the front page carries only some of them.</param>
public sealed record PublicPart(PublicPartKind Kind, int Total, IReadOnlyList<PublicTile> Tiles, IReadOnlyList<string> Lines);

/// <summary>Everything the front page says.</summary>
/// <param name="Updated">The newest change among what is shown.</param>
/// <param name="Image">The first plant's cover, for the preview of a link to the page.</param>
public sealed record PublicPageModel(
    string Title,
    string? Line,
    int PlantCount,
    int PropagationCount,
    IReadOnlyList<PublicPart> Parts,
    DateTimeOffset Updated,
    SharePhoto? Image);

/// <summary>A link to a part's page, for the link bar of the photo wall.</summary>
public sealed record PublicPartLink(PublicPartKind Kind, int Total);

/// <summary>One numbered page of one part.</summary>
public sealed record PublicPartPage(
    PublicPartKind Kind,
    int Total,
    int Page,
    int PageCount,
    IReadOnlyList<PublicTile> Tiles,
    IReadOnlyList<string> Lines);

/// <summary>A plant or a cutting that shows on the page, before its cover is looked for.</summary>
public sealed record PublicSubject(
    Guid Id,
    SubjectType Type,
    string Name,
    string? Latin,
    string? Cultivar,
    Guid? CoverPhotoId,
    DateTimeOffset UpdatedAt,
    string? Caption = null);

/// <summary>A line of the wishlist on the page.</summary>
public sealed record PublicLine(string Text, DateTimeOffset UpdatedAt);

/// <summary>
/// What shows on the page, in the order it is shown, with no photos yet. Parts that are off are empty.
/// </summary>
public sealed record PublicSubjects(
    PublicPageSettings Settings,
    IReadOnlyList<PublicSubject> Plants,
    IReadOnlyList<PublicSubject> Cuttings,
    IReadOnlyList<PublicSubject> Swap,
    IReadOnlyList<PublicLine> Wishlist)
{
    /// <summary>Plants always show. The others show when they are switched on.</summary>
    public bool IsOn(PublicPartKind kind) => kind switch
    {
        PublicPartKind.Plants => true,
        PublicPartKind.Cuttings => Settings.ShowPropagations,
        PublicPartKind.Swap => Settings.ShowSwap,
        _ => Settings.ShowWishlist
    };

    public int Total(PublicPartKind kind) => kind switch
    {
        PublicPartKind.Plants => Plants.Count,
        PublicPartKind.Cuttings => Cuttings.Count,
        PublicPartKind.Swap => Swap.Count,
        _ => Wishlist.Count
    };

    /// <summary>The plants and cuttings of a part, in order. The wishlist has none, only lines.</summary>
    public IReadOnlyList<PublicSubject> SubjectsOf(PublicPartKind kind) => kind switch
    {
        PublicPartKind.Plants => Plants,
        PublicPartKind.Cuttings => Cuttings,
        PublicPartKind.Swap => Swap,
        _ => []
    };
}

/// <summary>
/// Decides what the public collection page shows. The page is open to anyone with its address, so
/// everything is left out unless it is said here that it is shown. Rooms, prices, where a plant came
/// from, tags, pots, care, notes and plants that died or left are never on it, and a wish is only
/// its name. This is the one place that says it: the API finds the records and writes the HTML.
/// </summary>
public static class PublicCollectionPage
{
    // The name parts of a card don't depend on the day, so any day will do
    private static readonly DateOnly AnyDay = new(2000, 1, 1);

    /// <summary>A plant is on the page when it is here, active and not hidden. The example plants never are.</summary>
    public static bool ShowsPlant(Plant plant) => CouldShowPlant(plant) && !plant.HiddenFromPublicPage;

    /// <summary>A cutting is on the page when it is still going and not hidden. This ignores whether the Cuttings part is on.</summary>
    public static bool ShowsPropagation(Propagation propagation) => CouldShowPropagation(propagation) && !propagation.HiddenFromPublicPage;

    /// <summary>A photo is on the page when it is of the plant itself and not hidden. Whether its image is on the server is for the API to check.</summary>
    public static bool ShowsPhoto(Photo photo) => CouldShowPhoto(photo) && !photo.HiddenFromPublicPage;

    /// <summary>Whether the page could show the plant if it were not hidden. The app lists these for the owner to hide.</summary>
    public static bool CouldShowPlant(Plant plant) =>
        !plant.IsDeleted && plant.Status == PlantStatus.Active && !ExampleFamily.PlantIds.Contains(plant.Id);

    /// <summary>Whether the page could show the cutting if it were not hidden.</summary>
    public static bool CouldShowPropagation(Propagation propagation) =>
        !propagation.IsDeleted && propagation.IsActive && !ExampleFamily.PropagationIds.Contains(propagation.Id);

    /// <summary>Whether the page could show the photo if it were not hidden.</summary>
    public static bool CouldShowPhoto(Photo photo) => !photo.IsDeleted && !photo.IsReference;

    /// <summary>The title as it is shown: the cleaned one, or the app's own words.</summary>
    public static string TitleOf(string? title) => ShareLinkRules.Clean(title) ?? Strings.PublicPage.DefaultTitle;

    /// <summary>The part's name, like "Cuttings".</summary>
    public static string PartName(PublicPartKind kind) => kind switch
    {
        PublicPartKind.Plants => Strings.PublicPage.PartPlants,
        PublicPartKind.Cuttings => Strings.PublicPage.PartCuttings,
        PublicPartKind.Swap => Strings.PublicPage.PartSwap,
        _ => Strings.PublicPage.PartWishlist
    };

    /// <summary>"84 plants · 12 cuttings". The cuttings are only counted while that part is on.</summary>
    public static string CountsLine(PublicPageModel page)
    {
        var parts = new List<string> { Plural.Of(page.PlantCount, Strings.PublicPage.Plants_One, Strings.PublicPage.Plants_Other) };
        if (page.PropagationCount > 0)
            parts.Add(Plural.Of(page.PropagationCount, Strings.PublicPage.Cuttings_One, Strings.PublicPage.Cuttings_Other));
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Works out what shows, in the order it shows. Pass every plant, propagation and wish that isn't
    /// deleted, or all of them: the rules for what shows are applied here.
    /// </summary>
    public static PublicSubjects Subjects(
        PublicPageSettings settings, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, IEnumerable<Wish> wishes)
    {
        var shownPlants = plants.Where(ShowsPlant).ToList();
        var shownPropagations = propagations.Where(ShowsPropagation).ToList();

        var plantSubjects = Order(shownPlants.Select(p => (Subject: PlantSubject(p), p.CreatedAt)), settings.Order);
        var cuttingSubjects = settings.ShowPropagations
            ? Order(shownPropagations.Select(p => (Subject: PropagationSubject(p), p.CreatedAt)), settings.Order)
            : [];

        // The swap list keeps its own order: cuttings, then plants on request, then plants
        var swap = new List<PublicSubject>();
        if (settings.ShowSwap)
        {
            var list = SwapLists.Build(shownPlants, shownPropagations);
            var plantsById = shownPlants.ToDictionary(p => p.Id);
            var propagationsById = shownPropagations.ToDictionary(p => p.Id);
            foreach (var line in list.Cuttings)
            {
                if (line.SubjectId is { } id && propagationsById.TryGetValue(id, out var cutting))
                {
                    var stage = SwapLists.StageText(line.Stage ?? cutting.Stage);
                    swap.Add(PropagationSubject(cutting) with
                    {
                        Caption = string.Format(CultureInfo.CurrentCulture, Strings.PublicPage.SwapCuttingCaption, line.Count, stage)
                    });
                }
            }
            foreach (var (lines, mark) in new[] { (list.OnRequest, SpareMark.CuttingsToSpare), (list.Plants, SpareMark.UpForSwap) })
            {
                foreach (var line in lines)
                {
                    if (line.SubjectId is { } id && plantsById.TryGetValue(id, out var plant))
                        swap.Add(PlantSubject(plant) with { Caption = SwapLists.MarkText(mark) });
                }
            }
        }

        var wishlist = new List<PublicLine>();
        if (settings.ShowWishlist)
        {
            var open = wishes.Where(w => !w.IsDeleted && !w.IsGot).ToList();
            var ordered = settings.Order == PublicPageOrder.Name
                ? open.OrderBy(w => w.DisplayName, NameComparer).ThenBy(w => w.Id)
                : open.OrderByDescending(w => w.CreatedAt).ThenBy(w => w.Id);
            // Only the name. Never what it would cost, where to find it or the notes.
            wishlist.AddRange(ordered.Select(w => new PublicLine(w.DisplayName, w.UpdatedAt)));
        }

        return new PublicSubjects(settings, plantSubjects, cuttingSubjects, swap, wishlist);
    }

    private static StringComparer NameComparer => StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);

    private static IReadOnlyList<PublicSubject> Order(IEnumerable<(PublicSubject Subject, DateTimeOffset CreatedAt)> subjects, PublicPageOrder order) =>
        order == PublicPageOrder.Name
            ? [.. subjects.Select(s => s.Subject).OrderBy(s => s.Name, NameComparer).ThenBy(s => s.Id)]
            : [.. subjects.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Subject.Id).Select(s => s.Subject)];

    private static PublicSubject PlantSubject(Plant plant)
    {
        var card = ShareCardText.ForPlant(plant, null, null, [], AnyDay);
        return new PublicSubject(plant.Id, SubjectType.Plant, card.Name, card.Latin, card.Cultivar, plant.CoverPhotoId, plant.UpdatedAt);
    }

    private static PublicSubject PropagationSubject(Propagation propagation)
    {
        var card = ShareCardText.ForPropagation(propagation, null, AnyDay);
        return new PublicSubject(propagation.Id, SubjectType.Propagation, card.Name, card.Latin, card.Cultivar, propagation.CoverPhotoId, propagation.UpdatedAt);
    }

    /// <summary>The parts that are on and have something in them, with how many, in the order of the page. Plants always.</summary>
    public static IReadOnlyList<PublicPartLink> Links(PublicSubjects subjects) =>
    [
        .. Enum.GetValues<PublicPartKind>()
            .Where(k => subjects.IsOn(k) && (k == PublicPartKind.Plants || subjects.Total(k) > 0))
            .Select(k => new PublicPartLink(k, subjects.Total(k)))
    ];

    /// <summary>How many tiles of this part the front page carries in this layout.</summary>
    public static int FrontTileCount(PublicPageLayout layout, PublicPartKind kind) => layout switch
    {
        PublicPageLayout.Shelves => PublicPageRules.ShelfTiles,
        PublicPageLayout.LongPage => kind == PublicPartKind.Plants ? PublicPageRules.LongPageTiles : PublicPageRules.ShelfTiles,
        _ => kind == PublicPartKind.Plants ? PublicPageRules.TilesPerPage : 0
    };

    /// <summary>The ids of the plants and cuttings the front page draws, so the API reads only their photos.</summary>
    public static IReadOnlyCollection<Guid> FrontIds(PublicSubjects subjects)
    {
        var ids = new HashSet<Guid>();
        foreach (var kind in new[] { PublicPartKind.Plants, PublicPartKind.Cuttings, PublicPartKind.Swap })
        {
            if (subjects.IsOn(kind))
                ids.UnionWith(subjects.SubjectsOf(kind).Take(FrontTileCount(subjects.Settings.Layout, kind)).Select(s => s.Id));
        }
        return ids;
    }

    /// <summary>The ids of the plants and cuttings on one numbered page of a part.</summary>
    public static IReadOnlyCollection<Guid> PartIds(PublicSubjects subjects, PublicPartKind kind, int page) =>
        // A page past the end reads nothing, and a huge number can't wrap around to the first page
        subjects.IsOn(kind) && page >= 1 && (page == 1 || (long)(page - 1) * PublicPageRules.TilesPerPage < subjects.SubjectsOf(kind).Count)
            ? [.. subjects.SubjectsOf(kind).Skip((page - 1) * PublicPageRules.TilesPerPage).Take(PublicPageRules.TilesPerPage).Select(s => s.Id)]
            : [];

    /// <summary>
    /// The front page. <paramref name="photos"/> are the photos of the subjects in <see cref="FrontIds"/>
    /// that have an image on the server, deleted or hidden or not: the ones that don't show are skipped here.
    /// </summary>
    /// <param name="fallbackUpdated">What "updated" says when nothing is shown.</param>
    public static PublicPageModel Front(PublicSubjects subjects, IEnumerable<Photo> photos, DateTimeOffset fallbackUpdated)
    {
        var settings = subjects.Settings;
        var bySubject = photos.ToLookup(p => p.SubjectId);
        var changes = new List<DateTimeOffset>();
        var parts = new List<PublicPart>();

        foreach (var kind in Enum.GetValues<PublicPartKind>())
        {
            var total = subjects.Total(kind);
            if (!subjects.IsOn(kind) || (total == 0 && kind != PublicPartKind.Plants))
                continue;

            if (kind == PublicPartKind.Wishlist)
            {
                var lines = settings.Layout == PublicPageLayout.PhotoWall
                    ? []
                    : subjects.Wishlist.Take(PublicPageRules.ShelfTiles).ToList();
                changes.AddRange(lines.Select(l => l.UpdatedAt));
                parts.Add(new PublicPart(kind, total, [], [.. lines.Select(l => l.Text)]));
                continue;
            }

            var shown = subjects.SubjectsOf(kind).Take(FrontTileCount(settings.Layout, kind)).ToList();
            var tiles = shown.Select(s => TileOf(s, bySubject, changes)).ToList();
            parts.Add(new PublicPart(kind, total, tiles, []));
        }

        var first = parts.FirstOrDefault(p => p.Kind == PublicPartKind.Plants)?.Tiles.FirstOrDefault();
        return new PublicPageModel(
            TitleOf(settings.Title),
            ShareLinkRules.Clean(settings.Line),
            subjects.Plants.Count,
            subjects.IsOn(PublicPartKind.Cuttings) ? subjects.Cuttings.Count : 0,
            parts,
            changes.Count > 0 ? changes.Max() : fallbackUpdated,
            first?.Cover);
    }

    /// <summary>
    /// One numbered page of a part, counting from 1. Null for a page that isn't there and for a part
    /// that is off. Page 1 of a part with nothing in it is empty rather than missing.
    /// </summary>
    /// <param name="photos">The photos of the subjects in <see cref="PartIds"/> that have an image on the server.</param>
    public static PublicPartPage? Part(PublicSubjects subjects, PublicPartKind kind, int page, IEnumerable<Photo> photos)
    {
        if (!subjects.IsOn(kind))
            return null;

        var total = subjects.Total(kind);
        var perPage = kind == PublicPartKind.Wishlist ? PublicPageRules.LinesPerPage : PublicPageRules.TilesPerPage;
        var pageCount = Math.Max(1, (total + perPage - 1) / perPage);
        if (page < 1 || page > pageCount)
            return null;

        if (kind == PublicPartKind.Wishlist)
        {
            var lines = subjects.Wishlist.Skip((page - 1) * perPage).Take(perPage).Select(l => l.Text).ToList();
            return new PublicPartPage(kind, total, page, pageCount, [], lines);
        }

        var bySubject = photos.ToLookup(p => p.SubjectId);
        var tiles = subjects.SubjectsOf(kind).Skip((page - 1) * perPage).Take(perPage)
            .Select(s => TileOf(s, bySubject, null)).ToList();
        return new PublicPartPage(kind, total, page, pageCount, tiles, []);
    }

    private static PublicTile TileOf(PublicSubject subject, ILookup<Guid, Photo> bySubject, List<DateTimeOffset>? changes)
    {
        var cover = CoverOf(subject.CoverPhotoId, subject.Type, subject.Id, bySubject[subject.Id]);
        changes?.Add(subject.UpdatedAt);
        if (cover is not null)
            changes?.Add(cover.UpdatedAt);
        return new PublicTile(
            subject.Id,
            subject.Type,
            subject.Name,
            subject.Latin,
            subject.Cultivar,
            cover is null ? null : new SharePhoto(cover.Id, cover.Frame, cover.Width, cover.Height),
            subject.Caption);
    }

    /// <summary>
    /// The cover of a tile: the subject's chosen cover when that photo shows and is its own, else its
    /// newest own photo that shows, else none.
    /// </summary>
    /// <param name="photos">Photos of any subject that have an image on the server.</param>
    public static Photo? CoverOf(Guid? coverId, SubjectType type, Guid subjectId, IEnumerable<Photo> photos)
    {
        var own = photos.Where(p => p.SubjectType == type && p.SubjectId == subjectId && ShowsPhoto(p)).ToList();
        return own.FirstOrDefault(p => p.Id == coverId)
            ?? own.OrderByDescending(p => p.TakenAt).ThenBy(p => p.Id).FirstOrDefault();
    }

    /// <summary>
    /// The story of a plant, or null when stories are off or the plant doesn't show. This is the share
    /// page of the plant without the text of its notes, with what is hidden left out and no way to
    /// keep its history.
    /// </summary>
    /// <param name="parent">The plant it was propagated from.</param>
    /// <param name="from">The propagation it was potted up from.</param>
    /// <param name="taken">The propagations taken from it.</param>
    /// <param name="photos">Photos of the plant and of the propagation it came from, with an image on the server.</param>
    public static SharePageModel? PlantStory(
        PublicPageSettings settings, Plant plant, Plant? parent, Propagation? from, IEnumerable<Propagation> taken,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries, IEnumerable<ReceivedHistory> received,
        TimeZoneInfo zone, DateOnly today)
    {
        if (!settings.ShowStories || !ShowsPlant(plant))
            return null;

        var photoList = photos.ToList();
        var visibleFrom = from is { IsDeleted: false, HiddenFromPublicPage: false } ? from : null;
        var takenList = taken.ToList();
        var visibleTaken = takenList.Where(p => !p.IsDeleted && !p.HiddenFromPublicPage && !ExampleFamily.PropagationIds.Contains(p.Id)).ToList();
        // A hidden cutting leaves no trace: not in the count, and no Propagated milestone for it
        var hiddenTaken = takenList.Where(p => !visibleTaken.Contains(p)).Select(p => p.Id).ToHashSet();
        entries = entries.Where(e => !(e.Kind == TimelineKind.Propagated && e.RelatedId is { } related && hiddenTaken.Contains(related))).ToList();
        var propagations = visibleTaken.ToDictionary(p => p.Id);
        if (visibleFrom is not null)
            propagations[visibleFrom.Id] = visibleFrom;

        var model = SharePage.ForPlant(
            plant,
            parent is not null && ShowsPlant(parent) ? parent : null,
            visibleFrom,
            visibleTaken,
            photoList,
            entries,
            received,
            propagations,
            StorySettings(photoList),
            zone,
            today);
        return model with { CanKeep = false };
    }

    /// <summary>The story of a cutting, or null when stories or cuttings are off or it doesn't show. See <see cref="PlantStory"/>.</summary>
    public static SharePageModel? PropagationStory(
        PublicPageSettings settings, Propagation propagation, Plant? parent,
        IEnumerable<Photo> photos, IEnumerable<TimelineEntry> entries, IEnumerable<ReceivedHistory> received,
        TimeZoneInfo zone, DateOnly today)
    {
        if (!settings.ShowStories || !settings.ShowPropagations || !ShowsPropagation(propagation))
            return null;

        var photoList = photos.ToList();
        var model = SharePage.ForPropagation(
            propagation,
            parent is not null && ShowsPlant(parent) ? parent : null,
            photoList,
            entries,
            received,
            StorySettings(photoList),
            zone,
            today);
        return model with { CanKeep = false };
    }

    // Notes show as photos only, and the photos the owner hid are left out wherever they would come up
    private static ShareLinkSettings StorySettings(IEnumerable<Photo> photos) =>
        new(ShowNotes: false, Name: null, Line: null, LeftOutPhotoIds: [.. photos.Where(p => p.HiddenFromPublicPage).Select(p => p.Id)]);

    /// <summary>
    /// Whether the photo endpoint may serve a photo: only when it is the cover of a tile that shows,
    /// or, with stories on, when it is on the story of a plant or cutting that shows.
    /// </summary>
    /// <param name="photo">The photo asked for.</param>
    /// <param name="plant">The plant the photo is of, when it is of a plant.</param>
    /// <param name="propagation">The propagation the photo is of, when it is of a propagation.</param>
    /// <param name="subjectPhotos">All the photos of that plant or propagation that have an image on the server.</param>
    /// <param name="pottedUp">The plants that were potted up from that propagation. Only read for a photo of a propagation.</param>
    public static bool ServesPhoto(
        PublicPageSettings settings, Photo photo, Plant? plant, Propagation? propagation,
        IReadOnlyCollection<Photo> subjectPhotos, IReadOnlyCollection<Plant> pottedUp, TimeZoneInfo zone)
    {
        if (!ShowsPhoto(photo))
            return false;

        if (photo.SubjectType == SubjectType.Plant)
        {
            if (plant is null || plant.Id != photo.SubjectId || !ShowsPlant(plant))
                return false;
            // A plant's tile is always on the page, and its story is when stories are on
            return settings.ShowStories || CoverOf(plant.CoverPhotoId, SubjectType.Plant, plant.Id, subjectPhotos)?.Id == photo.Id;
        }

        if (photo.SubjectType != SubjectType.Propagation || propagation is null || propagation.Id != photo.SubjectId)
            return false;

        if (ShowsPropagation(propagation))
        {
            // Its tile is on the page under Cuttings, or under Up for swap while it has some to spare
            var tile = settings.ShowPropagations || (settings.ShowSwap && propagation.SpareAvailable > 0);
            if (tile && CoverOf(propagation.CoverPhotoId, SubjectType.Propagation, propagation.Id, subjectPhotos)?.Id == photo.Id)
                return true;
            if (settings.ShowStories && settings.ShowPropagations)
                return true;
        }

        // The rooting a shown plant was potted up from is the start of that plant's story
        if (settings.ShowStories && propagation is { IsDeleted: false, HiddenFromPublicPage: false })
        {
            foreach (var potted in pottedUp)
            {
                if (potted.FromPropagationId == propagation.Id && ShowsPlant(potted)
                    && SharePage.RootingPhotos(potted, propagation, [photo], zone).Count > 0)
                    return true;
            }
        }

        return false;
    }
}
