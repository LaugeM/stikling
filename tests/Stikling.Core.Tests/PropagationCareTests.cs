using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class PropagationCareTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly FakePropagationCareLogRepository logs = new();
    private readonly PropagationCareService service;

    public PropagationCareTests() => service = new PropagationCareService(logs, new FixedTime(Now));

    private static Propagation Pup(GrowingMedium medium = GrowingMedium.Soil) =>
        new() { Medium = medium, StartedOn = Today.AddDays(-10), InitialCount = 1 };

    [Fact]
    public async Task Logging_saves_an_entry_for_the_propagation()
    {
        var pup = Pup();

        var entry = await service.LogAsync(pup, PropagationCareKind.Watered, Today.AddDays(-1));

        var saved = Assert.Single(logs.Logs);
        Assert.Same(entry, saved);
        Assert.Equal(pup.Id, saved.PropagationId);
        Assert.Equal(PropagationCareKind.Watered, saved.Kind);
        Assert.Equal(Today.AddDays(-1), saved.OccurredOn);
        Assert.Null(saved.Moisture);
    }

    [Fact]
    public async Task A_moisture_reading_keeps_its_number_from_0_to_10()
    {
        var pup = Pup();

        await service.LogAsync(pup, PropagationCareKind.MoistureReading, Today, 0);
        await service.LogAsync(pup, PropagationCareKind.MoistureReading, Today, 10);

        Assert.Equal([0, 10], logs.Logs.Select(l => l.Moisture));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task A_moisture_reading_without_a_valid_number_is_refused(int? moisture)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogAsync(Pup(), PropagationCareKind.MoistureReading, Today, moisture));
        Assert.Empty(logs.Logs);
    }

    [Fact]
    public async Task Only_a_moisture_reading_has_a_number()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LogAsync(Pup(), PropagationCareKind.Watered, Today, 5));
    }

    [Fact]
    public async Task A_day_in_the_future_or_before_the_start_is_refused()
    {
        var pup = Pup();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogAsync(pup, PropagationCareKind.Watered, Today.AddDays(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogAsync(pup, PropagationCareKind.Watered, pup.StartedOn.AddDays(-1)));
        await service.LogAsync(pup, PropagationCareKind.Watered, pup.StartedOn);
        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task A_finished_propagation_takes_no_care()
    {
        var done = Pup();
        done.RecordFailed(1, Today);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogAsync(done, PropagationCareKind.Watered, Today));
    }

    [Fact]
    public async Task Deleting_is_soft_and_the_entry_leaves_the_list()
    {
        var entry = await service.LogAsync(Pup(), PropagationCareKind.Watered, Today);

        await service.DeleteAsync(entry.Id);

        Assert.NotNull(entry.DeletedAt);
        Assert.Empty(await logs.GetAllAsync());
    }

    [Fact]
    public void For_lists_one_propagation_newest_first_without_deleted_entries()
    {
        var pup = Pup();
        var older = new PropagationCareLog { PropagationId = pup.Id, OccurredOn = Today.AddDays(-5) };
        var sameDayFirst = new PropagationCareLog { PropagationId = pup.Id, OccurredOn = Today.AddDays(-1), CreatedAt = Now.AddHours(-3) };
        var sameDayLater = new PropagationCareLog
        {
            PropagationId = pup.Id, OccurredOn = Today.AddDays(-1), CreatedAt = Now, Kind = PropagationCareKind.ChangedWater
        };
        var deleted = new PropagationCareLog { PropagationId = pup.Id, OccurredOn = Today, DeletedAt = Now };
        var other = new PropagationCareLog { PropagationId = Guid.NewGuid(), OccurredOn = Today };

        var list = PropagationCareService.For([older, sameDayFirst, deleted, other, sameDayLater], pup.Id);

        Assert.Equal([sameDayLater, sameDayFirst, older], list);
    }

    [Fact]
    public void Latest_gives_the_newest_of_a_kind_and_how_long_ago()
    {
        var pup = Pup();
        PropagationCareLog Entry(PropagationCareKind kind, int daysAgo, bool deleted = false) => new()
        {
            PropagationId = pup.Id, Kind = kind, OccurredOn = Today.AddDays(-daysAgo), DeletedAt = deleted ? Now : null
        };
        var all = new[]
        {
            Entry(PropagationCareKind.Watered, 6),
            Entry(PropagationCareKind.Watered, 2),
            Entry(PropagationCareKind.Watered, 0, deleted: true),
            Entry(PropagationCareKind.ChangedWater, 4)
        };

        Assert.Equal(new PropagationCareLatest(Today.AddDays(-2), 2), PropagationCareService.Latest(all, pup.Id, PropagationCareKind.Watered, Today));
        Assert.Equal(new PropagationCareLatest(Today.AddDays(-4), 4), PropagationCareService.Latest(all, pup.Id, PropagationCareKind.ChangedWater, Today));
        Assert.Null(PropagationCareService.Latest(all, pup.Id, PropagationCareKind.MoistureReading, Today));
        Assert.Null(PropagationCareService.Latest(all, Guid.NewGuid(), PropagationCareKind.Watered, Today));
    }

    [Fact]
    public void Newest_days_gives_the_latest_day_per_propagation_without_deleted_entries()
    {
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var gone = Guid.NewGuid();
        var days = PropagationCareService.NewestDays(
        [
            new() { PropagationId = one, OccurredOn = Today.AddDays(-4) },
            new() { PropagationId = one, OccurredOn = Today.AddDays(-1) },
            new() { PropagationId = two, OccurredOn = Today.AddDays(-9) },
            new() { PropagationId = two, OccurredOn = Today, DeletedAt = Now },
            new() { PropagationId = gone, OccurredOn = Today, DeletedAt = Now }
        ]);

        Assert.Equal(Today.AddDays(-1), days[one]);
        Assert.Equal(Today.AddDays(-9), days[two]);
        Assert.False(days.ContainsKey(gone));
    }

    [Theory]
    [InlineData(GrowingMedium.Water)]
    [InlineData(GrowingMedium.CormRiser)]
    public void Water_and_a_riser_only_have_the_water_to_change(GrowingMedium medium)
    {
        Assert.Equal([PropagationCareKind.ChangedWater], PropagationCareService.KindsFor(medium));
    }

    [Theory]
    [InlineData(GrowingMedium.Soil)]
    [InlineData(GrowingMedium.OrchidBark)]
    [InlineData(GrowingMedium.Leca)]
    [InlineData(GrowingMedium.Pon)]
    [InlineData(GrowingMedium.Perlite)]
    [InlineData(GrowingMedium.Sphagnum)]
    [InlineData(GrowingMedium.Other)]
    public void Every_other_medium_is_watered_and_read(GrowingMedium medium)
    {
        Assert.Equal(
            [PropagationCareKind.Watered, PropagationCareKind.MoistureReading],
            PropagationCareService.KindsFor(medium));
    }

    [Fact]
    public void Describe_says_what_was_done()
    {
        Assert.Equal("Watered", PropagationCareService.Describe(new() { Kind = PropagationCareKind.Watered }));
        Assert.Equal("Changed the water", PropagationCareService.Describe(new() { Kind = PropagationCareKind.ChangedWater }));
        Assert.Equal("Moisture 4/10", PropagationCareService.Describe(new() { Kind = PropagationCareKind.MoistureReading, Moisture = 4 }));
    }

    [Fact]
    public async Task Correcting_a_reading_changes_the_number_on_the_same_entry()
    {
        var pup = Pup();
        var entry = await service.LogAsync(pup, PropagationCareKind.MoistureReading, Today, 2);

        var corrected = await service.CorrectReadingAsync(pup.Id, entry.Id, 6);

        Assert.Same(entry, corrected);
        Assert.Equal(6, Assert.Single(logs.Logs).Moisture);
    }

    [Fact]
    public async Task Correcting_refuses_a_bad_number_and_leaves_other_entries_alone()
    {
        var pup = Pup();
        var reading = await service.LogAsync(pup, PropagationCareKind.MoistureReading, Today, 2);
        var watered = await service.LogAsync(pup, PropagationCareKind.Watered, Today);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectReadingAsync(pup.Id, reading.Id, 11));
        Assert.Equal(2, reading.Moisture);
        Assert.Null(await service.CorrectReadingAsync(pup.Id, watered.Id, 4));
        Assert.Null(await service.CorrectReadingAsync(Guid.NewGuid(), reading.Id, 4));
    }
}
