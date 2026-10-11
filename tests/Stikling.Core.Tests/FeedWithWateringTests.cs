using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class FeedWithWateringTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private readonly FakeCareLogRepository logs = new();
    private readonly FakePlantRepository plants = new();
    private readonly FakeFeedRepository feeds = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly CareService care;
    private readonly Feed feed;

    public FeedWithWateringTests()
    {
        care = new CareService(logs, timeline, plants, feeds, new FixedTime(Now));
        feed = new Feed
        {
            Name = " Aroid feed ",
            Products = [new ProductDose { Name = "Hydro fertiliser", Amount = 2, Per = 1 }]
        };
        feeds.Feeds[feed.Id] = feed;
    }

    private static string Label(Enum value) => value.ToString();

    private Plant PlantWith(int? every, Guid? feedId = null)
    {
        var plant = new Plant { Nickname = "Monstera", WateringFeedId = feedId ?? feed.Id, WateringFeedEvery = every };
        plants.Plants[plant.Id] = plant;
        return plant;
    }

    private Task Water(Plant plant, DateOnly? on = null, CareKind kind = CareKind.Watered)
    {
        var entry = care.Start(plant.Id, kind);
        entry.OccurredOn = on ?? Today;
        return care.LogAsync(entry, Label);
    }

    private List<CareLog> For(Plant plant, CareKind kind) =>
        [.. logs.Logs.Where(l => l.PlantId == plant.Id && l.Kind == kind && !l.IsDeleted)];

    [Fact]
    public async Task Every_watering_logs_the_feed_with_each_one()
    {
        var plant = PlantWith(null);

        await Water(plant, Today.AddDays(-7));
        await Water(plant, Today);

        Assert.Equal(2, For(plant, CareKind.Fertilised).Count);
    }

    [Fact]
    public async Task The_extra_entry_copies_the_feed_and_is_dated_like_the_watering()
    {
        var plant = PlantWith(1);

        await Water(plant, Today.AddDays(-2));

        var fed = Assert.Single(For(plant, CareKind.Fertilised));
        Assert.Equal(Today.AddDays(-2), fed.OccurredOn);
        Assert.Equal(feed.Id, fed.FeedId);
        Assert.Equal("Aroid feed", fed.FeedName);
        Assert.Null(fed.Notes);
        Assert.Equal("Hydro fertiliser", Assert.Single(fed.Products).Name);
        Assert.NotSame(feed.Products[0], fed.Products[0]);
        Assert.Empty(fed.Validate(Today));
        // Fertilising is not one of the kinds that gets a history line, same as logging it by hand
        Assert.DoesNotContain(timeline.Entries, t => t.SubjectId == plant.Id);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Every_second_or_third_watering_waits_for_its_turn(int every)
    {
        var plant = PlantWith(every);
        await Water(plant, Today.AddDays(-20));
        Assert.Single(For(plant, CareKind.Fertilised)); // nothing fed before, so it is due

        for (var i = 1; i < every; i++)
        {
            await Water(plant, Today.AddDays(-20 + i));
            Assert.Single(For(plant, CareKind.Fertilised));
        }

        await Water(plant, Today.AddDays(-20 + every));
        Assert.Equal(2, For(plant, CareKind.Fertilised).Count);
    }

    [Fact]
    public async Task A_feed_logged_by_hand_starts_the_count_again()
    {
        var plant = PlantWith(3);
        logs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-5) });
        logs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-3) });

        await Water(plant, Today.AddDays(-1));
        Assert.Single(For(plant, CareKind.Fertilised));

        await Water(plant, Today);
        Assert.Equal(2, For(plant, CareKind.Fertilised).Count);
    }

    [Fact]
    public async Task Waterings_before_the_last_feed_dont_count()
    {
        var plant = PlantWith(2);
        logs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-4) });
        logs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Watered, OccurredOn = Today.AddDays(-3) });
        logs.Logs.Add(new CareLog { PlantId = plant.Id, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-2) });

        await Water(plant, Today);

        Assert.Single(For(plant, CareKind.Fertilised));
    }

    [Fact]
    public async Task A_deleted_feed_entry_is_not_the_last_feed()
    {
        var plant = PlantWith(2);
        logs.Logs.Add(new CareLog
        {
            PlantId = plant.Id, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-1), DeletedAt = DateTimeOffset.UtcNow
        });

        await Water(plant, Today);

        Assert.Single(For(plant, CareKind.Fertilised));
    }

    [Fact]
    public async Task Topping_up_without_products_takes_the_feed_on_the_same_entry()
    {
        var plant = PlantWith(1);

        await Water(plant, kind: CareKind.ToppedUp);

        var topped = Assert.Single(For(plant, CareKind.ToppedUp));
        Assert.Equal("Hydro fertiliser", Assert.Single(topped.Products).Name);
        Assert.Equal(feed.Id, topped.FeedId);
        Assert.Equal("Aroid feed", topped.FeedName);
        Assert.Empty(For(plant, CareKind.Fertilised));
    }

    [Fact]
    public async Task Topping_up_with_its_own_products_is_left_alone_and_counts_as_the_feed()
    {
        var plant = PlantWith(2);
        var entry = care.Start(plant.Id, CareKind.ToppedUp);
        entry.Products = [new ProductDose { Name = "Silica", Amount = 1, Per = 1 }];
        await care.LogAsync(entry, Label);
        var own = Assert.Single(For(plant, CareKind.ToppedUp));
        Assert.Equal("Silica", Assert.Single(own.Products).Name);
        Assert.Null(own.FeedId);

        // The next day is only the first watering since that feed, and every second is needed
        await Water(plant, Today.AddDays(1));
        Assert.Single(For(plant, CareKind.ToppedUp));
        Assert.Empty(For(plant, CareKind.Fertilised));
    }

    [Fact]
    public async Task A_deleted_or_missing_feed_logs_nothing_extra()
    {
        var plant = PlantWith(1);
        feed.DeletedAt = DateTimeOffset.UtcNow;
        var missing = PlantWith(1, Guid.NewGuid());

        await Water(plant);
        await Water(missing);

        Assert.DoesNotContain(logs.Logs, l => l.Kind == CareKind.Fertilised);
        Assert.Equal(2, logs.Logs.Count);
    }

    [Fact]
    public async Task A_plant_without_the_setting_is_logged_as_before()
    {
        var plant = new Plant { Nickname = "Pothos" };
        plants.Plants[plant.Id] = plant;

        await Water(plant);

        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task Other_kinds_of_care_dont_log_the_feed()
    {
        var plant = PlantWith(1);

        await Water(plant, kind: CareKind.Pruned);

        Assert.Single(logs.Logs);
    }

    [Fact]
    public async Task Logging_for_several_plants_follows_each_ones_setting()
    {
        var every = PlantWith(1);
        var second = PlantWith(2);
        var off = new Plant { Nickname = "Pothos" };
        plants.Plants[off.Id] = off;
        logs.Logs.Add(new CareLog { PlantId = second.Id, Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-3) });

        var count = await care.LogManyAsync([every.Id, second.Id, off.Id], CareKind.Watered, Today, null, Label);

        Assert.Equal(3, count);
        Assert.Single(For(every, CareKind.Fertilised));
        Assert.Single(For(second, CareKind.Fertilised)); // only the one from before
        Assert.Empty(For(off, CareKind.Fertilised));
    }

    [Fact]
    public void The_due_rule_reads_no_number_as_every_watering()
    {
        var entry = new CareLog { Kind = CareKind.Watered, OccurredOn = Today };
        var fed = new CareLog { Kind = CareKind.Fertilised, OccurredOn = Today.AddDays(-1) };

        Assert.True(FeedWithWatering.IsDue([fed], entry, null));
        Assert.False(FeedWithWatering.IsDue([fed], entry, 2));
        Assert.True(FeedWithWatering.IsDue([], entry, 3));
    }

    // The history lines

    private Plant Before => new() { Nickname = "Monstera" };

    [Fact]
    public void Choosing_a_feed_says_how_often()
    {
        var after = Before;
        after.WateringFeedId = feed.Id;
        after.WateringFeedEvery = 2;

        Assert.Equal(["Fed with the watering: Aroid feed, every second watering"],
            PlantChanges.Describe(Before, after, Label, feedName: _ => "Aroid feed"));

        after.WateringFeedEvery = null;
        Assert.Equal(["Fed with the watering: Aroid feed, every watering"],
            PlantChanges.Describe(Before, after, Label, feedName: _ => "Aroid feed"));

        after.WateringFeedEvery = 3;
        Assert.Equal(["Fed with the watering: Aroid feed, every third watering"],
            PlantChanges.Describe(Before, after, Label, feedName: _ => "Aroid feed"));
    }

    [Fact]
    public void Changing_only_how_often_and_turning_it_off_are_said()
    {
        var on = Before;
        on.WateringFeedId = feed.Id;
        var slower = on.Duplicate();
        slower.WateringFeedEvery = 2;

        Assert.Equal(["Fed with the watering: Aroid feed, every second watering"],
            PlantChanges.Describe(on, slower, Label, feedName: _ => "Aroid feed"));
        Assert.Equal(["No longer fed with the watering"], PlantChanges.Describe(on, Before, Label));
        Assert.Empty(PlantChanges.Describe(on, on.Duplicate(), Label));
    }

    [Fact]
    public void A_feed_that_cant_be_named_is_left_unsaid()
    {
        var after = Before;
        after.WateringFeedId = feed.Id;

        Assert.Empty(PlantChanges.Describe(Before, after, Label));
    }

    [Fact]
    public void Another_like_this_keeps_the_feed_setting()
    {
        var plant = PlantWith(3);

        var copy = plant.Duplicate();

        Assert.Equal(feed.Id, copy.WateringFeedId);
        Assert.Equal(3, copy.WateringFeedEvery);
    }
}
