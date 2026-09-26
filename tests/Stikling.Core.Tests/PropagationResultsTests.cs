using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class PropagationResultsTests
{
    private static readonly DateOnly Started = new(2026, 8, 1);

    private static Propagation Batch(
        GrowingMedium medium,
        PropagationType type = PropagationType.Cutting,
        int count = 1,
        int pottedUp = 0,
        int failed = 0,
        PropagationStage stage = PropagationStage.Started,
        int? rootedAfterDays = null)
    {
        var batch = new Propagation
        {
            Nickname = $"{type} in {medium}",
            Type = type,
            Medium = medium,
            StartedOn = Started,
            RootedOn = rootedAfterDays is { } days ? Started.AddDays(days) : null,
            InitialCount = count,
            PottedUpCount = pottedUp,
            FailedCount = failed,
            Stage = stage
        };
        batch.SyncStageWithCounts();
        return batch;
    }

    [Fact]
    public void Success_is_counted_in_units_not_batches()
    {
        // 4 of 5 corms potted up, 1 rotted
        var result = PropagationResults.ByMedium([Batch(GrowingMedium.Leca, PropagationType.Corm, count: 5, pottedUp: 4, failed: 1)]).Single();

        Assert.Equal(1, result.Batches);
        Assert.Equal(4, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0.8, result.SuccessRate);
    }

    [Fact]
    public void Units_left_in_a_rooted_batch_count_as_made_it()
    {
        var result = PropagationResults.ByMedium([Batch(GrowingMedium.Water, count: 3, failed: 1, stage: PropagationStage.Rooted, rootedAfterDays: 10)]).Single();

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Growing);
    }

    [Theory]
    [InlineData(PropagationStage.Started)]
    [InlineData(PropagationStage.Rooting)]
    public void Units_that_havent_rooted_are_still_growing_and_left_out_of_the_rate(PropagationStage stage)
    {
        var result = PropagationResults.ByMedium([Batch(GrowingMedium.Perlite, count: 3, failed: 1, stage: stage)]).Single();

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, result.Growing);
        Assert.Equal(0.0, result.SuccessRate);
    }

    [Fact]
    public void There_is_no_rate_while_nothing_is_decided()
    {
        var result = PropagationResults.ByMedium([Batch(GrowingMedium.Sphagnum, count: 2)]).Single();

        Assert.Null(result.SuccessRate);
        Assert.Equal(2, result.Growing);
    }

    [Fact]
    public void Days_to_root_uses_only_batches_with_a_rooted_date()
    {
        var result = PropagationResults.ByMedium(
        [
            Batch(GrowingMedium.Water, stage: PropagationStage.Rooted, rootedAfterDays: 21),
            Batch(GrowingMedium.Water, stage: PropagationStage.Rooted, rootedAfterDays: 9),
            // Went straight to potted up, so there is no rooted date to count
            Batch(GrowingMedium.Water, pottedUp: 1),
            Batch(GrowingMedium.Water)
        ]).Single();

        Assert.Equal(4, result.Batches);
        Assert.Equal([9, 21], result.DaysToRoot);
        Assert.Equal(15, result.AverageDaysToRoot);
        Assert.Equal(9, result.FastestDaysToRoot);
        Assert.Equal(21, result.SlowestDaysToRoot);
    }

    [Fact]
    public void Nothing_rooted_has_no_days_to_root()
    {
        var result = PropagationResults.ByMedium([Batch(GrowingMedium.Soil)]).Single();

        Assert.Empty(result.DaysToRoot);
        Assert.Null(result.AverageDaysToRoot);
        Assert.Null(result.FastestDaysToRoot);
        Assert.Null(result.SlowestDaysToRoot);
    }

    [Fact]
    public void Rows_are_per_medium_with_the_most_used_first()
    {
        var results = PropagationResults.ByMedium(
        [
            Batch(GrowingMedium.Perlite),
            Batch(GrowingMedium.Leca),
            Batch(GrowingMedium.Leca),
            Batch(GrowingMedium.Water)
        ]);

        Assert.Equal([GrowingMedium.Leca, GrowingMedium.Perlite, GrowingMedium.Water], results.Select(r => r.Key));
        Assert.Equal([2, 1, 1], results.Select(r => r.Batches));
    }

    [Fact]
    public void Rows_are_per_type_too()
    {
        var results = PropagationResults.ByType(
        [
            Batch(GrowingMedium.Leca, PropagationType.Corm, count: 3, pottedUp: 3),
            Batch(GrowingMedium.Perlite, PropagationType.Corm, count: 2, failed: 2),
            Batch(GrowingMedium.Water, PropagationType.Cutting, pottedUp: 1)
        ]);

        var corms = results.Single(r => r.Key == PropagationType.Corm);
        Assert.Equal(2, corms.Batches);
        Assert.Equal(0.6, corms.SuccessRate);
        Assert.Equal(1.0, results.Single(r => r.Key == PropagationType.Cutting).SuccessRate);
    }

    [Fact]
    public void Deleted_propagations_are_left_out()
    {
        var deleted = Batch(GrowingMedium.Leca, failed: 1);
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(PropagationResults.ByMedium([deleted]));
        Assert.Null(PropagationResults.Overall([deleted]));
    }

    [Fact]
    public void Overall_adds_everything_up()
    {
        var overall = PropagationResults.Overall(
        [
            Batch(GrowingMedium.Leca, count: 2, pottedUp: 1, failed: 1),
            Batch(GrowingMedium.Water, stage: PropagationStage.Rooted, rootedAfterDays: 12),
            Batch(GrowingMedium.Perlite, count: 4)
        ]);

        Assert.NotNull(overall);
        Assert.Equal(3, overall.Batches);
        Assert.Equal(2, overall.Succeeded);
        Assert.Equal(1, overall.Failed);
        Assert.Equal(4, overall.Growing);
        Assert.Equal([12], overall.DaysToRoot);
    }
}
