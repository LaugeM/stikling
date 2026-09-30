using Stikling.Core.Models;

namespace Stikling.Core.Rooms;

/// <summary>A spot inside a room, e.g. "On top of the PC" in the living room.</summary>
/// <param name="Name">The spot on its own, "On top of the PC".</param>
/// <param name="Place">The whole place, "Living room / On top of the PC".</param>
public sealed record RoomSpot(Guid Id, string Name, string Place, int Plants, int Propagations)
{
    public int Total => Plants + Propagations;
}

/// <summary>A room in use, and the spots inside it.</summary>
/// <param name="Plants">Plants placed in the room itself, not counting the spots.</param>
public sealed record Room(Guid Id, string Name, IReadOnlyList<RoomSpot> Spots, int Plants, int Propagations)
{
    /// <summary>Plants in the room itself and in every spot inside it.</summary>
    public int AllPlants => Plants + Spots.Sum(s => s.Plants);

    public int AllPropagations => Propagations + Spots.Sum(s => s.Propagations);

    public int Total => AllPlants + AllPropagations;

    /// <summary>
    /// Every room, sorted, with the spots inside it, counting what is in each. A room or spot with
    /// nothing in it is kept, since one can be made on the Rooms and tags page before anything is
    /// placed there. Screens that only want what is in use use <see cref="InUse"/>.
    /// </summary>
    public static IReadOnlyList<Room> List(Places places, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        var plantsIn = Tally(places, plants.Where(p => !p.IsDeleted).Select(p => p.PlaceId));
        var propagationsIn = Tally(places, propagations.Where(p => !p.IsDeleted).Select(p => p.PlaceId));

        int PlantsIn(Place place) => plantsIn.GetValueOrDefault(place.Id);
        int PropagationsIn(Place place) => propagationsIn.GetValueOrDefault(place.Id);

        return places.Rooms
            .Select(room => new Room(
                room.Id,
                room.Name,
                places.SpotsIn(room.Id)
                    .Select(s => new RoomSpot(s.Id, s.Name, places.NameOf(s.Id)!, PlantsIn(s), PropagationsIn(s)))
                    .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                PlantsIn(room),
                PropagationsIn(room)))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The rooms and spots with something in them, for filters where an empty one is no use.</summary>
    public static IReadOnlyList<Room> InUse(IEnumerable<Room> rooms) =>
        rooms
            .Select(r => r with { Spots = r.Spots.Where(s => s.Total > 0).ToList() })
            .Where(r => r.Total > 0)
            .ToList();

    // How many point at each place, counting what points at a merged place towards the one it went into
    private static Dictionary<Guid, int> Tally(Places places, IEnumerable<Guid?> placeIds) =>
        placeIds
            .Select(places.Find)
            .OfType<Place>()
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.Count());
}
