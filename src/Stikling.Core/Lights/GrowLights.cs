using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using static Stikling.Core.Timeline.HistoryText;

namespace Stikling.Core.Lights;

public static class GrowLights
{
    /// <summary>
    /// True when a light shines on the place the plant stands in: one on the spot itself, or one
    /// on the room around it. A place merged into another counts as the other, on both sides.
    /// </summary>
    public static bool IsLit(Plant plant, IEnumerable<GrowLight> lights, Places places) =>
        IsLit(plant.PlaceId, lights, places);

    public static bool IsLit(Guid? placeId, IEnumerable<GrowLight> lights, Places places) =>
        Lighting(placeId, lights, places).Count > 0;

    /// <summary>The lights that shine on the place: the ones on it, and for a spot the ones on its room.</summary>
    public static IReadOnlyList<GrowLight> Lighting(Guid? placeId, IEnumerable<GrowLight> lights, Places places)
    {
        if (places.Find(placeId) is not { } place)
            return [];

        var roomId = places.RoomOf(place.Id)?.Id;
        return [.. lights.Where(l =>
            !l.IsDeleted
            && places.Find(l.PlaceId) is { } lit
            && (lit.Id == place.Id || lit.Id == roomId))];
    }

    /// <summary>
    /// The lines a move between two places adds to the history when it changes whether the plant is under a
    /// grow light: "Now under a grow light: Shelf LED" or "No longer under a grow light". Nothing when it doesn't.
    /// </summary>
    public static IReadOnlyList<HistoryLine> MoveLines(Guid? from, Guid? to, IEnumerable<GrowLight> lights, Places places)
    {
        var all = lights as IReadOnlyList<GrowLight> ?? [.. lights];
        var before = Lighting(from, all, places);
        var after = Lighting(to, all, places);
        if (before.Count > 0 && after.Count == 0)
            return [Line("NoLongerUnderGrowLight")];
        if (before.Count == 0 && after.Count > 0)
            return [Line("NowUnderGrowLight", HistoryArg.Of(string.Join(", ", after.Select(l => l.Name))))];
        return [];
    }
}
