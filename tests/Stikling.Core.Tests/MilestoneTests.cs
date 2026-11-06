using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class MilestoneTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);

    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PropagationService service;

    public MilestoneTests() =>
        service = new PropagationService(propagations, new FakePlantRepository(), timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), new FixedTime(Now));

    private static string Label(Enum value) => value switch
    {
        RootingAid.HeatMat => "Heat mat",
        _ => value.ToString()
    };

    private static Propagation Corms(PropagationStage stage = PropagationStage.Started) => new()
    {
        Nickname = "Corm test: LECA",
        Type = PropagationType.Corm,
        Medium = GrowingMedium.Leca,
        StartedOn = Today.AddDays(-12),
        Stage = stage
    };

    [Fact]
    public void Days_to_a_milestone_count_from_the_start()
    {
        var corms = Corms();
        corms.FirstRootOn = Today.AddDays(-2);

        Assert.Equal(10, corms.DaysToFirstRoot);
        Assert.Null(corms.DaysToFirstLeaf);
    }

    [Fact]
    public void A_first_root_moves_a_started_propagation_to_rooting()
    {
        var corms = Corms();

        corms.RecordMilestone(Milestone.FirstRoot, Today);

        Assert.Equal(Today, corms.FirstRootOn);
        Assert.Equal(PropagationStage.Rooting, corms.Stage);
    }

    [Theory]
    [InlineData(PropagationStage.Rooting)]
    [InlineData(PropagationStage.Rooted)]
    public void A_first_root_leaves_a_later_stage_alone(PropagationStage stage)
    {
        var corms = Corms(stage);

        corms.RecordMilestone(Milestone.FirstRoot, Today);

        Assert.Equal(stage, corms.Stage);
    }

    [Fact]
    public void A_first_leaf_doesnt_change_the_stage()
    {
        var corms = Corms();

        corms.RecordMilestone(Milestone.FirstLeaf, Today);

        Assert.Equal(Today, corms.FirstLeafOn);
        Assert.Equal(PropagationStage.Started, corms.Stage);
    }

    [Fact]
    public void Moving_to_rooting_by_hand_doesnt_fill_in_a_first_root()
    {
        var corms = Corms();

        corms.SetStage(PropagationStage.Rooting);

        Assert.Null(corms.FirstRootOn);
    }

    [Fact]
    public async Task Moving_to_rooting_on_the_page_records_the_first_root_today()
    {
        var corms = Corms();
        await propagations.SaveAsync(corms);

        await service.SetStageAsync(corms, PropagationStage.Rooting, Label, seenToday: true);

        Assert.Equal(Today, corms.FirstRootOn);
        Assert.Null(corms.FirstGerminatedOn);
    }

    [Fact]
    public async Task Moving_to_rooting_on_the_page_keeps_a_first_root_already_seen()
    {
        var corms = Corms();
        corms.FirstRootOn = Today.AddDays(-3);
        await propagations.SaveAsync(corms);

        await service.SetStageAsync(corms, PropagationStage.Rooting, Label, seenToday: true);

        Assert.Equal(Today.AddDays(-3), corms.FirstRootOn);
    }

    [Fact]
    public async Task Seeds_moving_to_germinating_on_the_page_record_the_first_seedling()
    {
        var seeds = new Propagation
        {
            Nickname = "Tomatoes",
            Type = PropagationType.Seed,
            InitialCount = 12,
            StartedOn = Today.AddDays(-6)
        };
        await propagations.SaveAsync(seeds);

        await service.SetStageAsync(seeds, PropagationStage.Rooting, Label, seenToday: true);

        Assert.Equal(Today, seeds.FirstGerminatedOn);
        Assert.Null(seeds.FirstRootOn);
    }

    [Fact]
    public async Task Stepping_back_from_rooted_to_rooting_on_the_page_records_no_first_root()
    {
        var corms = Corms(PropagationStage.Rooted);
        await propagations.SaveAsync(corms);

        await service.SetStageAsync(corms, PropagationStage.Rooting, Label, seenToday: true);

        Assert.Null(corms.FirstRootOn);
    }

    [Fact]
    public async Task Moving_straight_to_rooted_on_the_page_records_no_first_root()
    {
        var corms = Corms();
        await propagations.SaveAsync(corms);

        await service.SetStageAsync(corms, PropagationStage.Rooted, Label, seenToday: true);

        Assert.Null(corms.FirstRootOn);
    }

    [Fact]
    public void Milestones_cant_be_recorded_on_a_finished_propagation()
    {
        var corms = Corms();
        corms.RecordFailed(1, new DateOnly(2026, 9, 20));

        Assert.Throws<InvalidOperationException>(() => corms.RecordMilestone(Milestone.FirstLeaf, Today));
    }

    [Fact]
    public void Validate_rejects_milestones_before_the_start_or_in_the_future()
    {
        var early = Corms();
        early.FirstRootOn = early.StartedOn.AddDays(-1);
        var future = Corms();
        future.FirstLeafOn = Today.AddDays(1);

        Assert.Contains("A root or leaf can't show before it was started.", early.Validate(Today));
        Assert.Contains("A root or leaf can't show in the future.", future.Validate(Today));
    }

    [Fact]
    public void Copy_doesnt_share_the_rooting_aids()
    {
        var corms = Corms();
        corms.RootingAids.Add(RootingAid.Cinnamon);

        var copy = corms.Copy();
        copy.RootingAids.Add(RootingAid.HeatMat);

        Assert.Equal([RootingAid.Cinnamon], corms.RootingAids);
    }

    [Fact]
    public async Task Recording_a_milestone_saves_it_and_goes_on_the_history()
    {
        var corms = Corms();

        await service.RecordMilestoneAsync(corms, Milestone.FirstRoot, Label);

        Assert.Same(corms, propagations.Propagations[corms.Id]);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal("Stage: Rooting (was Started)\nFirst root on day 12", entry.Text);
    }

    [Fact]
    public async Task A_first_root_date_set_while_editing_moves_it_to_rooting_too()
    {
        var corms = Corms();
        var before = corms.Copy();
        corms.FirstRootOn = Today.AddDays(-4);

        await service.UpdateAsync(before, corms, Label);

        Assert.Equal(PropagationStage.Rooting, corms.Stage);
    }

    [Fact]
    public void History_says_when_milestones_and_rooting_aids_change()
    {
        var before = Corms();
        before.FirstLeafOn = Today;
        var after = before.Copy();
        after.FirstLeafOn = null;
        after.RootingAids = [RootingAid.HeatMat, RootingAid.Cinnamon];

        Assert.Equal(["First leaf date cleared", "Rooting aids: Cinnamon, Heat mat"],
            PropagationChanges.Describe(before, after, Label));

        var cleared = after.Copy();
        cleared.RootingAids = [];
        Assert.Equal(["No rooting aids"], PropagationChanges.Describe(after, cleared, Label));
    }

    [Fact]
    public void The_same_rooting_aids_in_another_order_are_no_change()
    {
        var before = Corms();
        before.RootingAids = [RootingAid.Cinnamon, RootingAid.HeatMat];
        var after = before.Copy();
        after.RootingAids = [RootingAid.HeatMat, RootingAid.Cinnamon];

        Assert.Empty(PropagationChanges.Describe(before, after, Label));
    }

    [Fact]
    public void Rooting_aids_are_stored_as_text()
    {
        var corms = Corms();
        corms.RootingAids = [RootingAid.RootingPowder];

        var json = JsonSerializer.Serialize(corms, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"rootingAids\":[\"RootingPowder\"]", json);
    }

    private static bool Live(TimelineEntry e) => !e.IsDeleted;

    [Fact]
    public async Task A_first_leaf_can_be_recorded_on_a_chosen_day()
    {
        var corms = Corms();

        await service.RecordMilestoneAsync(corms, Milestone.FirstLeaf, Label, Today.AddDays(-5));

        Assert.Equal(Today.AddDays(-5), corms.FirstLeafOn);
        Assert.Equal("First leaf on day 7", Assert.Single(timeline.Entries).Text);
    }

    [Theory]
    [InlineData(-13)]
    [InlineData(1)]
    public async Task A_first_leaf_before_the_start_or_in_the_future_is_refused(int daysFromToday)
    {
        var corms = Corms();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordMilestoneAsync(corms, Milestone.FirstLeaf, Label, Today.AddDays(daysFromToday)));

        Assert.Null(corms.FirstLeafOn);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task Deleting_the_entry_of_a_first_leaf_clears_the_date()
    {
        var corms = Corms();
        await propagations.SaveAsync(corms);
        await service.RecordMilestoneAsync(corms, Milestone.FirstLeaf, Label, Today.AddDays(-5));
        var entry = Assert.Single(timeline.Entries);

        Assert.Equal([Milestone.FirstLeaf], await service.UndoneByDeleteAsync(entry));
        var cleared = await service.DeleteEntryAsync(entry);

        Assert.Equal([Milestone.FirstLeaf], cleared);
        Assert.Null(propagations.Propagations[corms.Id].FirstLeafOn);
        Assert.True(entry.IsDeleted);
        Assert.Single(timeline.Entries);
    }

    [Fact]
    public async Task Deleting_the_entry_with_the_first_root_clears_it_and_keeps_the_stage()
    {
        var corms = Corms();
        await propagations.SaveAsync(corms);
        await service.SetStageAsync(corms, PropagationStage.Rooting, Label, seenToday: true);
        var entry = Assert.Single(timeline.Entries);
        Assert.Contains("First root on day 12", entry.Text);

        var cleared = await service.DeleteEntryAsync(entry);

        Assert.Equal([Milestone.FirstRoot], cleared);
        Assert.Null(corms.FirstRootOn);
        Assert.Equal(PropagationStage.Rooting, corms.Stage);
        Assert.True(entry.IsDeleted);
        Assert.Single(timeline.Entries);
    }

    [Fact]
    public async Task A_date_changed_since_is_left_alone_when_the_entry_goes()
    {
        var corms = Corms();
        await propagations.SaveAsync(corms);
        await service.RecordMilestoneAsync(corms, Milestone.FirstLeaf, Label, Today.AddDays(-5));
        var entry = timeline.Entries.Single(Live);
        var before = corms.Copy();
        corms.FirstLeafOn = Today.AddDays(-3);
        await service.UpdateAsync(before, corms, Label);

        Assert.Empty(await service.UndoneByDeleteAsync(entry));
        var cleared = await service.DeleteEntryAsync(entry);

        Assert.Empty(cleared);
        Assert.Equal(Today.AddDays(-3), corms.FirstLeafOn);
        Assert.True(entry.IsDeleted);
    }

    [Fact]
    public async Task An_older_entry_with_only_its_text_is_matched()
    {
        var corms = Corms();
        corms.FirstLeafOn = Today.AddDays(-2);
        await propagations.SaveAsync(corms);
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = corms.Id,
            Kind = TimelineKind.Change,
            Text = HistoryText.English(HistoryText.Line("FirstLeafOnDay", HistoryArg.Of(10)), Label)
        };
        await timeline.AddAsync(entry);
        Assert.Null(entry.Lines);

        var cleared = await service.DeleteEntryAsync(entry);

        Assert.Equal([Milestone.FirstLeaf], cleared);
        Assert.Null(corms.FirstLeafOn);
    }

    [Fact]
    public async Task Deleting_a_note_clears_nothing_and_adds_no_entry()
    {
        var corms = Corms();
        corms.FirstLeafOn = Today.AddDays(-2);
        await propagations.SaveAsync(corms);
        var note = new TimelineEntry { SubjectType = SubjectType.Propagation, SubjectId = corms.Id, Kind = TimelineKind.Note, Text = "First leaf on day 10" };
        await timeline.AddAsync(note);

        var cleared = await service.DeleteEntryAsync(note);

        Assert.Empty(cleared);
        Assert.NotNull(corms.FirstLeafOn);
        Assert.Single(timeline.Entries);
    }
}
