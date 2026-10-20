using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Propagations;
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
/// <param name="StillDamp">Top watered, not done today, and its water reminder is put off, which is how "Still damp" is remembered.</param>
/// <param name="DefaultFeed">The feed the plant gets with its watering, when it is due on this one. Null when not due or already done today.</param>
public sealed record PassRow(
    Plant Plant, WateringForm Form, RoundReading? Reading, int WaterAt, Guid? DoneTodayId,
    bool Dry, bool StillDamp, Feed? DefaultFeed);

/// <summary>One cutting in soil or bark on the watering screen, with what is logged today on it.</summary>
/// <param name="Reading">Today's meter reading, when there is one.</param>
/// <param name="WaterAt">The reading it is watered at: the usual 3, since propagations have no setting of their own.</param>
/// <param name="DoneTodayId">The Watered entry logged today.</param>
/// <param name="Dry">Read today, and the reading is at or below <paramref name="WaterAt"/>.</param>
public sealed record PassCutting(Propagation Propagation, RoundReading? Reading, int WaterAt, Guid? DoneTodayId, bool Dry);

/// <summary>The plants of one room on the watering screen, in the order they are walked, and its cuttings after them.</summary>
public sealed record PassGroup(Guid RoomId, string Name, IReadOnlyList<PassRow> Rows)
{
    public IReadOnlyList<PassCutting> Cuttings { get; init; } = [];
}

/// <summary>
/// Watering many plants in one pass: pick rooms, answer how dry each plant is, and water the dry
/// ones. Everything here is worked out from what is logged, so leaving and coming back the
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
    public static IReadOnlyList<PassRoom> Rooms(
        IEnumerable<Plant> plants, Places places, IEnumerable<Propagation>? propagations = null)
    {
        var active = Active(plants).ToList();
        var used = active.Select(p => places.RoomOf(p.PlaceId)?.Id).ToHashSet();
        foreach (var cutting in Cuttings(propagations))
            used.Add(places.RoomOf(cutting.PlaceId)?.Id);

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
    /// <param name="putOffs">What is put off on Today, to tell a plant answered "Still damp" earlier today.</param>
    /// <param name="propagations">Propagations; the active ones in soil or bark are listed after the plants of their room.</param>
    /// <param name="propagationLogs">Their care logs, for today's reading and watering.</param>
    public static IReadOnlyList<PassGroup> Rows(
        IReadOnlyList<Guid> roomIds,
        IEnumerable<Plant> plants,
        IEnumerable<CareLog> logs,
        IEnumerable<Feed> feeds,
        Func<Guid, Pot?>? pot,
        Places places,
        TimeProvider time,
        PutOffs? putOffs = null,
        IEnumerable<Propagation>? propagations = null,
        IEnumerable<PropagationCareLog>? propagationLogs = null)
    {
        var today = time.Today();
        var cuttings = Cuttings(propagations).ToList();
        var careByCutting = (propagationLogs ?? []).Where(l => !l.IsDeleted).ToLookup(l => l.PropagationId);
        var active = Active(plants).ToList();
        var careByPlant = logs.Where(l => !l.IsDeleted).ToLookup(l => l.PlantId);
        var feedById = feeds.Where(f => !f.IsDeleted).GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());

        var groups = new List<PassGroup>();
        foreach (var roomId in roomIds.Distinct())
        {
            IEnumerable<Plant> inRoom;
            IEnumerable<Propagation> cuttingsInRoom;
            string name;
            if (roomId == NoRoom)
            {
                name = NoRoomName;
                inRoom = active
                    .Where(p => places.RoomOf(p.PlaceId) is null)
                    .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);
                cuttingsInRoom = cuttings
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
                cuttingsInRoom = GoingRound.Ordered(
                        cuttings.Where(p => places.RoomOf(p.PlaceId)?.Id == room.Id),
                        p => p.PlaceId, p => p.DisplayName, room, places)
                    .Select(x => x.Item);
            }
            else
            {
                continue;
            }

            var rows = inRoom.Select(p => RowOf(p, careByPlant[p.Id].ToList(), feedById, pot, putOffs, today)).ToList();
            var cuttingRows = cuttingsInRoom.Select(p => CuttingOf(p, careByCutting[p.Id].ToList(), today)).ToList();
            groups.Add(new PassGroup(roomId, name, rows) { Cuttings = cuttingRows });
        }
        return groups;
    }

    private static PassRow RowOf(
        Plant plant, List<CareLog> care, Dictionary<Guid, Feed> feeds, Func<Guid, Pot?>? pot, PutOffs? putOffs, DateOnly today)
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

        var lastWatered = WateringGuess.Waterings(plant, care).Cast<DateOnly?>().LastOrDefault();
        var stillDamp = form == WateringForm.TopWatered && done is null
            && putOffs?.IsPutOff(PutOffs.Water(plant.Id, lastWatered), today) == true;

        return new PassRow(plant, form, shown, waterAt, done?.Id, dry, stillDamp, feed);
    }

    private static PassCutting CuttingOf(Propagation propagation, List<PropagationCareLog> care, DateOnly today)
    {
        // The newest of the day, going by when it was last saved: a second tap corrects the same entry
        var reading = care
            .Where(l => l.Kind == PropagationCareKind.MoistureReading && l.Moisture is not null && l.OccurredOn == today)
            .OrderBy(l => l.UpdatedAt)
            .ThenBy(l => l.CreatedAt)
            .LastOrDefault();
        var done = care
            .Where(l => l.Kind == PropagationCareKind.Watered && l.OccurredOn == today)
            .OrderBy(l => l.CreatedAt)
            .LastOrDefault();
        RoundReading? shown = reading is null ? null : new RoundReading(reading.Id, reading.Moisture!.Value);
        var dry = shown is not null && shown.Value <= Plant.DefaultWaterAt;
        return new PassCutting(propagation, shown, Plant.DefaultWaterAt, done?.Id, dry);
    }

    // Active cuttings in soil or bark, which are watered from the top
    private static IEnumerable<Propagation> Cuttings(IEnumerable<Propagation>? propagations) =>
        (propagations ?? []).Where(p => !p.IsDeleted && p.IsActive
            && p.Medium is GrowingMedium.Soil or GrowingMedium.OrchidBark);

    private static IEnumerable<Plant> Active(IEnumerable<Plant> plants) =>
        plants.Where(p => !p.IsDeleted && p.InCollection);
}
