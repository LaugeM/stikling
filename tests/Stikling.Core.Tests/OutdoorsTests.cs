using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class OutdoorsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Place balcony = new() { Name = "Balcony", Outdoors = true };
    private readonly Place livingRoom = new() { Name = "Living room" };

    private Place Spot(string name, Place room, bool outdoors = false) =>
        new() { Name = name, RoomId = room.Id, Outdoors = outdoors };

    private static Places Of(params Place[] places) => new(places);

    [Fact]
    public void A_room_marked_outdoors_is_outdoors()
    {
        var places = Of(balcony, livingRoom);

        Assert.True(places.IsOutdoors(balcony.Id));
        Assert.False(places.IsOutdoors(livingRoom.Id));
    }

    [Fact]
    public void A_spot_in_an_outdoor_room_is_outdoors()
    {
        var table = Spot("Table", balcony);

        Assert.True(Of(balcony, table).IsOutdoors(table.Id));
    }

    [Fact]
    public void A_spot_marked_outdoors_in_an_indoor_room_is_outdoors()
    {
        var sill = Spot("Window sill outside", livingRoom, outdoors: true);
        var shelf = Spot("Shelf", livingRoom);
        var places = Of(livingRoom, sill, shelf);

        Assert.True(places.IsOutdoors(sill.Id));
        Assert.False(places.IsOutdoors(shelf.Id));
    }

    [Fact]
    public void A_merged_place_follows_the_place_it_went_into()
    {
        var old = new Place { Name = "Terrace", MergedIntoId = balcony.Id, DeletedAt = Now };

        Assert.True(Of(balcony, old).IsOutdoors(old.Id));
    }

    [Fact]
    public void No_place_and_an_unknown_one_are_not_outdoors()
    {
        var places = Of(balcony);

        Assert.False(places.IsOutdoors(null));
        Assert.False(places.IsOutdoors(Guid.NewGuid()));
    }

    [Fact]
    public void Moving_from_outdoors_to_indoors_came_inside()
    {
        var places = Of(balcony, livingRoom);

        Assert.True(PlantService.CameInside(balcony.Id, livingRoom.Id, places));
    }

    [Fact]
    public void Moving_within_outdoors_or_within_indoors_did_not_come_inside()
    {
        var table = Spot("Table", balcony);
        var shelf = Spot("Shelf", livingRoom);
        var places = Of(balcony, livingRoom, table, shelf);

        Assert.False(PlantService.CameInside(balcony.Id, table.Id, places));
        Assert.False(PlantService.CameInside(livingRoom.Id, shelf.Id, places));
        Assert.False(PlantService.CameInside(livingRoom.Id, balcony.Id, places));
    }

    [Fact]
    public void Moving_from_a_spot_in_an_outdoor_room_to_indoors_came_inside()
    {
        var table = Spot("Table", balcony);

        Assert.True(PlantService.CameInside(table.Id, livingRoom.Id, Of(balcony, livingRoom, table)));
    }

    [Fact]
    public void Moving_from_a_merged_outdoor_place_came_inside()
    {
        var old = new Place { Name = "Terrace", MergedIntoId = balcony.Id, DeletedAt = Now };

        Assert.True(PlantService.CameInside(old.Id, livingRoom.Id, Of(balcony, livingRoom, old)));
    }

    [Fact]
    public void Having_no_place_before_or_after_did_not_come_inside()
    {
        var places = Of(balcony, livingRoom);

        Assert.False(PlantService.CameInside(null, livingRoom.Id, places));
        Assert.False(PlantService.CameInside(balcony.Id, null, places));
    }

    [Theory]
    [InlineData(PlantOrigin.Purchased, true)]
    [InlineData(PlantOrigin.Swap, true)]
    [InlineData(PlantOrigin.Gift, true)]
    [InlineData(PlantOrigin.Propagated, false)]
    [InlineData(PlantOrigin.GrownFromSeed, false)]
    [InlineData(PlantOrigin.Unknown, false)]
    public void Only_bought_swapped_and_gifted_plants_start_in_quarantine(PlantOrigin origin, bool expected)
    {
        Assert.Equal(expected, PlantService.StartsInQuarantine(origin, settingOn: true));
        Assert.False(PlantService.StartsInQuarantine(origin, settingOn: false));
    }

    [Fact]
    public async Task Starting_a_quarantine_uses_the_length_given_when_the_plant_has_none()
    {
        var service = new PlantService(new FakePlantRepository(), new FakeTimelineRepository(), new FixedTime(Now));
        var plant = new Plant { Nickname = "A" };

        await service.SetQuarantineAsync(plant, new DateOnly(2026, 10, 14), v => v.ToString(), days: 21);

        Assert.Equal(21, plant.QuarantineDays);
    }

    [Fact]
    public async Task A_length_the_plant_has_wins_over_the_one_given()
    {
        var service = new PlantService(new FakePlantRepository(), new FakeTimelineRepository(), new FixedTime(Now));
        var plant = new Plant { Nickname = "A", QuarantineDays = 7 };

        await service.SetQuarantineAsync(plant, new DateOnly(2026, 10, 14), v => v.ToString(), days: 21);

        Assert.Equal(7, plant.QuarantineDays);
    }

    [Fact]
    public async Task Without_a_length_the_default_is_used()
    {
        var service = new PlantService(new FakePlantRepository(), new FakeTimelineRepository(), new FixedTime(Now));
        var plant = new Plant { Nickname = "A" };

        await service.SetQuarantineAsync(plant, new DateOnly(2026, 10, 14), v => v.ToString());

        Assert.Equal(Plant.DefaultQuarantineDays, plant.QuarantineDays);
    }

    [Fact]
    public async Task Marking_a_place_as_outdoors_saves_it()
    {
        var places = new FakePlaceRepository();
        var room = new Place { Name = "Garden" };
        await places.SaveAsync(room);
        var service = new RoomService(places, new FakePlantRepository(), new FakePropagationRepository());

        await service.SetOutdoorsAsync(room.Id, true);
        Assert.True((await service.GetPlacesAsync()).IsOutdoors(room.Id));

        await service.SetOutdoorsAsync(room.Id, false);
        Assert.False((await service.GetPlacesAsync()).IsOutdoors(room.Id));
    }
}
