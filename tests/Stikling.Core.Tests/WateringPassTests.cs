using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class WateringPassTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTime(Now);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static string Label(Enum value) => value.ToString();

    private readonly Place living = new() { Name = "Living room", CreatedAt = Created };
    private readonly Place kitchen = new() { Name = "Kitchen", CreatedAt = Created.AddDays(1) };
    private readonly Place office = new() { Name = "Office", CreatedAt = Created.AddDays(2) };
    private readonly Place windowsill;
    private readonly List<Place> placeList = [];
    private readonly List<Plant> plants = [];
    private readonly List<CareLog> logs = [];
    private readonly List<Feed> feeds = [];

    public WateringPassTests()
    {
        windowsill = new Place { Name = "Windowsill", RoomId = living.Id, CreatedAt = Created.AddDays(3) };
        placeList.AddRange([living, kitchen, office, windowsill]);
    }

    private Places Places => new(placeList);

    private Plant Plant(string name, Place? place = null, int? waterAt = null)
    {
        var plant = new Plant { Nickname = name, PlaceId = place?.Id, WaterAt = waterAt };
        plants.Add(plant);
        return plant;
    }

    private CareLog Log(Plant plant, CareKind kind, int daysAgo = 0, int? moisture = null)
    {
        var entry = new CareLog { PlantId = plant.Id, Kind = kind, OccurredOn = Today.AddDays(-daysAgo), Moisture = moisture };
        logs.Add(entry);
        return entry;
    }

    private Feed Feed(string name = "Aroid feed")
    {
        var feed = new Feed { Name = name, Products = [new ProductDose { Name = "Hydro fertiliser", Amount = 2, Per = 1 }] };
        feeds.Add(feed);
        return feed;
    }

    private IReadOnlyList<PassGroup> Rows(params Place[] rooms) =>
        WateringPass.Rows([.. rooms.Select(r => r.Id)], plants, logs, feeds, null, Places, Time);

    private PassRow RowOf(Plant plant, params Place[] rooms) =>
        Rows(rooms).SelectMany(g => g.Rows).Single(r => r.Plant.Id == plant.Id);

    // Rooms

    [Fact]
    public void Rooms_with_active_plants_come_in_the_order_of_the_rooms_and_no_room_last()
    {
        Plant("A", kitchen);
        Plant("B", living);
        Plant("C");

        var rooms = WateringPass.Rooms(plants, Places);

        Assert.Equal(["Living room", "Kitchen", "No room"], rooms.Select(r => r.Name));
        Assert.Equal(WateringPass.NoRoom, rooms[^1].Id);
    }

    [Fact]
    public void A_room_with_only_a_spot_plant_is_listed_and_an_empty_one_is_not()
    {
        Plant("A", windowsill);

        var rooms = WateringPass.Rooms(plants, Places);

        Assert.Equal([living.Id], rooms.Select(r => r.Id));
    }

    [Fact]
    public void Deleted_and_inactive_plants_do_not_make_a_room_or_a_row()
    {
        var gone = Plant("Gone", kitchen);
        gone.DeletedAt = Now;
        Plant("Sold", kitchen).Status = PlantStatus.Sold;
        Plant("Here", living);

        Assert.Equal([living.Id], WateringPass.Rows([living.Id, kitchen.Id], plants, logs, feeds, null, Places, Time)
            .Where(g => g.Rows.Count > 0).Select(g => g.RoomId));
        Assert.Equal([living.Id], WateringPass.Rooms(plants, Places).Select(r => r.Id));
        Assert.Empty(WateringPass.Rows([kitchen.Id], plants, logs, feeds, null, Places, Time).Single().Rows);
    }

    [Fact]
    public void Rows_come_in_the_order_the_rooms_were_picked_with_spots_after_the_room_itself()
    {
        Plant("Yam", windowsill);
        Plant("Zed", living);
        Plant("Bee", living);
        Plant("Kit", kitchen);
        Plant("Loose");

        var groups = WateringPass.Rows([kitchen.Id, WateringPass.NoRoom, living.Id], plants, logs, feeds, null, Places, Time);

        Assert.Equal(["Kitchen", "No room", "Living room"], groups.Select(g => g.Name));
        Assert.Equal([kitchen.Id, WateringPass.NoRoom, living.Id], groups.Select(g => g.RoomId));
        Assert.Equal(["Bee", "Zed", "Yam"], groups[2].Rows.Select(r => r.Plant.DisplayName));
        Assert.Equal(["Loose"], groups[1].Rows.Select(r => r.Plant.DisplayName));
    }

    [Fact]
    public void A_room_that_does_not_exist_or_is_picked_twice_gives_no_extra_group()
    {
        Plant("A", living);

        var groups = WateringPass.Rows([living.Id, Guid.NewGuid(), living.Id], plants, logs, feeds, null, Places, Time);

        Assert.Single(groups);
    }

    // Readings and dryness

    [Fact]
    public void Todays_reading_is_shown_and_yesterdays_is_not()
    {
        var read = Plant("Read", living);
        var old = Plant("Old", living);
        var entry = Log(read, CareKind.MoistureReading, moisture: 6);
        Log(old, CareKind.MoistureReading, daysAgo: 1, moisture: 2);

        Assert.Equal(new RoundReading(entry.Id, 6), RowOf(read, living).Reading);
        Assert.Null(RowOf(old, living).Reading);
        Assert.False(RowOf(old, living).Dry);
    }

    [Fact]
    public void The_newest_of_several_readings_today_is_the_one_shown()
    {
        var plant = Plant("P", living);
        var first = Log(plant, CareKind.MoistureReading, moisture: 7);
        first.UpdatedAt = Now.AddHours(-2);
        var second = Log(plant, CareKind.MoistureReading, moisture: 4);
        second.UpdatedAt = Now.AddHours(-1);

        Assert.Equal(new RoundReading(second.Id, 4), RowOf(plant, living).Reading);
    }

    [Theory]
    [InlineData(2, null, true)]
    [InlineData(3, null, true)]
    [InlineData(4, null, false)]
    [InlineData(0, null, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void A_plant_is_dry_at_or_below_the_level_it_is_watered_at(int reading, int? waterAt, bool dry)
    {
        var plant = Plant("P", living, waterAt);
        Log(plant, CareKind.MoistureReading, moisture: reading);

        var row = RowOf(plant, living);

        Assert.Equal(dry, row.Dry);
        Assert.Equal(waterAt ?? 3, row.WaterAt);
    }

    [Fact]
    public void A_plant_with_no_reading_is_not_dry()
    {
        var row = RowOf(Plant("P", living), living);

        Assert.Null(row.Reading);
        Assert.False(row.Dry);
    }

    [Theory]
    [InlineData(GrowingMedium.Leca)]
    [InlineData(GrowingMedium.Water)]
    public void Reservoir_and_in_water_plants_are_never_dry(GrowingMedium medium)
    {
        var plant = Plant("P", living);
        plant.Medium = medium;
        Log(plant, CareKind.MoistureReading, moisture: 0);

        var row = RowOf(plant, living);

        Assert.NotEqual(WateringForm.TopWatered, row.Form);
        Assert.False(row.Dry);
    }

    [Fact]
    public void A_plant_watered_today_has_the_entry()
    {
        var plant = Plant("P", living);
        Log(plant, CareKind.MoistureReading, moisture: 1);
        var watered = Log(plant, CareKind.Watered);

        var row = RowOf(plant, living);

        Assert.Equal(watered.Id, row.DoneTodayId);
        Assert.True(row.Dry);
    }

    [Fact]
    public void A_reservoir_topped_up_today_is_done_and_a_deleted_watering_is_not()
    {
        var reservoir = Plant("R", living);
        reservoir.Medium = GrowingMedium.Pon;
        var topped = Log(reservoir, CareKind.ToppedUp);
        var soil = Plant("S", living);
        Log(soil, CareKind.Watered).DeletedAt = Now;

        Assert.Equal(topped.Id, RowOf(reservoir, living).DoneTodayId);
        Assert.Null(RowOf(soil, living).DoneTodayId);
    }

    // Still damp

    private PassRow RowWith(PutOffs putOffs, Plant plant) =>
        WateringPass.Rows([living.Id], plants, logs, feeds, null, Places, Time, putOffs)
            .SelectMany(g => g.Rows).Single(r => r.Plant.Id == plant.Id);

    [Fact]
    public void A_plant_put_off_since_its_last_watering_is_still_damp()
    {
        var plant = Plant("P", living);
        Log(plant, CareKind.Watered, daysAgo: 5);
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-5)), Today.AddDays(2));

        Assert.True(RowWith(putOffs, plant).StillDamp);
        Assert.False(RowOf(plant, living).StillDamp);
    }

    [Fact]
    public void A_plant_without_any_watering_can_be_still_damp()
    {
        var plant = Plant("P", living);
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(plant.Id, null), Today.AddDays(1));

        Assert.True(RowWith(putOffs, plant).StillDamp);
    }

    [Fact]
    public void A_put_off_that_has_come_back_or_is_for_an_older_watering_is_not_still_damp()
    {
        var plant = Plant("P", living);
        Log(plant, CareKind.Watered, daysAgo: 5);
        var back = PutOffs.Empty();
        back.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-5)), Today);
        var older = PutOffs.Empty();
        older.PutOff(PutOffs.Water(plant.Id, Today.AddDays(-9)), Today.AddDays(2));

        Assert.False(RowWith(back, plant).StillDamp);
        Assert.False(RowWith(older, plant).StillDamp);
    }

    [Fact]
    public void A_plant_watered_today_or_not_top_watered_is_not_still_damp()
    {
        var watered = Plant("W", living);
        Log(watered, CareKind.Watered);
        var reservoir = Plant("R", living);
        reservoir.Medium = GrowingMedium.Pon;
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Water(watered.Id, Today), Today.AddDays(2));
        putOffs.PutOff(PutOffs.Water(reservoir.Id, null), Today.AddDays(2));

        Assert.False(RowWith(putOffs, watered).StillDamp);
        Assert.False(RowWith(putOffs, reservoir).StillDamp);
    }

    // How the screen asks

    [Fact]
    public void A_chosen_way_of_checking_is_used_whatever_is_logged()
    {
        var plant = Plant("P", living);
        Log(plant, CareKind.MoistureReading, moisture: 4);

        Assert.Equal(WaterCheck.Soil, WaterChecks.Effective(WaterCheck.Soil, logs));
        Assert.Equal(WaterCheck.Meter, WaterChecks.Effective(WaterCheck.Meter, []));
    }

    [Fact]
    public void Without_a_choice_the_meter_is_used_once_a_reading_is_logged_and_the_soil_before()
    {
        var plant = Plant("P", living);
        Log(plant, CareKind.Watered);
        Assert.Equal(WaterCheck.Soil, WaterChecks.Effective(null, logs));

        var reading = Log(plant, CareKind.MoistureReading, moisture: 0);
        Assert.Equal(WaterCheck.Meter, WaterChecks.Effective(null, logs));

        reading.DeletedAt = Now;
        Assert.Equal(WaterCheck.Soil, WaterChecks.Effective(null, logs));
    }

    // Feed

    [Fact]
    public void The_feed_a_plant_gets_with_its_watering_is_the_default_when_it_is_due()
    {
        var feed = Feed();
        var plant = Plant("P", living);
        plant.WateringFeedId = feed.Id;

        Assert.Same(feed, RowOf(plant, living).DefaultFeed);
    }

    [Fact]
    public void The_feed_is_not_the_default_when_it_is_not_due_yet()
    {
        var feed = Feed();
        var plant = Plant("P", living);
        plant.WateringFeedId = feed.Id;
        plant.WateringFeedEvery = 2;
        // Fed three days ago with no watering since, so this would be only the first of two
        Log(plant, CareKind.Fertilised, daysAgo: 3);

        Assert.Null(RowOf(plant, living).DefaultFeed);
    }

    [Fact]
    public void A_deleted_feed_or_one_without_products_is_not_the_default()
    {
        var deleted = Feed("Old");
        deleted.DeletedAt = Now;
        var empty = new Feed { Name = "Empty" };
        feeds.Add(empty);
        var a = Plant("A", living);
        a.WateringFeedId = deleted.Id;
        var b = Plant("B", living);
        b.WateringFeedId = empty.Id;

        Assert.Null(RowOf(a, living).DefaultFeed);
        Assert.Null(RowOf(b, living).DefaultFeed);
    }

    [Fact]
    public void A_plant_without_a_feed_setting_has_no_default_feed()
    {
        Feed();

        Assert.Null(RowOf(Plant("P", living), living).DefaultFeed);
    }

    // Saving

    private sealed class Saving
    {
        public FakeCareLogRepository Logs { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public FakePlantRepository Plants { get; } = new();
        public FakeFeedRepository Feeds { get; } = new();
        public FakeProductRepository Products { get; } = new();
        public CareService Care { get; }

        public Saving() =>
            Care = new CareService(Logs, Timeline, Plants, Feeds, Products, Time);
    }

    [Fact]
    public async Task Logging_without_a_feed_logs_only_the_watering_even_when_the_plant_has_a_feed_due()
    {
        var saving = new Saving();
        var feed = Feed();
        var plant = new Plant { Nickname = "P", WateringFeedId = feed.Id };
        saving.Plants.Plants[plant.Id] = plant;
        saving.Feeds.Feeds[feed.Id] = feed;

        var count = await saving.Care.LogWateringsAsync([new PassWatering(plant.Id, CareKind.Watered, null)], Label);

        Assert.Equal(1, count);
        var entry = Assert.Single(saving.Logs.Logs);
        Assert.Equal(CareKind.Watered, entry.Kind);
        Assert.Equal(Today, entry.OccurredOn);
        Assert.Empty(entry.Products);
    }

    [Fact]
    public async Task A_feed_on_a_watering_adds_a_fertilised_entry_with_its_doses()
    {
        var saving = new Saving();
        var feed = Feed(" Aroid feed ");
        var plant = Guid.NewGuid();

        await saving.Care.LogWateringsAsync([new PassWatering(plant, CareKind.Watered, feed)], Label);

        Assert.Equal([CareKind.Watered, CareKind.Fertilised], saving.Logs.Logs.Select(l => l.Kind));
        var fed = saving.Logs.Logs[1];
        Assert.Equal(Today, fed.OccurredOn);
        Assert.Equal(feed.Id, fed.FeedId);
        Assert.Equal("Aroid feed", fed.FeedName);
        Assert.Equal("Hydro fertiliser", Assert.Single(fed.Products).Name);
        Assert.Empty(saving.Logs.Logs[0].Products);
    }

    [Fact]
    public async Task A_feed_on_a_topping_up_goes_on_that_entry()
    {
        var saving = new Saving();
        var feed = Feed();
        var plant = Guid.NewGuid();

        await saving.Care.LogWateringsAsync([new PassWatering(plant, CareKind.ToppedUp, feed)], Label);

        var entry = Assert.Single(saving.Logs.Logs);
        Assert.Equal(CareKind.ToppedUp, entry.Kind);
        Assert.Equal(feed.Id, entry.FeedId);
        Assert.Equal("Aroid feed", entry.FeedName);
        Assert.Equal("Hydro fertiliser", Assert.Single(entry.Products).Name);
    }

    [Fact]
    public async Task Several_plants_each_get_their_own_entries()
    {
        var saving = new Saving();
        var feed = Feed();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var count = await saving.Care.LogWateringsAsync(
            [new PassWatering(a, CareKind.Watered, feed), new PassWatering(b, CareKind.Watered, null), new PassWatering(c, CareKind.ToppedUp, null)],
            Label);

        Assert.Equal(3, count);
        Assert.Equal(4, saving.Logs.Logs.Count);
        Assert.Single(saving.Logs.Logs, l => l.Kind == CareKind.Fertilised && l.PlantId == a);
    }

    [Fact]
    public async Task A_failing_entry_saves_nothing()
    {
        var saving = new Saving();
        var good = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => saving.Care.LogWateringsAsync(
            [new PassWatering(good, CareKind.Watered, null), new PassWatering(Guid.Empty, CareKind.Watered, null)], Label));

        Assert.Equal("A care entry has to belong to a plant.", error.Message);
        Assert.Empty(saving.Logs.Logs);
    }

    [Fact]
    public async Task Only_a_watering_or_a_topping_up_can_be_logged_and_nothing_is_saved_otherwise()
    {
        var saving = new Saving();

        await Assert.ThrowsAsync<InvalidOperationException>(() => saving.Care.LogWateringsAsync(
            [new PassWatering(Guid.NewGuid(), CareKind.Watered, null), new PassWatering(Guid.NewGuid(), CareKind.Pruned, null)], Label));

        Assert.Empty(saving.Logs.Logs);
    }

    // Undo

    [Fact]
    public async Task Undoing_a_watering_deletes_it_and_the_feeding_logged_with_it()
    {
        var saving = new Saving();
        var feed = Feed();
        var plant = Guid.NewGuid();
        await saving.Care.LogWateringsAsync([new PassWatering(plant, CareKind.Watered, feed)], Label);
        var watering = saving.Logs.Logs.Single(l => l.Kind == CareKind.Watered);

        await saving.Care.UndoWateringAsync(watering.Id);

        Assert.All(saving.Logs.Logs, l => Assert.True(l.IsDeleted));
        Assert.Equal(2, saving.Logs.Logs.Count);
    }

    [Fact]
    public async Task Undoing_a_watering_keeps_a_feeding_logged_by_hand_earlier_or_later_the_same_day()
    {
        var saving = new Saving();
        var feed = Feed();
        var plant = Guid.NewGuid();
        var earlier = new CareLog
        {
            PlantId = plant, Kind = CareKind.Fertilised, OccurredOn = Today, FeedId = feed.Id,
            CreatedAt = Now.AddHours(-3)
        };
        var later = new CareLog
        {
            PlantId = plant, Kind = CareKind.Fertilised, OccurredOn = Today, FeedId = feed.Id,
            CreatedAt = Now.AddMinutes(1)
        };
        saving.Logs.Logs.Add(earlier);
        saving.Logs.Logs.Add(later);
        await saving.Care.LogWateringsAsync([new PassWatering(plant, CareKind.Watered, null)], Label);
        var watering = saving.Logs.Logs.Single(l => l.Kind == CareKind.Watered);
        watering.CreatedAt = Now;

        await saving.Care.UndoWateringAsync(watering.Id);

        Assert.False(earlier.IsDeleted);
        Assert.False(later.IsDeleted);
        Assert.True(watering.IsDeleted);
    }

    [Fact]
    public async Task Undoing_keeps_another_plants_feeding_and_another_days()
    {
        var saving = new Saving();
        var feed = Feed();
        var (plant, other) = (Guid.NewGuid(), Guid.NewGuid());
        await saving.Care.LogWateringsAsync(
            [new PassWatering(plant, CareKind.Watered, null), new PassWatering(other, CareKind.Watered, feed)], Label);
        var yesterday = new CareLog { PlantId = plant, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-1), FeedId = feed.Id };
        saving.Logs.Logs.Add(yesterday);
        var watering = saving.Logs.Logs.First(l => l.Kind == CareKind.Watered && l.PlantId == plant);

        await saving.Care.UndoWateringAsync(watering.Id);

        Assert.Equal(3, saving.Logs.Logs.Count(l => !l.IsDeleted));
        Assert.False(yesterday.IsDeleted);
        Assert.Contains(saving.Logs.Logs, l => l.PlantId == other && l.Kind == CareKind.Fertilised && !l.IsDeleted);
    }

    [Fact]
    public async Task Undoing_a_topping_up_deletes_that_entry_and_nothing_but_a_watering_can_be_undone()
    {
        var saving = new Saving();
        var feed = Feed();
        var plant = Guid.NewGuid();
        await saving.Care.LogWateringsAsync([new PassWatering(plant, CareKind.ToppedUp, feed)], Label);
        var reading = new CareLog { PlantId = plant, Kind = CareKind.MoistureReading, OccurredOn = Today, Moisture = 3 };
        saving.Logs.Logs.Add(reading);

        await saving.Care.UndoWateringAsync(reading.Id);
        Assert.False(reading.IsDeleted);

        var topped = saving.Logs.Logs.Single(l => l.Kind == CareKind.ToppedUp);
        await saving.Care.UndoWateringAsync(topped.Id);
        Assert.True(topped.IsDeleted);
        Assert.False(reading.IsDeleted);
    }
}
