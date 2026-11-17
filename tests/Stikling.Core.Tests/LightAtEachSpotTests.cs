using System.Text.Json;
using Stikling.Core.Care;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Readings;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class LightCheckTests
{
    [Fact]
    public void Nothing_is_guessed_until_all_three_are_answered()
    {
        Assert.Null(LightCheck.Guess(null, WindowDistance.AtWindow, Shade.Nothing));
        Assert.Null(LightCheck.Guess(SunHours.OverFour, null, Shade.Nothing));
        Assert.Null(LightCheck.Guess(SunHours.OverFour, WindowDistance.AtWindow, null));
    }

    [Theory]
    [InlineData(SunHours.None, LightLevel.Medium)]
    [InlineData(SunHours.UnderTwo, LightLevel.BrightIndirect)]
    [InlineData(SunHours.TwoToFour, LightLevel.BrightIndirect)]
    [InlineData(SunHours.OverFour, LightLevel.DirectSun)]
    public void Sun_sets_the_start(SunHours sun, LightLevel expected) =>
        Assert.Equal(expected, LightCheck.Guess(sun, WindowDistance.AtWindow, Shade.Nothing));

    [Theory]
    [InlineData(WindowDistance.AtWindow, LightLevel.DirectSun)]
    [InlineData(WindowDistance.UnderOneMetre, LightLevel.DirectSun)]
    [InlineData(WindowDistance.OneToTwoMetres, LightLevel.BrightIndirect)]
    [InlineData(WindowDistance.OverTwoMetres, LightLevel.Medium)]
    public void Distance_takes_levels_off(WindowDistance distance, LightLevel expected) =>
        Assert.Equal(expected, LightCheck.Guess(SunHours.OverFour, distance, Shade.Nothing));

    [Theory]
    [InlineData(Shade.Nothing, LightLevel.DirectSun)]
    [InlineData(Shade.Some, LightLevel.BrightIndirect)]
    [InlineData(Shade.Lots, LightLevel.Medium)]
    public void Shade_takes_levels_off(Shade shade, LightLevel expected) =>
        Assert.Equal(expected, LightCheck.Guess(SunHours.OverFour, WindowDistance.AtWindow, shade));

    [Fact]
    public void The_guess_never_goes_below_low()
    {
        Assert.Equal(LightLevel.Low, LightCheck.Guess(SunHours.None, WindowDistance.OverTwoMetres, Shade.Lots));
        Assert.Equal(LightLevel.Low, LightCheck.Guess(SunHours.UnderTwo, WindowDistance.OverTwoMetres, Shade.Lots));
    }

    [Fact]
    public void A_spot_without_answers_uses_its_rooms()
    {
        var room = new Place { Name = "Living room", SunHours = SunHours.TwoToFour, FromWindow = WindowDistance.AtWindow, InTheWay = Shade.Nothing };
        var spot = new Place { Name = "Shelf", RoomId = room.Id };

        Assert.Equal((LightLevel.BrightIndirect, true), LightText.CheckOf(spot, room));
        Assert.Equal((LightLevel.BrightIndirect, false), LightText.CheckOf(room, null));

        spot.SunHours = SunHours.None;
        spot.FromWindow = WindowDistance.AtWindow;
        spot.InTheWay = Shade.Nothing;
        Assert.Equal((LightLevel.Medium, false), LightText.CheckOf(spot, room));
    }
}

