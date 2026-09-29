using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class PropagationTests
{
    private static Propagation Batch(int count) => new() { Genus = "Alocasia", Type = PropagationType.Corm, InitialCount = count };

    [Fact]
    public void Potting_up_part_of_a_batch_keeps_it_active()
    {
        var corms = Batch(3);

        corms.RecordPottedUp(2, new DateOnly(2026, 9, 20));

        Assert.Equal(2, corms.PottedUpCount);
        Assert.Equal(1, corms.RemainingCount);
        Assert.True(corms.IsActive);
    }

    [Fact]
    public void Batch_is_done_when_the_last_unit_is_potted_up()
    {
        var corms = Batch(3);

        corms.RecordFailed(1, new DateOnly(2026, 9, 20));
        corms.RecordPottedUp(2, new DateOnly(2026, 9, 20));

        Assert.Equal(PropagationStage.Done, corms.Stage);
        Assert.False(corms.IsActive);
        Assert.Equal(0, corms.RemainingCount);
    }

    [Fact]
    public void Batch_failed_when_nothing_made_it()
    {
        var corms = Batch(3);

        corms.RecordFailed(1, new DateOnly(2026, 9, 20));
        corms.RecordFailed(2, new DateOnly(2026, 9, 20));

        Assert.Equal(PropagationStage.Failed, corms.Stage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void Counts_outside_what_is_left_are_rejected(int count)
    {
        var corms = Batch(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => corms.RecordPottedUp(count, new DateOnly(2026, 9, 20)));
        Assert.Throws<ArgumentOutOfRangeException>(() => corms.RecordFailed(count, new DateOnly(2026, 9, 20)));
        Assert.Equal(3, corms.RemainingCount);
    }

    [Fact]
    public void Finished_propagations_cannot_change_stage_or_counts()
    {
        var cutting = Batch(1);
        cutting.RecordPottedUp(1, new DateOnly(2026, 9, 20));

        Assert.Throws<InvalidOperationException>(() => cutting.SetStage(PropagationStage.Rooting));
        Assert.Throws<InvalidOperationException>(() => cutting.RecordFailed(1, new DateOnly(2026, 9, 20)));
    }

    [Theory]
    [InlineData(PropagationStage.Done)]
    [InlineData(PropagationStage.Failed)]
    public void Done_and_failed_cannot_be_set_by_hand(PropagationStage stage)
    {
        Assert.Throws<ArgumentException>(() => Batch(1).SetStage(stage));
    }

    [Fact]
    public void Stage_can_move_back_while_active()
    {
        var cutting = Batch(1);
        cutting.SetStage(PropagationStage.Rooted);

        cutting.SetStage(PropagationStage.Rooting);

        Assert.Equal(PropagationStage.Rooting, cutting.Stage);
    }

    [Fact]
    public void The_day_the_last_unit_is_used_up_is_kept_as_the_finished_day()
    {
        var corms = Batch(2);
        var first = new DateOnly(2026, 9, 1);
        var last = new DateOnly(2026, 9, 5);

        corms.RecordPottedUp(1, first);
        Assert.Null(corms.FinishedOn);

        corms.RecordFailed(1, last);
        Assert.Equal(last, corms.FinishedOn);

        corms.SyncStageWithCounts(last.AddDays(10));
        Assert.Equal(last, corms.FinishedOn);
    }

    [Fact]
    public void Opening_a_finished_batch_again_clears_the_finished_day()
    {
        var corms = Batch(2);
        corms.RecordPottedUp(2, new DateOnly(2026, 9, 1));

        corms.InitialCount = 3;
        corms.SyncStageWithCounts(new DateOnly(2026, 9, 2));

        Assert.Null(corms.FinishedOn);
    }

    [Fact]
    public void Raising_the_count_of_a_finished_batch_opens_it_again()
    {
        var corms = Batch(2);
        corms.RecordPottedUp(2, new DateOnly(2026, 9, 20));

        corms.InitialCount = 3;
        corms.SyncStageWithCounts(new DateOnly(2026, 9, 20));

        Assert.True(corms.IsActive);
        Assert.Equal(1, corms.RemainingCount);
    }

    [Fact]
    public void Validate_requires_a_name_and_a_sensible_count()
    {
        Assert.Contains("Give the propagation a nickname or a genus.", new Propagation().Validate(DateOnly.MaxValue));
        Assert.Contains("Start with at least 1.", new Propagation { Genus = "Coleus", InitialCount = 0 }.Validate(DateOnly.MaxValue));

        var corms = Batch(3);
        corms.RecordPottedUp(2, new DateOnly(2026, 9, 20));
        corms.InitialCount = 1;
        Assert.Contains("The count can't be lower than the 2 already potted up or failed.", corms.Validate(DateOnly.MaxValue));
    }

    [Fact]
    public void Germination_rate_is_the_share_that_came_up()
    {
        Assert.Equal(0.6, Seeds(20, 12).GerminationRate);
        Assert.Equal(0, Seeds(20, 0).GerminationRate);
        Assert.Null(Seeds(20, null).GerminationRate);

        var cuttings = Seeds(20, 12);
        cuttings.Type = PropagationType.Cutting;
        Assert.Null(cuttings.GerminationRate);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    [InlineData(201, false)]
    [InlineData(0.5, true)]
    [InlineData(200, true)]
    public void Validate_checks_the_corm_size(double mm, bool valid)
    {
        var day = new DateOnly(2026, 5, 1);
        var corm = new Propagation { Genus = "Alocasia", Type = PropagationType.Corm, StartedOn = day, CormSizeMm = (decimal)mm };
        Assert.Equal(valid, corm.Validate(day).Count == 0);
    }

    private static Propagation Seeds(int sown, int? up) =>
        new() { Genus = "Coleus", Type = PropagationType.Seed, InitialCount = sown, SeedsGerminated = up };

    [Fact]
    public void Validate_checks_the_seed_counts_and_date()
    {
        var day = new DateOnly(2026, 5, 1);
        var seeds = Seeds(10, 11);
        seeds.StartedOn = day;
        Assert.Contains("More seeds can't have come up than were sown.", seeds.Validate(day));

        seeds.SeedsGerminated = -1;
        Assert.Contains("The number that came up can't be negative.", seeds.Validate(day));

        seeds.SeedsGerminated = 10;
        seeds.FirstGerminatedOn = day.AddDays(-1);
        Assert.Contains("A seed can't come up before it was sown.", seeds.Validate(day));

        seeds.FirstGerminatedOn = day.AddDays(1);
        Assert.Contains("A seed can't come up in the future.", seeds.Validate(day));

        seeds.FirstGerminatedOn = day;
        Assert.Empty(seeds.Validate(day));
    }

    [Fact]
    public void Days_since_start_counts_from_zero()
    {
        var cutting = new Propagation { StartedOn = new DateOnly(2026, 9, 1) };

        Assert.Equal(0, cutting.DaysSinceStart(new DateOnly(2026, 9, 1)));
        Assert.Equal(24, cutting.DaysSinceStart(new DateOnly(2026, 9, 25)));
        Assert.Equal(0, cutting.DaysSinceStart(new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void Display_name_prefers_the_nickname()
    {
        Assert.Equal("Corm test", new Propagation { Nickname = "Corm test", Genus = "Alocasia" }.DisplayName);
        Assert.Equal("Alocasia zebrina", new Propagation { Genus = "alocasia", Species = "Zebrina" }.DisplayName);
        Assert.Equal("Unnamed propagation", new Propagation().DisplayName);
    }

    [Fact]
    public void Filter_shows_active_ones_oldest_first_and_searches_names()
    {
        var older = new Propagation { Genus = "Coleus", StartedOn = new DateOnly(2026, 8, 1) };
        var newer = new Propagation { Genus = "Alocasia", Species = "zebrina", StartedOn = new DateOnly(2026, 9, 1) };
        var finished = Batch(1);
        finished.RecordFailed(1, new DateOnly(2026, 9, 20));
        var all = new[] { newer, finished, older };

        Assert.Equal([older, newer], new PropagationFilter().Apply(all));
        Assert.Equal([finished], new PropagationFilter(Progress: ProgressFilter.Finished).Apply(all));
        Assert.Equal([newer], new PropagationFilter("zebr", ProgressFilter.All).Apply(all));
    }

    [Fact]
    public void By_stage_follows_the_order_of_the_stages()
    {
        var rooted = new Propagation { Stage = PropagationStage.Rooted };
        var started = new Propagation { Stage = PropagationStage.Started };

        var groups = PropagationFilter.ByStage([rooted, started]);

        Assert.Equal([PropagationStage.Started, PropagationStage.Rooted], groups.Select(g => g.Stage));
    }
}
