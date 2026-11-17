using Strings = Stikling.Core.Resources;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Moving;

/// <summary>What happens to a room that is lived in when the household moves house.</summary>
public enum RoomMove
{
    /// <summary>What is in the room goes to a new room, and the old room is left behind.</summary>
    GoesTo,

    /// <summary>The room itself moves along, like a plant cabinet. It stays in use as it is.</summary>
    CameAlong,

    /// <summary>The room is left behind and what is in it waits to be placed afterwards.</summary>
    PlaceLater,

    /// <summary>The room is left behind with nothing in it.</summary>
    LeftBehind
}

/// <param name="RoomId">A room in use.</param>
/// <param name="NewRoom">For <see cref="RoomMove.GoesTo"/>, the name of the room it goes to. Rooms given the same name go to the same one.</param>
public sealed record RoomPlan(Guid RoomId, RoomMove Move, string? NewRoom = null);

public sealed record MovePlan(DateOnly Day, IReadOnlyList<RoomPlan> Rooms);

/// <param name="NewRooms">How many rooms were made.</param>
/// <param name="Moved">How many plants and propagations went to a new room.</param>
/// <param name="ToPlace">How many are left in rooms from before the move.</param>
/// <param name="Marked">How many plants and propagations got the line about the move.</param>
public sealed record MoveResult(int NewRooms, int Moved, int ToPlace, int Marked);

/// <summary>A room from before a move, and the plants and propagations still in it or its spots.</summary>
public sealed record StillToPlaceRoom(Place Room, IReadOnlyList<Plant> Plants, IReadOnlyList<Propagation> Propagations)
{
    public int Total => Plants.Count + Propagations.Count;
}

