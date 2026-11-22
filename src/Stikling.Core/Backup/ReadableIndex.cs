using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Backup;

/// <summary>Plants in the index with the room they are listed under. <see cref="Room"/> is null for no room, and <see cref="Left"/> for plants that are gone.</summary>
public sealed record ReadablePlantGroup(string? Room, bool Left, IReadOnlyList<Plant> Plants);

/// <summary>Cuttings in the index with the stage they are listed under.</summary>
public sealed record ReadableCuttingGroup(PropagationStage Stage, IReadOnlyList<Propagation> Cuttings);

/// <summary>
/// The order the readable pages list the collection in: plants by room and then by name, and
/// propagations by stage. The backup's parts follow the same order, so a part holds neighbours.
/// Deleted records are left out.
/// </summary>
public static class ReadableIndex
{
    public static IReadOnlyList<ReadablePlantGroup> Plants(BackupData data, Places places, CultureInfo culture)
    {
        var comparer = StringComparer.Create(culture, ignoreCase: true);
        var living = data.Plants.Where(p => !p.IsDeleted).ToList();

        var groups = new List<ReadablePlantGroup>();
        var here = living.Where(p => p.Status == PlantStatus.Active).ToList();
        var roomOf = here.ToDictionary(p => p.Id, p => places.RoomOf(p.PlaceId));

        foreach (var room in places.CurrentRooms.Concat(places.RoomsFromBeforeMove))
        {
            var inRoom = here.Where(p => roomOf[p.Id]?.Id == room.Id).ToList();
            if (inRoom.Count > 0)
                groups.Add(new ReadablePlantGroup(room.Name, false, Sorted(inRoom, comparer)));
        }

        var noRoom = here.Where(p => roomOf[p.Id] is null).ToList();
        if (noRoom.Count > 0)
            groups.Add(new ReadablePlantGroup(null, false, Sorted(noRoom, comparer)));

        var gone = living.Where(p => p.Status != PlantStatus.Active).ToList();
        if (gone.Count > 0)
            groups.Add(new ReadablePlantGroup(null, true, Sorted(gone, comparer)));

        return groups;
    }

    public static IReadOnlyList<ReadableCuttingGroup> Cuttings(BackupData data, CultureInfo culture)
    {
        var comparer = StringComparer.Create(culture, ignoreCase: true);
        return data.Propagations
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.Stage)
            .OrderBy(g => g.Key)
            .Select(g => new ReadableCuttingGroup(g.Key, g
                .OrderBy(p => p.DisplayName, comparer).ThenBy(p => p.StartedOn).ThenBy(p => p.Id).ToList()))
            .ToList();
    }

    private static List<Plant> Sorted(IEnumerable<Plant> plants, StringComparer comparer) =>
        [.. plants.OrderBy(p => p.DisplayName, comparer).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id)];

    /// <summary>
    /// The photo each plant and propagation shows in the index: the chosen cover when it is still there,
    /// else the newest photo that isn't a reference photo, else the newest of any.
    /// </summary>
    public static IReadOnlyDictionary<Guid, Guid> CoverPhotoIds(BackupData data)
    {
        var live = data.Photos.Where(p => !p.IsDeleted).ToList();
        var bySubject = live
            .Where(p => p.SubjectType is SubjectType.Plant or SubjectType.Propagation)
            .GroupBy(p => (p.SubjectType, p.SubjectId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<Guid, Guid>();

        void Add(SubjectType type, Guid id, Guid? chosen)
        {
            if (!bySubject.TryGetValue((type, id), out var photos))
                return;
            var cover = photos.FirstOrDefault(p => p.Id == chosen && !p.IsReference)
                ?? photos.Where(p => !p.IsReference).OrderByDescending(p => p.TakenAt).ThenBy(p => p.Id).FirstOrDefault()
                ?? photos.OrderByDescending(p => p.TakenAt).ThenBy(p => p.Id).First();
            result[id] = cover.Id;
        }

        foreach (var plant in data.Plants.Where(p => !p.IsDeleted))
            Add(SubjectType.Plant, plant.Id, plant.CoverPhotoId);
        foreach (var cutting in data.Propagations.Where(p => !p.IsDeleted))
            Add(SubjectType.Propagation, cutting.Id, cutting.CoverPhotoId);
        return result;
    }
}
