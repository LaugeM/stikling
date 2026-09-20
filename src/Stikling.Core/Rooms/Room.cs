using Stikling.Core.Models;

namespace Stikling.Core.Rooms;

/// <summary>A spot inside a room, e.g. "On top of the PC" in the living room.</summary>
/// <param name="Name">The spot on its own, "On top of the PC".</param>
/// <param name="Place">The whole place, "Living room / On top of the PC".</param>
public sealed record RoomSpot(string Name, string Place, int Plants, int Propagations)
{
    public int Total => Plants + Propagations;
}

/// <summary>A room in use, and the spots inside it.</summary>
/// <param name="Plants">Plants placed in the room itself, not counting the spots.</param>
public sealed record Room(string Name, IReadOnlyList<RoomSpot> Spots, int Plants, int Propagations)
{
    /// <summary>Plants in the room itself and in every spot inside it.</summary>
    public int AllPlants => Plants + Spots.Sum(s => s.Plants);

    public int AllPropagations => Propagations + Spots.Sum(s => s.Propagations);

    public int Total => AllPlants + AllPropagations;

    /// <summary>The rooms in use by these plants and propagations, sorted, with their spots.</summary>
    public static IReadOnlyList<Room> List(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations) =>
        List(plants.Where(p => !p.IsDeleted).Select(p => p.Location),
             propagations.Where(p => !p.IsDeleted).Select(p => p.Location));

    /// <summary>The same from bare place names, e.g. when the plants have already been filtered.</summary>
    public static IReadOnlyList<Room> List(IEnumerable<string?> plantPlaces, IEnumerable<string?> propagationPlaces)
    {
        var places = new Dictionary<string, Tally>(StringComparer.OrdinalIgnoreCase);

        foreach (var place in plantPlaces)
            Count(places, place, plants: 1, propagations: 0);
        foreach (var place in propagationPlaces)
            Count(places, place, plants: 0, propagations: 1);

        // A room stays on the list even when everything in it sits in one of its spots
        foreach (var room in places.Values.Select(t => RoomName.RoomOf(t.Place)).OfType<string>().ToList())
            Count(places, room, plants: 0, propagations: 0);

        return places.Values
            .Where(t => RoomName.SpotOf(t.Place) is null)
            .Select(room => new Room(room.Place, SpotsOf(places, room.Place), room.Plants, room.Propagations))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<RoomSpot> SpotsOf(Dictionary<string, Tally> places, string room) =>
        places.Values
            .Where(t => RoomName.SpotOf(t.Place) is not null && RoomName.Same(RoomName.RoomOf(t.Place), room))
            .Select(t => new RoomSpot(RoomName.SpotOf(t.Place)!, t.Place, t.Plants, t.Propagations))
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static void Count(Dictionary<string, Tally> places, string? place, int plants, int propagations)
    {
        if (RoomName.Clean(place) is not { } cleaned)
            return;

        // The first spelling seen wins, so "Kitchen" and "kitchen" stay one place
        if (!places.TryGetValue(cleaned, out var tally))
            places[cleaned] = tally = new Tally(cleaned);

        tally.Plants += plants;
        tally.Propagations += propagations;
    }

    private sealed class Tally(string place)
    {
        public string Place { get; } = place;
        public int Plants { get; set; }
        public int Propagations { get; set; }
    }
}
