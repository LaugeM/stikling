using System.Globalization;
using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Readings;
using Stikling.Core.Text;

namespace Stikling.Core.Tests;

[Collection("Display choices")]
public sealed class PlaceReadingTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private readonly CultureInfo was = CultureInfo.CurrentCulture;
    private readonly FakePlaceReadingRepository repo = new();
    private readonly FakePlaceRepository placeRepo = new();
    private readonly PlaceReadingService service;
    private readonly Place room = new() { Name = "Living room" };
    private readonly Place spot;

    public PlaceReadingTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        Reset();
        service = new PlaceReadingService(repo, placeRepo);
        spot = new Place { Name = "Window sill", RoomId = room.Id };
        placeRepo.Places[room.Id] = room;
        placeRepo.Places[spot.Id] = spot;
    }

    public void Dispose()
    {
        Reset();
        CultureInfo.CurrentCulture = was;
    }

    private static void Reset()
    {
        Climate.TemperatureUnit = TemperatureUnit.Celsius;
        Lengths.Unit = LengthUnit.Centimetres;
    }

    private async Task<PlaceReading> Add(Guid placeId, int day, int? lux = null, decimal? c = null, int? humidity = null, int? hour = null, string? window = null)
    {
        var reading = new PlaceReading
        {
            PlaceId = placeId,
            Day = new DateOnly(2026, 11, 1).AddDays(day),
            Time = hour is { } h ? new TimeOnly(h, 0) : null,
            Lux = lux,
            TemperatureC = c,
            HumidityPercent = humidity,
            Window = window
        };
        await service.SaveAsync(reading);
        return reading;
    }

    // ---- validation ----

    [Fact]
    public void A_reading_needs_a_place_and_one_value()
    {
        Assert.Empty(new PlaceReading { PlaceId = room.Id, Day = Today, Lux = 100 }.Validate(Today));
        Assert.Empty(new PlaceReading { PlaceId = room.Id, Day = Today, HumidityPercent = 40 }.Validate(Today));
        Assert.NotEmpty(new PlaceReading { PlaceId = room.Id, Day = Today }.Validate(Today));
        Assert.NotEmpty(new PlaceReading { Day = Today, Lux = 100 }.Validate(Today));
        Assert.False(new PlaceReading { Window = "East" }.HasValue);
    }

    [Fact]
    public void A_reading_from_a_day_that_has_not_come_is_not_valid()
    {
        Assert.NotEmpty(new PlaceReading { PlaceId = room.Id, Day = Today.AddDays(1), Lux = 100 }.Validate(Today));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(200000, true)]
    [InlineData(-1, false)]
    [InlineData(200001, false)]
    public void Light_is_between_0_and_200000(int lux, bool valid) =>
        Assert.Equal(valid, new PlaceReading { PlaceId = room.Id, Day = Today, Lux = lux }.Validate(Today).Count == 0);

    [Theory]
    [InlineData(-50, true)]
    [InlineData(60, true)]
    [InlineData(-50.1, false)]
    [InlineData(60.1, false)]
    public void Temperature_is_between_minus_50_and_60(double c, bool valid) =>
        Assert.Equal(valid, new PlaceReading { PlaceId = room.Id, Day = Today, TemperatureC = (decimal)c }.Validate(Today).Count == 0);

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void Humidity_is_between_0_and_100(int percent, bool valid) =>
        Assert.Equal(valid, new PlaceReading { PlaceId = room.Id, Day = Today, HumidityPercent = percent }.Validate(Today).Count == 0);

    // ---- the light guess ----

    [Theory]
    [InlineData(0, LightLevel.Low)]
    [InlineData(2152, LightLevel.Low)]
    [InlineData(2153, LightLevel.Medium)]
    [InlineData(5381, LightLevel.Medium)]
    [InlineData(5382, LightLevel.BrightIndirect)]
    [InlineData(10763, LightLevel.BrightIndirect)]
    [InlineData(10764, LightLevel.DirectSun)]
    public void The_light_guess_changes_at_the_foot_candle_cut_offs(int lux, LightLevel level) =>
        Assert.Equal(level, LightGuess.For(lux));

    // ---- places ----

    [Fact]
    public async Task A_reading_of_a_merged_place_counts_as_the_other_places()
    {
        var other = new Place { Name = "Bedroom" };
        placeRepo.Places[other.Id] = other;
        var old = new Place { Name = "Old room", DeletedAt = DateTimeOffset.UtcNow, MergedIntoId = other.Id };
        placeRepo.Places[old.Id] = old;

        var reading = await Add(old.Id, 0, lux: 500);

        Assert.Equal([reading.Id], (await service.GetForPlaceAsync(other.Id)).Select(r => r.Id));
        Assert.Equal([reading.Id], (await service.GetForPlaceAsync(old.Id)).Select(r => r.Id));
        Assert.Empty(await service.GetForPlaceAsync(room.Id));
    }

    [Fact]
    public async Task A_reading_of_a_removed_place_is_left_out_but_kept()
    {
        var gone = new Place { Name = "Gone", DeletedAt = DateTimeOffset.UtcNow };
        placeRepo.Places[gone.Id] = gone;
        var reading = await Add(gone.Id, 0, lux: 500);

        Assert.Empty(await service.GetForPlaceAsync(gone.Id));
        Assert.Empty(await service.GetForPlaceAsync(room.Id));
        Assert.False((await service.ConditionsForAsync(gone.Id)).Any);
        Assert.True(repo.Readings.ContainsKey(reading.Id));
        Assert.False(repo.Readings[reading.Id].IsDeleted);
    }

    [Fact]
    public async Task A_spot_falls_back_to_its_room_for_each_measure()
    {
        var spotLux = await Add(spot.Id, 2, lux: 3000);
        var roomTemperature = await Add(room.Id, 3, c: 19.5m, humidity: 50);
        await Add(room.Id, 1, lux: 800);

        var conditions = await service.ConditionsForAsync(spot.Id);

        Assert.Equal(spotLux.Id, conditions.Light!.Reading.Id);
        Assert.False(conditions.Light.FromRoom);
        Assert.Equal(roomTemperature.Id, conditions.Temperature!.Reading.Id);
        Assert.True(conditions.Temperature.FromRoom);
        Assert.True(conditions.Humidity!.FromRoom);
        Assert.Equal(roomTemperature.Day, conditions.NewestDay);
    }

    [Fact]
    public async Task A_room_does_not_take_its_spots_readings()
    {
        await Add(spot.Id, 0, lux: 3000, c: 20, humidity: 40);

        var conditions = await service.ConditionsForAsync(room.Id);

        Assert.False(conditions.Any);
        Assert.Null(conditions.NewestDay);
    }

    // ---- lists and curves ----

    [Fact]
    public async Task Readings_are_newest_first_by_day_then_time_with_none_last()
    {
        var earlierDay = await Add(room.Id, 0, lux: 100, hour: 20);
        var noTime = await Add(room.Id, 1, lux: 100);
        var morning = await Add(room.Id, 1, lux: 100, hour: 9);
        var afternoon = await Add(room.Id, 1, lux: 100, hour: 14);
        var noTimeLater = await Add(room.Id, 1, lux: 100);

        var ids = (await service.GetForPlaceAsync(room.Id)).Select(r => r.Id).ToList();

        Assert.Equal([afternoon.Id, morning.Id, noTimeLater.Id, noTime.Id, earlierDay.Id], ids);
    }

    [Fact]
    public async Task A_curve_needs_three_days_and_takes_the_days_newest_reading()
    {
        await Add(room.Id, 0, lux: 100, c: 18);
        await Add(room.Id, 1, lux: 200, c: 19);
        await Add(room.Id, 1, lux: 250, hour: 15);
        await Add(room.Id, 1, lux: 210, hour: 9);

        Assert.Empty(await service.CurvesAsync(room.Id));

        await Add(room.Id, 2, lux: 300, humidity: 40);
        var curves = await service.CurvesAsync(room.Id);

        var light = Assert.Single(curves);
        Assert.Equal(ReadingMeasure.Light, light.Measure);
        Assert.Equal([100m, 250m, 300m], light.Points.Select(p => p.Value));

        await Add(room.Id, 2, humidity: 41);
        await Add(room.Id, 3, c: 20, humidity: 42);
        await Add(room.Id, 4, c: 21);
        await Add(room.Id, 5, humidity: 43);
        curves = await service.CurvesAsync(room.Id);

        Assert.Equal([ReadingMeasure.Light, ReadingMeasure.Temperature, ReadingMeasure.Humidity], curves.Select(c => c.Measure));
    }

    [Fact]
    public async Task Windows_used_are_distinct_ignoring_case_and_newest_first()
    {
        await Add(room.Id, 0, lux: 1, window: "East");
        await Add(room.Id, 1, lux: 1, window: "south window");
        await Add(room.Id, 2, lux: 1, window: "  EAST ");
        await Add(room.Id, 3, lux: 1);
        await Add(spot.Id, 4, lux: 1, window: "Other");

        Assert.Equal(["EAST", "south window"], await service.WindowsUsedAsync(room.Id));
    }

    [Fact]
    public async Task Saving_tidies_the_text_and_rounds_the_temperature()
    {
        var reading = await Add(room.Id, 0, c: 19.54m, window: "  ");
        reading.Notes = "  by the radiator ";
        await service.SaveAsync(reading);

        Assert.Null(reading.Window);
        Assert.Equal("by the radiator", reading.Notes);
        Assert.Equal(19.5m, reading.TemperatureC);

        await service.DeleteAsync(reading.Id);
        Assert.Null(await service.GetAsync(reading.Id));
    }

    // ---- units ----

    [Fact]
    public void Temperature_shows_in_celsius_and_fahrenheit()
    {
        Assert.Equal("19.5 °C", Climate.Temperature(19.5m));
        Assert.Equal("19.5°", Climate.TemperatureShort(19.5m));
        Assert.Equal("°C", Climate.TemperatureUnitName);

        Climate.TemperatureUnit = TemperatureUnit.Fahrenheit;

        Assert.Equal("67.1 °F", Climate.Temperature(19.5m));
        Assert.Equal("67.1°", Climate.TemperatureShort(19.5m));
        Assert.Equal("°F", Climate.TemperatureUnitName);
        Assert.Equal(32, Climate.TemperatureToInput(0));
        Assert.Equal(0, Climate.TemperatureFromInput(32));
        Assert.Equal(20, Climate.TemperatureFromInput(68));
    }

    [Fact]
    public void A_stored_temperature_survives_a_round_trip_through_fahrenheit()
    {
        Climate.TemperatureUnit = TemperatureUnit.Fahrenheit;
        for (var tenths = -500; tenths <= 600; tenths++)
        {
            var c = tenths / 10m;
            Assert.Equal(c, Climate.TemperatureFromInput(Climate.TemperatureToInput(c)));
        }
    }

    [Fact]
    public void Light_shows_in_lux_or_foot_candles_with_the_lengths()
    {
        Assert.Equal("1,200 lux", Climate.Light(1200));
        Assert.Equal("lux", Climate.LightUnitName);
        Assert.Equal(1200, Climate.LightToInput(1200));
        Assert.Equal(1200, Climate.LightFromInput(1200));

        Lengths.Unit = LengthUnit.Inches;

        Assert.Equal("111 fc", Climate.Light(1200));
        Assert.Equal("5.6 fc", Climate.Light(60));
        Assert.Equal("fc", Climate.LightUnitName);
        Assert.Equal(10764, Climate.LightFromInput(1000));
    }

    [Fact]
    public void Stored_light_survives_a_round_trip_through_foot_candles()
    {
        Lengths.Unit = LengthUnit.Inches;
        foreach (var lux in new[] { 0, 1, 7, 60, 1200, 2153, 5382, 10764, 99999, 200000 })
            Assert.Equal(lux, Climate.LightFromInput(Climate.LightToInput(lux)));
    }

    [Fact]
    public void Humidity_has_a_percent_sign() => Assert.Equal("60%", Climate.Humidity(60));

    [Fact]
    public void Temperature_uses_the_decimal_sign_of_the_language()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");

        Assert.Equal("19,5 °C", Climate.Temperature(19.5m));
        Assert.Equal("1.200 lux", Climate.Light(1200));
    }

    // ---- settings ----

    [Fact]
    public void Older_settings_without_a_temperature_unit_read_as_null()
    {
        Assert.Null(JsonSerializer.Deserialize<UserSettings>("""{"Theme":"Dark"}""")!.Temperatures);
        Assert.Null(JsonSerializer.Deserialize<UserSettings>("""{"Temperatures":"Kelvin"}""")!.Temperatures);
        Assert.Equal(TemperatureUnit.Fahrenheit, JsonSerializer.Deserialize<UserSettings>("""{"Temperatures":"Fahrenheit"}""")!.Temperatures);
    }
}
