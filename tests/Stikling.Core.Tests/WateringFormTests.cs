using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class WateringFormTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static string Label(Enum value) => value.ToString();

    [Theory]
    [InlineData(GrowingMedium.Soil, WateringForm.TopWatered)]
    [InlineData(GrowingMedium.OrchidBark, WateringForm.TopWatered)]
    [InlineData(GrowingMedium.Perlite, WateringForm.TopWatered)]
    [InlineData(GrowingMedium.Sphagnum, WateringForm.TopWatered)]
    [InlineData(GrowingMedium.Other, WateringForm.TopWatered)]
    [InlineData(GrowingMedium.Leca, WateringForm.Reservoir)]
    [InlineData(GrowingMedium.Pon, WateringForm.Reservoir)]
    [InlineData(GrowingMedium.CormRiser, WateringForm.Reservoir)]
    [InlineData(GrowingMedium.Water, WateringForm.InWater)]
    public void The_medium_decides_the_form(GrowingMedium medium, WateringForm expected) =>
        Assert.Equal(expected, WateringForms.Of(new Plant { Medium = medium }));

    [Fact]
    public void Water_in_the_outer_pot_makes_it_a_reservoir()
    {
        Assert.Equal(WateringForm.Reservoir, WateringForms.Of(new Plant { WaterInOuterPot = true }));
    }

    [Fact]
    public void A_self_watering_pot_makes_it_a_reservoir_when_the_pot_can_be_looked_up()
    {
        var inner = new Pot { Name = "Wick pot", SelfWatering = PotWatering.Wick };
        var outer = new Pot { Name = "Ceramic" };
        var pots = new[] { inner, outer }.ToDictionary(p => p.Id);
        Pot? Find(Guid id) => pots.GetValueOrDefault(id);

        Assert.Equal(WateringForm.Reservoir, WateringForms.Of(new Plant { InnerPotId = inner.Id }, Find));
        Assert.Equal(WateringForm.Reservoir, WateringForms.Of(new Plant { OuterPotId = inner.Id }, Find));
        Assert.Equal(WateringForm.TopWatered, WateringForms.Of(new Plant { InnerPotId = outer.Id }, Find));
        // Without the lookup the pot is taken to be an ordinary one
        Assert.Equal(WateringForm.TopWatered, WateringForms.Of(new Plant { InnerPotId = inner.Id }));
    }

    [Fact]
    public async Task A_new_form_starts_the_watering_history_over()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var before = new Plant { Nickname = "Hoya", Medium = GrowingMedium.Soil };
        var after = before.Copy();
        after.Medium = GrowingMedium.Leca;

        await service.UpdateAsync(before, after, Label);

        Assert.Equal(new DateOnly(2026, 9, 19), after.WateringSince);
    }

    [Fact]
    public async Task A_self_watering_pot_found_through_the_lookup_starts_it_over()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var wick = new Pot { Name = "Wick pot", SelfWatering = PotWatering.Wick };
        var before = new Plant { Nickname = "Hoya" };
        var after = before.Copy();
        after.InnerPotId = wick.Id;

        await service.UpdateAsync(before, after, Label, pot: id => id == wick.Id ? wick : null);

        Assert.Equal(new DateOnly(2026, 9, 19), after.WateringSince);
    }

    [Fact]
    public async Task A_repot_in_the_same_form_keeps_what_it_learned()
    {
        var plants = new FakePlantRepository();
        var service = new PlantService(plants, new FakeTimelineRepository(), new FixedTime(Now));
        var since = new DateOnly(2026, 1, 1);
        var before = new Plant { Nickname = "Hoya", WateringSince = since };
        var after = before.Copy();
        after.InnerPotId = Guid.NewGuid();
        after.Medium = GrowingMedium.OrchidBark;

        await service.UpdateAsync(before, after, Label);

        Assert.Equal(since, after.WateringSince);
    }

    [Fact]
    public void The_watering_level_goes_from_1_to_10()
    {
        Assert.Empty(new Plant { Nickname = "A", WaterAt = 1 }.Validate(DateOnly.MaxValue));
        Assert.Empty(new Plant { Nickname = "A", WaterAt = 10 }.Validate(DateOnly.MaxValue));
        Assert.NotEmpty(new Plant { Nickname = "A", WaterAt = 0 }.Validate(DateOnly.MaxValue));
        Assert.NotEmpty(new Plant { Nickname = "A", WaterAt = 11 }.Validate(DateOnly.MaxValue));
    }

    [Fact]
    public void A_plant_saved_before_the_reminder_existed_has_it_on()
    {
        var plant = System.Text.Json.JsonSerializer.Deserialize<Plant>("""{"nickname":"Old"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.True(plant.WateringReminder);
        Assert.Null(plant.WaterAt);
        Assert.Null(plant.WateringSince);
        Assert.True(plant.Copy().WateringReminder);
    }

    [Fact]
    public void The_watering_reminder_setting_is_off_until_turned_on() =>
        Assert.False(new UserSettings().WateringReminder);

    [Fact]
    public void The_northern_hemisphere_is_found_from_the_time_zone()
    {
        if (Zone("Europe/Copenhagen") is { } zone)
            Assert.Equal(Hemisphere.Northern, Seasons.HemisphereOf(zone, 2026));
    }

    [Fact]
    public void The_southern_hemisphere_is_found_from_summer_time_in_january()
    {
        if (Zone("Australia/Sydney") is { } zone)
            Assert.Equal(Hemisphere.Southern, Seasons.HemisphereOf(zone, 2026));
    }

    [Fact]
    public void A_southern_zone_without_summer_time_is_found_by_its_name()
    {
        var lima = TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "Lima", "Lima");
        var utc = TimeZoneInfo.CreateCustomTimeZone("Etc/Test", TimeSpan.Zero, "Test", "Test");

        Assert.Equal(Hemisphere.Southern, Seasons.HemisphereOf(lima, 2026));
        Assert.Equal(Hemisphere.Northern, Seasons.HemisphereOf(utc, 2026));
    }

    // A machine without the time zone data can't run the zone tests, so they pass without checking
    private static TimeZoneInfo? Zone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
    }

    [Fact]
    public void Days_shorten_from_midsummer_to_midwinter_and_the_other_way_in_the_south()
    {
        Assert.False(Seasons.DaysShortening(new DateOnly(2026, 6, 20), Hemisphere.Northern));
        Assert.True(Seasons.DaysShortening(new DateOnly(2026, 6, 21), Hemisphere.Northern));
        Assert.True(Seasons.DaysShortening(new DateOnly(2026, 12, 20), Hemisphere.Northern));
        Assert.False(Seasons.DaysShortening(new DateOnly(2026, 12, 21), Hemisphere.Northern));
        Assert.False(Seasons.DaysShortening(new DateOnly(2026, 3, 1), Hemisphere.Northern));

        Assert.True(Seasons.DaysShortening(new DateOnly(2026, 12, 21), Hemisphere.Southern));
        Assert.True(Seasons.DaysShortening(new DateOnly(2026, 3, 1), Hemisphere.Southern));
        Assert.False(Seasons.DaysShortening(new DateOnly(2026, 10, 9), Hemisphere.Southern));
    }

    [Fact]
    public void The_same_time_of_year_wraps_round_new_year()
    {
        Assert.True(Seasons.SameTimeOfYear(new DateOnly(2025, 12, 20), new DateOnly(2026, 1, 10)));
        Assert.False(Seasons.SameTimeOfYear(new DateOnly(2025, 10, 1), new DateOnly(2026, 1, 10)));
        Assert.True(Seasons.SameTimeOfYear(new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 20)));
        Assert.False(Seasons.SameTimeOfYear(new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 20), withinDays: 10));
    }
}
