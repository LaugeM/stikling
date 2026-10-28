using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class CareIntervalTests
{
    // A Sunday
    private static readonly DateOnly Today = new(2026, 10, 18);
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly FixedTime Time = new(Now);

    private readonly FakePlaceRepository places = new();
    private Places Places => places.Current;

    private Plant Plant(string name, string? room = null, string[]? tags = null) => new()
    {
        Nickname = name,
        PlaceId = places.IdOf(room),
        Tags = [.. tags ?? []],
        CreatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
    };

    private static CareInterval Interval(
        CareKind kind = CareKind.Fertilised, int every = 2, IntervalUnit unit = IntervalUnit.Weeks, DateOnly? starts = null) =>
        new() { Kind = kind, Every = every, Unit = unit, StartsOn = starts ?? Today.AddDays(-100) };

    private static CareLog Log(Plant plant, DateOnly on, CareKind kind = CareKind.Fertilised, Guid? own = null) =>
        new() { PlantId = plant.Id, OccurredOn = on, Kind = kind, OwnKindId = own, OwnKindName = own is null ? null : "Own" };

    private IReadOnlyList<IntervalItem> Plan(
        IEnumerable<CareInterval> intervals, IEnumerable<Plant> plants, IEnumerable<CareLog>? logs = null,
        IEnumerable<OwnCareKind>? own = null, IEnumerable<Feed>? feeds = null,
        IReadOnlyList<Weekday>? round = null, PutOffs? putOffs = null) =>
        CareIntervals.Plan(intervals, plants, Places, logs ?? [], own ?? [], feeds ?? [], Time, round, putOffs ?? PutOffs.Empty());

    // Due days

    [Theory]
    [InlineData(IntervalUnit.Days, 10, "2026-10-14", "2026-10-24")]
    [InlineData(IntervalUnit.Weeks, 2, "2026-10-14", "2026-10-28")]
    [InlineData(IntervalUnit.Months, 3, "2026-10-18", "2027-01-18")]
    [InlineData(IntervalUnit.Years, 2, "2026-10-18", "2028-10-18")]
    public void Each_unit_adds_up_to_the_due_day(IntervalUnit unit, int every, string last, string expected)
    {
        var plant = Plant("Monstera");
        var logs = new[] { Log(plant, DateOnly.Parse(last)) };

        var (_, due) = CareIntervals.DueFor(Interval(every: every, unit: unit), plant, logs, Time);

        Assert.Equal(DateOnly.Parse(expected), due);
    }

    [Fact]
    public void A_month_from_the_31st_ends_on_the_last_day_of_a_shorter_month()
    {
        var plant = Plant("Monstera");
        var logs = new[] { Log(plant, new DateOnly(2026, 1, 31)) };

        var (_, due) = CareIntervals.DueFor(Interval(every: 1, unit: IntervalUnit.Months), plant, logs, Time);

        Assert.Equal(new DateOnly(2026, 2, 28), due);
    }

    [Fact]
    public void The_newest_matching_entry_is_the_base()
    {
        var plant = Plant("Monstera");
        var logs = new[] { Log(plant, new DateOnly(2026, 9, 1)), Log(plant, new DateOnly(2026, 10, 1)), Log(plant, new DateOnly(2026, 8, 1)) };

        var (last, due) = CareIntervals.DueFor(Interval(), plant, logs, Time);

        Assert.Equal(new DateOnly(2026, 10, 1), last);
        Assert.Equal(new DateOnly(2026, 10, 15), due);
    }

    // The months window

    [Theory]
    [InlineData("2026-11-03", "2027-04-01")]
    [InlineData("2026-02-02", "2026-04-01")]
    [InlineData("2026-04-01", "2026-04-01")]
    [InlineData("2026-10-31", "2026-10-31")]
    [InlineData("2026-07-15", "2026-07-15")]
    public void A_plain_window_moves_a_day_outside_it_to_the_next_first_month(string due, string expected)
    {
        var interval = Interval();
        interval.FromMonth = 4;
        interval.ToMonth = 10;

        Assert.Equal(DateOnly.Parse(expected), CareIntervals.InWindow(interval, DateOnly.Parse(due)));
    }

    [Theory]
    [InlineData("2026-06-10", "2026-10-01")]
    [InlineData("2026-12-24", "2026-12-24")]
    [InlineData("2027-02-01", "2027-02-01")]
    [InlineData("2026-03-31", "2026-03-31")]
    [InlineData("2026-04-01", "2026-10-01")]
    public void A_window_over_the_new_year_keeps_the_winter_months(string due, string expected)
    {
        var interval = Interval();
        interval.FromMonth = 10;
        interval.ToMonth = 3;

        Assert.Equal(DateOnly.Parse(expected), CareIntervals.InWindow(interval, DateOnly.Parse(due)));
    }

    [Fact]
    public void A_due_day_outside_the_window_is_pushed_in_the_plan()
    {
        var plant = Plant("Monstera");
        var interval = Interval(starts: new DateOnly(2026, 10, 18));
        interval.FromMonth = 4;
        interval.ToMonth = 10;
        var logs = new[] { Log(plant, new DateOnly(2026, 10, 25)) };

        var item = Assert.Single(Plan([interval], [plant], logs));

        Assert.Equal(new DateOnly(2027, 4, 1), item.Due);
    }

    // First due

    [Fact]
    public void With_nothing_logged_the_first_one_is_due_an_interval_after_the_day_it_was_set()
    {
        var plant = Plant("Monstera");
        var interval = Interval(starts: Today);

        var item = Assert.Single(Plan([interval], [plant]));

        Assert.Null(item.Last);
        Assert.Equal(Today.AddDays(14), item.Due);
        Assert.Empty(CareIntervals.Now([item], Today, PutOffs.Empty()));
    }

    [Fact]
    public void A_plant_that_came_in_after_the_interval_was_set_counts_from_the_day_it_came()
    {
        var plant = Plant("New one");
        plant.AcquiredOn = LooseDate.Of(new DateOnly(2026, 10, 10));
        var old = Plant("Old one");
        var interval = Interval(starts: new DateOnly(2026, 9, 1));

        var items = Plan([interval], [plant, old]);

        Assert.Equal(new DateOnly(2026, 10, 24), items.Single(i => i.Plant == plant).Due);
        Assert.Equal(new DateOnly(2026, 9, 15), items.Single(i => i.Plant == old).Due);
    }

    [Fact]
    public void Without_an_acquired_date_the_day_it_was_added_counts()
    {
        var plant = Plant("New one");
        plant.CreatedAt = new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);

        var item = Assert.Single(Plan([Interval(starts: new DateOnly(2026, 9, 1))], [plant]));

        Assert.Equal(new DateOnly(2026, 10, 26), item.Due);
    }

    // Dormant

    [Fact]
    public void A_dormant_plant_skips_feedings_but_not_the_other_kinds()
    {
        var plant = Plant("Sleeping");
        plant.DormantSince = Today.AddDays(-20);

        Assert.Empty(Plan([Interval(CareKind.Fertilised)], [plant]));
        Assert.Single(Plan([Interval(CareKind.Repotted)], [plant]));
        Assert.Single(Plan([Interval(CareKind.Pruned)], [plant]));
    }

    // What counts as done

    [Fact]
    public void An_own_kind_only_counts_its_own_entries()
    {
        var kind = new OwnCareKind { Name = "Systemic granules" };
        var other = new OwnCareKind { Name = "Misted" };
        var plant = Plant("Monstera");
        var interval = Interval(CareKind.Rotated);
        interval.OwnKindId = kind.Id;
        var logs = new[]
        {
            Log(plant, new DateOnly(2026, 10, 10), CareKind.Rotated),
            Log(plant, new DateOnly(2026, 10, 12), CareKind.Rotated, other.Id),
            Log(plant, new DateOnly(2026, 10, 1), CareKind.Rotated, kind.Id)
        };

        var item = Assert.Single(Plan([interval], [plant], logs, [kind, other]));

        Assert.Equal(new DateOnly(2026, 10, 1), item.Last);
        Assert.Equal("Systemic granules", item.Label);
    }

    [Fact]
    public void A_feeding_counts_every_feeding_also_one_logged_with_a_watering_or_by_an_own_kind()
    {
        var feedKind = new OwnCareKind { Name = "Foliar feed", CountsAs = CareKind.Fertilised };
        var plant = Plant("Monstera");
        var withWatering = new CareLog
        {
            PlantId = plant.Id, OccurredOn = new DateOnly(2026, 10, 3), Kind = CareKind.Fertilised, FeedId = Guid.NewGuid(), FeedName = "Aroid feed"
        };
        var viaOwn = Log(plant, new DateOnly(2026, 10, 9), CareKind.Fertilised, feedKind.Id);

        Assert.Equal(new DateOnly(2026, 10, 3), CareIntervals.DueFor(Interval(), plant, [withWatering], Time).Last);
        Assert.Equal(new DateOnly(2026, 10, 9), CareIntervals.DueFor(Interval(), plant, [withWatering, viaOwn], Time).Last);
    }

    [Fact]
    public void Rotating_does_not_count_an_own_kind_and_a_watering_does_not_count_for_anything()
    {
        var plant = Plant("Monstera");
        var own = Log(plant, new DateOnly(2026, 10, 10), CareKind.Rotated, Guid.NewGuid());
        var watered = Log(plant, new DateOnly(2026, 10, 12), CareKind.Watered);

        var (last, _) = CareIntervals.DueFor(Interval(CareKind.Rotated), plant, [own, watered], Time);

        Assert.Null(last);
    }

    [Fact]
    public void A_deleted_entry_does_not_count()
    {
        var plant = Plant("Monstera");
        var entry = Log(plant, new DateOnly(2026, 10, 10));
        entry.DeletedAt = Now;

        Assert.Null(CareIntervals.DueFor(Interval(), plant, [entry], Time).Last);
    }

    [Fact]
    public void An_interval_of_a_removed_own_kind_is_left_out()
    {
        var kind = new OwnCareKind { Name = "Misted" };
        kind.DeletedAt = Now;
        var interval = Interval(CareKind.Rotated);
        interval.OwnKindId = kind.Id;

        Assert.Empty(Plan([interval], [Plant("Monstera")], own: [kind]));
    }

    // Overlaps

    [Fact]
    public void Two_intervals_for_the_same_thing_list_the_plant_once_under_the_earliest_due()
    {
        var plant = Plant("Monstera", tags: ["Semi-hydro"]);
        var everywhere = Interval(every: 4, starts: new DateOnly(2026, 10, 1));
        var tagged = Interval(every: 2, starts: new DateOnly(2026, 10, 1));
        tagged.Scope = CareScope.Tag;
        tagged.Tag = "semi-hydro";

        var item = Assert.Single(Plan([everywhere, tagged], [plant]));

        Assert.Equal(tagged.Id, item.Interval.Id);
        Assert.Equal(new DateOnly(2026, 10, 15), item.Due);
    }

    [Fact]
    public void Intervals_for_different_things_both_list_the_plant()
    {
        var plant = Plant("Monstera");

        var items = Plan([Interval(CareKind.Fertilised), Interval(CareKind.Repotted, 2, IntervalUnit.Years)], [plant]);

        Assert.Equal(2, items.Count);
    }

    // Coverage

    [Fact]
    public void A_tag_covers_the_plants_with_it_however_it_is_spelled()
    {
        var a = Plant("A", tags: ["Semi-hydro"]);
        var b = Plant("B", tags: ["semi-hydro "]);
        var c = Plant("C", tags: ["Rare"]);
        var gone = Plant("D", tags: ["Semi-hydro"]);
        gone.DeletedAt = Now;
        var given = Plant("E", tags: ["Semi-hydro"]);
        given.Status = PlantStatus.GivenAway;
        var interval = Interval();
        interval.Scope = CareScope.Tag;
        interval.Tag = "Semi-hydro";

        var covered = CareIntervals.PlantsIn(interval, [a, b, c, gone, given], Places);

        Assert.Equal(["A", "B"], covered.Select(p => p.DisplayName));
    }

    [Fact]
    public void A_room_covers_its_spots_and_nothing_else()
    {
        var living = places.Add("Living room");
        var sill = places.Add("Windowsill", living);
        var kitchen = places.Add("Kitchen");
        var inRoom = Plant("A", "Living room");
        var onSill = new Plant { Nickname = "B", PlaceId = sill.Id };
        var elsewhere = Plant("C", "Kitchen");
        var interval = Interval();
        interval.Scope = CareScope.Room;
        interval.PlaceId = living.Id;

        var covered = CareIntervals.PlantsIn(interval, [inRoom, onSill, elsewhere], Places);

        Assert.Equal(["A", "B"], covered.Select(p => p.DisplayName));
        Assert.NotNull(kitchen);
    }

    [Fact]
    public void Everywhere_and_picked_plants_cover_active_plants_only()
    {
        var a = Plant("A");
        var b = Plant("B");
        b.Status = PlantStatus.Died;
        var everywhere = Interval();
        var picked = Interval();
        picked.Scope = CareScope.PickedPlants;
        picked.PlantIds = [b.Id, a.Id];

        Assert.Equal(["A"], CareIntervals.PlantsIn(everywhere, [a, b], Places).Select(p => p.DisplayName));
        Assert.Equal(["A"], CareIntervals.PlantsIn(picked, [a, b], Places).Select(p => p.DisplayName));
    }

    [Fact]
    public void Intervals_covering_a_plant_are_found_from_the_plant()
    {
        var plant = Plant("A", tags: ["Rare"]);
        var tagged = Interval();
        tagged.Scope = CareScope.Tag;
        tagged.Tag = "Rare";
        var other = Interval();
        other.Scope = CareScope.Tag;
        other.Tag = "Variegated";

        Assert.Equal([tagged.Id], CareIntervals.CoveringPlant(plant, [tagged, other], Places, [], [], Time).Select(i => i.Id));
    }

    // Round days and put-offs

    [Fact]
    public void The_put_off_key_has_the_interval_the_plant_and_the_last_day()
    {
        var interval = Guid.NewGuid();
        var plant = Guid.NewGuid();

        Assert.Equal($"interval:{interval}:{plant}:2026-10-01", PutOffs.Interval(interval, plant, new DateOnly(2026, 10, 1)));
        Assert.Equal($"interval:{interval}:{plant}:", PutOffs.Interval(interval, plant, null));
    }

    [Fact]
    public void With_round_days_it_comes_up_on_the_round_day_before_it_is_due()
    {
        var plant = Plant("Monstera");
        // Last done 6 October, due Tuesday 20 October. Round days are Sunday only, and the 18th is one
        var interval = Interval(every: 14, unit: IntervalUnit.Days);
        var logs = new[] { Log(plant, new DateOnly(2026, 10, 6)) };

        var withRound = Assert.Single(Plan([interval], [plant], logs, round: [Weekday.Sunday]));
        var without = Assert.Single(Plan([interval], [plant], logs));

        Assert.Equal(new DateOnly(2026, 10, 20), without.Due);
        Assert.Equal(new DateOnly(2026, 10, 20), without.ShowOn);
        Assert.Equal(new DateOnly(2026, 10, 18), withRound.ShowOn);
        Assert.Single(CareIntervals.Now([withRound], Today, PutOffs.Empty()));
    }

    [Fact]
    public void Putting_it_off_hides_it_until_the_day_and_a_new_entry_makes_a_new_key()
    {
        var plant = Plant("Monstera");
        var interval = Interval();
        var logs = new[] { Log(plant, Today.AddDays(-20)) };
        var key = PutOffs.Interval(interval.Id, plant.Id, Today.AddDays(-20));
        var putOffs = PutOffs.From([new PutOff { Key = key, Until = Today.AddDays(3) }], Today);

        var items = Plan([interval], [plant], logs, putOffs: putOffs);

        Assert.Empty(CareIntervals.Now(items, Today, putOffs));
        Assert.Single(CareIntervals.Now(items, Today.AddDays(3), PutOffs.Empty()));

        var done = Plan([interval], [plant], [.. logs, Log(plant, Today)], putOffs: putOffs);
        Assert.Equal(Today, done.Single().Last);
    }

    [Fact]
    public void A_put_off_moves_the_round_day_to_after_it()
    {
        var plant = Plant("Monstera");
        var interval = Interval(every: 14, unit: IntervalUnit.Days);
        var last = new DateOnly(2026, 10, 3);
        var logs = new[] { Log(plant, last) };
        var key = PutOffs.Interval(interval.Id, plant.Id, last);
        var putOffs = PutOffs.From([new PutOff { Key = key, Until = Today.AddDays(1) }], Today);

        var item = Assert.Single(Plan([interval], [plant], logs, round: [Weekday.Wednesday], putOffs: putOffs));

        // Due 17 October, put off to the 19th, so it waits for Wednesday the 21st
        Assert.Equal(new DateOnly(2026, 10, 21), item.ShowOn);
    }

    [Fact]
    public void Plants_waiting_for_the_next_round_are_counted()
    {
        var plant = Plant("Monstera");
        var interval = Interval(every: 14, unit: IntervalUnit.Days);
        var logs = new[] { Log(plant, new DateOnly(2026, 10, 10)) };
        var items = Plan([interval], [plant], logs, round: [Weekday.Monday]);

        Assert.Equal(new DateOnly(2026, 10, 19), items.Single().ShowOn);
        Assert.Single(CareIntervals.WaitingForRound(items, new DateOnly(2026, 10, 19), Today, PutOffs.Empty()));
    }

    // Today

    [Fact]
    public void Today_groups_by_kind_own_kind_and_feed_with_the_earliest_due_first_and_plants_by_name()
    {
        var aroid = new Feed { Name = "Aroid feed" };
        var kind = new OwnCareKind { Name = "Systemic granules" };
        var zed = Plant("Zed");
        var amy = Plant("Amy");
        var fed = Interval(CareKind.Fertilised, 2, IntervalUnit.Weeks, new DateOnly(2026, 9, 1));
        fed.FeedId = aroid.Id;
        var repot = Interval(CareKind.Repotted, 6, IntervalUnit.Months, new DateOnly(2025, 9, 1));
        var own = Interval(CareKind.Rotated, 8, IntervalUnit.Weeks, new DateOnly(2026, 8, 1));
        own.OwnKindId = kind.Id;

        var items = Plan([fed, repot, own], [zed, amy], own: [kind], feeds: [aroid]);
        var groups = CareIntervals.Now(items, Today, PutOffs.Empty());

        // Plants came in on 1 January, so repotting is due 1 July, the feeding 15 September, the granules 26 September
        Assert.Equal(["Repot", "Fertilise with Aroid feed", "Systemic granules"], groups.Select(g => g.Label));
        Assert.Equal(["Amy", "Zed"], groups[0].Items.Select(i => i.Plant.DisplayName));
        Assert.Equal(aroid.Id, groups[1].FeedId);
        Assert.Equal("every 2 weeks", CareIntervals.EveryText(groups[1].Interval));
    }

    [Fact]
    public void Coming_up_is_what_is_due_in_the_next_two_days()
    {
        var plant = Plant("Monstera");
        var interval = Interval(every: 14, unit: IntervalUnit.Days);
        var logs = new[] { Log(plant, Today.AddDays(-12)) };
        var items = Plan([interval], [plant], logs);

        Assert.Empty(CareIntervals.Now(items, Today, PutOffs.Empty()));
        Assert.Single(CareIntervals.Coming(items, Today, PutOffs.Empty()));
        Assert.Empty(CareIntervals.Coming(items, Today.AddDays(-1), PutOffs.Empty()));
    }

    [Fact]
    public void A_deleted_feed_is_left_off_the_label()
    {
        var feed = new Feed { Name = "Aroid feed", DeletedAt = Now };
        var interval = Interval();
        interval.FeedId = feed.Id;

        var item = Assert.Single(Plan([interval], [Plant("A")], feeds: [feed]));

        Assert.Equal("Fertilise", item.Label);
        Assert.Null(item.FeedId);
    }

    [Fact]
    public void First_due_is_the_range_over_the_plants_covered()
    {
        var a = Plant("A");
        var b = Plant("B");
        var interval = Interval(starts: new DateOnly(2026, 10, 1));
        var logs = new[] { Log(b, new DateOnly(2026, 10, 10)) };

        var range = CareIntervals.FirstDue(interval, [a, b], Places, logs, Time);

        Assert.Equal((new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 24)), range);
        Assert.Null(CareIntervals.FirstDue(interval, [], Places, logs, Time));
    }

    // Texts

    [Fact]
    public void The_texts_read_plainly()
    {
        var interval = Interval(every: 1, unit: IntervalUnit.Months);
        interval.FromMonth = 10;
        interval.ToMonth = 3;

        Assert.Equal("every month", CareIntervals.EveryText(interval));
        Assert.Equal("October to March", CareIntervals.MonthsText(interval));
        Assert.Null(CareIntervals.MonthsText(Interval()));
    }

    // An own kind follows what it counts as now

    private static CareInterval OwnInterval(OwnCareKind kind, CareKind storedKind)
    {
        var interval = Interval(storedKind);
        interval.OwnKindId = kind.Id;
        return interval;
    }

    [Fact]
    public void An_own_kind_changed_to_count_as_a_watering_drops_its_interval_from_Today()
    {
        var kind = new OwnCareKind { Name = "Bottom watered", CountsAs = CareKind.Watered };
        var interval = OwnInterval(kind, CareKind.Rotated);
        var ownMap = CareLabels.Lookup([kind]);

        Assert.Empty(Plan([interval], [Plant("A")], own: [kind]));
        Assert.Null(CareIntervals.Effective(interval, ownMap));
        Assert.True(CareIntervals.CountsAsWatering(interval, ownMap));
        Assert.Empty(CareIntervals.CoveringPlant(Plant("A"), [interval], Places, [], [kind], Time));
    }

    [Fact]
    public void An_own_kind_changed_between_feed_and_neither_changes_the_dormant_and_feed_behaviour()
    {
        var kind = new OwnCareKind { Name = "Foliar", CountsAs = CareKind.Fertilised };
        var sleeping = Plant("Sleeping");
        sleeping.DormantSince = Today.AddDays(-5);
        var interval = OwnInterval(kind, CareKind.Rotated);
        interval.FeedId = Guid.NewGuid();
        var feed = new Feed { Id = interval.FeedId.Value, Name = "Aroid feed" };

        // Stored as neither, but now a feed: dormant plants skip it and the feed shows
        Assert.Empty(Plan([interval], [sleeping], own: [kind], feeds: [feed]));
        var awake = Assert.Single(Plan([interval], [Plant("Awake")], own: [kind], feeds: [feed]));
        Assert.Equal("Foliar with Aroid feed", awake.Label);

        // Stored as a feed, but now neither: dormant plants go on and the feed is dropped
        kind.CountsAs = null;
        var stored = OwnInterval(kind, CareKind.Fertilised);
        stored.FeedId = feed.Id;
        var item = Assert.Single(Plan([stored], [sleeping], own: [kind], feeds: [feed]));
        Assert.Null(item.FeedId);
        Assert.Equal(CareKind.Rotated, item.Interval.Kind);
    }

    [Fact]
    public void Comes_back_for_a_plant_with_nothing_logged_uses_the_same_due_day_as_Today_and_names_the_year()
    {
        var plant = Plant("Monstera");
        var interval = Interval(CareKind.Repotted, 2, IntervalUnit.Years, Today);

        var covering = Assert.Single(CareIntervals.CoveringPlant(plant, [interval], Places, [], [], Time));
        var (last, due) = CareIntervals.DueFor(covering, plant, [], Time);
        var item = Assert.Single(Plan([interval], [plant]));

        Assert.Null(last);
        Assert.Equal(item.Due, due);
        Assert.Equal(new DateOnly(2028, 10, 18), due);
        Assert.Equal("18 Oct 2028", CareIntervals.DayText(due, Today));
        Assert.Equal("4 Nov", CareIntervals.DayText(new DateOnly(2026, 11, 4), Today));
    }

    [Fact]
    public void Intervals_covering_a_plant_are_sorted_by_next_due()
    {
        var plant = Plant("A");
        var later = Interval(CareKind.Repotted, 2, IntervalUnit.Years, Today);
        later.CreatedAt = Now;
        var sooner = Interval(CareKind.Fertilised, 1, IntervalUnit.Weeks, Today);
        sooner.CreatedAt = Now.AddDays(10);

        var order = CareIntervals.CoveringPlant(plant, [later, sooner], Places, [], [], Time);

        Assert.Equal([sooner.Id, later.Id], order.Select(i => i.Id));
    }

    [Fact]
    public void Items_due_in_one_and_two_days_are_coming_up_and_not_due_now()
    {
        var one = Plant("One");
        var two = Plant("Two");
        var three = Plant("Three");
        var interval = Interval(every: 14, unit: IntervalUnit.Days);
        var logs = new[]
        {
            Log(one, Today.AddDays(-13)), Log(two, Today.AddDays(-12)), Log(three, Today.AddDays(-11))
        };

        var items = Plan([interval], [one, two, three], logs);

        Assert.Empty(CareIntervals.Now(items, Today, PutOffs.Empty()));
        var coming = Assert.Single(CareIntervals.Coming(items, Today, PutOffs.Empty()));
        Assert.Equal(["One", "Two"], coming.Items.Select(i => i.Plant.DisplayName));
    }

    // Validation

    [Fact]
    public void A_good_interval_passes()
    {
        Assert.Empty(Interval().Validate());
    }

    [Theory]
    [InlineData(CareKind.Watered)]
    [InlineData(CareKind.ToppedUp)]
    [InlineData(CareKind.MoistureReading)]
    public void Watering_kinds_are_refused(CareKind kind)
    {
        Assert.Contains(Interval(kind).Validate(), e => e.Contains("watering reminder"));
    }

    [Fact]
    public void An_own_kind_counting_as_watering_is_refused_and_one_counting_as_neither_or_a_feed_passes()
    {
        var watering = new OwnCareKind { Name = "Bottom watered", CountsAs = CareKind.Watered };
        var feeding = new OwnCareKind { Name = "Foliar feed", CountsAs = CareKind.Fertilised };
        var neither = new OwnCareKind { Name = "Granules" };

        CareInterval For(OwnCareKind kind)
        {
            var interval = Interval(kind.LogKind);
            interval.OwnKindId = kind.Id;
            return interval;
        }

        Assert.NotEmpty(For(watering).Validate());
        Assert.Empty(For(feeding).Validate());
        Assert.Empty(For(neither).Validate());
    }

    [Fact]
    public void A_feed_belongs_only_to_a_feeding()
    {
        var interval = Interval(CareKind.Repotted);
        interval.FeedId = Guid.NewGuid();

        Assert.NotEmpty(interval.Validate());
        interval.Kind = CareKind.Fertilised;
        Assert.Empty(interval.Validate());
    }

    [Fact]
    public void The_number_months_and_scope_are_checked()
    {
        Assert.NotEmpty(Interval(every: 0).Validate());

        var half = Interval();
        half.FromMonth = 4;
        Assert.NotEmpty(half.Validate());

        var outside = Interval();
        outside.FromMonth = 0;
        outside.ToMonth = 13;
        Assert.NotEmpty(outside.Validate());

        var room = Interval();
        room.Scope = CareScope.Room;
        Assert.NotEmpty(room.Validate());

        var tag = Interval();
        tag.Scope = CareScope.Tag;
        tag.Tag = " ";
        Assert.NotEmpty(tag.Validate());

        var picked = Interval();
        picked.Scope = CareScope.PickedPlants;
        Assert.NotEmpty(picked.Validate());
    }

    // Logging from Today

    [Fact]
    public async Task Logging_a_group_writes_entries_for_today_with_the_feed_and_undo_takes_them_back()
    {
        var logs = new FakeCareLogRepository();
        var feeds = new FakeFeedRepository();
        var products = new FakeProductRepository();
        var kinds = new FakeOwnCareKindRepository();
        var care = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), feeds, products, kinds, Time);
        var service = new CareIntervalService(care, logs, feeds, products, kinds, Time);

        var product = new Product { Name = "Hydro fertiliser" };
        await products.SaveAsync(product);
        var feed = new Feed { Name = "Aroid feed", Products = [new ProductDose { ProductId = product.Id, Name = "Hydro fertiliser", Amount = 2, Per = 1 }] };
        await feeds.SaveAsync(feed);

        var a = Plant("A");
        var b = Plant("B");
        var interval = Interval();
        interval.FeedId = feed.Id;
        var group = Assert.Single(CareIntervals.Group(Plan([interval], [a, b], feeds: [feed])));

        var ids = await service.LogAsync(group, [a.Id, b.Id], k => k.ToString());

        Assert.Equal(2, ids.Count);
        Assert.All(logs.Logs, l =>
        {
            Assert.Equal(Today, l.OccurredOn);
            Assert.Equal(CareKind.Fertilised, l.Kind);
            Assert.Equal(feed.Id, l.FeedId);
            Assert.Equal("Aroid feed", l.FeedName);
            Assert.Single(l.Products);
        });

        await service.UndoAsync(ids);

        Assert.Empty(await logs.GetAllAsync());
    }

    [Fact]
    public async Task Logging_an_own_kind_writes_its_id_and_name()
    {
        var logs = new FakeCareLogRepository();
        var feeds = new FakeFeedRepository();
        var products = new FakeProductRepository();
        var kinds = new FakeOwnCareKindRepository();
        var care = new CareService(logs, new FakeTimelineRepository(), new FakePlantRepository(), feeds, products, kinds, Time);
        var service = new CareIntervalService(care, logs, feeds, products, kinds, Time);
        var kind = new OwnCareKind { Name = "Systemic granules" };
        await kinds.SaveAsync(kind);

        var plant = Plant("A");
        var interval = Interval(kind.LogKind);
        interval.OwnKindId = kind.Id;
        var group = Assert.Single(CareIntervals.Group(Plan([interval], [plant], own: [kind])));

        await service.LogAsync(group, [plant.Id], k => k.ToString());

        var entry = Assert.Single(logs.Logs);
        Assert.Equal((kind.Id, "Systemic granules", CareKind.Rotated), (entry.OwnKindId, entry.OwnKindName, entry.Kind));
    }
}
