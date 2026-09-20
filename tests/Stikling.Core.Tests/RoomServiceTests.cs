using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class RoomServiceTests
{
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();

    private RoomService Service => new(plants, propagations);

    private Plant AddPlant(string name, string? location)
    {
        var plant = new Plant { Nickname = name, Location = location };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Propagation AddPropagation(string genus, string? location)
    {
        var propagation = new Propagation { Genus = genus, Location = location };
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    [Fact]
    public async Task Renaming_a_room_moves_everything_in_it()
    {
        var plant = AddPlant("Monstera", "Stue");
        var cutting = AddPropagation("Coleus", "stue");

        var result = await Service.RenameAsync("Stue", "Living room");

        Assert.Equal("Living room", plant.Location);
        Assert.Equal("Living room", cutting.Location);
        Assert.Equal(1, result.Plants);
        Assert.Equal(1, result.Propagations);
        Assert.False(result.Merged);
    }

    [Fact]
    public async Task Renaming_a_room_takes_its_spots_with_it()
    {
        var pc = AddPlant("Pilea", "Stue / On top of the PC");

        await Service.RenameAsync("Stue", "Living room");

        Assert.Equal("Living room / On top of the PC", pc.Location);
    }

    [Fact]
    public async Task Renaming_onto_a_room_that_already_exists_merges_the_two()
    {
        var danish = AddPlant("Monstera", "Stue");
        var english = AddPlant("Basil", "Living room");

        var result = await Service.RenameAsync("Stue", "Living room");

        Assert.Equal("Living room", danish.Location);
        Assert.Equal("Living room", english.Location);
        Assert.True(result.Merged);
        Assert.Equal(1, result.Plants); // only the one that actually moved
        Assert.Single(await Service.GetAllAsync());
    }

    [Fact]
    public async Task A_spot_can_be_renamed_without_touching_the_rest_of_the_room()
    {
        var pc = AddPlant("Pilea", "Living room / PC");
        var sofa = AddPlant("Monstera", "Living room");

        await Service.RenameAsync("Living room / PC", "Living room / On top of the PC");

        Assert.Equal("Living room / On top of the PC", pc.Location);
        Assert.Equal("Living room", sofa.Location);
    }

    [Fact]
    public async Task Fixing_only_the_spelling_of_a_room_is_not_a_merge()
    {
        var plant = AddPlant("Monstera", "living room");

        var result = await Service.RenameAsync("living room", "Living room");

        Assert.Equal("Living room", plant.Location);
        Assert.False(result.Merged);
        Assert.Equal(1, result.Plants);
    }

    [Fact]
    public async Task Plants_in_other_rooms_are_left_alone()
    {
        var basil = AddPlant("Basil", "Kitchen");
        AddPlant("Monstera", "Stue");

        await Service.RenameAsync("Stue", "Living room");

        Assert.Equal("Kitchen", basil.Location);
    }

    [Fact]
    public async Task A_room_needs_a_name()
    {
        AddPlant("Monstera", "Stue");

        await Assert.ThrowsAsync<ArgumentException>(() => Service.RenameAsync("Stue", "   "));
    }
}