public class PlaceLightStorageTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Windows_and_answers_are_stored_as_text()
    {
        var place = new Place
        {
            Name = "Room",
            Windows = [WindowDirection.South, WindowDirection.Roof],
            SunHours = SunHours.UnderTwo,
            FromWindow = WindowDistance.OneToTwoMetres,
            InTheWay = Shade.Some
        };

        var json = JsonSerializer.Serialize(place, Options);
        Assert.Contains("\"South\"", json);
        Assert.Contains("\"UnderTwo\"", json);

        var back = JsonSerializer.Deserialize<Place>(json, Options)!;
        Assert.Equal([WindowDirection.South, WindowDirection.Roof], back.Windows);
        Assert.Equal(Shade.Some, back.InTheWay);
    }

    [Fact]
    public void Names_this_version_does_not_know_are_dropped_instead_of_failing()
    {
        var json = """{"name":"Room","windows":["South","Skylight","West"],"sunHours":"AllDay","fromWindow":"AtWindow","inTheWay":"Heaps"}""";

        var place = JsonSerializer.Deserialize<Place>(json, Options)!;

        Assert.Equal([WindowDirection.South, WindowDirection.West], place.Windows);
        Assert.Null(place.SunHours);
        Assert.Equal(WindowDistance.AtWindow, place.FromWindow);
        Assert.Null(place.InTheWay);
    }

    [Fact]
    public void A_place_from_before_has_none_of_it()
    {
        var place = JsonSerializer.Deserialize<Place>("""{"name":"Room"}""", Options)!;

        Assert.Null(place.Windows);
        Assert.Null(place.SunHours);
        Assert.Null(place.FromWindow);
        Assert.Null(place.InTheWay);
    }

    [Fact]
    public void An_old_grow_light_reads_with_no_watts_or_hours()
    {
        var light = JsonSerializer.Deserialize<GrowLight>("""{"name":"Lamp"}""", Options)!;

        Assert.Null(light.Watts);
        Assert.Null(light.HoursPerDay);
        Assert.Empty(light.Validate());
    }

    [Theory]
    [InlineData(0, null, false)]
    [InlineData(5001, null, false)]
    [InlineData(1, null, true)]
    [InlineData(5000, 24.0, true)]
    [InlineData(null, 0.0, true)]
    [InlineData(null, 12.5, true)]
    [InlineData(null, 12.3, false)]
    [InlineData(null, 24.5, false)]
    [InlineData(null, -0.5, false)]
    public void Watts_and_hours_are_checked(int? watts, double? hours, bool valid)
    {
        var light = new GrowLight { Name = "Lamp", Watts = watts, HoursPerDay = hours is { } h ? (decimal)h : null };

        Assert.Equal(valid, light.Validate().Count == 0);
    }

    [Fact]
    public void The_windows_are_written_in_order_with_and()
    {
        Assert.Equal("South window", LightText.Windows([WindowDirection.South]));
        Assert.Equal("South and west windows", LightText.Windows([WindowDirection.West, WindowDirection.South]));
        Assert.Equal("North, south and roof windows", LightText.Windows([WindowDirection.Roof, WindowDirection.South, WindowDirection.North]));
        Assert.Equal("", LightText.Windows([]));
    }
}

public class GrowLightHistoryTests
{
    private readonly Place room = new() { Name = "Living room" };
    private readonly Place shelf;
    private readonly Place bedroom = new() { Name = "Bedroom" };
    private readonly FakeGrowLightRepository lights = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakePlaceRepository places = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly GrowLightService service;

