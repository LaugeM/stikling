using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Shelves;

/// <summary>A mark on a shelf photo, with the plant or propagation it points at.</summary>
/// <param name="Subject">The <see cref="Plant"/> or <see cref="Propagation"/>.</param>
/// <param name="Faded">True when the subject is no longer in the collection: it died, was given away or sold, or the propagation is finished.</param>
public sealed record ShownMark(PhotoMark Mark, Entity Subject, string Name, bool Faded)
{
    public SubjectType SubjectType => Subject is Propagation ? SubjectType.Propagation : SubjectType.Plant;
}

/// <summary>One shelf photo on a plant's or propagation's history.</summary>
/// <param name="PlaceName">The place the photo is of, or null when that place is gone.</param>
public sealed record ShelfHistoryItem(Photo Photo, double X, double Y, string? PlaceName = null);

/// <summary>A plant or propagation that can be marked on a photo.</summary>
public sealed record PickItem(Entity Subject, string Name)
{
    public SubjectType SubjectType => Subject is Propagation ? SubjectType.Propagation : SubjectType.Plant;
}

/// <summary>A shelf photo in a list, with how many plants are marked on it.</summary>
public sealed record ShelfTile(Photo Photo, int Marks);

/// <summary>The shelf photos of one place. <see cref="Place"/> is null for photos whose place was removed.</summary>
public sealed record ShelfGroup(Place? Place, string? Name, IReadOnlyList<ShelfTile> Tiles);

/// <summary>A shelf photo with its place and the marks that show on it.</summary>
public sealed record ShelfPhotoView(Photo Photo, Place? Place, string? PlaceName, IReadOnlyList<ShownMark> Marks);

/// <summary>
/// What can be worked out about shelf photos without a database. Marks, plants and photos are
/// separate records that can arrive in any order, so nothing here throws on one that points at
/// something that isn't there. It is left out instead.
/// </summary>
public static class ShelfMarks
{
    /// <summary>
    /// The marks to show on a photo, oldest first. The plants and propagations are what the
    /// repositories return, so a deleted one is absent and its mark is hidden. The mark stays
    /// stored, so it shows again if a restore brings the plant back.
    /// </summary>
    public static IReadOnlyList<ShownMark> Visible(
        IEnumerable<PhotoMark> marks, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        var plantsById = Index(plants.Where(p => !p.IsDeleted));
        var propagationsById = Index(propagations.Where(p => !p.IsDeleted));

        var shown = new List<ShownMark>();
        foreach (var mark in marks.Where(m => !m.IsDeleted).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
        {
            if (mark.SubjectType == SubjectType.Plant && plantsById.TryGetValue(mark.SubjectId, out var plant))
                shown.Add(new ShownMark(mark, plant, plant.DisplayName, Faded: !plant.InCollection));
            else if (mark.SubjectType == SubjectType.Propagation && propagationsById.TryGetValue(mark.SubjectId, out var propagation))
                shown.Add(new ShownMark(mark, propagation, propagation.ListName, Faded: !propagation.IsActive));
        }
        return shown;
    }

    /// <summary>
    /// One item for each photo this plant or propagation is marked on, newest photo first. When two
    /// devices marked it on the same photo, the mark made first is the one used. A mark whose photo
    /// isn't in <paramref name="photos"/> is left out.
    /// </summary>
    public static IReadOnlyList<ShelfHistoryItem> ForHistory(
        Guid subjectId, IEnumerable<PhotoMark> marks, IEnumerable<Photo> photos)
    {
        var photosById = Index(photos.Where(p => !p.IsDeleted && p.SubjectType == SubjectType.Place));

        return marks
            .Where(m => !m.IsDeleted && m.SubjectId == subjectId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .GroupBy(m => m.PhotoId)
            .Where(g => photosById.ContainsKey(g.Key))
            .Select(g => new ShelfHistoryItem(photosById[g.Key], g.First().X, g.First().Y))
            .OrderByDescending(i => i.Photo.TakenAt).ThenByDescending(i => i.Photo.Id)
            .ToList();
    }

    /// <summary>
    /// The plants and propagations to pick from when marking a spot on a photo of a place. Those in
    /// the place come first (a room includes its spots), then those marked on the newest earlier
    /// photo of it, then the rest. Each part is in name order. Whatever is already marked on this
    /// photo is left out, and every word of <paramref name="search"/> has to be in one of the names.
    /// </summary>
    public static IReadOnlyList<PickItem> PickList(
        Guid? placeId, Places places, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations,
        IReadOnlySet<Guid> lastPhotoSubjectIds, IReadOnlySet<Guid> alreadyMarked, string? search)
    {
        var words = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var items = new List<(PickItem Item, Guid? PlaceId, string Haystack)>();
        foreach (var plant in plants.Where(p => !p.IsDeleted && p.InCollection))
            items.Add((new PickItem(plant, plant.DisplayName), plant.PlaceId, string.Join(' ',
                [plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameNote, plant.BotanicalName, plant.DisplayName, plant.CollectionNumber])));
        foreach (var propagation in propagations.Where(p => !p.IsDeleted && p.IsActive))
            items.Add((new PickItem(propagation, propagation.ListName), propagation.PlaceId, string.Join(' ',
                [propagation.Nickname, propagation.Genus, propagation.Species, propagation.Cultivar, propagation.NameNote, propagation.BotanicalName, propagation.DisplayName, propagation.ListName])));

        int Rank(Guid id, Guid? itsPlace) =>
            placeId is not null && places.IsIn(itsPlace, placeId) ? 0
            : lastPhotoSubjectIds.Contains(id) ? 1
            : 2;

        return items
            .Where(i => !alreadyMarked.Contains(i.Item.Subject.Id))
            .Where(i => words.All(w => i.Haystack.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(i => Rank(i.Item.Subject.Id, i.PlaceId))
            .ThenBy(i => i.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.Item.Subject.Id)
            .Select(i => i.Item)
            .ToList();
    }

    /// <summary>
    /// Shelf photos grouped by the place they are of, following a place that was merged into another
    /// to where it went. Newest photo first in a group and the groups in order of the place's whole
    /// name. Photos of a place that was removed without being merged share one group with no place,
    /// last, so they stay visible.
    /// </summary>
    public static IReadOnlyList<ShelfGroup> Group(
        IEnumerable<Photo> photos, IReadOnlyDictionary<Guid, int> markCounts, Places places)
    {
        var groups = photos
            .Where(p => !p.IsDeleted && p.SubjectType == SubjectType.Place)
            .GroupBy(p => places.Find(p.SubjectId)?.Id)
            .Select(g => new ShelfGroup(
                g.Key is { } id ? places.Find(id) : null,
                g.Key is { } named ? places.NameOf(named) : null,
                g.OrderByDescending(p => p.TakenAt).ThenByDescending(p => p.Id)
                    .Select(p => new ShelfTile(p, markCounts.GetValueOrDefault(p.Id)))
                    .ToList()));

        return groups
            .OrderBy(g => g.Place is null)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Place?.Id)
            .ToList();
    }

    // Two records with one id can't both be in a store, but a list from a merge might repeat one
    private static Dictionary<Guid, T> Index<T>(IEnumerable<T> items) where T : Entity
    {
        var byId = new Dictionary<Guid, T>();
        foreach (var item in items)
            byId[item.Id] = item;
        return byId;
    }
}
