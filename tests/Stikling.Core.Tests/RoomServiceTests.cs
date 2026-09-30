using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class RoomServiceTests
{
    private readonly FakePlaceRepository places = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();

    private RoomService Service => new(places, plants, propagations);

    private Places Places => places.Current;

    private string? NameOf(Guid? id) => Places.NameOf(id);

    private Plant AddPlant(string name, Place? place)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Propagation AddPropagation(string genus, Place? place)
    {
        var propagation = new Propagation { Genus = genus, PlaceId = place?.Id };
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    [Fact]
    public async Task Typing_a_room_in_use_picks_it()
    {
        var living = places.Add("Living room");

        Assert.Equal(living.Id, await Service.PlaceIdAsync("living room "));
        Assert.Single(places.Places);
    }

    [Fact]
    public async Task Typing_a_new_room_or_spot_makes_it()
    {
        var living = places.Add("Living room");

        var id = await Service.PlaceIdAsync("Living room / On top of the PC");

        var spot = places.Places[id!.Value];
        Assert.Equal("On top of the PC", spot.Name);
        Assert.Equal(living.Id, spot.RoomId);
        Assert.Equal("Bedroom", NameOf(await Service.PlaceIdAsync("Bedroom")));
    }

    [Fact]
    public async Task Typing_nothing_is_no_room()
    {
        Assert.Null(await Service.PlaceIdAsync("  "));
        Assert.Empty(places.Places);
    }

    [Fact]
    public async Task Renaming_a_room_changes_the_room_and_nothing_in_it()
    {
        var stue = places.Add("Stue");
        var plant = AddPlant("Monstera", stue);
        var cutting = AddPropagation("Coleus", stue);

        var result = await Service.RenameAsync(stue.Id, "Living room");

        Assert.Equal(stue.Id, plant.PlaceId);
        Assert.Equal("Living room", NameOf(plant.PlaceId));
        Assert.Equal("Living room", NameOf(cutting.PlaceId));
        Assert.Equal(1, result.Plants);
        Assert.Equal(1, result.Propagations);
        Assert.False(result.Merged);
    }

    [Fact]
    public async Task Renaming_a_room_takes_its_spots_with_it()
    {
        var stue = places.Add("Stue");
        var pc = AddPlant("Pilea", places.Add("On top of the PC", stue));

        await Service.RenameAsync(stue.Id, "Living room");

        Assert.Equal("Living room / On top of the PC", NameOf(pc.PlaceId));
    }

    [Fact]
    public async Task Renaming_onto_a_room_that_already_exists_merges_the_two()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        var danish = AddPlant("Monstera", stue);
        var english = AddPlant("Basil", living);

        var result = await Service.RenameAsync(stue.Id, "Living room");

        Assert.Equal("Living room", NameOf(danish.PlaceId));
        Assert.Equal("Living room", NameOf(english.PlaceId));
        Assert.True(result.Merged);
        Assert.Equal(1, result.Plants); // only the one that actually moved
        Assert.Single(await Service.GetAllAsync());
        Assert.True(stue.IsDeleted);
        Assert.Equal(living.Id, stue.MergedIntoId);
    }

    [Fact]
    public async Task Something_placed_in_a_merged_room_on_another_device_ends_up_in_the_merged_one()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        await Service.RenameAsync(stue.Id, "Living room");

        // Synced in after the merge, still pointing at the old room
        var late = AddPlant("Alocasia", stue);

        Assert.Equal("Living room", NameOf(late.PlaceId));
        Assert.Equal(1, (await Service.GetAllAsync()).Single(r => r.Id == living.Id).Plants);
    }

    [Fact]
    public async Task A_spot_both_rooms_have_becomes_one_spot_when_they_merge()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        AddPlant("Monstera", places.Add("Windowsill", stue));
        AddPlant("Basil", places.Add("windowsill", living));
        AddPlant("Pilea", places.Add("Shelf", stue));

        await Service.RenameAsync(stue.Id, "Living room");

        var room = (await Service.GetAllAsync()).Single();
        Assert.Equal(["Shelf", "windowsill"], room.Spots.Select(s => s.Name));
        Assert.Equal(2, room.Spots.Single(s => s.Name == "windowsill").Plants);
    }

    [Fact]
    public async Task Two_rooms_merged_into_each_other_on_different_devices_become_one()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        var danish = AddPlant("Monstera", stue);
        var english = AddPlant("Basil", living);
        var cutting = AddPropagation("Coleus", places.Add("Windowsill", living));

        // One device renamed Stue to Living room, the other Living room to Stue, and a restore
        // brought both merges together
        stue.MergedIntoId = living.Id;
        living.MergedIntoId = stue.Id;
        await places.DeleteAsync(stue.Id);
        await places.DeleteAsync(living.Id);

        var room = (await Service.GetAllAsync()).Single();
        Assert.Equal(stue.Id, room.Id);
        Assert.Equal(2, room.AllPlants);
        Assert.Equal("Stue", NameOf(danish.PlaceId));
        Assert.Equal("Stue", NameOf(english.PlaceId));
        Assert.Equal("Stue / Windowsill", NameOf(cutting.PlaceId));
    }

    [Fact]
    public async Task A_room_kept_from_a_merge_loop_can_be_renamed()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        var plant = AddPlant("Monstera", living);
        stue.MergedIntoId = living.Id;
        living.MergedIntoId = stue.Id;
        await places.DeleteAsync(stue.Id);
        await places.DeleteAsync(living.Id);

        var result = await Service.RenameAsync(living.Id, "Lounge");

        Assert.Equal("Lounge", NameOf(plant.PlaceId));
        Assert.Equal(1, result.Plants);
        Assert.False(result.Merged);
    }

    [Fact]
    public async Task A_spot_whose_room_was_deleted_can_still_be_renamed()
    {
        // The room was deleted on another device, without being merged into anything
        var shed = places.Add("Shed");
        var shelf = places.Add("Shelf", shed);
        places.Add("Bench", shed);
        var plant = AddPlant("Pilea", shelf);
        await places.DeleteAsync(shed.Id);

        var result = await Service.RenameAsync(shelf.Id, "Bench");

        Assert.False(result.Merged);
        Assert.Equal(1, result.Plants);
        Assert.Equal("Bench", NameOf(plant.PlaceId));
        Assert.Same(shelf, Places.Find(plant.PlaceId));
    }

    [Fact]
    public async Task A_spot_can_be_renamed_without_touching_the_rest_of_the_room()
    {
        var living = places.Add("Living room");
        var pc = AddPlant("Pilea", places.Add("PC", living));
        var sofa = AddPlant("Monstera", living);

        await Service.RenameAsync(pc.PlaceId!.Value, "On top of the PC");

        Assert.Equal("Living room / On top of the PC", NameOf(pc.PlaceId));
        Assert.Equal("Living room", NameOf(sofa.PlaceId));
    }

    [Fact]
    public async Task Renaming_a_spot_onto_another_spot_in_the_room_merges_them()
    {
        var living = places.Add("Living room");
        var pc = AddPlant("Pilea", places.Add("PC", living));
        var shelf = AddPlant("Monstera", places.Add("Shelf", living));

        var result = await Service.RenameAsync(pc.PlaceId!.Value, "Shelf");

        Assert.True(result.Merged);
        Assert.Equal(Places.Find(shelf.PlaceId), Places.Find(pc.PlaceId));
    }

    [Fact]
    public async Task Fixing_only_the_spelling_of_a_room_is_not_a_merge()
    {
        var living = places.Add("living room");
        var plant = AddPlant("Monstera", living);

        var result = await Service.RenameAsync(living.Id, "Living room");

        Assert.Equal("Living room", NameOf(plant.PlaceId));
        Assert.False(result.Merged);
        Assert.Equal(1, result.Plants);
    }

    [Fact]
    public async Task A_pest_case_on_the_room_follows_the_rename_and_the_merge()
    {
        var stue = places.Add("Stue");
        var living = places.Add("Living room");
        var plant = AddPlant("Monstera", places.Add("Windowsill", stue));
        var item = new PestCase { Scope = PestScope.Room, PlaceId = living.Id };

        await Service.RenameAsync(stue.Id, "Living room");

        Assert.Equal([plant], PestService.PlantsIn(item, [plant], Places));
    }

    [Fact]
    public async Task A_room_needs_a_name_without_a_slash()
    {
        var stue = places.Add("Stue");

        await Assert.ThrowsAsync<ArgumentException>(() => Service.RenameAsync(stue.Id, "   "));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.RenameAsync(stue.Id, "Living room / Shelf"));
    }

    [Fact]
    public async Task An_empty_room_is_removed_together_with_its_empty_spots()
    {
        var office = places.Add("Office");
        var shelf = places.Add("Shelf", office);
        var kitchen = places.Add("Kitchen");

        await Service.RemoveAsync(office.Id);

        Assert.True(places.Places[office.Id].IsDeleted);
        Assert.True(places.Places[shelf.Id].IsDeleted);
        Assert.False(places.Places[kitchen.Id].IsDeleted);
        Assert.Equal(["Kitchen"], (await Service.GetAllAsync()).Select(r => r.Name));
    }

    [Fact]
    public async Task An_empty_spot_is_removed_and_its_room_stays()
    {
        var office = places.Add("Office");
        var shelf = places.Add("Shelf", office);

        await Service.RemoveAsync(shelf.Id);

        Assert.True(places.Places[shelf.Id].IsDeleted);
        Assert.False(places.Places[office.Id].IsDeleted);
    }

    [Fact]
    public async Task A_room_or_spot_with_a_plant_or_propagation_is_not_removed()
    {
        var office = places.Add("Office");
        var shelf = places.Add("Shelf", office);
        var desk = places.Add("Desk", office);
        AddPlant("Monstera", shelf);
        AddPropagation("Coleus", desk);
        var hall = places.Add("Hall");
        AddPlant("Pilea", hall);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RemoveAsync(shelf.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RemoveAsync(desk.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RemoveAsync(office.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RemoveAsync(hall.Id));

        Assert.All(places.Places.Values, p => Assert.False(p.IsDeleted));
    }

    [Fact]
    public async Task A_deleted_plant_does_not_keep_a_room_from_being_removed()
    {
        var office = places.Add("Office");
        AddPlant("Gone", office).DeletedAt = DateTimeOffset.UtcNow;

        await Service.RemoveAsync(office.Id);

        Assert.True(places.Places[office.Id].IsDeleted);
    }

    [Fact]
    public async Task Adding_a_room_or_spot_makes_it_or_finds_the_one_there()
    {
        var (living, existed) = await Service.AddPlaceAsync("Living room");
        Assert.False(existed);

        var (again, existedAgain) = await Service.AddPlaceAsync(" living room");
        Assert.True(existedAgain);
        Assert.Equal(living.Id, again.Id);

        var (shelf, _) = await Service.AddPlaceAsync("Shelf", living.Id);
        Assert.Equal("Living room / Shelf", NameOf(shelf.Id));
    }

    [Fact]
    public async Task A_spot_goes_in_the_room_it_was_added_to_when_two_rooms_share_a_name()
    {
        // Two devices each made an "Office" before syncing
        places.Add("Office");
        var second = places.Add("Office");

        var (shelf, _) = await Service.AddPlaceAsync("Shelf", second.Id);

        Assert.Equal(second.Id, shelf.RoomId);
    }

    [Fact]
    public async Task A_name_of_only_slashes_or_a_room_with_a_slash_is_turned_down()
    {
        var office = places.Add("Office");

        await Assert.ThrowsAsync<ArgumentException>(() => Service.AddPlaceAsync("/"));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.AddPlaceAsync("//", office.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.AddPlaceAsync("Office / Shelf"));
        Assert.Single(places.Places);
    }

    [Fact]
    public async Task A_spot_added_to_a_room_removed_on_another_device_shows_as_a_room()
    {
        var office = places.Add("Office");
        var shelf = places.Add("Shelf", office);
        await places.DeleteAsync(office.Id);

        var rooms = await Service.GetAllAsync();

        Assert.Equal("Shelf", Assert.Single(rooms).Name);
        Assert.Equal("Shelf", NameOf(shelf.Id));
        Assert.Same(shelf, Places.RoomOf(shelf.Id));
    }
}
