using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class ExperimentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 20);

    private static Propagation Batch(
        string? experiment,
        GrowingMedium medium,
        DateOnly startedOn,
        DateOnly? rootedOn = null) =>
        new()
        {
            Nickname = $"Corms in {medium}",
            Type = PropagationType.Corm,
            Experiment = experiment,
            Medium = medium,
            StartedOn = startedOn,
            RootedOn = rootedOn,
            Stage = rootedOn is null ? PropagationStage.Started : PropagationStage.Rooted
        };

    // The corm test as it actually is: same day, same box, four substrates
    private static readonly DateOnly Started = new(2026, 9, 1);

    private static List<Propagation> CormTest() =>
    [
        Batch("Alocasia corm test", GrowingMedium.Leca, Started, new DateOnly(2026, 9, 19)),
        Batch("Alocasia corm test", GrowingMedium.Perlite, Started),
        Batch("Alocasia corm test", GrowingMedium.Sphagnum, Started, new DateOnly(2026, 9, 13)),
        Batch("Alocasia corm test", GrowingMedium.CormRiser, Started)
    ];

    [Fact]
    public void Noting_rooted_records_the_day_it_reached_rooted()
    {
        var batch = Batch(null, GrowingMedium.Perlite, Started);
        batch.Stage = PropagationStage.Rooted;

        batch.NoteRooted(Today);

        Assert.Equal(Today, batch.RootedOn);
    }

    [Theory]
    [InlineData(PropagationStage.Started)]
    [InlineData(PropagationStage.Rooting)]
    public void Noting_rooted_does_nothing_before_it_has_rooted(PropagationStage stage)
    {
        var batch = Batch(null, GrowingMedium.Perlite, Started);
        batch.Stage = stage;

        batch.NoteRooted(Today);

        Assert.Null(batch.RootedOn);
    }

    [Fact]
    public void Noting_rooted_keeps_the_first_date()
    {
        var first = new DateOnly(2026, 9, 13);
        var batch = Batch(null, GrowingMedium.Perlite, Started, first);

        batch.NoteRooted(Today);

        Assert.Equal(first, batch.RootedOn);
    }

    [Fact]
    public void Days_to_root_is_the_gap_between_starting_and_rooting()
    {
        Assert.Null(Batch(null, GrowingMedium.Perlite, Started).DaysToRoot);
        Assert.Equal(0, Batch(null, GrowingMedium.Perlite, Started, Started).DaysToRoot);
        Assert.Equal(18, Batch(null, GrowingMedium.Leca, Started, new DateOnly(2026, 9, 19)).DaysToRoot);
    }

    [Fact]
    public void A_propagation_cant_root_before_it_was_started()
    {
        var batch = Batch(null, GrowingMedium.Perlite, Started, Started.AddDays(-1));

        Assert.Contains("It can't have rooted before it was started.", batch.Validate());
    }

    [Fact]
    public void Names_are_distinct_trimmed_and_sorted()
    {
        List<Propagation> all =
        [
            Batch("  Alocasia corm test ", GrowingMedium.Leca, Started),
            Batch("alocasia corm test", GrowingMedium.Perlite, Started),
            Batch("Monstera water test", GrowingMedium.Water, Started),
            Batch(null, GrowingMedium.Soil, Started),
            Batch("   ", GrowingMedium.Soil, Started)
        ];

        Assert.Equal(["Alocasia corm test", "Monstera water test"], Experiments.Names(all));
    }

    [Fact]
    public void Names_ignores_deleted_propagations()
    {
        var gone = Batch("Old test", GrowingMedium.Water, Started);
        gone.DeletedAt = Now;

        Assert.Empty(Experiments.Names([gone]));
    }

    [Fact]
    public void Batches_without_an_experiment_are_left_out()
    {
        var all = CormTest();
        all.Add(Batch(null, GrowingMedium.Water, Started));

        var group = Assert.Single(Experiments.Group(all, Today));

        Assert.Equal("Alocasia corm test", group.Name);
        Assert.Equal(4, group.Entries.Count);
    }

    [Fact]
    public void The_same_name_typed_differently_is_one_experiment()
    {
        List<Propagation> all =
        [
            Batch("Alocasia corm test", GrowingMedium.Leca, Started),
            Batch("alocasia CORM test ", GrowingMedium.Perlite, Started)
        ];

        var group = Assert.Single(Experiments.Group(all, Today));

        Assert.Equal(2, group.Entries.Count);
    }

    [Fact]
    public void Rooted_batches_come_first_fastest_at_the_top()
    {
        var group = Assert.Single(Experiments.Group(CormTest(), Today));

        Assert.Equal(
            [GrowingMedium.Sphagnum, GrowingMedium.Leca, GrowingMedium.CormRiser, GrowingMedium.Perlite],
            group.Entries.Select(e => e.Propagation.Medium));
        Assert.Equal([12, 18, null, null], group.Entries.Select(e => e.DaysToRoot));
    }

    [Fact]
    public void A_group_counts_how_many_have_rooted_and_whether_its_still_running()
    {
        var group = Assert.Single(Experiments.Group(CormTest(), Today));

        Assert.Equal(2, group.RootedCount);
        Assert.True(group.IsActive);
        Assert.Equal(Started, group.StartedOn);
        Assert.All(group.Entries, e => Assert.Equal(19, e.DaysRunning));
    }

    [Fact]
    public void The_newest_experiment_comes_first()
    {
        var all = CormTest();
        all.Add(Batch("Monstera water test", GrowingMedium.Water, new DateOnly(2026, 6, 1)));

        var groups = Experiments.Group(all, Today);

        Assert.Equal(["Alocasia corm test", "Monstera water test"], groups.Select(g => g.Name));
    }

    [Fact]
    public void The_experiment_and_rooted_date_survive_a_backup()
    {
        var backup = new Backup.BackupData
        {
            Propagations = [Batch("Alocasia corm test", GrowingMedium.CormRiser, Started, new DateOnly(2026, 9, 13))]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(backup);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Backup.BackupData>(json)!;

        var batch = Assert.Single(restored.Propagations);
        Assert.Equal("Alocasia corm test", batch.Experiment);
        Assert.Equal(new DateOnly(2026, 9, 13), batch.RootedOn);
        // Stored as text, so a reordered enum can't change what old data means
        Assert.Contains("\"CormRiser\"", json);
    }

    [Fact]
    public async Task Moving_a_propagation_to_rooted_stores_the_date()
    {
        var plants = new FakePlantRepository();
        var propagations = new FakePropagationRepository();
        var service = new PropagationService(propagations, plants, new FakeTimelineRepository(), new FixedTime(Now));

        var batch = Batch("Alocasia corm test", GrowingMedium.Leca, Started);
        batch.Stage = PropagationStage.Started;
        await propagations.SaveAsync(batch);

        await service.SetStageAsync(batch, PropagationStage.Rooting, Labels);
        Assert.Null(batch.RootedOn);

        await service.SetStageAsync(batch, PropagationStage.Rooted, Labels);
        Assert.Equal(Today, batch.RootedOn);
        Assert.Equal(19, batch.DaysToRoot);

        static string Labels(Enum value) => value.ToString();
    }
}
