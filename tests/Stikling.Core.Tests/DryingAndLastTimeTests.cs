using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class DryingAndLastTimeTests
{
    private static readonly DateOnly Started = new(2026, 8, 1);

    private static Propagation Batch(int? driedHours, int failed = 0, int pottedUp = 0, int? rootedAfterDays = null) => new()
    {
        Medium = GrowingMedium.Water,
        StartedOn = Started,
        InitialCount = 2,
        DriedForHours = driedHours,
        FailedCount = failed,
        PottedUpCount = pottedUp,
        RootedOn = rootedAfterDays is { } d ? Started.AddDays(d) : null
    };

    [Fact]
    public void Nothing_is_compared_while_no_batch_was_dried()
    {
        Assert.Empty(PropagationResults.ByDrying([Batch(null), Batch(null)]));
        Assert.Empty(PropagationResults.ByDrying([Batch(0)]));
    }

    [Fact]
    public void Batches_are_grouped_by_how_long_the_cut_end_dried()
    {
        var results = PropagationResults.ByDrying(
        [
            Batch(null, failed: 1, pottedUp: 1),
            Batch(6, pottedUp: 2, rootedAfterDays: 10),
            Batch(23, pottedUp: 2, rootedAfterDays: 14),
            Batch(24, pottedUp: 1, failed: 1, rootedAfterDays: 20),
            Batch(72, pottedUp: 2, rootedAfterDays: 8)
        ]);

        Assert.Equal(["Not dried", "Dried under a day", "Dried a day or more"], results.Select(r => r.Name));
        Assert.Equal(1, results[0].Result.Batches);
        Assert.Equal(0.5, results[0].Result.SuccessRate);
        Assert.Equal(2, results[1].Result.Batches);
        Assert.Equal(12, results[1].Result.AverageDaysToRoot);
        Assert.Equal(2, results[2].Result.Batches);
        Assert.Equal(0.75, results[2].Result.SuccessRate);
    }

    [Fact]
    public void Groups_without_batches_are_left_out_and_deleted_ones_dont_count()
    {
        var gone = Batch(48);
        gone.DeletedAt = DateTimeOffset.UtcNow;
        var results = PropagationResults.ByDrying([Batch(null), Batch(12), gone]);
        Assert.Equal(["Not dried", "Dried under a day"], results.Select(r => r.Name));
    }

    [Fact]
    public void Copy_keeps_the_drying()
    {
        Assert.Equal(48, Batch(48).Copy().DriedForHours);
    }

    private static Propagation Past(Guid? parent, string? genus, int daysAgo, bool deleted = false) => new()
    {
        ParentPlantId = parent,
        Genus = genus,
        StartedOn = new DateOnly(2026, 9, 1).AddDays(-daysAgo),
        DeletedAt = deleted ? DateTimeOffset.UtcNow : null
    };

    [Fact]
    public void The_same_parent_beats_the_same_genus()
    {
        var parent = Guid.NewGuid();
        var byGenus = Past(Guid.NewGuid(), "Monstera", 1);
        var byParent = Past(parent, "Monstera", 30);
        Assert.Same(byParent, LastTime.Find([byGenus, byParent], parent, "Monstera"));
    }

    [Fact]
    public void The_newest_wins()
    {
        var parent = Guid.NewGuid();
        var older = Past(parent, "Monstera", 30);
        var newer = Past(parent, "Monstera", 3);
        Assert.Same(newer, LastTime.Find([older, newer], parent, null));
    }

    [Fact]
    public void Genus_matches_ignoring_case_when_the_parent_has_none()
    {
        var other = Past(null, "monstera", 5);
        Assert.Same(other, LastTime.Find([other], Guid.NewGuid(), "Monstera"));
        Assert.Same(other, LastTime.Find([other], null, " MONSTERA "));
    }

    [Fact]
    public void Deleted_ones_are_skipped_and_nothing_gives_null()
    {
        var parent = Guid.NewGuid();
        var live = Past(parent, "Ficus", 40);
        Assert.Same(live, LastTime.Find([Past(parent, "Ficus", 1, deleted: true), live], parent, "Ficus"));
        Assert.Null(LastTime.Find([Past(parent, "Ficus", 1, deleted: true)], parent, "Ficus"));
        Assert.Null(LastTime.Find([], parent, "Ficus"));
        Assert.Null(LastTime.Find([Past(null, "Ficus", 1)], null, null));
        Assert.Null(LastTime.Find([Past(null, "Ficus", 1)], null, "  "));
    }

    [Fact]
    public void Apply_fills_the_setup_and_SameSetup_notices_it()
    {
        var last = new Propagation
        {
            Type = PropagationType.Cutting,
            Medium = GrowingMedium.Water,
            Container = "Jar",
            RootingAids = [RootingAid.Cinnamon],
            DriedForHours = 48
        };
        var form = new Propagation { Nickname = "Mine", Medium = GrowingMedium.Leca };
        Assert.False(LastTime.SameSetup(last, form));

        LastTime.Apply(last, form);

        Assert.True(LastTime.SameSetup(last, form));
        Assert.Equal("Mine", form.Nickname);
        last.RootingAids.Add(RootingAid.HeatMat);
        Assert.Single(form.RootingAids);
    }
}