/// <summary>Moving house: new rooms, what goes where, and placing what was left over afterwards.</summary>
public sealed class MovingService(
    IPlaceRepository places, IPlantRepository plants, IPropagationRepository propagations,
    ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>The choices to start from: a room with something in it waits to be placed, an empty one is left behind.</summary>
    public static IReadOnlyList<RoomPlan> Defaults(IEnumerable<Room> rooms) =>
        [.. rooms.Select(r => new RoomPlan(r.Id, r.Total > 0 ? RoomMove.PlaceLater : RoomMove.LeftBehind))];

    /// <summary>
    /// The plants and propagations in the collection that are still in a room from before a move,
    /// grouped by that room, oldest room first. Empty when everything has been placed.
    /// </summary>
    public static IReadOnlyList<StillToPlaceRoom> StillToPlace(
        IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, Places places)
    {
        var plantList = plants.Where(p => !p.IsDeleted && p.InCollection && places.IsFromBeforeMove(p.PlaceId)).ToList();
        var propagationList = propagations.Where(p => !p.IsDeleted && p.IsActive && places.IsFromBeforeMove(p.PlaceId)).ToList();

        return [.. places.RoomsFromBeforeMove
            .Select(room => new StillToPlaceRoom(
                room,
                [.. plantList.Where(p => places.RoomOf(p.PlaceId)?.Id == room.Id)
                    .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)],
                [.. propagationList.Where(p => places.RoomOf(p.PlaceId)?.Id == room.Id)
                    .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)]))
            .Where(g => g.Total > 0)];
    }

    /// <summary>How many plants and propagations are still to place, for the card on Today.</summary>
    public static int CountStillToPlace(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, Places places) =>
        StillToPlace(plants, propagations, places).Sum(g => g.Total);

    /// <summary>
    /// Carries out a move house. Makes the new rooms, marks the rooms that stayed behind, moves what is
    /// in a room that goes somewhere, and writes "Moved house" on every plant and propagation in the
    /// collection, dated the day of the move. A plant also starts learning its watering again, since
    /// what it was watered like before says little about a new home.
    /// </summary>
    /// <remarks>Nothing is deleted. The old rooms are kept with everything they held.</remarks>
    public async Task<MoveResult> MoveAsync(MovePlan plan)
    {
        if (plan.Day > time.Today())
            throw new ArgumentException(Strings.Moving.ErrDayFuture, nameof(plan));

        var all = await places.GetPlacesAsync();
        var current = all.CurrentRooms.ToDictionary(r => r.Id);
        var chosen = new Dictionary<Guid, RoomPlan>();
        foreach (var room in plan.Rooms)
        {
            if (!current.ContainsKey(room.RoomId))
                throw new ArgumentException(Strings.Shared.RoomGone, nameof(plan));
            chosen[room.RoomId] = room;
        }

        // A room that is left behind is no place to move to, even if a new room has the same name
        var staying = all.CurrentRooms.Where(r => chosen.GetValueOrDefault(r.Id)?.Move is null or RoomMove.CameAlong).ToList();
        var targets = new Dictionary<Guid, Place>();
        var made = new List<Place>();
        foreach (var goes in chosen.Values.Where(c => c.Move == RoomMove.GoesTo))
        {
            var name = RoomName.Clean(goes.NewRoom) ?? "";
            var target = made.FirstOrDefault(r => RoomName.Same(r.Name, name))
                ?? staying.FirstOrDefault(r => RoomName.Same(r.Name, name));
            if (target is null)
            {
                target = new Place { Name = name };
                if (target.Validate().FirstOrDefault() is { } error)
                    throw new ArgumentException(error, nameof(plan));
                made.Add(target);
            }
            targets[goes.RoomId] = target;
        }

        foreach (var room in made)
            await places.SaveAsync(room);

        // Marked first, so a move that stops halfway leaves what is left waiting to be placed instead of moved twice
        foreach (var room in chosen.Values.Where(c => c.Move != RoomMove.CameAlong))
        {
            foreach (var place in all.SpotsIn(room.RoomId).Append(current[room.RoomId]))
            {
                place.LeftOn = plan.Day;
                await places.SaveAsync(place);
            }
        }

        var at = time.MomentOn(plan.Day);
        string? Name(Guid id) => all.NameOf(id) ?? made.FirstOrDefault(r => r.Id == id)?.Name;
        Place? Target(Guid? placeId) =>
            all.RoomOf(placeId) is { } room && targets.TryGetValue(room.Id, out var target) ? target : null;
        int moved = 0, marked = 0;

        foreach (var plant in (await plants.GetAllAsync()).Where(p => !p.IsDeleted && p.InCollection))
        {
            var before = plant.PlaceId;
            if (Target(plant.PlaceId) is { } target)
            {
                plant.PlaceId = target.Id;
                moved++;
            }
            // What it learned about watering was in the old home, so it learns again from the move
            if (plant.WateringSince is not { } since || since < plan.Day)
                plant.WateringSince = plan.Day;
            await plants.SaveAsync(plant);
            await WriteAsync(SubjectType.Plant, plant.Id, before, plant.PlaceId, at, Name);
            marked++;
        }

        foreach (var propagation in (await propagations.GetAllAsync()).Where(p => !p.IsDeleted && p.IsActive))
        {
            var before = propagation.PlaceId;
            if (Target(propagation.PlaceId) is { } target)
            {
                propagation.PlaceId = target.Id;
                moved++;
            }
            await propagations.SaveAsync(propagation);
            await WriteAsync(SubjectType.Propagation, propagation.Id, before, propagation.PlaceId, at, Name);
            marked++;
        }

        var left = StillToPlace(await plants.GetAllAsync(), await propagations.GetAllAsync(), await places.GetPlacesAsync());
        return new MoveResult(made.Count, moved, left.Sum(g => g.Total), marked);
    }

    /// <summary>
    /// Moves plants and propagations to a room in use, after a move house. Each gets the usual line
    /// about the new room. Anything that isn't in the collection any more is left alone.
    /// </summary>
    /// <returns>How many were moved.</returns>
    public async Task<int> PlaceAsync(IEnumerable<(SubjectType Type, Guid Id, Guid RoomId)> moves)
    {
        var all = await places.GetPlacesAsync();
        var moved = 0;
        var list = moves.ToList();
        if (list.Any(m => all.Find(m.RoomId) is not { } room || all.IsFromBeforeMove(room.Id)))
            throw new ArgumentException(Strings.Moving.ErrPlaceFromBefore, nameof(moves));

        foreach (var (type, id, roomId) in list)
        {
            var room = all.Find(roomId)!;

            Guid? before;
            if (type == SubjectType.Plant)
            {
                if (await plants.GetAsync(id) is not { IsDeleted: false } plant || !plant.InCollection)
                    continue;
                before = plant.PlaceId;
                plant.PlaceId = room.Id;
                await plants.SaveAsync(plant);
            }
            else
            {
                if (await propagations.GetAsync(id) is not { IsDeleted: false } propagation || !propagation.IsActive)
                    continue;
                before = propagation.PlaceId;
                propagation.PlaceId = room.Id;
                await propagations.SaveAsync(propagation);
            }

            await WriteLocationAsync(type, id, before, room.Id, time.GetLocalNow(), id2 => all.NameOf(id2));
            moved++;
        }
        return moved;
    }

    /// <summary>Brings a room from before a move back into use. It and its spots lose the mark from the move.</summary>
    public async Task UseAgainAsync(Guid roomId)
    {
        var all = await places.GetPlacesAsync();
        if (all.RoomOf(roomId) is not { } room)
            throw new ArgumentException(Strings.Shared.RoomGone, nameof(roomId));

        foreach (var place in all.SpotsIn(room.Id).Append(room).Where(p => p.LeftOn is not null))
        {
            place.LeftOn = null;
            await places.SaveAsync(place);
        }
    }

    private async Task WriteAsync(
        SubjectType type, Guid id, Guid? before, Guid? after, DateTimeOffset at, Func<Guid, string?> name)
    {
        await WriteLocationAsync(type, id, before, after, at, name);

        var entry = new TimelineEntry
        {
            SubjectType = type, SubjectId = id, Kind = TimelineKind.Change, Event = TimelineEvent.MovedHouse, OccurredAt = at
        };
        HistoryText.Set(entry, [HistoryText.Line("MovedHouse")]);
        await timeline.AddAsync(entry);
    }

    private async Task WriteLocationAsync(
        SubjectType type, Guid id, Guid? before, Guid? after, DateTimeOffset at, Func<Guid, string?> name)
    {
        var lines = new List<HistoryLine>();
        ChangeText.AddLocation(lines, before, after, name);
        if (lines.Count == 0)
            return;

        var entry = new TimelineEntry { SubjectType = type, SubjectId = id, Kind = TimelineKind.Change, OccurredAt = at };
        HistoryText.Set(entry, lines);
        await timeline.AddAsync(entry);
    }
}
