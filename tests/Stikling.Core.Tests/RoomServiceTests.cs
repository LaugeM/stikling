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
}
