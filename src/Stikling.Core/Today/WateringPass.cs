using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Today;

/// <summary>A room with at least one active plant, for the room picker.</summary>
/// <param name="Id">The room's id, or <see cref="WateringPass.NoRoom"/> for plants with no room.</param>
public sealed record PassRoom(Guid Id, string Name);

/// <summary>One plant on the watering screen, with what is logged today on it.</summary>
/// <param name="Form">How the plant gets its water. Reservoir and in-water plants are topped up, not read.</param>
/// <param name="Reading">Today's meter reading, when there is one.</param>
/// <param name="WaterAt">The reading it is watered at: the plant's own, or the usual 3.</param>
/// <param name="DoneTodayId">The watering, or topping up, logged today.</param>
/// <param name="Dry">Top watered, read today, and the reading is at or below <paramref name="WaterAt"/>.</param>
/// <param name="Ticked">Whether it starts ticked on the second step: dry by its reading and not done today.</param>
/// <param name="DefaultFeed">The feed the plant gets with its watering, when it is due on this one. Null when not due or already done today.</param>
public sealed record PassRow(
    Plant Plant, WateringForm Form, RoundReading? Reading, int WaterAt, Guid? DoneTodayId,
    bool Dry, bool Ticked, Feed? DefaultFeed);

/// <summary>The plants of one room on the watering screen, in the order they are walked.</summary>
public sealed record PassGroup(Guid RoomId, string Name, IReadOnlyList<PassRow> Rows);

/// <summary>
/// Watering many plants in one pass: pick rooms, read every plant with the meter, then tick what
/// was watered. Everything here is worked out from what is logged, so leaving and coming back the
/// same day picks up where it was.
/// </summary>
public static class WateringPass
{
    /// <summary>The pseudo-room for active plants that are in no room.</summary>
    public static readonly Guid NoRoom = Guid.Empty;

    /// <summary>The name shown for <see cref="NoRoom"/>.</summary>
    public const string NoRoomName = "No room";

    /// <summary>
    /// The rooms that have at least one active plant, in the order of the rooms, and <see cref="NoRoom"/>
    /// last when a plant has none.
    /// </summary>
    public static IReadOnlyList<PassRoom> Rooms(IEnumerable<Plant> plants, Places places)
    {
        var active = Active(plants).ToList();
        var used = active.Select(p => places.RoomOf(p.PlaceId)?.Id).ToHashSet();

        var rooms = places.Rooms
            .Where(r => used.Contains(r.Id))
            .Select(r => new PassRoom(r.Id, r.Name))
            .ToList();
        if (used.Contains(null))
            rooms.Add(new PassRoom(NoRoom, NoRoomName));
        return rooms;
    }

    /// <summary>
    /// The plants of the picked rooms, one group per room in the order of <paramref name="roomIds"/>.
    /// A room includes its spots. In a group, what is in the room itself comes first, then each
    /// spot by name, then by name. Rooms that don't exist, or are listed twice, are left out.
    /// </summary>
    /// <param name="feeds">Every feed, for the one a plant gets with its watering.</param>
    /// <param name="pot">Finds a pot by id, to see whether it waters itself.</param>
    public static IReadOnlyList<PassGroup> Rows(
        IReadOnlyList<Guid> roomIds,
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<Feed> feeds,
        Func<Guid, Pot?>? pot,
        Places places,
        TimeProvider time)
    {
        var today = time.Today();
        var active = Active(plants).ToList();
        var careByPlant = logs.Where(l => !l.IsDeleted).ToLookup(l => l.PlantId);
        var feedById = feeds.Where(f => !f.IsDeleted).GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());

        var groups = new List<PassGroup>();
        foreach (var roomId in roomIds.Distinct())
        {
            IEnumerable<Plant> inRoom;
            string name;
            if (roomId == NoRoom)
            {
                name = NoRoomName;
                inRoom = active
                    .Where(p => places.RoomOf(p.PlaceId) is null)
                    .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
            }
            else if (places.Rooms.FirstOrDefault(r => r.Id == roomId) is { } room)
            {
                name = room.Name;
                inRoom = GoingRound.Ordered(
                        active.Where(p => places.RoomOf(p.PlaceId)?.Id == room.Id),
                        p => p.PlaceId, p => p.DisplayName, room, places)
                    .Select(x => x.Item);
            }
            else
            {
                continue;
            }

            var rows = inRoom.Select(p => RowOf(p, careByPlant[p.Id].ToList(), feedById, pot, today)).ToList();
            groups.Add(new PassGroup(roomId, name, rows));
        }
        return groups;
    }

    private static PassRow RowOf(
        Plant plant, List<CareLog> care, Dictionary<Guid, Feed> feeds, Func<Guid, Pot?>? pot, DateOnly today)
    {
        var form = WateringForms.Of(plant, pot);
        var waterAt = plant.WaterAt ?? Plant.DefaultWaterAt;
        var doneKind = WateringForms.DoneKind(form);

        // The newest of the day, going by when it was last saved: a second tap corrects the same entry
        var reading = care
            .Where(l => l.Kind == CareKind.MoistureReading && l.Moisture is not null && l.OccurredOn == today)
            .OrderBy(l => l.UpdatedAt)
            .ThenBy(l => l.CreatedAt)
            .LastOrDefault();
        var done = care
            .Where(l => l.Kind is CareKind.Watered or CareKind.ToppedUp && l.OccurredOn == today)
            .OrderBy(l => l.CreatedAt)
            .LastOrDefault();

        RoundReading? shown = reading is null ? null : new RoundReading(reading.Id, reading.Moisture!.Value);
        var dry = form == WateringForm.TopWatered && shown is not null && shown.Value <= waterAt;

        Feed? feed = null;
        if (done is null
            && plant.WateringFeedId is { } feedId
            && feeds.TryGetValue(feedId, out var found)
            && found.Products.Count > 0
            && FeedWithWatering.IsDue(
                care, new CareLog { PlantId = plant.Id, Kind = doneKind, OccurredOn = today }, plant.WateringFeedEvery))
            feed = found;

        return new PassRow(plant, form, shown, waterAt, done?.Id, dry, dry && done is null, feed);
    }

    private static IEnumerable<Plant> Active(IEnumerable<Plant> plants) =>
        plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active);
}
