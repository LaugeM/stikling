using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class FailedComparisonTests
{
    private static readonly DateOnly March = new(2026, 3, 10);

    private static Propagation Failed(GrowingMedium medium = GrowingMedium.Water, DateOnly? started = null, Action<Propagation>? change = null)
    {
        var batch = new Propagation
        {
            Nickname = "Failed one",
            Medium = medium,
            StartedOn = started ?? March,
            FailedCount = 1,
            Stage = PropagationStage.Failed,
            FinishedOn = (started ?? March).AddDays(30)
        };
        change?.Invoke(batch);
        return batch;
    }

    private static Propagation OneRooted(GrowingMedium medium = GrowingMedium.Water, DateOnly? started = null, Action<Propagation>? change = null)
    {
        var batch = new Propagation
        {
            Nickname = "Rooted one",
            Medium = medium,
            StartedOn = started ?? March,
            RootedOn = (started ?? March).AddDays(20),
            Stage = PropagationStage.Rooted
        };
        change?.Invoke(batch);
        return batch;
    }

    private static List<Propagation> Rooted(int count, GrowingMedium medium = GrowingMedium.Water, DateOnly? started = null, Action<Propagation>? change = null) =>
        Enumerable.Range(0, count).Select(_ => OneRooted(medium, started, change)).ToList();

    private static FailedComparison Compare(Propagation failed, IEnumerable<Propagation> others, IEnumerable<PropagationCareLog>? logs = null) =>
        FailedComparisons.For(failed, [failed, .. others], logs ?? [])!;

    private static List<PropagationCareLog> Changes(Propagation p, params int[] days) =>
        days.Select(d => new PropagationCareLog
        {
            PropagationId = p.Id,
            Kind = PropagationCareKind.ChangedWater,
            OccurredOn = p.StartedOn.AddDays(d)
        }).ToList();

    [Fact]
    public void Only_a_failed_propagation_is_compared()
    {
        Assert.Null(FailedComparisons.For(OneRooted(), Rooted(5), []));
    }

    [Fact]
    public void A_deleted_propagation_is_not_compared()
    {
        var failed = Failed();
        failed.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Null(FailedComparisons.For(failed, Rooted(5), []));
    }

    [Fact]
    public void Too_few_rooted_batches_compare_nothing()
    {
        var comparison = Compare(Failed(GrowingMedium.Perlite), Rooted(2));

        Assert.Equal(2, comparison.Rooted);
        Assert.False(comparison.HasEnough);
        Assert.False(comparison.NothingStandsOut);
        Assert.Empty(comparison.Differences);
    }

    [Fact]
    public void No_rooted_batches_at_all_is_too_few()
    {
        var comparison = Compare(Failed(), []);

        Assert.Equal(0, comparison.Rooted);
        Assert.False(comparison.HasEnough);
    }

    [Fact]
    public void Says_so_when_nothing_stands_out()
    {
        var comparison = Compare(Failed(), Rooted(3));

        Assert.True(comparison.HasEnough);
        Assert.True(comparison.NothingStandsOut);
    }

    [Fact]
    public void Only_rooted_batches_of_the_same_type_count()
    {
        List<Propagation> others =
        [
            .. Rooted(2),
            OneRooted(change: p => p.Type = PropagationType.Corm),
            Failed(),
            new Propagation { Nickname = "Still going", StartedOn = March },
            new Propagation { Nickname = "Gone without roots", StartedOn = March, Stage = PropagationStage.Done, GivenAwayCount = 1 }
        ];

        Assert.Equal(2, Compare(Failed(), others).Rooted);
    }

    [Fact]
    public void A_batch_potted_up_without_a_rooted_date_counts_as_rooted()
    {
        var pottedUp = new Propagation { Nickname = "Potted", StartedOn = March, PottedUpCount = 1, Stage = PropagationStage.Done };

        Assert.Equal(1, Compare(Failed(), [pottedUp]).Rooted);
    }

    [Fact]
    public void A_failed_batch_with_a_rooted_date_is_not_one_that_rooted()
    {
        var lost = Failed(change: p => p.RootedOn = March.AddDays(5));

        Assert.Equal(0, Compare(Failed(), [lost]).Rooted);
    }

    [Fact]
    public void Deleted_rooted_batches_do_not_count()
    {
        var others = Rooted(3);
        others[0].DeletedAt = DateTimeOffset.UtcNow;

        Assert.Equal(2, Compare(Failed(), others).Rooted);
    }

    [Fact]
    public void A_medium_few_of_the_rooted_used_stands_out()
    {
        List<Propagation> others = [.. Rooted(6), OneRooted(GrowingMedium.Perlite), OneRooted(GrowingMedium.Sphagnum)];

        var difference = Assert.Single(Compare(Failed(GrowingMedium.Perlite), others).Differences);

        Assert.Equal(new MediumDifference(GrowingMedium.Perlite, 1, GrowingMedium.Water, 6), difference);
    }

    [Fact]
    public void A_medium_many_of_the_rooted_used_does_not_stand_out()
    {
        List<Propagation> others = [.. Rooted(2), OneRooted(GrowingMedium.Perlite)];

        Assert.True(Compare(Failed(GrowingMedium.Perlite), others).NothingStandsOut);
    }

    [Fact]
    public void An_aid_few_of_the_rooted_had_stands_out()
    {
        var failed = Failed(change: p => p.RootingAids = [RootingAid.HumidityDome]);

        var difference = Assert.Single(Compare(failed, Rooted(4)).Differences);

        Assert.Equal(new AidUsedDifference(RootingAid.HumidityDome, 0), difference);
    }

    [Fact]
    public void An_aid_most_of_the_rooted_had_stands_out_when_it_was_not_used()
    {
        List<Propagation> others = [.. Rooted(3, change: p => p.RootingAids = [RootingAid.HumidityDome]), OneRooted()];

        var difference = Assert.Single(Compare(Failed(), others).Differences);

        Assert.Equal(new AidNotUsedDifference(RootingAid.HumidityDome, 3), difference);
    }

    [Fact]
    public void A_month_far_from_when_the_rooted_ones_started_stands_out()
    {
        var failed = Failed(started: new DateOnly(2026, 1, 15));

        var difference = Assert.Single(Compare(failed, Rooted(4, started: new DateOnly(2025, 7, 1))).Differences);

        Assert.Equal(new StartMonthDifference(1, 0, 7, 4), difference);
    }

    [Fact]
    public void The_month_next_to_when_the_rooted_ones_started_does_not_stand_out()
    {
        var failed = Failed(started: new DateOnly(2026, 1, 15));

        Assert.True(Compare(failed, Rooted(4, started: new DateOnly(2025, 12, 1))).NothingStandsOut);
    }

    [Fact]
    public void Water_changed_much_less_often_stands_out()
    {
        var failed = Failed();
        var others = Rooted(3);
        var logs = Changes(failed, 10, 20, 30)
            .Concat(others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20)));

        var difference = Assert.Single(Compare(failed, others, logs).Differences);

        Assert.Equal(new WaterChangeDifference(10, 4, 3), difference);
    }

    [Fact]
    public void Water_changed_about_as_often_does_not_stand_out()
    {
        var failed = Failed();
        var others = Rooted(3);
        var logs = Changes(failed, 5, 10, 15, 20, 25, 30)
            .Concat(others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20)));

        Assert.True(Compare(failed, others, logs).NothingStandsOut);
    }

    [Fact]
    public void No_water_changes_logged_stands_out_once_it_ran_long_enough()
    {
        var failed = Failed();
        var others = Rooted(3);
        var logs = others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20));

        var difference = Assert.Single(Compare(failed, others, logs).Differences);

        Assert.Equal(new WaterChangeDifference(null, 4, 3), difference);
    }

    [Fact]
    public void No_water_changes_logged_says_nothing_without_a_finish_date()
    {
        var failed = Failed(change: p => p.FinishedOn = null);
        var others = Rooted(3);
        var logs = others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20));

        Assert.True(Compare(failed, others, logs).NothingStandsOut);
    }

    [Fact]
    public void Too_few_rooted_batches_with_changes_logged_compare_no_water()
    {
        var failed = Failed();
        var others = Rooted(4);
        var logs = Changes(failed, 10, 20, 30)
            .Concat(others.Take(2).SelectMany(p => Changes(p, 4, 8, 12, 16, 20)));

        Assert.True(Compare(failed, others, logs).NothingStandsOut);
    }

    [Fact]
    public void Deleted_water_changes_do_not_count()
    {
        var failed = Failed();
        var others = Rooted(3);
        var logs = Changes(failed, 10, 20, 30)
            .Concat(others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20)))
            .ToList();
        foreach (var log in logs.Where(l => l.PropagationId != failed.Id))
            log.DeletedAt = DateTimeOffset.UtcNow;

        Assert.True(Compare(failed, others, logs).NothingStandsOut);
    }

    [Fact]
    public void Water_changes_are_not_compared_outside_water()
    {
        var failed = Failed(GrowingMedium.Sphagnum);
        var others = Rooted(3, GrowingMedium.Sphagnum);
        var logs = others.SelectMany(p => Changes(p, 4, 8, 12, 16, 20));

        Assert.True(Compare(failed, others, logs).NothingStandsOut);
    }
}
