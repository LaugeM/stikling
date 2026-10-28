using System.Text.Json;
using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class RoundDaysTests
{
    // Sunday 19 April 2026
    private static readonly DateOnly Sunday = new(2026, 4, 19);
    private static readonly Weekday[] Sundays = [Weekday.Sunday];
    private static readonly Weekday[] WednesdayAndSunday = [Weekday.Wednesday, Weekday.Sunday];

    private static TimeProvider At(DateOnly day) =>
        new FixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];

    // Four waterings spaced the given days apart, the last on the given day, so it is due that many days after
    private Plant Spaced(string name, DateOnly last, int every = 7)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-every * i) });
        return plant;
    }

    private IReadOnlyList<WateringDue> Due(
        DateOnly today, IReadOnlyList<Weekday>? roundDays, PutOffs? putOffs = null) =>
        WateringRound.Due(plants, logs, [], null, [], Stikling.Core.Rooms.Places.None, [], Hemisphere.Northern,
            At(today), putOffs ?? PutOffs.Empty(), roundDays);

    // The round days

    [Fact]
    public void Weekdays_convert_to_and_from_the_calendar_days()
    {
        foreach (var day in Enum.GetValues<Weekday>())
            Assert.Equal(day, day.ToDayOfWeek().ToWeekday());

        Assert.Equal(DayOfWeek.Monday, Weekday.Monday.ToDayOfWeek());
        Assert.Equal(DayOfWeek.Sunday, Weekday.Sunday.ToDayOfWeek());
        Assert.Equal(Weekday.Sunday, Sunday.DayOfWeek.ToWeekday());
    }

    [Fact]
    public void A_round_day_is_one_of_the_picked_weekdays()
    {
        Assert.True(RoundSchedule.IsRoundDay(Sundays, Sunday));
        Assert.False(RoundSchedule.IsRoundDay(Sundays, Sunday.AddDays(1)));
        Assert.False(RoundSchedule.IsRoundDay(null, Sunday));
        Assert.False(RoundSchedule.IsRoundDay([], Sunday));
    }

    [Fact]
    public void Next_is_the_first_round_day_after_the_date_and_OnOrAfter_can_be_the_date_itself()
    {
        Assert.Equal(Sunday.AddDays(7), RoundSchedule.Next(Sundays, Sunday));
        Assert.Equal(Sunday, RoundSchedule.OnOrAfter(Sundays, Sunday));
        Assert.Equal(Sunday.AddDays(3), RoundSchedule.Next(WednesdayAndSunday, Sunday));
        Assert.Equal(Sunday, RoundSchedule.Next(WednesdayAndSunday, Sunday.AddDays(-1)));
        Assert.Null(RoundSchedule.Next(null, Sunday));
        Assert.Null(RoundSchedule.OnOrAfter([], Sunday));
    }

    [Fact]
    public void The_latest_round_day_in_a_stretch_is_found_or_null()
    {
        Assert.Equal(Sunday, RoundSchedule.LatestIn(Sundays, Sunday.AddDays(-4), Sunday.AddDays(2)));
        Assert.Equal(Sunday.AddDays(3), RoundSchedule.LatestIn(WednesdayAndSunday, Sunday, Sunday.AddDays(5)));
        Assert.Null(RoundSchedule.LatestIn(Sundays, Sunday.AddDays(1), Sunday.AddDays(6)));
        Assert.Null(RoundSchedule.LatestIn(null, Sunday, Sunday.AddDays(6)));
    }

    [Fact]
    public void The_longest_gap_goes_round_the_week()
    {
        Assert.Equal(7, RoundSchedule.LongestGap(Sundays));
        Assert.Equal(4, RoundSchedule.LongestGap(WednesdayAndSunday));
        Assert.Equal(1, RoundSchedule.LongestGap(Enum.GetValues<Weekday>()));
        Assert.Equal(0, RoundSchedule.LongestGap(null));
    }

    [Fact]
    public void ShowOn_follows_the_four_rules()
    {
        var due = Sunday.AddDays(2);

        // No round days: its own day
        Assert.Equal(due, RoundSchedule.ShowOn(null, due, DateOnly.MinValue, 7));
        // A plant faster than the longest gap keeps its own day
        Assert.Equal(due, RoundSchedule.ShowOn(Sundays, due, DateOnly.MinValue, 3));
        // Not before it may come up: the first round day from then
        Assert.Equal(Sunday.AddDays(7), RoundSchedule.ShowOn(Sundays, due, Sunday.AddDays(3), 7));
        // Otherwise the latest round day up to the due day
        Assert.Equal(Sunday, RoundSchedule.ShowOn(Sundays, due, Sunday.AddDays(-3), 7));
        // No round day in between: its own day
        Assert.Equal(due, RoundSchedule.ShowOn(Sundays, due, Sunday.AddDays(1), null));
    }

    [Fact]
    public void Something_seen_to_the_day_before_a_round_day_does_not_come_back_on_it()
    {
        // Looked at on Saturday, so it may come up from Sunday and is due again next Saturday
        var saturday = Sunday.AddDays(-1);
        var due = saturday.AddDays(7);

        Assert.Equal(due, RoundSchedule.ShowOn(Sundays, due, Sunday, null));
    }

    // Watering

    [Fact]
    public void A_plant_due_on_a_Tuesday_comes_up_on_the_Sunday_before()
    {
        // Watered on Tuesday 14 April, so due on Tuesday 21 April
        Spaced("Pothos", new DateOnly(2026, 4, 14));

        var saturday = Assert.Single(Due(Sunday.AddDays(-1), Sundays));
        Assert.False(saturday.DueNow);
        Assert.True(saturday.Gathered);
        Assert.Equal(Sunday, saturday.ShowOn);

        var onSunday = Assert.Single(Due(Sunday, Sundays));
        Assert.True(onSunday.DueNow);
    }

    [Fact]
    public void A_reading_logged_on_the_round_day_does_not_bring_the_plant_back_that_day()
    {
        var plant = Spaced("Pothos", new DateOnly(2026, 4, 7));
        Assert.True(Assert.Single(Due(Sunday, Sundays)).DueNow);

        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.MoistureReading, Moisture = 5, OccurredOn = Sunday });

        var due = Assert.Single(Due(Sunday, Sundays));
        Assert.False(due.DueNow);
        Assert.Equal(Sunday.AddDays(7), due.ShowOn);
    }

    [Fact]
    public void A_missed_round_day_keeps_the_plant_due()
    {
        // Due on Tuesday 21 April, and Sunday 19 April has passed
        Spaced("Pothos", new DateOnly(2026, 4, 14));

        var due = Assert.Single(Due(Sunday.AddDays(1), Sundays));

        Assert.True(due.DueNow);
        Assert.Equal(Sunday, due.ShowOn);
    }

    [Fact]
    public void A_put_off_that_ends_on_an_off_day_waits_for_the_next_round_day()
    {
        var plant = Spaced("Pothos", new DateOnly(2026, 4, 7));
        var tuesday = Sunday.AddDays(2);
        var key = PutOffs.Water(plant.Id, new DateOnly(2026, 4, 7));
        // Answered Still wet on the Sunday, which logged nothing but this put-off
        PutOff[] saved = [new() { Key = key, Until = tuesday }];

        var putOffs = PutOffs.From(saved, tuesday);
        Assert.False(putOffs.IsPutOff(key, tuesday));
        Assert.Equal(tuesday, putOffs.EndedOn(key));

        var due = Assert.Single(Due(tuesday, Sundays, putOffs));
        Assert.False(due.DueNow);
        Assert.Equal(Sunday.AddDays(7), due.ShowOn);
    }

    [Fact]
    public void A_plant_that_dries_out_faster_than_the_round_days_keeps_its_own_day()
    {
        Spaced("Fast", new DateOnly(2026, 4, 14), every: 3);

        var due = Assert.Single(Due(new DateOnly(2026, 4, 17), Sundays));

        Assert.False(due.Gathered);
        Assert.Equal(due.Guess.CheckOn, due.ShowOn);
        Assert.True(due.DueNow);
    }

    [Fact]
    public void Without_round_days_nothing_changes()
    {
        Spaced("Pothos", new DateOnly(2026, 4, 14));

        var due = Assert.Single(Due(Sunday, null));

        Assert.False(due.Gathered);
        Assert.False(due.DueNow);
        Assert.Equal(due.Guess.CheckOn, due.ShowOn);
        Assert.Single(Due(Sunday.AddDays(2), null), d => d.DueNow);
    }

    [Fact]
    public void A_gathered_plant_waits_for_the_next_round_but_one_further_off_is_left_out()
    {
        Spaced("Soon", Sunday);
        // Due on 3 May, which is two rounds away
        Spaced("Far", Sunday, every: 14);

        var due = Assert.Single(Due(Sunday, Sundays));

        Assert.Equal("Soon", due.Plant.Nickname);
        Assert.False(due.DueNow);
        Assert.True(due.Gathered);
        Assert.Equal(Sunday.AddDays(7), due.ShowOn);
    }

    [Fact]
    public void The_next_round_has_the_gathered_plants_that_are_not_due_yet()
    {
        Spaced("Soon", Sunday);
        Spaced("Far", Sunday, every: 14);
        Spaced("Late", new DateOnly(2026, 4, 5));

        var next = GoingRound.ForNext(
            plants, logs, [], null, [], Stikling.Core.Rooms.Places.None, [], Hemisphere.Northern, At(Sunday),
            PutOffs.Empty(), Sundays);

        Assert.Equal(["Soon"], next.Select(c => c.Name));
    }

    // Worth a look

    [Fact]
    public void A_propagation_waiting_for_a_look_comes_up_on_the_round_day_before_it_is_due()
    {
        var today = new DateOnly(2026, 4, 22);
        var propagation = new Propagation { Nickname = "Coleus", StartedOn = today.AddDays(-5) };

        var gathered = TodayBoard.NeedsChecking(
            [propagation], new Dictionary<Guid, TimelineEntry>(), At(today),
            roundDays: new[] { Weekday.Wednesday }, putOffs: PutOffs.Empty());

        var check = Assert.Single(gathered);
        Assert.Equal(5, check.DaysSince);

        // On its own days it would wait until the seventh
        Assert.Empty(TodayBoard.NeedsChecking([propagation], new Dictionary<Guid, TimelineEntry>(), At(today)));
    }

    [Fact]
    public void A_propagation_put_off_past_its_round_day_waits_for_the_next()
    {
        var today = new DateOnly(2026, 4, 22);
        var propagation = new Propagation { Nickname = "Coleus", StartedOn = today.AddDays(-9) };
        var putOffs = PutOffs.From([new PutOff { Key = PutOffs.Check(propagation.Id), Until = today }], today);
        Weekday[] thursdays = [Weekday.Thursday];

        Assert.Empty(TodayBoard.NeedsChecking(
            [propagation], new Dictionary<Guid, TimelineEntry>(), At(today), roundDays: thursdays, putOffs: putOffs));
    }

    [Fact]
    public void Propagations_are_unchanged_without_round_days()
    {
        var today = new DateOnly(2026, 4, 22);
        var quiet = new Propagation { Nickname = "Corms", StartedOn = today.AddDays(-7) };
        var fresh = new Propagation { Nickname = "Coleus", StartedOn = today.AddDays(-6) };

        var checks = TodayBoard.NeedsChecking(
            [quiet, fresh], new Dictionary<Guid, TimelineEntry>(), At(today), roundDays: null, putOffs: PutOffs.Empty());

        Assert.Same(quiet, Assert.Single(checks).Propagation);
    }

    // The setting

    [Fact]
    public void Round_days_are_stored_as_day_names()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new UserSettings { RoundDays = [Weekday.Wednesday, Weekday.Sunday] }, options);

        Assert.Contains("\"roundDays\":[\"Wednesday\",\"Sunday\"]", json);
        Assert.Equal([Weekday.Wednesday, Weekday.Sunday], JsonSerializer.Deserialize<UserSettings>(json, options)!.RoundDays);
        Assert.Null(JsonSerializer.Deserialize<UserSettings>("""{"theme":"Dark"}""", options)!.RoundDays);
    }

    [Fact]
    public async Task Picking_round_days_saves_them_in_week_order_and_none_turns_them_off()
    {
        var repository = new FakeSettingsRepository();
        var service = new Stikling.Core.Settings.SettingsService(repository);

        await service.SetRoundDaysAsync([Weekday.Sunday, Weekday.Wednesday, Weekday.Sunday]);
        Assert.Equal([Weekday.Wednesday, Weekday.Sunday], repository.Settings[UserSettings.SettingsId].RoundDays);

        await service.SetRoundDaysAsync([]);
        Assert.Null(repository.Settings[UserSettings.SettingsId].RoundDays);
    }
}
