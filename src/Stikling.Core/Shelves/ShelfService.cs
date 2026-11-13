using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Shelves;

/// <summary>
/// What a page does with shelf photos: photos of a room or spot with a dot on each plant. The photos
/// are ordinary photos of the place, and the dots are <see cref="PhotoMark"/>s. Deleting a plant
/// doesn't touch its marks, and marking a plant never changes where it is.
/// </summary>
public sealed class ShelfService(
    IPhotoMarkRepository marks, IPhotoRepository photos, IPlantRepository plants,
    IPropagationRepository propagations, IPlaceRepository places)
{
    /// <summary>The shelf photos grouped by place, with how many plants are marked on each.</summary>
    public async Task<IReadOnlyList<ShelfGroup>> GetGroupsAsync()
    {
        var all = await photos.GetOfKindAsync(SubjectType.Place);
        var shown = ShelfMarks.Visible(await marks.GetAllAsync(), await plants.GetAllAsync(), await propagations.GetAllAsync());
        var counts = shown.GroupBy(m => m.Mark.PhotoId).ToDictionary(g => g.Key, g => g.Count());
        return ShelfMarks.Group(all, counts, await places.GetPlacesAsync());
    }

    /// <summary>A shelf photo with the marks that show on it, or null when it isn't there or isn't a shelf photo.</summary>
    public async Task<ShelfPhotoView?> GetPhotoAsync(Guid photoId)
    {
        if (await photos.GetAsync(photoId) is not { SubjectType: SubjectType.Place } photo)
            return null;

        var all = await places.GetPlacesAsync();
        var shown = ShelfMarks.Visible(await marks.GetForPhotoAsync(photoId), await plants.GetAllAsync(), await propagations.GetAllAsync());
        return new ShelfPhotoView(photo, all.Find(photo.SubjectId), all.NameOf(photo.SubjectId), shown);
    }

    /// <summary>
    /// What can be marked on a photo: the plants and propagations in the place first, then those on
    /// the newest earlier photo of it, then the rest.
    /// </summary>
    public async Task<IReadOnlyList<PickItem>> PickListAsync(ShelfPhotoView view, string? search)
    {
        var all = await places.GetPlacesAsync();
        var placeId = all.Find(view.Photo.SubjectId)?.Id;

        // The newest photo of the same place taken before this one
        var earlier = (await photos.GetOfKindAsync(SubjectType.Place))
            .Where(p => p.Id != view.Photo.Id && p.TakenAt < view.Photo.TakenAt && all.Find(p.SubjectId)?.Id == placeId && placeId is not null)
            .OrderByDescending(p => p.TakenAt)
            .FirstOrDefault();

        var allPlants = await plants.GetAllAsync();
        var allPropagations = await propagations.GetAllAsync();
        var last = earlier is null
            ? new HashSet<Guid>()
            : ShelfMarks.Visible(await marks.GetForPhotoAsync(earlier.Id), allPlants, allPropagations).Select(m => m.Subject.Id).ToHashSet();
        var already = view.Marks.Select(m => m.Subject.Id).ToHashSet();

        return ShelfMarks.PickList(placeId, all, allPlants, allPropagations, last, already, search);
    }

    /// <summary>
    /// Puts a dot on a photo. Where it is is kept inside the photo. Throws when the photo or the
    /// plant or propagation isn't there.
    /// </summary>
    public async Task<PhotoMark> MarkAsync(Guid photoId, SubjectType subjectType, Guid subjectId, double x, double y)
    {
        if (await photos.GetAsync(photoId) is not { SubjectType: SubjectType.Place })
            throw new ArgumentException("The photo is gone.", nameof(photoId));

        var exists = subjectType switch
        {
            SubjectType.Plant => await plants.GetAsync(subjectId) is not null,
            SubjectType.Propagation => await propagations.GetAsync(subjectId) is not null,
            _ => throw new ArgumentException("Only a plant or a propagation can be marked.", nameof(subjectType))
        };
        if (!exists)
            throw new ArgumentException("The plant is gone.", nameof(subjectId));

        var mark = new PhotoMark
        {
            PhotoId = photoId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            X = PhotoMark.Tidy(x),
            Y = PhotoMark.Tidy(y)
        };
        await marks.SaveAsync(mark);
        return mark;
    }

    public Task RemoveMarkAsync(Guid markId) => marks.DeleteAsync(markId);

    /// <summary>Removes every mark on a photo, for when the photo is deleted. The plants aren't touched.</summary>
    public async Task RemoveMarksOfPhotoAsync(Guid photoId)
    {
        foreach (var mark in await marks.GetForPhotoAsync(photoId))
            await marks.DeleteAsync(mark.Id);
    }

    /// <summary>The shelf photos a plant or propagation is marked in, newest first, for its history.</summary>
    public async Task<IReadOnlyList<ShelfHistoryItem>> HistoryForAsync(Guid subjectId)
    {
        var own = await marks.GetForSubjectAsync(subjectId);
        if (own.Count == 0)
            return [];

        var items = ShelfMarks.ForHistory(subjectId, own, await photos.GetOfKindAsync(SubjectType.Place));
        if (items.Count == 0)
            return items;

        var all = await places.GetPlacesAsync();
        return [.. items.Select(i => i with { PlaceName = all.NameOf(i.Photo.SubjectId) })];
    }

    /// <summary>How many shelf photos are of the place, counting its spots when it is a room.</summary>
    public async Task<int> CountForPlaceAsync(Guid placeId)
    {
        var all = await places.GetPlacesAsync();
        return (await photos.GetOfKindAsync(SubjectType.Place)).Count(p => all.IsIn(p.SubjectId, placeId));
    }

    /// <summary>The shelf photos of the place, counting its spots when it is a room, newest first.</summary>
    public async Task<IReadOnlyList<Photo>> GetForPlaceAsync(Guid placeId)
    {
        var all = await places.GetPlacesAsync();
        return [.. (await photos.GetOfKindAsync(SubjectType.Place))
            .Where(p => all.IsIn(p.SubjectId, placeId))
            .OrderByDescending(p => p.TakenAt)];
    }

    /// <summary>
    /// The place each shelf photo is of, one id for each photo, as stored. For a page that counts the
    /// photos of several places with <see cref="Places.IsIn"/> without reading the photos again.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> PhotoPlaceIdsAsync() =>
        [.. (await photos.GetOfKindAsync(SubjectType.Place)).Select(p => p.SubjectId)];

    /// <summary>How many shelf photos there are in all.</summary>
    public async Task<int> CountAsync() => (await photos.GetOfKindAsync(SubjectType.Place)).Count;
}
