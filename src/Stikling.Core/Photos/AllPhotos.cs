using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Photos;

/// <summary>One photo on the All photos page, with whose it is.</summary>
public sealed record PhotoTile(Photo Photo, SubjectType Kind, Guid SubjectId, string Name);

/// <summary>The photos taken in one month, newest first.</summary>
public sealed record PhotoMonth(int Year, int Month, IReadOnlyList<PhotoTile> Photos);

/// <summary>
/// What the All photos page is narrowed to. A plant shows only its own photos, not its cuttings'.
/// A room includes its spots. A tag is on plants only, so cuttings are left out under one.
/// </summary>
public sealed record AllPhotosFilter(Guid? PlantId = null, Guid? RoomId = null, string? Tag = null)
{
    public bool IsEmpty => PlantId is null && RoomId is null && PlantTags.Clean(Tag) is null;
}

/// <summary>
/// Every photo of the plants and cuttings, by month, newest first. Worked out from what is stored, nothing is kept.
/// </summary>
public static class AllPhotos
{
    /// <summary>
    /// The tiles for plants and cuttings that still exist. Reference photos, deleted photos and photos of
    /// anything else are left out. Months follow the given zone, the same one the history uses for dates.
    /// </summary>
    public static IReadOnlyList<PhotoTile> Tiles(
        IEnumerable<Photo> photos, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations,
        Places places, AllPhotosFilter? filter = null)
    {
        filter ??= new AllPhotosFilter();
        var tag = PlantTags.Clean(filter.Tag);
        var plantsById = plants.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        var propagationsById = propagations.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);
        var tiles = new List<PhotoTile>();

        foreach (var photo in photos)
        {
            if (photo.IsDeleted || photo.IsReference)
                continue;

            if (photo.SubjectType == SubjectType.Plant && plantsById.GetValueOrDefault(photo.SubjectId) is { } plant)
            {
                if (filter.PlantId is { } id && plant.Id != id)
                    continue;
                if (filter.RoomId is { } room && !places.IsIn(plant.PlaceId, room))
                    continue;
                if (tag is not null && !PlantTags.Has(plant, tag))
                    continue;

                tiles.Add(new PhotoTile(photo, SubjectType.Plant, plant.Id, plant.DisplayName));
            }
            else if (photo.SubjectType == SubjectType.Propagation && propagationsById.GetValueOrDefault(photo.SubjectId) is { } propagation)
            {
                if (filter.PlantId is not null || tag is not null)
                    continue;
                if (filter.RoomId is { } room && !places.IsIn(propagation.PlaceId, room))
                    continue;

                tiles.Add(new PhotoTile(photo, SubjectType.Propagation, propagation.Id, propagation.DisplayName));
            }
        }

        return tiles;
    }

    /// <summary>The tiles grouped by the month they were taken in the given zone, newest first.</summary>
    public static IReadOnlyList<PhotoMonth> Months(
        IEnumerable<Photo> photos, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations,
        Places places, TimeProvider time, AllPhotosFilter? filter = null) =>
        Group(Tiles(photos, plants, propagations, places, filter), time.LocalTimeZone);

    public static IReadOnlyList<PhotoMonth> Group(IEnumerable<PhotoTile> tiles, TimeZoneInfo zone) =>
        [.. tiles
            .Select(t => (Tile: t, Local: TimeZoneInfo.ConvertTime(t.Photo.TakenAt, zone)))
            .GroupBy(x => (x.Local.Year, x.Local.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g => new PhotoMonth(g.Key.Year, g.Key.Month,
                [.. g.OrderByDescending(x => x.Tile.Photo.TakenAt).Select(x => x.Tile)]))];
}
