using System.Text.Json;
using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class ReservoirTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private static string Label(Enum value) => value.ToString();

    private readonly FakeCareLogRepository logs = new();
    private readonly FakePutOffRepository putOffRecords = new();
    private readonly WateringAnswers answers;
    private readonly CareService care;
    private readonly Plant plant = new() { Nickname = "P", Medium = GrowingMedium.Leca };

    public ReservoirTests()
    {
        care = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), Time);
        answers = new WateringAnswers(care, logs, new PutOffService(putOffRecords), Time);
    }

    private static CareLog Reading(Guid plantId, int? level, DateOnly? on = null) => new()
    {
        PlantId = plantId,
        Kind = CareKind.Rotated,
        OccurredOn = on ?? Today,
        OwnKindId = CareKinds.ReservoirReadingId,
        OwnKindName = "Reservoir level",
        ReservoirLevel = level
    };

    private static WateringGuess Guess() =>
        new(WateringForm.Reservoir, WateringMode.Interval, Today.AddDays(-9), 9, 8, 4, 0, Today, null, false, false);

    // Validation

    [Fact]
    public void A_reading_needs_a_level_and_the_level_goes_from_0_to_100()
    {
        Assert.Contains("Say how full the reservoir is.", Reading(Guid.NewGuid(), null).Validate(Today));
        Assert.Contains("How full the reservoir is goes from 0 to 100.", Reading(Guid.NewGuid(), 101).Validate(Today));
        Assert.Contains("How full the reservoir is goes from 0 to 100.", Reading(Guid.NewGuid(), -1).Validate(Today));
        Assert.Empty(Reading(Guid.NewGuid(), 0).Validate(Today));
        Assert.Empty(Reading(Guid.NewGuid(), 100).Validate(Today));
    }

    [Fact]
    public void Only_a_reading_has_a_level()
    {
        var watered = new CareLog { PlantId = Guid.NewGuid(), Kind = CareKind.Watered, OccurredOn = Today, ReservoirLevel = 50 };

        Assert.Contains("Only a reservoir reading has a level.", watered.Validate(Today));
    }

    [Fact]
    public void A_reading_is_recognised_by_its_fixed_id()
    {
        Assert.True(CareKinds.IsReservoirReading(Reading(Guid.NewGuid(), 50)));
        Assert.False(CareKinds.IsReservoirReading(new CareLog { Kind = CareKind.Rotated, OwnKindId = Guid.NewGuid() }));
        Assert.False(CareKinds.IsReservoirReading(new CareLog { Kind = CareKind.Rotated }));
    }

    [Fact]
    public void A_fill_line_has_to_be_more_than_nothing()
    {
        Assert.Contains("The fill line has to be more than 0.", new Plant { Nickname = "P", ReservoirFillCm = 0 }.Validate(Today));
        Assert.Contains("The fill line has to be more than 0.", new Plant { Nickname = "P", ReservoirFillCm = -2 }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "P", ReservoirFillCm = 3.5m }.Validate(Today));
        Assert.Empty(new Plant { Nickname = "P" }.Validate(Today));
    }

    // Words

    [Theory]
    [InlineData(0, "Empty")]
    [InlineData(1, "Low")]
    [InlineData(25, "Low")]
    [InlineData(37, "Low")]
    [InlineData(38, "Half")]
    [InlineData(50, "Half")]
    [InlineData(75, "Half")]
    [InlineData(76, "Full")]
    [InlineData(100, "Full")]
    public void A_level_reads_as_the_nearest_word(int level, string expected)
    {
        Assert.Equal(expected, ReservoirLevels.Text(level));
    }

    [Fact]
    public void The_four_choices_read_as_themselves()
    {
        Assert.All(ReservoirLevels.Choices, level => Assert.Equal(level, ReservoirLevels.Nearest(level)));
    }

    [Fact]
    public void A_reading_is_called_by_its_level_in_the_current_language_and_not_the_stored_name()
    {
        var entry = Reading(Guid.NewGuid(), 50);
        entry.OwnKindName = "Vandstand";

        Assert.Equal("Reservoir: half", CareLabels.OwnName(entry, null));
        Assert.Equal("Reservoir: half", CareLabels.For(entry, null, Label));
    }

    // Reading back

    [Fact]
    public void The_latest_reading_has_its_level_and_age()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var all = new[]
        {
            Reading(id, 25, Today.AddDays(-6)),
            Reading(id, 100, Today.AddDays(-3)),
            Reading(other, 0, Today),
            new CareLog { PlantId = id, Kind = CareKind.Watered, OccurredOn = Today }
        };

        var latest = CareSummary.LatestReservoir(all, id, Today);

        Assert.Equal(new ReservoirReading(100, Today.AddDays(-3), 3), latest);
        Assert.Null(CareSummary.LatestReservoir(all, Guid.NewGuid(), Today));
    }

    [Fact]
    public void A_reading_is_not_a_headline_kind_and_not_water()
    {
        var all = new[] { Reading(plant.Id, 50) };

        Assert.Null(CareSummary.LastWater(all, plant.Id, Today));
        Assert.All(CareSummary.Headline, kind => Assert.Null(CareSummary.Latest(all, plant.Id, kind, Today)));
    }

    [Fact]
    public void A_reading_never_counts_for_an_interval()
    {
        var reading = Reading(plant.Id, 50);

        Assert.False(CareIntervals.Matches(new CareInterval { Kind = CareKind.Rotated }, reading));
        Assert.False(CareIntervals.Matches(new CareInterval { Kind = CareKind.Fertilised }, reading));
        Assert.False(CareIntervals.Matches(new CareInterval { Kind = CareKind.Flushed }, reading));
    }

    // Logging

    [Fact]
    public async Task A_reservoir_answer_logs_one_reading_and_puts_the_row_off()
    {
        var putOff = await answers.ReservoirAsync(plant, Guess(), 50, Label);

        var saved = Assert.Single(logs.Logs);
        Assert.Equal((plant.Id, Today, CareKind.Rotated), (saved.PlantId, saved.OccurredOn, saved.Kind));
        Assert.Equal((CareKinds.ReservoirReadingId, 50), (saved.OwnKindId, saved.ReservoirLevel));
        Assert.Equal("Reservoir level", saved.OwnKindName);
        Assert.NotNull(putOff);
    }

    [Fact]
    public async Task A_reservoir_answer_without_a_level_logs_nothing_but_still_puts_the_row_off()
    {
        var putOff = await answers.ReservoirAsync(plant, Guess(), null, Label);

        Assert.Empty(logs.Logs);
        Assert.NotNull(putOff);
    }

    [Fact]
    public async Task A_level_out_of_range_is_not_logged()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => care.LogReservoirAsync(plant.Id, 150, Label));

        Assert.Empty(logs.Logs);
    }

    [Fact]
    public async Task A_reading_can_be_fixed_to_another_level()
    {
        var entry = await care.LogReservoirAsync(plant.Id, 25, Label);

        await care.UpdateAsync(new CareLog { Id = entry.Id, OccurredOn = Today, ReservoirLevel = 100 }, Today);

        var saved = Assert.Single(logs.Logs);
        Assert.Equal(100, saved.ReservoirLevel);
        Assert.Equal(CareKinds.ReservoirReadingId, saved.OwnKindId);
    }

    // Flush reminder for the plants in LECA or PON

    [Fact]
    public void An_interval_for_semi_hydro_covers_leca_and_pon_and_nothing_else()
    {
        var plants = new[]
        {
            new Plant { Nickname = "Leca", Medium = GrowingMedium.Leca },
            new Plant { Nickname = "Pon", Medium = GrowingMedium.Pon },
            new Plant { Nickname = "Soil", Medium = GrowingMedium.Soil },
            new Plant { Nickname = "Riser", Medium = GrowingMedium.CormRiser },
            new Plant { Nickname = "Water", Medium = GrowingMedium.Water }
        };
        var interval = new CareInterval { Kind = CareKind.Flushed, OnlySemiHydro = true };

        var covered = CareIntervals.PlantsIn(interval, plants, Places.None);

        Assert.Equal(["Leca", "Pon"], covered.Select(p => p.Nickname));
    }

    [Fact]
    public void A_plant_moved_to_leca_is_covered_from_then_on()
    {
        var moved = new Plant { Nickname = "Moved", Medium = GrowingMedium.Soil };
        var interval = new CareInterval { Kind = CareKind.Flushed, OnlySemiHydro = true };
        Assert.Empty(CareIntervals.PlantsIn(interval, [moved], Places.None));

        moved.Medium = GrowingMedium.Leca;

        Assert.Single(CareIntervals.PlantsIn(interval, [moved], Places.None));
    }

    [Fact]
    public void Without_the_flag_everywhere_covers_every_plant()
    {
        var plants = new[] { new Plant { Nickname = "Soil" }, new Plant { Nickname = "Leca", Medium = GrowingMedium.Leca } };

        Assert.Equal(2, CareIntervals.PlantsIn(new CareInterval { Kind = CareKind.Flushed }, plants, Places.None).Count);
    }

    [Fact]
    public void The_semi_hydro_flag_only_goes_with_everywhere()
    {
        var picked = new CareInterval { Kind = CareKind.Flushed, OnlySemiHydro = true, Scope = CareScope.PickedPlants, PlantIds = [Guid.NewGuid()] };

        Assert.Contains("Plants in LECA or PON can only be chosen on its own.", picked.Validate());
        Assert.Empty(new CareInterval { Kind = CareKind.Flushed, OnlySemiHydro = true }.Validate());
    }

    // Stored

    [Fact]
    public void The_fill_line_the_level_and_the_flag_survive_a_backup()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var data = new BackupData
        {
            Plants = [new Plant { Nickname = "P", ReservoirFillCm = 3.5m }],
            CareLogs = [Reading(plant.Id, 50)],
            CareIntervals = [new CareInterval { Kind = CareKind.Flushed, OnlySemiHydro = true }]
        };

        var back = JsonSerializer.Deserialize<BackupData>(JsonSerializer.Serialize(data, options), options)!;

        Assert.Equal(3.5m, Assert.Single(back.Plants).ReservoirFillCm);
        var log = Assert.Single(back.CareLogs);
        Assert.Equal((CareKinds.ReservoirReadingId, 50), (log.OwnKindId, log.ReservoirLevel));
        Assert.True(Assert.Single(back.CareIntervals).OnlySemiHydro);
    }
}
