using Stikling.Core.Models;
using Stikling.Core.Settings;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>Easy day: Today shows only what is worse for waiting, and only on the day it was turned on.</summary>
public class EasyDayTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<Propagation> propagations = [];
    private readonly List<CareLog> logs = [];
    private readonly List<AwayStretch> stretches = [];
    private DateOnly? easyDay;

    private TodaySections Sections() => TodaySections.For(new TodayData
    {
        Plants = plants,
        Propagations = propagations,
        CareLogs = logs,
        Stretches = stretches,
        Settings = new UserSettings { WateringReminder = true, EasyDay = easyDay }
    }, Today, Time);

    private void Flagged() =>
        plants.Add(new Plant { Nickname = "Flagged", Attention = new Attention("Yellow leaves", Today.AddDays(-1)) });

    private void Quarantined() =>
        plants.Add(new Plant { Nickname = "New", QuarantinedSince = Today.AddDays(-20) });

    private void Thirsty()
    {
        var plant = new Plant { Nickname = "Thirsty" };
        plants.Add(plant);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-14 - 7 * i) });
    }

    private void WaterToChange() => propagations.Add(new Propagation
    {
        Nickname = "Cutting",
        Medium = GrowingMedium.Water,
        StartedOn = Today.AddDays(-10),
        WaterChangeDays = 3
    });

    [Fact]
    public void It_is_off_unless_the_day_matches_today()
    {
        Assert.False(Sections().IsEasyDay);

        easyDay = Today.AddDays(-1);
        Assert.False(Sections().IsEasyDay);

        easyDay = Today;
        Assert.True(Sections().IsEasyDay);
    }

    [Fact]
    public void An_easy_day_keeps_flagged_plants_and_water_changes_and_drops_the_rest()
    {
        Flagged();
        Quarantined();
        Thirsty();
        WaterToChange();
        easyDay = Today;
        var full = Sections();
        Assert.NotEmpty(full.WaterNow);
        Assert.NotEmpty(full.QuarantineNow);

        var shown = full.Shown();

        Assert.Single(shown.FlaggedNow);
        Assert.Single(shown.WaterChanges);
        Assert.Empty(shown.WaterNow);
        Assert.Empty(shown.WateringDue);
        Assert.Empty(shown.QuarantineNow);
        Assert.Equal(2, shown.DueCount);
    }

    [Fact]
    public void A_normal_day_shows_everything()
    {
        Flagged();
        Thirsty();

        var shown = Sections().Shown();

        Assert.NotEmpty(shown.WaterNow);
        Assert.Single(shown.FlaggedNow);
    }

    [Fact]
    public void Away_time_still_shows_and_nothing_is_counted()
    {
        Flagged();
        stretches.Add(new AwayStretch { From = Today, Until = Today.AddDays(3) });
        easyDay = Today;

        var shown = Sections().Shown();

        Assert.NotNull(shown.AwayNow);
        Assert.Equal(0, shown.DueCount);
    }

    [Fact]
    public async Task Turning_it_on_and_off_is_kept_in_the_settings()
    {
        var service = new SettingsService(new FakeSettingsRepository());

        await service.SetEasyDayAsync(Today);
        Assert.Equal(Today, (await service.GetAsync()).EasyDay);

        await service.SetEasyDayAsync(null);
        Assert.Null((await service.GetAsync()).EasyDay);
    }
}
