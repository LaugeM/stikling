using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class RoomNameTests
{
    [Theory]
    [InlineData("  Living room  ", "Living room")]
    [InlineData("Living room/On top of the PC", "Living room / On top of the PC")]
    [InlineData("Living room /  On top of the PC ", "Living room / On top of the PC")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Places_are_tidied_the_same_way_however_they_were_typed(string? typed, string? expected) =>
        Assert.Equal(expected, RoomName.Clean(typed));

    [Fact]
    public void A_place_splits_into_the_room_and_the_spot_inside_it()
    {
        Assert.Equal(("Living room", "On top of the PC"), RoomName.Split("Living room / On top of the PC"));
        Assert.Equal(("Living room", null), RoomName.Split("Living room"));
    }

    [Fact]
    public void A_room_holds_itself_and_its_spots_but_not_another_room()
    {
        Assert.True(RoomName.IsIn("Living room", "living room"));
        Assert.True(RoomName.IsIn("Living room / On top of the PC", "Living room"));
        Assert.False(RoomName.IsIn("Living room", "Living room / On top of the PC"));
        Assert.False(RoomName.IsIn("Living room table", "Living room"));
        Assert.False(RoomName.IsIn("Kitchen", "Living room"));
    }

    [Fact]
    public void Renaming_a_room_keeps_the_spots_inside_it()
    {
        Assert.Equal("Living room", RoomName.Rename("Stue", "Stue", "Living room"));
        Assert.Equal("Living room / Windowsill", RoomName.Rename("Stue / Windowsill", "Stue", "Living room"));
        Assert.Equal("Kitchen", RoomName.Rename("Kitchen", "Stue", "Living room"));
    }
}

public class RoomListTests
{
    private static readonly Plant Thai = new() { Nickname = "Thai", Location = "Living room" };
    private static readonly Plant Monstera = new() { Nickname = "Monstera", Location = "living room " };
    private static readonly Plant Pilea = new() { Nickname = "Pilea", Location = "Living room / On top of the PC" };
    private static readonly Plant Basil = new() { Nickname = "Basil", Location = "Kitchen" };
    private static readonly Plant Homeless = new() { Nickname = "Homeless" };
    private static readonly Plant Deleted = new() { Nickname = "Deleted", Location = "Shed", DeletedAt = DateTimeOffset.UnixEpoch };

    private static readonly Plant[] Plants = [Thai, Monstera, Pilea, Basil, Homeless, Deleted];
    private static readonly Propagation[] Propagations =
    [
        new() { Genus = "Coleus", Location = "Living room / On top of the PC" },
        new() { Genus = "Pothos", Location = "Bathroom" }
    ];

    [Fact]
    public void Rooms_are_distinct_sorted_and_free_of_deleted_plants()
    {
        var rooms = Room.List(Plants, Propagations);

        Assert.Equal(["Bathroom", "Kitchen", "Living room"], rooms.Select(r => r.Name));
    }

    [Fact]
    public void A_spot_is_listed_under_its_room_not_beside_it()
    {
        var living = Room.List(Plants, Propagations).Single(r => r.Name == "Living room");

        Assert.Equal(["On top of the PC"], living.Spots.Select(s => s.Name));
        Assert.Equal("Living room / On top of the PC", living.Spots[0].Place);
    }

    [Fact]
    public void A_room_counts_what_sits_in_it_and_what_sits_in_its_spots()
    {
        var living = Room.List(Plants, Propagations).Single(r => r.Name == "Living room");

        Assert.Equal(2, living.Plants); // Thai and Monstera, not Pilea on the PC
        Assert.Equal(3, living.AllPlants);
        Assert.Equal(1, living.AllPropagations);
        Assert.Equal(4, living.Total);
    }

    [Fact]
    public void A_room_nothing_sits_in_directly_still_appears_for_its_spots()
    {
        var rooms = Room.List([new Plant { Nickname = "Pilea", Location = "Office / Shelf" }], []);

        Assert.Equal("Office", rooms.Single().Name);
        Assert.Equal(0, rooms.Single().Plants);
        Assert.Equal(1, rooms.Single().AllPlants);
    }
}
