using Stikling.Core.Backup;
using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Tests;

public class CareTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 20);

    private readonly Guid plant = Guid.NewGuid();
    private readonly Guid otherPlant = Guid.NewGuid();

    private readonly FakeCareLogRepository logs = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly CareService service;

    public CareTests()
    {
        service = new CareService(logs, timeline, new FixedTime(Now));
    }

    private static string Label(Enum value) => value switch
    {
        CareKind.LeavesCleaned => "Leaves cleaned",
        CareKind.MoistureReading => "Moisture reading",
        _ => value.ToString()
    };

    private CareLog Entry(CareKind kind, DateOnly on, Guid? plantId = null, int? moisture = null, string? notes = null) =>
        new() { PlantId = plantId ?? plant, Kind = kind, OccurredOn = on, Moisture = moisture, Notes = notes };

    // Validation

    [Fact]
    public void An_entry_has_to_belong_to_a_plant()
    {
        var entry = Entry(CareKind.Watered, Today);
        entry.PlantId = Guid.Empty;

        Assert.Contains("A care entry has to belong to a plant.", entry.Validate(Today));
    }

    [Fact]
    public void An_entry_cant_be_dated_in_the_future() =>
        Assert.Contains("That date is in the future.", Entry(CareKind.Watered, Today.AddDays(1)).Validate(Today));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void A_moisture_reading_goes_from_1_to_10(int value) =>
        Assert.Contains(
            "The moisture reading goes from 1 to 10.",
            Entry(CareKind.MoistureReading, Today, moisture: value).Validate(Today));

    [Fact]
    public void A_moisture_reading_needs_a_number() =>
        Assert.Contains(
            "Give the moisture reading a number from 1 to 10.",
            Entry(CareKind.MoistureReading, Today).Validate(Today));

    [Fact]
    public void Only_a_moisture_reading_carries_a_number() =>
        Assert.Contains(
            "Only a moisture reading has a number.",
            Entry(CareKind.Watered, Today, moisture: 5).Validate(Today));

    [Fact]
    public void A_plain_watering_is_valid() =>
        Assert.Empty(Entry(CareKind.Watered, Today).Validate(Today));

    // What counts as notable

    [Theory]
    [InlineData(CareKind.Repotted)]
    [InlineData(CareKind.Flushed)]
    [InlineData(CareKind.Pruned)]
    public void Notable_care_belongs_in_the_history(CareKind kind) => Assert.True(CareKinds.IsNotable(kind));

    [Theory]
    [InlineData(CareKind.Watered)]
    [InlineData(CareKind.Fertilised)]
    [InlineData(CareKind.ToppedUp)]
    [InlineData(CareKind.Rotated)]
    [InlineData(CareKind.LeavesCleaned)]
    [InlineData(CareKind.Harvested)]
    [InlineData(CareKind.MoistureReading)]
    public void Routine_care_stays_out_of_the_history(CareKind kind) => Assert.False(CareKinds.IsNotable(kind));

    // Reading the log back

    private List<CareLog> AWeekOfCare() =>
    [
        Entry(CareKind.Watered, new DateOnly(2026, 9, 1)),
        Entry(CareKind.Watered, new DateOnly(2026, 9, 17)),
        Entry(CareKind.Fertilised, new DateOnly(2026, 9, 10)),
        Entry(CareKind.Flushed, new DateOnly(2026, 8, 20)),
        Entry(CareKind.MoistureReading, new DateOnly(2026, 9, 18), moisture: 4),
        Entry(CareKind.MoistureReading, new DateOnly(2026, 9, 12), moisture: 8)
    ];

    [Fact]
    public void Latest_picks_the_newest_of_a_kind()
    {
        var watered = CareSummary.Latest(AWeekOfCare(), plant, CareKind.Watered, Today);

        Assert.NotNull(watered);
        Assert.Equal(new DateOnly(2026, 9, 17), watered.On);
        Assert.Equal(3, watered.DaysAgo);
    }

    [Fact]
    public void Latest_is_null_when_it_has_never_been_done() =>
        Assert.Null(CareSummary.Latest(AWeekOfCare(), plant, CareKind.Repotted, Today));

    [Fact]
    public void Last_per_kind_gives_one_row_each_newest_first()
    {
        var rows = CareSummary.LastPerKind(AWeekOfCare(), plant, Today);

        Assert.Equal(
            [CareKind.MoistureReading, CareKind.Watered, CareKind.Fertilised, CareKind.Flushed],
            rows.Select(r => r.Kind));
    }

    [Fact]
    public void The_newest_moisture_reading_wins()
    {
        var reading = CareSummary.LatestMoisture(AWeekOfCare(), plant, Today);

        Assert.NotNull(reading);
        Assert.Equal(4, reading.Value);
        Assert.Equal(2, reading.DaysAgo);
    }

    [Fact]
    public void Another_plants_care_is_left_out()
    {
        var all = AWeekOfCare();
        all.Add(Entry(CareKind.Repotted, Today, plantId: otherPlant));

        Assert.Null(CareSummary.Latest(all, plant, CareKind.Repotted, Today));
        Assert.Equal([CareKind.Repotted], CareSummary.For(all, otherPlant).Select(l => l.Kind));
    }

    [Fact]
    public void Deleted_entries_are_left_out()
    {
        var all = AWeekOfCare();
        var deleted = Entry(CareKind.Repotted, Today);
        deleted.DeletedAt = Now;
        all.Add(deleted);

        Assert.Null(CareSummary.Latest(all, plant, CareKind.Repotted, Today));
    }

    [Fact]
    public void A_plant_with_no_care_yet_reads_as_empty()
    {
        Assert.Empty(CareSummary.For(AWeekOfCare(), otherPlant));
        Assert.Empty(CareSummary.LastPerKind(AWeekOfCare(), otherPlant, Today));
        Assert.Null(CareSummary.LatestMoisture(AWeekOfCare(), otherPlant, Today));
    }

    [Fact]
    public void Care_logged_today_reads_as_0_days_ago() =>
        Assert.Equal(0, CareSummary.Latest([Entry(CareKind.Watered, Today)], plant, CareKind.Watered, Today)!.DaysAgo);

    // Writing

    [Fact]
    public async Task Logging_a_repot_puts_it_on_the_plants_history()
    {
        await service.LogAsync(Entry(CareKind.Repotted, Today, notes: "Into the big terracotta"), Label);

        var written = Assert.Single(timeline.Entries);
        Assert.Equal(plant, written.SubjectId);
        Assert.Equal(SubjectType.Plant, written.SubjectType);
        Assert.Equal("Repotted: Into the big terracotta", written.Text);
    }

    [Fact]
    public async Task Logging_a_watering_does_not_touch_the_history()
    {
        await service.LogAsync(Entry(CareKind.Watered, Today), Label);

        Assert.Single(logs.Logs);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task A_moisture_reading_reads_out_of_ten()
    {
        await service.LogAsync(Entry(CareKind.Flushed, Today), Label);
        Assert.Equal("Flushed", timeline.Entries[0].Text);

        Assert.Equal(
            "Moisture reading: 4/10",
            CareService.Describe(Entry(CareKind.MoistureReading, Today, moisture: 4), Label));
    }

    [Fact]
    public async Task Logging_several_plants_gives_each_one_its_own_entry()
    {
        var third = Guid.NewGuid();

        var count = await service.LogManyAsync([plant, otherPlant, third], CareKind.Fertilised, Today, " hydro feed ", Label);

        Assert.Equal(3, count);
        Assert.Equal(3, logs.Logs.Count);
        Assert.Equal([plant, otherPlant, third], logs.Logs.Select(l => l.PlantId));
        Assert.All(logs.Logs, l => Assert.Equal(Today, l.OccurredOn));
        Assert.All(logs.Logs, l => Assert.Equal("hydro feed", l.Notes));
        // Fertilising is routine, so none of them reaches the history
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task The_same_plant_twice_is_only_logged_once()
    {
        var count = await service.LogManyAsync([plant, plant], CareKind.Watered, Today, null, Label);

        Assert.Equal(1, count);
        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task Nothing_is_saved_when_one_of_them_is_invalid()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LogManyAsync([plant, otherPlant], CareKind.Watered, Today.AddDays(1), null, Label));

        Assert.Empty(logs.Logs);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Logging_several_repots_puts_each_on_its_own_history()
    {
        await service.LogManyAsync([plant, otherPlant], CareKind.Repotted, Today, null, Label);

        Assert.Equal(2, timeline.Entries.Count);
        Assert.Equal([plant, otherPlant], timeline.Entries.Select(e => e.SubjectId));
    }

    [Fact]
    public void A_new_entry_starts_on_today() =>
        Assert.Equal(Today, service.Start(plant).OccurredOn);

    // Backup

    [Fact]
    public void Care_logs_survive_a_backup()
    {
        var backup = new BackupData
        {
            CareLogs = [Entry(CareKind.MoistureReading, Today, moisture: 7, notes: "dry on top")]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(backup);
        var restored = System.Text.Json.JsonSerializer.Deserialize<BackupData>(json)!;

        var entry = Assert.Single(restored.CareLogs);
        Assert.Equal(CareKind.MoistureReading, entry.Kind);
        Assert.Equal(7, entry.Moisture);
        Assert.Equal(Today, entry.OccurredOn);
        Assert.Equal(1, restored.Counts.CareEntries);
        // Stored as text, so reordering the enum can't change what old data means
        Assert.Contains("\"MoistureReading\"", json);
    }
}
