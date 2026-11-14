using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>The water change reminder, the comparison on the results page, Ready by, and a feed on a propagation's care.</summary>
public class PropagationLooseEndsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static Propagation InWater(int startedDaysAgo, int? every, Action<Propagation>? change = null)
    {
        var p = new Propagation
        {
            Nickname = "Cutting",
            Medium = GrowingMedium.Water,
            StartedOn = Today.AddDays(-startedDaysAgo),
            WaterChangeDays = every
        };
        change?.Invoke(p);
        return p;
    }

    private static PropagationCareLog Changed(Propagation p, int daysAgo) => new()
    {
        PropagationId = p.Id,
        Kind = PropagationCareKind.ChangedWater,
        OccurredOn = Today.AddDays(-daysAgo)
    };

    // The water change reminder

    [Fact]
    public void No_reminder_without_a_number_of_days()
    {
        Assert.Empty(WaterChangeReminders.Due([InWater(30, null)], [], Today));
    }

    [Fact]
    public void No_reminder_in_a_medium_that_has_no_water_to_change()
    {
        var pup = InWater(30, 3, p => p.Medium = GrowingMedium.Soil);

        Assert.Empty(WaterChangeReminders.Due([pup], [], Today));
    }

    [Fact]
    public void A_corm_riser_has_water_to_change_too()
    {
        var corm = InWater(30, 3, p => p.Medium = GrowingMedium.CormRiser);

        Assert.Single(WaterChangeReminders.Due([corm], [], Today));
    }

    [Fact]
    public void No_reminder_for_a_dormant_finished_or_deleted_propagation()
    {
        var dormant = InWater(30, 3, p => p.DormantSince = Today.AddDays(-5));
        var done = InWater(30, 3, p => p.Stage = PropagationStage.Done);
        var failed = InWater(30, 3, p => p.Stage = PropagationStage.Failed);
        var deleted = InWater(30, 3, p => p.DeletedAt = DateTimeOffset.UtcNow);

        Assert.Empty(WaterChangeReminders.Due([dormant, done, failed, deleted], [], Today));
    }

    [Fact]
    public void With_no_change_logged_it_counts_from_the_day_it_was_started()
    {
        var waiting = InWater(5, 7);
        var due = InWater(6, 6);

        var found = Assert.Single(WaterChangeReminders.Due([waiting, due], [], Today));

        Assert.Same(due, found.Propagation);
        Assert.Null(found.LastChanged);
        Assert.Equal(6, found.DaysSince);
    }

    [Fact]
    public void With_a_change_logged_it_counts_from_the_newest_one()
    {
        var pup = InWater(30, 5);
        PropagationCareLog[] logs = [Changed(pup, 9), Changed(pup, 5), Changed(pup, 7)];

        var found = Assert.Single(WaterChangeReminders.Due([pup], logs, Today));

        Assert.Equal(Today.AddDays(-5), found.LastChanged);
        Assert.Equal(5, found.DaysSince);
    }

    [Fact]
    public void Not_due_before_the_days_have_passed()
    {
        var pup = InWater(30, 5);

        Assert.Empty(WaterChangeReminders.Due([pup], [Changed(pup, 4)], Today));
    }

    [Fact]
    public void A_deleted_change_entry_is_not_counted()
    {
        var pup = InWater(30, 5);
        var gone = Changed(pup, 1);
        gone.DeletedAt = DateTimeOffset.UtcNow;

        var found = Assert.Single(WaterChangeReminders.Due([pup], [gone], Today));

        Assert.Null(found.LastChanged);
    }

    [Fact]
    public void Today_lists_the_due_propagations_and_counts_them()
    {
        var pup = InWater(10, 3);
        var data = new TodayData { Propagations = [pup] };

        var sections = TodaySections.For(data, Today, new FixedTime(Now));

        Assert.Equal(pup.Id, Assert.Single(sections.WaterChanges).Propagation.Id);
        Assert.True(sections.DueCount >= 1);
    }

    [Fact]
    public void A_set_number_outside_1_to_60_is_refused_and_empty_is_fine()
    {
        Assert.Empty(InWater(1, null).Validate(Today).Where(e => e.Contains("60")));
        Assert.Empty(InWater(1, 60).Validate(Today).Where(e => e.Contains("60")));
        Assert.NotEmpty(InWater(1, 0).Validate(Today).Where(e => e.Contains("60")));
        Assert.NotEmpty(InWater(1, 61).Validate(Today).Where(e => e.Contains("60")));
    }

    [Fact]
    public void Start_like_last_time_carries_the_number_over()
    {
        var earlier = InWater(10, 4);
        var next = new Propagation();

        LastTime.Apply(earlier, next);

        Assert.Equal(4, next.WaterChangeDays);
        Assert.False(LastTime.SameSetup(earlier, new Propagation { Medium = GrowingMedium.Water }));
    }

    // The comparison on the results page

    // A batch in water that ran this many days, rooted on the last one, with the water changed this many times
    private static (Propagation Batch, List<PropagationCareLog> Logs) Batch(int ranDays, int changes, GrowingMedium medium = GrowingMedium.Water)
    {
        var started = Today.AddDays(-100);
        var batch = new Propagation
        {
            Nickname = "Batch",
            Medium = medium,
            StartedOn = started,
            RootedOn = started.AddDays(ranDays),
            Stage = PropagationStage.Rooted
        };
        var logs = Enumerable.Range(1, changes).Select(i => new PropagationCareLog
        {
            PropagationId = batch.Id,
            Kind = PropagationCareKind.ChangedWater,
            OccurredOn = started.AddDays(i)
        }).ToList();
        return (batch, logs);
    }

    private static IReadOnlyList<WaterChangeGroup> Groups(params (Propagation Batch, List<PropagationCareLog> Logs)[] batches) =>
        PropagationResults.ByWaterChanges(batches.Select(b => b.Batch), batches.SelectMany(b => b.Logs));

    [Fact]
    public void Batches_fall_in_three_groups_by_the_days_between_changes()
    {
        var groups = Groups(
            Batch(21, 10),   // 2.1
            Batch(21, 7),    // 3
            Batch(21, 5),    // 4.2
            Batch(28, 4),    // 7
            Batch(30, 3));   // 10

        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups[0].Result.Batches);
        Assert.Equal(2, groups[1].Result.Batches);
        Assert.Equal(1, groups[2].Result.Batches);
        Assert.Equal([21, 21], groups[0].Result.DaysToRoot);
    }

    [Fact]
    public void The_edges_are_3_and_a_half_and_7_and_a_half()
    {
        var groups = Groups(
            Batch(7, 2),     // 3.5 goes with 4 to 7
            Batch(15, 2),    // 7.5 goes with more than a week
            Batch(14, 4));   // 3.5

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].Result.Batches);
        Assert.Equal(1, groups[1].Result.Batches);
    }

    [Fact]
    public void Only_batches_with_a_change_logged_in_water_are_counted()
    {
        var groups = Groups(
            Batch(21, 10),
            Batch(30, 3),
            Batch(21, 0),                       // nothing logged
            Batch(21, 5, GrowingMedium.Soil));  // no water to change

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups.Sum(g => g.Result.Batches));
    }

    [Fact]
    public void Nothing_is_shown_while_fewer_than_two_groups_have_a_batch()
    {
        Assert.Empty(Groups(Batch(21, 10), Batch(21, 9)));
        Assert.Empty(Groups());
    }

    [Fact]
    public void A_fair_comparison_notes_what_else_differs_between_the_groups()
    {
        var (fast, fastLogs) = Batch(21, 10);
        var (slow, slowLogs) = Batch(30, 3);
        fast.Genus = "Monstera";
        slow.Genus = "Philodendron";

        var groups = PropagationResults.ByWaterChanges([fast, slow], [.. fastLogs, .. slowLogs]);
        var across = FairComparison.AcrossWaterChanges([.. groups.Select(g => g.Batches.ToList())], Places.None);

        Assert.Contains(ComparedThing.Plant, across);
        Assert.DoesNotContain(ComparedThing.WaterChanges, across);
    }

    // Ready by

    private static Propagation Rooted(int days, PropagationType type = PropagationType.Cutting, GrowingMedium medium = GrowingMedium.Water)
    {
        var started = Today.AddDays(-200);
        return new Propagation
        {
            Type = type,
            Medium = medium,
            StartedOn = started,
            RootedOn = started.AddDays(days),
            Stage = PropagationStage.Rooted
        };
    }

    [Fact]
    public void Ready_by_gives_the_usual_and_the_safe_day_to_start()
    {
        var readyOn = new DateOnly(2027, 6, 1);
        Propagation[] all = [Rooted(20), Rooted(25), Rooted(30)];

        var result = ReadyBy.For(PropagationType.Cutting, GrowingMedium.Water, readyOn, all);

        Assert.True(result.HasEnough);
        Assert.Equal(3, result.Compared);
        Assert.Equal(25, result.UsualDays);
        Assert.Equal(30, result.SlowestDays);
        Assert.Equal(readyOn.AddDays(-25), result.StartBy);
        Assert.Equal(readyOn.AddDays(-30), result.SafeStartBy);
        Assert.True(result.HasSaferDay);
    }

    [Fact]
    public void Ready_by_only_goes_by_the_same_type_and_medium_and_live_batches()
    {
        var deleted = Rooted(20);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        Propagation[] all =
        [
            Rooted(20), Rooted(20),
            Rooted(20, medium: GrowingMedium.Soil),
            Rooted(20, type: PropagationType.Leaf),
            deleted,
            new Propagation { Type = PropagationType.Cutting, Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-5) }
        ];

        var result = ReadyBy.For(PropagationType.Cutting, GrowingMedium.Water, Today.AddDays(60), all);

        Assert.False(result.HasEnough);
        Assert.Equal(2, result.Compared);
        Assert.Null(result.StartBy);
        Assert.Null(result.SafeStartBy);
        Assert.Null(result.UsualDays);
    }

    [Fact]
    public void Ready_by_with_the_same_days_has_no_safer_day()
    {
        Propagation[] all = [Rooted(20), Rooted(20), Rooted(20)];

        var result = ReadyBy.For(PropagationType.Cutting, GrowingMedium.Water, Today.AddDays(60), all);

        Assert.False(result.HasSaferDay);
    }

    [Fact]
    public void Ready_by_can_give_a_day_that_has_passed()
    {
        Propagation[] all = [Rooted(40), Rooted(40), Rooted(40)];

        var result = ReadyBy.For(PropagationType.Cutting, GrowingMedium.Water, Today.AddDays(10), all);

        Assert.True(result.StartBy < Today);
    }

    // A feed on a propagation's care

    private static Feed RootingWater() => new() { Name = " Rooting water " };

    private static List<ProductDose> Doses() =>
        [new() { Name = "Rooting hormone", Amount = 1, Unit = DoseUnit.Millilitres, Per = 1, PerUnit = WaterUnit.Litres }];

    [Fact]
    public void A_watering_or_water_change_can_carry_a_feed()
    {
        foreach (var kind in new[] { PropagationCareKind.Watered, PropagationCareKind.ChangedWater })
        {
            var entry = new PropagationCareLog
            {
                PropagationId = Guid.NewGuid(),
                Kind = kind,
                Products = Doses(),
                FeedId = Guid.NewGuid(),
                FeedName = "Rooting water",
                WaterLitres = 0.5m
            };

            Assert.Empty(entry.Validate());
        }
    }

    [Fact]
    public void A_moisture_reading_cannot_carry_a_feed()
    {
        var entry = new PropagationCareLog
        {
            PropagationId = Guid.NewGuid(),
            Kind = PropagationCareKind.MoistureReading,
            Moisture = 4,
            Products = Doses(),
            FeedName = "Rooting water"
        };

        Assert.NotEmpty(entry.Validate());
    }

    [Fact]
    public void The_product_checks_are_the_ones_a_plants_care_has()
    {
        PropagationCareLog Water(Action<PropagationCareLog> change)
        {
            var entry = new PropagationCareLog { PropagationId = Guid.NewGuid(), Kind = PropagationCareKind.Watered, Products = Doses() };
            change(entry);
            return entry;
        }

        Assert.NotEmpty(Water(e => e.Products[0].Name = " ").Validate());
        Assert.NotEmpty(Water(e => e.Products[0].Amount = -1).Validate());
        Assert.NotEmpty(Water(e => e.Products[0].Per = 0).Validate());
        Assert.NotEmpty(Water(e => e.WaterLitres = 0).Validate());
    }

    [Fact]
    public void A_copy_has_its_own_product_list()
    {
        var entry = new PropagationCareLog { Products = Doses() };

        var copy = entry.Copy();
        copy.Products[0].Name = "Changed";
        copy.Products.Add(new ProductDose { Name = "More" });

        Assert.Equal("Rooting hormone", entry.Products[0].Name);
        Assert.Single(entry.Products);
    }

    [Fact]
    public async Task Logging_with_a_feed_keeps_its_products_id_and_name()
    {
        var logs = new FakePropagationCareLogRepository();
        var service = new PropagationCareService(logs, new FixedTime(Now));
        var pup = InWater(10, null);
        var feed = RootingWater();
        var doses = Doses();

        var entry = await service.LogAsync(pup, PropagationCareKind.ChangedWater, Today, null, doses, feed);

        Assert.Same(entry, Assert.Single(logs.Logs));
        Assert.Equal(feed.Id, entry.FeedId);
        Assert.Equal("Rooting water", entry.FeedName);
        Assert.Equal("Rooting hormone", Assert.Single(entry.Products).Name);
        Assert.NotSame(doses[0], entry.Products[0]);
        Assert.Equal("Changed the water, with Rooting water", PropagationCareService.Describe(entry));
    }

    [Fact]
    public async Task Logging_without_a_feed_is_as_before()
    {
        var logs = new FakePropagationCareLogRepository();
        var service = new PropagationCareService(logs, new FixedTime(Now));
        var pup = InWater(10, null, p => p.Medium = GrowingMedium.Soil);

        var entry = await service.LogAsync(pup, PropagationCareKind.Watered, Today);

        Assert.Null(entry.FeedId);
        Assert.Null(entry.FeedName);
        Assert.Empty(entry.Products);
        Assert.Equal("Watered", PropagationCareService.Describe(entry));
    }

    [Fact]
    public async Task A_moisture_reading_logged_with_a_feed_is_refused()
    {
        var logs = new FakePropagationCareLogRepository();
        var service = new PropagationCareService(logs, new FixedTime(Now));
        var pup = InWater(10, null, p => p.Medium = GrowingMedium.Soil);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LogAsync(pup, PropagationCareKind.MoistureReading, Today, 4, Doses(), RootingWater()));

        Assert.Empty(logs.Logs);
    }

    [Fact]
    public void A_watering_with_a_feed_reads_with_its_name()
    {
        var entry = new PropagationCareLog { Kind = PropagationCareKind.Watered, FeedName = "Rooting water" };

        Assert.Equal("Watered with Rooting water", PropagationCareService.Describe(entry));
    }
}
