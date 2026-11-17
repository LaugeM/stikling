using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Moving;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class MovingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateOnly Day = new(2026, 11, 10);

    private readonly FakePlaceRepository places = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();

    private readonly Place living;
    private readonly Place sill;
    private readonly Place cabinet;
    private readonly Place shed;

    public MovingTests()
    {
        living = places.Add("Living room");
        sill = places.Add("Windowsill", living);
        cabinet = places.Add("Plant cabinet");
        shed = places.Add("Shed");
    }

    private MovingService Service => new(places, plants, propagations, timeline, new FixedTime(Now));

    private Plant AddPlant(string name, Place? place, PlantStatus status = PlantStatus.Active)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id, Status = status };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Propagation AddCutting(string genus, Place? place, PropagationStage stage = PropagationStage.Rooting)
    {
        var cutting = new Propagation { Genus = genus, PlaceId = place?.Id, Stage = stage };
        propagations.Propagations[cutting.Id] = cutting;
        return cutting;
    }

    private IEnumerable<TimelineEntry> Entries(Entity subject) => timeline.Entries.Where(e => e.SubjectId == subject.Id);

    [Fact]
    public async Task A_room_that_goes_somewhere_moves_its_plants_and_is_left_behind()
    {
        var hoya = AddPlant("Hoya", living);
        var onSill = AddPlant("Pothos", sill);
        var cutting = AddCutting("Monstera", sill);

        var result = await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.GoesTo, "Lounge"),
            new RoomPlan(cabinet.Id, RoomMove.CameAlong),
            new RoomPlan(shed.Id, RoomMove.LeftBehind)]));

        var lounge = places.Current.RoomNamed("Lounge")!;
        Assert.Equal(lounge.Id, hoya.PlaceId);
        // A spot's plants go to the new room itself
        Assert.Equal(lounge.Id, onSill.PlaceId);
        Assert.Equal(lounge.Id, cutting.PlaceId);
        Assert.Equal(new MoveResult(1, 3, 0, 3), result);

        Assert.Equal(Day, living.LeftOn);
        Assert.Equal(Day, sill.LeftOn);
        Assert.Equal(Day, shed.LeftOn);
        Assert.Null(cabinet.LeftOn);
        Assert.Null(lounge.LeftOn);
        // Nothing is deleted
        Assert.All(new[] { living, sill, cabinet, shed }, p => Assert.False(p.IsDeleted));
    }

    [Fact]
    public async Task Every_plant_and_propagation_gets_the_moved_house_line_on_the_day_of_the_move()
    {
        var goes = AddPlant("Hoya", living);
        var along = AddPlant("Fern", cabinet);
        var later = AddPlant("Ficus", shed);
        var cutting = AddCutting("Monstera", shed);
        var gone = AddPlant("Dead one", living, PlantStatus.Died);
        var done = AddCutting("Finished", living, PropagationStage.Done);

        await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.GoesTo, "Lounge"),
            new RoomPlan(cabinet.Id, RoomMove.CameAlong),
            new RoomPlan(shed.Id, RoomMove.PlaceLater)]));

        foreach (var subject in new Entity[] { goes, along, later, cutting })
        {
            var line = Assert.Single(Entries(subject), e => e.Event == TimelineEvent.MovedHouse);
            Assert.Equal("Moved house", line.Text);
            Assert.Equal(TimelineKind.Change, line.Kind);
            Assert.Equal(Day, new FixedTime(Now).LocalDay(line.OccurredAt));
        }
        Assert.Empty(Entries(gone));
        Assert.Empty(Entries(done));
    }

    [Fact]
    public async Task What_moves_gets_the_usual_room_change_line_and_what_waits_gets_none()
    {
        var goes = AddPlant("Hoya", living);
        var later = AddPlant("Ficus", shed);

        await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.GoesTo, "Lounge"),
            new RoomPlan(shed.Id, RoomMove.PlaceLater)]));

        Assert.Contains(Entries(goes), e => e.Text == "Moved from Living room to Lounge");
        Assert.DoesNotContain(Entries(later), e => e.Text?.StartsWith("Moved from") == true);
        Assert.Equal(shed.Id, later.PlaceId);
    }

    [Fact]
    public async Task Rooms_that_go_to_the_same_name_share_one_new_room()
    {
        var a = AddPlant("A", living);
        var b = AddPlant("B", shed);

        var result = await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.GoesTo, "Lounge"),
            new RoomPlan(shed.Id, RoomMove.GoesTo, " lounge ")]));

        Assert.Equal(1, result.NewRooms);
        Assert.Equal(a.PlaceId, b.PlaceId);
    }

    [Fact]
    public async Task A_new_room_can_have_the_name_of_a_room_that_is_left_behind()
    {
        var hoya = AddPlant("Hoya", living);

        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.GoesTo, "Living room")]));

        Assert.NotEqual(living.Id, hoya.PlaceId);
        // Typing the name now finds the room in use, not the one from before
        Assert.Equal(hoya.PlaceId, places.Current.RoomNamed("Living room")!.Id);
    }

    [Fact]
    public async Task A_new_room_can_be_a_room_that_came_along()
    {
        var hoya = AddPlant("Hoya", living);

        var result = await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.GoesTo, "Plant cabinet"),
            new RoomPlan(cabinet.Id, RoomMove.CameAlong)]));

        Assert.Equal(0, result.NewRooms);
        Assert.Equal(cabinet.Id, hoya.PlaceId);
    }

    [Fact]
    public async Task The_watering_reminder_learns_again_after_the_move()
    {
        var hoya = AddPlant("Hoya", living);
        var logs = Enumerable.Range(0, 5)
            .Select(i => new CareLog { PlantId = hoya.Id, Kind = CareKind.Watered, OccurredOn = Day.AddDays(-7 * (i + 1)) }).ToList();
        var time = new FixedTime(Now);
        WateringGuess Guess() => WateringGuess.Of(
            hoya, logs, [], WateringForm.TopWatered, false, Hemisphere.Northern, Today, null, [], time);
        Assert.Equal(WateringMode.Interval, Guess().Mode);

        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.PlaceLater)]));

        Assert.Equal(Day, hoya.WateringSince);
        Assert.Equal(WateringMode.Learning, Guess().Mode);

        // What is watered after the move counts again
        logs.Add(new CareLog { PlantId = hoya.Id, Kind = CareKind.Watered, OccurredOn = Day.AddDays(1) });
        Assert.Equal(WateringMode.Learning, Guess().Mode);
        Assert.Equal(1, Guess().LearnedFrom);
    }

    [Fact]
    public async Task A_move_does_not_pull_an_existing_cut_off_back()
    {
        var hoya = AddPlant("Hoya", living);
        hoya.WateringSince = new DateOnly(2026, 11, 14);

        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.PlaceLater)]));

        Assert.Equal(new DateOnly(2026, 11, 14), hoya.WateringSince);
    }

    [Fact]
    public async Task The_day_of_the_move_cannot_be_later_than_today()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.MoveAsync(new MovePlan(Today.AddDays(1), [new RoomPlan(living.Id, RoomMove.LeftBehind)])));
    }

    [Fact]
    public async Task Only_rooms_in_use_can_be_picked()
    {
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(shed.Id, RoomMove.LeftBehind)]));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.MoveAsync(new MovePlan(Day, [new RoomPlan(shed.Id, RoomMove.LeftBehind)])));
    }

    [Fact]
    public async Task A_new_room_needs_a_name()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.GoesTo, "  ")])));
        Assert.Null(living.LeftOn);
    }

    [Fact]
    public void Rooms_with_something_in_them_default_to_place_later_and_empty_ones_to_left_behind()
    {
        AddPlant("Hoya", sill);
        var rooms = Room.List(places.Current, plants.Plants.Values, propagations.Propagations.Values);

        var plan = MovingService.Defaults(rooms).ToDictionary(p => p.RoomId, p => p.Move);

        Assert.Equal(RoomMove.PlaceLater, plan[living.Id]);
        Assert.Equal(RoomMove.LeftBehind, plan[shed.Id]);
    }

    [Fact]
    public async Task Rooms_from_before_the_move_are_left_out_of_the_lists_of_rooms()
    {
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.LeftBehind)]));
        var all = places.Current;

        Assert.DoesNotContain(living.Id, all.CurrentRooms.Select(r => r.Id));
        Assert.Contains(living.Id, all.RoomsFromBeforeMove.Select(r => r.Id));
        Assert.True(all.IsFromBeforeMove(sill.Id));
        Assert.False(all.IsFromBeforeMove(cabinet.Id));
        Assert.DoesNotContain(Room.List(all, [], []), r => r.Id == living.Id);
        var old = Assert.Single(Room.List(all, [], [], fromBeforeMove: true), r => r.Id == living.Id);
        Assert.Equal(Day, old.LeftOn);
    }

    [Fact]
    public async Task What_is_left_in_old_rooms_is_still_to_place_and_grouped_by_room()
    {
        AddPlant("A", living);
        AddPlant("B", sill);
        var c = AddCutting("C", shed);
        AddPlant("In cabinet", cabinet);
        await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.PlaceLater),
            new RoomPlan(shed.Id, RoomMove.PlaceLater),
            new RoomPlan(cabinet.Id, RoomMove.CameAlong)]));

        var left = MovingService.StillToPlace(plants.Plants.Values, propagations.Propagations.Values, places.Current);

        Assert.Equal(2, left.Count);
        Assert.Equal(["A", "B"], left.Single(g => g.Room.Id == living.Id).Plants.Select(p => p.DisplayName));
        Assert.Equal(c.Id, left.Single(g => g.Room.Id == shed.Id).Propagations.Single().Id);
        Assert.Equal(3, MovingService.CountStillToPlace(plants.Plants.Values, propagations.Propagations.Values, places.Current));
    }

    [Fact]
    public async Task Placing_moves_what_is_left_writes_the_room_change_line_and_empties_the_card()
    {
        var a = AddPlant("A", living);
        var c = AddCutting("C", sill);
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.PlaceLater)]));

        var moved = await Service.PlaceAsync([(SubjectType.Plant, a.Id, cabinet.Id), (SubjectType.Propagation, c.Id, cabinet.Id)]);

        Assert.Equal(2, moved);
        Assert.Equal(cabinet.Id, a.PlaceId);
        Assert.Equal(cabinet.Id, c.PlaceId);
        Assert.Contains(Entries(a), e => e.Text == "Moved from Living room to Plant cabinet");
        Assert.Contains(Entries(c), e => e.Text == "Moved from Living room / Windowsill to Plant cabinet");
        Assert.Equal(0, MovingService.CountStillToPlace(plants.Plants.Values, propagations.Propagations.Values, places.Current));
    }

    [Fact]
    public async Task Things_cannot_be_placed_in_a_room_from_before_the_move()
    {
        var a = AddPlant("A", living);
        await Service.MoveAsync(new MovePlan(Day, [
            new RoomPlan(living.Id, RoomMove.PlaceLater), new RoomPlan(shed.Id, RoomMove.LeftBehind)]));

        await Assert.ThrowsAsync<ArgumentException>(() => Service.PlaceAsync([(SubjectType.Plant, a.Id, shed.Id)]));
    }

    [Fact]
    public async Task Using_a_room_again_clears_the_mark_on_it_and_its_spots()
    {
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.LeftBehind)]));

        await Service.UseAgainAsync(living.Id);

        Assert.Null(living.LeftOn);
        Assert.Null(sill.LeftOn);
        Assert.Contains(living.Id, places.Current.CurrentRooms.Select(r => r.Id));
    }

    [Fact]
    public async Task A_record_still_in_an_old_room_keeps_pointing_at_it_when_its_place_is_unchanged()
    {
        var a = AddPlant("A", shed);
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(shed.Id, RoomMove.PlaceLater)]));

        var rooms = new RoomService(places, plants, propagations);
        Assert.Equal(shed.Id, await rooms.PlaceIdAsync("Shed", a.PlaceId));
    }

    [Fact]
    public async Task Typing_the_name_of_an_old_room_makes_a_new_room_instead()
    {
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(shed.Id, RoomMove.LeftBehind)]));
        var rooms = new RoomService(places, plants, propagations);

        var id = await rooms.PlaceIdAsync("Shed");

        Assert.NotEqual(shed.Id, id);
        Assert.Null(places.Current.Find(id)!.LeftOn);

        var (added, existed) = await rooms.AddPlaceAsync("Attic");
        Assert.False(existed);
        Assert.Null(added.LeftOn);
    }

    [Fact]
    public async Task Renaming_a_room_to_the_name_of_an_old_room_does_not_merge_into_it()
    {
        var hoya = AddPlant("Hoya", cabinet);
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(shed.Id, RoomMove.LeftBehind)]));
        var rooms = new RoomService(places, plants, propagations);

        var rename = await rooms.RenameAsync(cabinet.Id, "Shed");

        Assert.False(rename.Merged);
        Assert.Equal(cabinet.Id, hoya.PlaceId);
        Assert.False(shed.IsDeleted);
    }

    [Fact]
    public async Task Placing_checks_every_room_before_moving_anything()
    {
        var a = AddPlant("A", living);
        var b = AddPlant("B", living);
        await Service.MoveAsync(new MovePlan(Day, [new RoomPlan(living.Id, RoomMove.PlaceLater), new RoomPlan(shed.Id, RoomMove.LeftBehind)]));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.PlaceAsync([(SubjectType.Plant, a.Id, cabinet.Id), (SubjectType.Plant, b.Id, shed.Id)]));

        Assert.Equal(living.Id, a.PlaceId);
    }
}
