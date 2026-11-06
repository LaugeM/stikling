using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class WeekAheadTests
{
    private static readonly DateOnly Today = new(2026, 4, 20);
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero));
    private static string Label(Enum value) => value.ToString();

    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];
    private readonly List<CareInterval> intervals = [];
    private readonly List<AwayStretch> stretches = [];
    private readonly PutOffs putOffs = PutOffs.Empty();
    private bool watering = true;

    private TodayData Data() => new()
    {
        Plants = plants,
        CareLogs = logs,
        CareIntervals = intervals,
        Stretches = stretches,
        PutOffs = putOffs,
        Settings = new UserSettings { WateringReminder = watering }
    };

    private IReadOnlyList<WeekDay> Week() => WeekAhead.For(Data(), Today, Time);

    // Four weekly waterings ending on the given day, so it is due a week after
    private Plant Weekly(string name, DateOnly last)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = last.AddDays(-7 * i) });
        return plant;
    }

    // Covered by an interval of two weeks and last fed so that it is due in the given number of days
    private Plant Fed(string name, int dueInDays)
    {
        var plant = new Plant { Nickname = name };
        plants.Add(plant);
        logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(dueInDays - 14) });
        if (intervals.Count == 0)
            intervals.Add(new CareInterval { Kind = CareKind.Fertilised, Every = 2, Unit = IntervalUnit.Weeks, Scope = CareScope.Everywhere, StartsOn = Today.AddDays(-60) });
        return plant;
    }

    [Fact]
    public void Each_plant_is_on_the_day_it_comes_up_and_days_with_nothing_are_left_out()
    {
        Weekly("Soon", Today.AddDays(-3));
        Weekly("Later", Today.AddDays(-1));

        var week = Week();

        Assert.Equal([Today.AddDays(4), Today.AddDays(6)], week.Select(d => d.Day));
        Assert.Equal("Soon", week[0].Water.Single().Plant.Nickname);
        Assert.Equal("Later", week[1].Water.Single().Plant.Nickname);
    }

    [Fact]
    public void Overdue_is_on_today_and_nothing_goes_past_seven_days()
    {
        Weekly("Late", Today.AddDays(-10));
        Weekly("LastDay", Today);
        var far = new Plant { Nickname = "Far" };
        plants.Add(far);
        for (var i = 0; i < 4; i++)
            logs.Add(new CareLog { PlantId = far.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-20 * i) });

        var week = Week();

        Assert.Equal(Today, week[0].Day);
        Assert.Equal("Late", week[0].Water.Single().Plant.Nickname);
        Assert.Equal(Today.AddDays(7), week[^1].Day);
        Assert.DoesNotContain(week.SelectMany(d => d.Water), d => d.Plant.Nickname == "Far");
    }

    [Fact]
    public void Each_thing_is_listed_once_even_when_it_would_come_round_again()
    {
        Weekly("Today", Today.AddDays(-7));

        Assert.Single(Week().SelectMany(d => d.Water));
    }

    [Fact]
    public void A_plant_put_off_is_on_the_day_it_comes_back()
    {
        var plant = Weekly("Late", Today.AddDays(-10));
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-10)), Today.AddDays(3));

        var day = Assert.Single(Week());

        Assert.Equal(Today.AddDays(3), day.Day);
        Assert.False(day.Water[0].DueNow);
    }

    [Fact]
    public void A_plant_put_off_past_the_week_is_not_in_it()
    {
        var plant = Weekly("Late", Today.AddDays(-10));
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-10)), Today.AddDays(8));

        Assert.Empty(Week());
    }

    [Fact]
    public void Looking_ahead_leaves_todays_list_as_it_was()
    {
        var putOff = Weekly("PutOff", Today.AddDays(-10));
        Weekly("Coming", Today.AddDays(-6));
        Weekly("Further", Today.AddDays(-3));
        putOffs.PutOff(PutOffs.Water(putOff.Id, Today.AddDays(-10)), Today.AddDays(3));

        var today = TodaySections.For(Data(), Today, Time);

        Assert.Equal(["Coming"], today.WateringDue.Select(d => d.Plant.Nickname));
    }

    [Fact]
    public void The_watering_reminder_off_leaves_water_out()
    {
        watering = false;
        Weekly("Soon", Today.AddDays(-3));

        Assert.Empty(Week());
    }

    [Fact]
    public void Care_is_grouped_on_the_day_it_is_due()
    {
        watering = false;
        Fed("A", 5);
        Fed("B", 5);

        var day = Assert.Single(Week());

        Assert.Equal(Today.AddDays(5), day.Day);
        Assert.Equal(2, day.Care.Single().Items.Count);
        Assert.Equal(2, day.Count);
    }

    [Fact]
    public void Care_put_off_is_on_the_day_it_comes_back()
    {
        watering = false;
        Fed("A", 0);
        var item = CareIntervals.Plan(intervals, plants, Places.None, logs, [], [], Time, null, putOffs).Single();
        putOffs.PutOff(PutOffs.Interval(item.Interval.Id, item.Plant.Id, item.Last), Today.AddDays(2));

        Assert.Equal(Today.AddDays(2), Assert.Single(Week()).Day);
    }

    [Fact]
    public void An_away_day_asks_for_nothing()
    {
        Weekly("Soon", Today.AddDays(-3));
        stretches.Add(new AwayStretch { From = Today.AddDays(4), Until = Today.AddDays(5) });

        var away = Week().Single(d => d.Day == Today.AddDays(4));

        Assert.True(away.Away);
        Assert.Equal(0, away.Count);
    }

    [Fact]
    public async Task Marking_done_logs_each_as_today_and_undo_takes_it_all_back()
    {
        var repo = new FakeCareLogRepository();
        var feeds = new FakeFeedRepository();
        var products = new FakeProductRepository();
        var kinds = new FakeOwnCareKindRepository();
        var care = new CareService(repo, new FakeTimelineRepository(), new FakePlantRepository(), feeds, products, kinds, Time);
        var service = new WeekAheadService(
            new WateringAnswers(care, repo, new PutOffService(new FakePutOffRepository()), Time),
            care,
            new CareIntervalService(care, repo, feeds, products, kinds, Time));

        var watered = Weekly("Soon", Today.AddDays(-3));
        var fed = Fed("Fed", 3);
        var week = Week();
        var water = week.SelectMany(d => d.Water).Where(d => d.Plant.Id == watered.Id).ToList();
        var items = week.SelectMany(d => d.Care).SelectMany(g => g.Items).Where(i => i.Plant.Id == fed.Id).ToList();

        var done = await service.MarkDoneAsync(water, items, Label);

        var saved = (await repo.GetAllAsync()).Where(l => !l.IsDeleted).ToList();
        Assert.Equal(2, done.Count);
        Assert.Equal(2, saved.Count);
        Assert.All(saved, l => Assert.Equal(Today, l.OccurredOn));
        Assert.Contains(saved, l => l.PlantId == watered.Id && l.Kind == CareKind.Watered);
        Assert.Contains(saved, l => l.PlantId == fed.Id && l.Kind == CareKind.Fertilised);

        await service.UndoAsync(done);

        Assert.DoesNotContain(await repo.GetAllAsync(), l => !l.IsDeleted);
    }
}
