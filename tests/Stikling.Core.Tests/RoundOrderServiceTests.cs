using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class RoundOrderServiceTests
{
    private readonly FakePlaceRepository places = new();
    private RoomService Service => new(places, new FakePlantRepository(), new FakePropagationRepository());

    [Fact]
    public async Task The_order_is_kept_on_the_room_and_cleared_with_null()
    {
        var living = places.Add("Living room");
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await Service.SetRoundOrderAsync(living.Id, ids);
        Assert.Equal(ids, living.RoundOrder);

        await Service.SetRoundOrderAsync(living.Id, null);
        Assert.Null(living.RoundOrder);
    }

    [Fact]
    public async Task An_empty_order_is_the_usual_order()
    {
        var living = places.Add("Living room");
        await Service.SetRoundOrderAsync(living.Id, [Guid.NewGuid()]);

        await Service.SetRoundOrderAsync(living.Id, []);

        Assert.Null(living.RoundOrder);
    }

    [Fact]
    public async Task A_spot_has_no_order_of_its_own()
    {
        var living = places.Add("Living room");
        var sill = places.Add("Windowsill", living);

        await Assert.ThrowsAsync<ArgumentException>(() => Service.SetRoundOrderAsync(sill.Id, [Guid.NewGuid()]));
    }

    [Fact]
    public void A_place_from_before_Moving_reads_with_no_order_or_mark()
    {
        // Records saved before these fields existed read as null
        var place = System.Text.Json.JsonSerializer.Deserialize<Place>("""{"name":"Old","id":"3f2f0b7e-6f3c-4a6e-9d34-1b7c1a9d2c11"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Null(place.LeftOn);
        Assert.Null(place.RoundOrder);
    }

    [Fact]
    public void An_event_this_version_does_not_know_reads_as_null()
    {
        var entry = System.Text.Json.JsonSerializer.Deserialize<TimelineEntry>("""{"event":"SomethingNew"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var moved = System.Text.Json.JsonSerializer.Deserialize<TimelineEntry>("""{"event":"MovedHouse"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Null(entry.Event);
        Assert.Equal(TimelineEvent.MovedHouse, moved.Event);
    }
}
