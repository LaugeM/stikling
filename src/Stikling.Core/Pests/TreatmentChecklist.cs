using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Pests;

/// <summary>One plant on the checklist of a treatment, and whether it has had it.</summary>
public sealed record ChecklistRow(Plant Plant, bool Ticked);

/// <summary>
/// The plants of one room on the checklist. <paramref name="FoundOn"/> marks the group of plants
/// the pest was found on, which comes first and has no room name.
/// </summary>
public sealed record ChecklistGroup(string? Name, bool FoundOn, IReadOnlyList<ChecklistRow> Rows);

/// <summary>
/// Ticking off the plants of a case as they are treated. A treatment with no list covered the
/// whole case. A list, even an empty one, holds the plants that have had it so far. Ids of plants
/// that are gone or left the case stay in the list and are just never shown or counted.
/// </summary>
public static class TreatmentChecklist
{
    /// <summary>
    /// The plants the case covers, the ones the pest was found on first, then by room in the order
    /// of the rooms (plants with no room last), each by name.
    /// </summary>
    public static IReadOnlyList<ChecklistGroup> Rows(
        PestCase item, PestTreatment treatment, IEnumerable<Plant> plants, Places places)
    {
        var ticked = treatment.TreatedPlantIds?.ToHashSet() ?? [];
        var covered = PestService.PlantsIn(item, plants, places);
        ChecklistRow RowOf(Plant p) => new(p, ticked.Contains(p.Id));

        var groups = new List<ChecklistGroup>();

        var found = covered.Where(p => item.FoundOnPlantIds.Contains(p.Id)).ToList();
        if (found.Count > 0)
            groups.Add(new ChecklistGroup(null, true, [.. found.Select(RowOf)]));

        var rest = covered.Where(p => !item.FoundOnPlantIds.Contains(p.Id)).ToList();
        var roomIds = places.Rooms.Select(r => r.Id).ToHashSet();
        foreach (var room in places.Rooms)
        {
            var inRoom = rest.Where(p => places.RoomOf(p.PlaceId)?.Id == room.Id).ToList();
            if (inRoom.Count > 0)
                groups.Add(new ChecklistGroup(room.Name, false, [.. inRoom.Select(RowOf)]));
        }

        var elsewhere = rest.Where(p => places.RoomOf(p.PlaceId) is not { } r || !roomIds.Contains(r.Id)).ToList();
        if (elsewhere.Count > 0)
            groups.Add(new ChecklistGroup(WateringPass.NoRoomName, false, [.. elsewhere.Select(RowOf)]));

        return groups;
    }

    /// <summary>Ticks the plant, or takes the tick off. A treatment with no list gets one.</summary>
    public static void Toggle(PestTreatment treatment, Guid plantId)
    {
        var list = treatment.TreatedPlantIds ?? [];
        if (!list.Remove(plantId))
            list.Add(plantId);
        treatment.TreatedPlantIds = list;
    }

    /// <summary>The plants shown that are ticked, and how many are shown.</summary>
    public static (int Ticked, int Total) Progress(IReadOnlyList<ChecklistGroup> groups) =>
        (groups.Sum(g => g.Rows.Count(r => r.Ticked)), groups.Sum(g => g.Rows.Count));

    /// <summary>
    /// Progress from the plants the case covers (<see cref="PestService.PlantsIn"/>), for the
    /// history on the case page.
    /// </summary>
    public static (int Ticked, int Total) Progress(PestTreatment treatment, IReadOnlyList<Plant> covered)
    {
        var ticked = treatment.TreatedPlantIds?.ToHashSet() ?? [];
        return (covered.Count(p => ticked.Contains(p.Id)), covered.Count);
    }
}
