using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class GrowLightTests
{
    private readonly Place room = new() { Name = "Living room" };
    private readonly Place shelf;
    private readonly Place windowsill;
    private readonly FakeGrowLightRepository repository = new();
    private readonly GrowLightService service;

    public GrowLightTests()
    {
        shelf = new Place { Name = "Shelf", RoomId = room.Id };
        windowsill = new Place { Name = "Windowsill", RoomId = room.Id };
        service = new GrowLightService(repository);
    }

    private Places Places() => new([room, shelf, windowsill]);

    [Fact]
    public void A_light_on_a_spot_lights_the_plants_on_it_only()
    {
        var lights = new[] { new GrowLight { Name = "Lamp", PlaceId = shelf.Id } };

        Assert.True(GrowLights.IsLit(new Plant { PlaceId = shelf.Id }, lights, Places()));
        Assert.False(GrowLights.IsLit(new Plant { PlaceId = windowsill.Id }, lights, Places()));
        Assert.False(GrowLights.IsLit(new Plant { PlaceId = room.Id }, lights, Places()));
        Assert.False(GrowLights.IsLit(new Plant(), lights, Places()));
    }

    [Fact]
    public void A_light_on_a_room_lights_the_spots_in_it()
    {
        var lights = new[] { new GrowLight { Name = "Lamp", PlaceId = room.Id } };

        Assert.True(GrowLights.IsLit(new Plant { PlaceId = room.Id }, lights, Places()));
        Assert.True(GrowLights.IsLit(new Plant { PlaceId = shelf.Id }, lights, Places()));
        Assert.True(GrowLights.IsLit(new Plant { PlaceId = windowsill.Id }, lights, Places()));
    }

    [Fact]
    public void A_deleted_light_lights_nothing()
    {
        var lights = new[] { new GrowLight { Name = "Lamp", PlaceId = room.Id, DeletedAt = DateTimeOffset.UtcNow } };

        Assert.False(GrowLights.IsLit(new Plant { PlaceId = shelf.Id }, lights, Places()));
    }

    [Fact]
    public void A_place_merged_into_another_counts_as_the_other()
    {
        shelf.DeletedAt = DateTimeOffset.UtcNow;
        shelf.MergedIntoId = windowsill.Id;
        var lights = new[] { new GrowLight { Name = "Lamp", PlaceId = windowsill.Id } };

        // A plant placed on the shelf on another device before the merge reached it
        Assert.True(GrowLights.IsLit(new Plant { PlaceId = shelf.Id }, lights, Places()));

        // And a light left on the merged place
        var old = new[] { new GrowLight { Name = "Lamp", PlaceId = shelf.Id } };
        Assert.True(GrowLights.IsLit(new Plant { PlaceId = windowsill.Id }, old, Places()));
    }

    [Fact]
    public async Task Turning_a_place_on_adds_one_light_and_does_not_add_a_second()
    {
        await service.SetLitAsync(room.Id, true);
        await service.SetLitAsync(room.Id, true);

        var light = Assert.Single(await service.GetAllAsync());
        Assert.Equal("Grow light", light.Name);
        Assert.Equal(room.Id, light.PlaceId);
    }

    [Fact]
    public async Task Turning_a_place_off_removes_every_light_pointing_at_it()
    {
        await service.SaveAsync(new GrowLight { Name = "One", PlaceId = shelf.Id });
        await service.SaveAsync(new GrowLight { Name = "Two", PlaceId = shelf.Id });
        await service.SaveAsync(new GrowLight { Name = "Other", PlaceId = windowsill.Id });

        await service.SetLitAsync(shelf.Id, false);

        var left = Assert.Single(await service.GetAllAsync());
        Assert.Equal("Other", left.Name);
        Assert.Equal(2, repository.Lights.Values.Count(l => l.IsDeleted));
    }

    [Fact]
    public async Task A_light_needs_a_name_and_gets_it_trimmed()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new GrowLight { Name = "  " }));

        var light = new GrowLight { Name = "  Tent lamp ", Notes = "  " };
        await service.SaveAsync(light);

        Assert.Equal("Tent lamp", light.Name);
        Assert.Null(light.Notes);
    }
}
