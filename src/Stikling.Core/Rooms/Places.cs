using Stikling.Core.Models;

namespace Stikling.Core.Rooms;

/// <summary>
/// Every room and spot, for turning the place something points at into a name and for telling
/// whether it's in a room. Places merged into another are kept, and following one leads to the
/// place it went into.
/// </summary>
public sealed class Places
{
    private readonly Dictionary<Guid, Place> byId;

    public Places(IEnumerable<Place> places)
    {
        byId = [];
        foreach (var place in places)
            byId[place.Id] = place;
    }

    public static Places None { get; } = new([]);

    /// <summary>Every room that isn't deleted, oldest first, so the first of two with the same name stays first.</summary>
    public IEnumerable<Place> Rooms => Live.Where(p => !p.IsSpot);

    /// <summary>The spots inside a room, including the ones that came with a room merged into it.</summary>
    public IEnumerable<Place> SpotsIn(Guid roomId) =>
        Live.Where(p => p.IsSpot && Find(p.RoomId)?.Id == roomId);

    // The places nothing else stands in for, which includes the one kept from a merge loop
    private IEnumerable<Place> Live =>
        byId.Values.Where(p => Find(p.Id) == p).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id);

    /// <summary>
    /// The place with this id, or the one it was merged into. Null for no id, an unknown one,
    /// and a place that was deleted without being merged.
    /// </summary>
    /// <remarks>
    /// Two devices can merge the same two places in opposite directions, so after a sync each is
    /// deleted and points at the other. The oldest place in such a loop is the one kept, which
    /// every device works out the same way.
    /// </remarks>
    public Place? Find(Guid? id)
    {
        var path = new List<Place>();
        while (id is { } current)
        {
            if (!byId.TryGetValue(current, out var place))
                return null;
            if (!place.IsDeleted)
                return place;

            var seen = path.IndexOf(place);
            if (seen >= 0)
                return path[seen..].OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).First();

            path.Add(place);
            id = place.MergedIntoId;
        }
        return null;
    }

    /// <summary>The room a place is in: the place itself for a room, the room around it for a spot.</summary>
    public Place? RoomOf(Guid? id) => Find(id) is { } place && place.IsSpot ? Find(place.RoomId) : Find(id);

    /// <summary>"Living room", or "Living room / On top of the PC" for a spot. Null when there's no place.</summary>
    public string? NameOf(Guid? id) =>
        Find(id) is not { } place ? null
        : place.IsSpot ? RoomName.Combine(Find(place.RoomId)?.Name, place.Name)
        : place.Name;

    /// <summary>True when the place is the given one, or a spot inside it.</summary>
    public bool IsIn(Guid? placeId, Guid? scopeId)
    {
        if (Find(placeId) is not { } place || Find(scopeId) is not { } scope)
            return false;

        return place.Id == scope.Id || (place.IsSpot && Find(place.RoomId)?.Id == scope.Id);
    }

    /// <summary>
    /// Every id that counts as being in the place: the place, the spots inside it when it's a room,
    /// and whatever was merged into those.
    /// </summary>
    public IReadOnlySet<Guid> IdsIn(Guid? id) => byId.Keys.Where(k => IsIn(k, id)).ToHashSet();

    /// <summary>A room by its name, ignoring case and spacing. The oldest wins if two share it.</summary>
    public Place? RoomNamed(string? name) => Rooms.FirstOrDefault(r => RoomName.Same(r.Name, name));

    /// <summary>A spot inside the room by its name.</summary>
    public Place? SpotNamed(Guid roomId, string? name) => SpotsIn(roomId).FirstOrDefault(s => RoomName.Same(s.Name, name));

    /// <summary>A place written out as "Living room" or "Living room / On top of the PC", if it exists.</summary>
    public Place? Named(string? place)
    {
        var (roomName, spotName) = RoomName.Split(place);
        if (RoomNamed(roomName) is not { } room)
            return null;
        return spotName is null ? room : SpotNamed(room.Id, spotName);
    }
}