    public GrowLightHistoryTests()
    {
        shelf = new Place { Name = "Shelf", RoomId = room.Id };
        foreach (var place in new[] { room, shelf, bedroom })
            places.Places[place.Id] = place;
        service = new GrowLightService(lights, plants, propagations, places, timeline, new FixedTime(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)));
    }

    private async Task<Plant> PlantAt(Place place, PlantStatus status = PlantStatus.Active)
    {
        var plant = new Plant { Nickname = "Plant", PlaceId = place.Id, Status = status };
        await plants.SaveAsync(plant);
        return plant;
    }

    private async Task<string[]> HistoryOf(Guid id) =>
        [.. (await timeline.GetForAsync(id)).Select(e => e.Text ?? "")];

    [Fact]
    public async Task A_light_on_a_room_writes_a_line_for_plants_in_the_room_and_its_spots()
    {
        var inRoom = await PlantAt(room);
        var onShelf = await PlantAt(shelf);
        var elsewhere = await PlantAt(bedroom);
        var gone = await PlantAt(room, PlantStatus.Died);
        var cutting = new Propagation { Nickname = "Cutting", PlaceId = shelf.Id };
        await propagations.SaveAsync(cutting);

        await service.SaveAsync(new GrowLight { Name = "Shelf LED", PlaceId = room.Id });

        Assert.Equal(["Got a grow light: Shelf LED"], await HistoryOf(inRoom.Id));
        Assert.Equal(["Got a grow light: Shelf LED"], await HistoryOf(onShelf.Id));
        Assert.Equal(["Got a grow light: Shelf LED"], await HistoryOf(cutting.Id));
        Assert.Empty(await HistoryOf(elsewhere.Id));
        Assert.Empty(await HistoryOf(gone.Id));
    }

    [Fact]
    public async Task Changing_a_light_other_than_its_place_writes_nothing()
    {
        var plant = await PlantAt(shelf);
        var light = new GrowLight { Name = "Lamp", PlaceId = shelf.Id };
        await service.SaveAsync(light);

        await service.SaveAsync(new GrowLight { Id = light.Id, Name = "Bigger lamp", PlaceId = shelf.Id, Watts = 40, HoursPerDay = 12 });

        Assert.Equal(["Got a grow light: Lamp"], await HistoryOf(plant.Id));
    }

    [Fact]
    public async Task Moving_a_light_is_a_removal_where_it_was_and_an_addition_where_it_went()
    {
        var onShelf = await PlantAt(shelf);
        var inBedroom = await PlantAt(bedroom);
        var light = new GrowLight { Name = "Lamp", PlaceId = shelf.Id };
        await service.SaveAsync(light);

        // The repository of the app gives a copy, so the light is moved on a copy here too
        await service.SaveAsync(new GrowLight { Id = light.Id, Name = "Lamp", PlaceId = bedroom.Id });

        Assert.Equal(["Got a grow light: Lamp", "Grow light removed: Lamp"], await HistoryOf(onShelf.Id));
        Assert.Equal(["Got a grow light: Lamp"], await HistoryOf(inBedroom.Id));
    }

    [Fact]
    public async Task Moving_a_light_from_a_room_to_its_own_spot_leaves_plants_that_stay_lit_alone()
    {
        var onShelf = await PlantAt(shelf);
        var inRoom = await PlantAt(room);
        var light = new GrowLight { Name = "Lamp", PlaceId = room.Id };
        await service.SaveAsync(light);

        await service.SaveAsync(new GrowLight { Id = light.Id, Name = "Lamp", PlaceId = shelf.Id });

        Assert.Equal(["Got a grow light: Lamp"], await HistoryOf(onShelf.Id));
        Assert.Equal(["Got a grow light: Lamp", "Grow light removed: Lamp"], await HistoryOf(inRoom.Id));
    }

    [Fact]
    public async Task Removing_a_light_writes_a_line_and_turning_a_place_on_and_off_does_too()
    {
        var plant = await PlantAt(room);
        await service.SetLitAsync(room.Id, true);
        await service.SetLitAsync(room.Id, false);

        Assert.Equal(["Got a grow light: Grow light", "Grow light removed: Grow light"], await HistoryOf(plant.Id));
    }

    [Fact]
    public void A_move_says_when_the_plant_goes_under_a_light_or_out_from_under_it()
    {
        var all = new[] { new GrowLight { Name = "Shelf LED", PlaceId = shelf.Id }, new GrowLight { Name = "Lamp", PlaceId = shelf.Id } };
        var known = new Places([room, shelf, bedroom]);

        Assert.Equal("Now under a grow light: Shelf LED, Lamp",
            HistoryText.English(GrowLights.MoveLines(bedroom.Id, shelf.Id, all, known), e => e.ToString()));
        Assert.Equal("No longer under a grow light",
            HistoryText.English(GrowLights.MoveLines(shelf.Id, bedroom.Id, all, known), e => e.ToString()));
        Assert.Empty(GrowLights.MoveLines(room.Id, bedroom.Id, all, known));
        Assert.Empty(GrowLights.MoveLines(shelf.Id, shelf.Id, all, known));
    }

    [Fact]
    public async Task A_move_through_the_plant_service_gets_the_extra_line_in_the_same_entry()
    {
        await service.SaveAsync(new GrowLight { Name = "Shelf LED", PlaceId = shelf.Id });
        var plantService = new PlantService(plants, timeline, new FixedTime(DateTimeOffset.UtcNow));
        var plant = await PlantAt(bedroom);
        var names = await places.GetPlacesAsync();

        await plantService.MoveAsync([plant], shelf.Id, e => e.ToString(), id => names.NameOf(id), await service.MoveLinesAsync());

        var entry = Assert.Single(await timeline.GetForAsync(plant.Id));
        Assert.Equal("Moved from Bedroom to Living room / Shelf\nNow under a grow light: Shelf LED", entry.Text);
    }
}

public class WinterLightTests
{
    private readonly Place room = new() { Name = "Living room" };
    private readonly Place balcony = new() { Name = "Balcony", Outdoors = true };

    private Places Known() => new([room, balcony]);

    [Theory]
    [InlineData(2026, 10, 14, false)]
    [InlineData(2026, 10, 15, true)]
    [InlineData(2026, 12, 31, true)]
    [InlineData(2027, 2, 28, true)]
    [InlineData(2027, 3, 1, false)]
    [InlineData(2028, 2, 29, true)]
    public void The_northern_season_runs_over_new_year(int year, int month, int day, bool inSeason) =>
        Assert.Equal(inSeason, WinterLight.SeasonOf(new DateOnly(year, month, day), Hemisphere.Northern) is not null);

