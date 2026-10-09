using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Lights;

public static class GrowLights
{
    /// <summary>
    /// True when a light shines on the place the plant stands in: one on the spot itself, or one
    /// on the room around it. A place merged into another counts as the other, on both sides.
    /// </summary>
    public static bool IsLit(Plant plant, IEnumerable<GrowLight> lights, Places places)
    {
        if (places.Find(plant.PlaceId) is not { } place)
            return false;

        var roomId = places.RoomOf(place.Id)?.Id;
        return lights.Any(l =>
            !l.IsDeleted
            && places.Find(l.PlaceId) is { } lit
            && (lit.Id == place.Id || lit.Id == roomId));
    }
}
