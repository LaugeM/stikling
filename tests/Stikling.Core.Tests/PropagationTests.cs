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

        corms.RecordPottedUp(2);

        Assert.Equal(2, corms.PottedUpCount);
        Assert.Equal(1, corms.RemainingCount);
        Assert.True(corms.IsActive);
    }

    [Fact]
    public void Batch_is_done_when_the_last_unit_is_potted_up()
    {
        var corms = Batch(3);

        corms.RecordFailed(1);
        corms.RecordPottedUp(2);

        Assert.Equal(PropagationStage.Done, corms.Stage);
        Assert.False(corms.IsActive);
        Assert.Equal(0, corms.RemainingCount);
    }

    [Fact]
    public void Batch_failed_when_nothing_made_it()
    {
        var corms = Batch(3);

        corms.RecordFailed(1);
        corms.RecordFailed(2);

        Assert.Equal(PropagationStage.Failed, corms.Stage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    public void Counts_outside_what_is_left_are_rejected(int count)
    {
        var corms = Batch(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => corms.RecordPottedUp(count));
        Assert.Throws<ArgumentOutOfRangeException>(() => corms.RecordFailed(count));
        Assert.Equal(3, corms.RemainingCount);
    }

    [Fact]
    public void Finished_propagations_cannot_change_stage_or_counts()
    {
        var cutting = Batch(1);
        cutting.RecordPottedUp(1);

        Assert.Throws<InvalidOperationException>(() => cutting.SetStage(PropagationStage.Rooting));
        Assert.Throws<InvalidOperationException>(() => cutting.RecordFailed(1));
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
    public void Raising_the_count_of_a_finished_batch_opens_it_again()
    {
        var corms = Batch(2);
        corms.RecordPottedUp(2);

        corms.InitialCount = 3;
        corms.SyncStageWithCounts();

        Assert.True(corms.IsActive);
        Assert.Equal(1, corms.RemainingCount);
    }

    [Fact]
    public void Validate_requires_a_name_and_a_sensible_count()
    {
        Assert.Contains("Give the propagation a nickname or a genus.", new Propagation().Validate());
        Assert.Contains("Start with at least 1.", new Propagation { Genus = "Coleus", InitialCount = 0 }.Validate());

        var corms = Batch(3);
        corms.RecordPottedUp(2);
        corms.InitialCount = 1;
        Assert.Contains("The count can't be lower than the 2 already potted up or failed.", corms.Validate());
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
        finished.RecordFailed(1);
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