    [Theory]
    [InlineData(2026, 4, 14, false)]
    [InlineData(2026, 4, 15, true)]
    [InlineData(2026, 8, 31, true)]
    [InlineData(2026, 9, 1, false)]
    [InlineData(2026, 12, 1, false)]
    public void The_southern_season_is_the_other_half(int year, int month, int day, bool inSeason) =>
        Assert.Equal(inSeason, WinterLight.SeasonOf(new DateOnly(year, month, day), Hemisphere.Southern) is not null);

    [Fact]
    public void The_key_and_the_return_day_come_from_the_season()
    {
        var season = WinterLight.SeasonOf(new DateOnly(2027, 1, 10), Hemisphere.Northern)!.Value;

        Assert.Equal("winter-light:2026", WinterLight.Key(season.Start));
        Assert.Equal(new DateOnly(2027, 3, 1), WinterLight.PutOffUntil(season));
    }

    [Fact]
    public void Plants_that_like_bright_light_and_have_no_lamp_are_listed()
    {
        var day = new DateOnly(2026, 11, 17);
        var bright = new Plant { Nickname = "Bright", LikesLight = LightLevel.BrightIndirect, PlaceId = room.Id };
        var sun = new Plant { Nickname = "Sun", LikesLight = LightLevel.DirectSun, PlaceId = room.Id };
        var lit = new Plant { Nickname = "Lit", LikesLight = LightLevel.DirectSun, PlaceId = room.Id };
        var low = new Plant { Nickname = "Low", LikesLight = LightLevel.Low, PlaceId = room.Id };
        var outside = new Plant { Nickname = "Out", LikesLight = LightLevel.DirectSun, PlaceId = balcony.Id };
        var gone = new Plant { Nickname = "Gone", LikesLight = LightLevel.DirectSun, PlaceId = room.Id, Status = PlantStatus.Died };
        var other = new Plant { Nickname = "Other", LikesLight = LightLevel.DirectSun, PlaceId = room.Id };
        var potted = new Plant { Nickname = "Potted", LikesLight = LightLevel.DirectSun, PlaceId = room.Id, PottedWithPlantId = other.Id };
        var all = new[] { bright, sun, low, outside, gone, other, potted };

        var listed = WinterLight.Plants(all, [], Known(), day, Hemisphere.Northern, PutOffs.Empty());
        Assert.Equal(["Bright", "Other", "Sun"], listed.Select(p => p.DisplayName));

        // A lamp on the room takes them off the list
        var lamp = new GrowLight { Name = "Lamp", PlaceId = room.Id };
        Assert.Empty(WinterLight.Plants([.. all, lit], [lamp], Known(), day, Hemisphere.Northern, PutOffs.Empty()));
    }

    [Fact]
    public void Nothing_is_listed_out_of_season_or_once_put_off_for_the_season()
    {
        var plant = new Plant { Nickname = "Bright", LikesLight = LightLevel.DirectSun, PlaceId = room.Id };

        Assert.Empty(WinterLight.Plants([plant], [], Known(), new DateOnly(2026, 7, 1), Hemisphere.Northern, PutOffs.Empty()));

        var season = WinterLight.SeasonOf(new DateOnly(2026, 11, 17), Hemisphere.Northern)!.Value;
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(WinterLight.Key(season.Start), WinterLight.PutOffUntil(season));
        Assert.Empty(WinterLight.Plants([plant], [], Known(), new DateOnly(2027, 1, 20), Hemisphere.Northern, putOffs));
        Assert.Single(WinterLight.Plants([plant], [], Known(), new DateOnly(2027, 3, 1).AddDays(-1), Hemisphere.Northern, PutOffs.Empty()));
    }
}

public class PlantFilterLightTests
{
    [Fact]
    public void The_light_filters_pick_lit_plants_and_the_light_where_it_stands()
    {
        var lit = new Plant { Nickname = "Lit", Light = LightLevel.Medium };
        var bright = new Plant { Nickname = "Bright", Light = LightLevel.BrightIndirect };
        var unset = new Plant { Nickname = "Unset" };
        Plant[] all = [lit, bright, unset];

        Assert.Equal(["Lit"], new PlantFilter(UnderGrowLight: true, IsLit: p => p.Id == lit.Id).Apply(all).Select(p => p.DisplayName));
        Assert.Empty(new PlantFilter(UnderGrowLight: true).Apply(all));
        Assert.Equal(["Bright"], new PlantFilter(LightAt: LightLevel.BrightIndirect).Apply(all).Select(p => p.DisplayName));
        Assert.Equal(3, new PlantFilter().Apply(all).Count());
    }
}
