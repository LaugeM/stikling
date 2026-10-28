using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class WateringAnswersTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
    private static string Label(Enum value) => value.ToString();

    private readonly FakeCareLogRepository logs = new();
    private readonly FakePutOffRepository putOffRecords = new();
    private readonly PutOffService putOffs;
    private readonly WateringAnswers answers;
    private readonly Plant plant = new() { Nickname = "P" };

    public WateringAnswersTests()
    {
        putOffs = new PutOffService(putOffRecords);
        answers = new WateringAnswers(new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), new FakeFeedRepository(), new FakeProductRepository(), new FakeOwnCareKindRepository(), Time), logs, putOffs, Time);
    }

    private static WateringGuess Guess(WateringMode mode, int usualDays = 8) =>
        new(WateringForm.TopWatered, mode, Today.AddDays(-9), 9, usualDays, 4, 0, Today, null, false, false);

    [Theory]
    [InlineData(WateringForm.TopWatered, CareKind.Watered)]
    [InlineData(WateringForm.Reservoir, CareKind.ToppedUp)]
    [InlineData(WateringForm.InWater, CareKind.ToppedUp)]
    public async Task Watering_logs_the_kind_for_the_form_today_and_returns_the_entry(WateringForm form, CareKind kind)
    {
        var entry = await answers.WaterAsync(plant.Id, form, Label);

        var saved = Assert.Single(logs.Logs);
        Assert.Same(saved, entry);
        Assert.Equal(kind, saved.Kind);
        Assert.Equal(plant.Id, saved.PlantId);
        Assert.Equal(Today, saved.OccurredOn);
    }

    [Fact]
    public async Task A_reading_is_logged_for_today()
    {
        var entry = await answers.ReadingAsync(plant.Id, 6, null, Label);

        Assert.Equal(CareKind.MoistureReading, entry.Kind);
        Assert.Equal(6, entry.Moisture);
        Assert.Equal(Today, entry.OccurredOn);
        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task A_changed_reading_updates_the_same_entry()
    {
        var first = await answers.ReadingAsync(plant.Id, 6, null, Label);

        var second = await answers.ReadingAsync(plant.Id, 3, first.Id, Label);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(3, Assert.Single(logs.Logs).Moisture);
    }

    [Fact]
    public async Task Replacing_a_reading_that_is_gone_or_not_a_reading_adds_a_new_one()
    {
        var watered = await answers.WaterAsync(plant.Id, WateringForm.TopWatered, Label);

        var fromWatering = await answers.ReadingAsync(plant.Id, 4, watered.Id, Label);
        var fromNothing = await answers.ReadingAsync(plant.Id, 5, Guid.NewGuid(), Label);

        Assert.NotEqual(watered.Id, fromWatering.Id);
        Assert.NotEqual(fromWatering.Id, fromNothing.Id);
        Assert.Equal(CareKind.Watered, watered.Kind);
        Assert.Equal(3, logs.Logs.Count);
    }

    [Fact]
    public async Task A_reading_moves_a_guess_made_from_the_meter_without_putting_it_off()
    {
        var result = await answers.NotYetAsync(plant, Guess(WateringMode.Meter), 8, logReading: true, Label);

        Assert.Null(result);
        Assert.Equal(8, Assert.Single(logs.Logs).Moisture);
        Assert.Empty(putOffRecords.PutOffs);
    }

    [Fact]
    public async Task A_reading_on_a_guess_from_the_days_between_waterings_logs_it_and_puts_the_plant_off()
    {
        var guess = Guess(WateringMode.Interval, usualDays: 8);

        var result = await answers.NotYetAsync(plant, guess, 8, logReading: true, Label);

        Assert.Equal(CareKind.MoistureReading, Assert.Single(logs.Logs).Kind);
        var (key, until) = result!.Value;
        Assert.Equal(PutOffs.Water(plant.Id, guess.LastWatered), key);
        Assert.Equal(Today.AddDays(WateringGuess.StillWetWait(8)), until);
        Assert.True((await putOffs.GetAsync(Today)).IsPutOff(key, Today));
    }

    [Theory]
    [InlineData(WateringMode.Meter)]
    [InlineData(WateringMode.Interval)]
    public async Task Without_a_reading_nothing_is_logged_and_the_plant_is_put_off(WateringMode mode)
    {
        var result = await answers.NotYetAsync(plant, Guess(mode), null, logReading: true, Label);

        Assert.Empty(logs.Logs);
        Assert.NotNull(result);
        Assert.True((await putOffs.GetAsync(Today)).IsPutOff(result.Value.Key, Today));
    }

    [Fact]
    public async Task With_logReading_off_no_reading_is_logged_but_the_put_off_follows_the_same_rule()
    {
        var meter = await answers.NotYetAsync(plant, Guess(WateringMode.Meter), 8, logReading: false, Label);
        var interval = await answers.NotYetAsync(plant, Guess(WateringMode.Interval), 8, logReading: false, Label);
        var none = await answers.NotYetAsync(plant, Guess(WateringMode.Meter), null, logReading: false, Label);

        Assert.Empty(logs.Logs);
        Assert.Null(meter);
        Assert.NotNull(interval);
        Assert.NotNull(none);
    }
}
