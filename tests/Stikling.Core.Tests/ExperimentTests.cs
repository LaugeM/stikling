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

        Assert.Contains("It can't have rooted before it was started.", batch.Validate(DateOnly.MaxValue));
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
        var service = new PropagationService(propagations, plants, new FakeTimelineRepository(), new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));

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

    // ---- The result, for the picture ----

    private static Propagation Counted(GrowingMedium medium, int count, int failed, DateOnly? rootedOn, Guid? cover = null, string experiment = "Test")
    {
        var batch = Batch(experiment, medium, Started, rootedOn);
        batch.InitialCount = count;
        batch.FailedCount = failed;
        batch.CoverPhotoId = cover;
        return batch;
    }

    private static ExperimentResult ResultOf(IEnumerable<Propagation> batches, IEnumerable<Photo>? photos = null) =>
        Experiments.Result(Experiments.Group(batches, Today).Single(), photos ?? []);

    [Fact]
    public void A_rooted_batch_counts_what_it_started_with_minus_what_failed()
    {
        var result = ResultOf([Counted(GrowingMedium.Leca, 5, 2, Started.AddDays(10)), Counted(GrowingMedium.Perlite, 3, 0, null)]);

        var leca = result.Rows.Single(r => r.Medium == GrowingMedium.Leca);
        Assert.Equal(3, leca.Rooted);
        Assert.Equal(5, leca.Total);
    }

    [Fact]
    public void A_batch_that_has_not_rooted_counts_none_and_never_less_than_none()
    {
        var result = ResultOf([
            Counted(GrowingMedium.Leca, 4, 1, null),
            Counted(GrowingMedium.Perlite, 2, 5, Started.AddDays(7))]);

        Assert.Equal(0, result.Rows.Single(r => r.Medium == GrowingMedium.Leca).Rooted);
        Assert.Equal(0, result.Rows.Single(r => r.Medium == GrowingMedium.Perlite).Rooted);
        Assert.Null(result.Rows.Single(r => r.Medium == GrowingMedium.Leca).AverageDays);
    }

    [Fact]
    public void The_average_is_over_the_batches_that_rooted_and_is_rounded()
    {
        var result = ResultOf([
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(10)),
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(11)),
            Counted(GrowingMedium.Leca, 1, 0, null),
            Counted(GrowingMedium.Perlite, 1, 0, Started.AddDays(4))]);

        var leca = result.Rows.Single(r => r.Medium == GrowingMedium.Leca);
        Assert.Equal(11, leca.AverageDays);
        Assert.Equal(2, leca.Rooted);
        Assert.Equal(3, leca.Total);
    }

    [Fact]
    public void Several_batches_of_one_medium_make_one_row()
    {
        var result = ResultOf([
            Counted(GrowingMedium.Leca, 4, 0, Started.AddDays(20)),
            Counted(GrowingMedium.Leca, 6, 1, Started.AddDays(30)),
            Counted(GrowingMedium.Perlite, 2, 0, null)]);

        Assert.Equal(2, result.Rows.Count);
        var leca = result.Rows.Single(r => r.Medium == GrowingMedium.Leca);
        Assert.Equal(9, leca.Rooted);
        Assert.Equal(10, leca.Total);
        Assert.Equal(25, leca.AverageDays);
    }

    [Fact]
    public void The_photo_is_the_fastest_rooted_batch_with_one_else_the_first_with_one()
    {
        var slow = new Photo { Id = Guid.NewGuid(), Frame = new PhotoFrame(0.2, 0.3, 2) };
        var fast = new Photo { Id = Guid.NewGuid() };
        var other = new Photo { Id = Guid.NewGuid() };
        var unrooted = new Photo { Id = Guid.NewGuid() };

        var result = ResultOf([
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(30), slow.Id),
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(5), fast.Id),
            Counted(GrowingMedium.Perlite, 1, 0, null, unrooted.Id),
            Counted(GrowingMedium.Sphagnum, 1, 0, Started.AddDays(9), null)], [slow, fast, other, unrooted]);

        Assert.Equal(fast.Id, result.Rows.Single(r => r.Medium == GrowingMedium.Leca).PhotoId);
        Assert.Equal(unrooted.Id, result.Rows.Single(r => r.Medium == GrowingMedium.Perlite).PhotoId);
        Assert.Null(result.Rows.Single(r => r.Medium == GrowingMedium.Sphagnum).PhotoId);
    }

    [Fact]
    public void The_photo_carries_its_frame_and_skips_reference_photos()
    {
        var reference = new Photo { Id = Guid.NewGuid(), IsReference = true };
        var framed = new Photo { Id = Guid.NewGuid(), Frame = new PhotoFrame(0.2, 0.3, 2) };

        var result = ResultOf([
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(3), reference.Id),
            Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(8), framed.Id),
            Counted(GrowingMedium.Perlite, 1, 0, Started.AddDays(8), reference.Id)], [reference, framed]);

        var leca = result.Rows.Single(r => r.Medium == GrowingMedium.Leca);
        Assert.Equal(framed.Id, leca.PhotoId);
        Assert.Equal(framed.Frame, leca.Frame);
        Assert.Null(result.Rows.Single(r => r.Medium == GrowingMedium.Perlite).PhotoId);
    }

    [Fact]
    public void The_best_medium_comes_first_then_the_fastest_then_by_name()
    {
        var result = ResultOf([
            Counted(GrowingMedium.Sphagnum, 4, 0, Started.AddDays(20)),
            Counted(GrowingMedium.Perlite, 4, 0, Started.AddDays(12)),
            Counted(GrowingMedium.Water, 4, 2, Started.AddDays(5)),
            Counted(GrowingMedium.Leca, 4, 0, null),
            Counted(GrowingMedium.Soil, 4, 0, null)]);

        Assert.Equal(
            [GrowingMedium.Perlite, GrowingMedium.Sphagnum, GrowingMedium.Water, GrowingMedium.Leca, GrowingMedium.Soil],
            result.Rows.Select(r => r.Medium));
    }

    [Fact]
    public void The_result_is_still_running_while_a_batch_is_active()
    {
        var running = ResultOf([Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(5)), Counted(GrowingMedium.Perlite, 1, 0, null)]);
        Assert.True(running.StillRunning);
        Assert.Equal("Test", running.Name);
        Assert.Equal(Started, running.StartedOn);

        var finished = new[] { Counted(GrowingMedium.Leca, 1, 0, Started.AddDays(5)), Counted(GrowingMedium.Perlite, 1, 0, Started.AddDays(9)) };
        foreach (var batch in finished)
            batch.Stage = PropagationStage.Done;
        Assert.False(ResultOf(finished).StillRunning);
    }
}
