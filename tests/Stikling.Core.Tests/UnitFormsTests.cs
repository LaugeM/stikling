using Stikling.Core.Models;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class UnitFormsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private IPropagationRepository propagationRepository;
    private ITimelineRepository timelineRepository;

    public UnitFormsTests()
    {
        propagationRepository = propagations;
        timelineRepository = timeline;
    }

    private UnitForms Forms()
    {
        var time = new FixedTime(Now);
        return new UnitForms(
            new PropagationService(propagationRepository, plants, timelineRepository, new FakePropagationCareLogRepository(),
                new FakeCareLogRepository(), new FakePhotoRepository(), time),
            new RoomService(new FakePlaceRepository(), plants, propagations),
            plants,
            time);
    }

    private Propagation Stored(Action<Propagation>? change = null)
    {
        var propagation = new Propagation { Nickname = "Corms", StartedOn = Today.AddDays(-10), InitialCount = 4 };
        change?.Invoke(propagation);
        propagations.Propagations[propagation.Id] = propagation;
        return propagation;
    }

    [Fact]
    public void Marking_failed_starts_at_one_today_and_finishes_when_all_are_left()
    {
        var form = Forms().Failed(Stored());

        Assert.Equal(1, form.Count);
        Assert.Equal(Today, form.Date);
        Assert.True(form.CountIsValid);
        Assert.False(form.Finishes);

        form.Count = 4;
        Assert.True(form.CountIsValid);
        Assert.True(form.Finishes);

        form.Count = 5;
        Assert.False(form.CountIsValid);
        form.Count = 0;
        Assert.False(form.CountIsValid);
    }

    [Fact]
    public void Giving_away_starts_at_one_today_and_finishes_when_all_are_left()
    {
        var form = Forms().GiveAway(Stored());

        Assert.Equal(1, form.Count);
        Assert.Equal(Today, form.Date);
        Assert.True(form.CountIsValid);
        Assert.False(form.Finishes);

        form.Count = 4;
        Assert.True(form.Finishes);
        form.Count = 5;
        Assert.False(form.CountIsValid);
    }

    [Fact]
    public async Task Marking_failed_records_the_reason_and_note()
    {
        var propagation = Stored();
        var form = Forms().Failed(propagation);
        form.Count = 2;
        form.Reason = FailReason.Rotted;
        form.Note = "too wet";

        var result = await form.SaveAsync();

        Assert.Null(result.LaterError);
        Assert.Empty(result.Plants);
        Assert.Equal(2, propagation.FailedCount);
        Assert.Contains(timeline.Entries, e => e.SubjectId == propagation.Id && e.Text.StartsWith("2 failed: "));
    }

    [Fact]
    public async Task A_failed_save_leaves_the_count_and_a_second_one_counts_once()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored();
        var form = Forms().Failed(propagation);
        form.Count = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());

        Assert.Equal(0, propagation.FailedCount);
        Assert.Empty(timeline.Entries);

        await form.SaveAsync();

        Assert.Equal(2, propagation.FailedCount);
        Assert.Equal(2, propagations.Propagations[propagation.Id].FailedCount);
        Assert.Single(timeline.Entries, e => e.SubjectId == propagation.Id);
    }

    [Fact]
    public async Task A_history_failure_after_marking_failed_is_a_later_error_with_the_count_stored_once()
    {
        timelineRepository = new FailingOnceTimeline(timeline, _ => true);
        var propagation = Stored();
        var form = Forms().Failed(propagation);

        var result = await form.SaveAsync();

        Assert.NotNull(result.LaterError);
        Assert.Equal(1, propagation.FailedCount);
        Assert.Equal(1, propagations.Propagations[propagation.Id].FailedCount);
    }

    [Fact]
    public async Task A_reload_during_marking_failed_still_reports_the_count_as_stored()
    {
        var propagation = Stored();
        var stale = propagation.Copy();
        FailedForm? form = null;
        timelineRepository = new FailingOnceTimeline(timeline, _ =>
        {
            // A sync reloads the page from before the count was stored, then the history line fails
            form!.Propagation = stale;
            return true;
        });
        form = Forms().Failed(propagation);

        var result = await form.SaveAsync();

        Assert.NotNull(result.LaterError);
        Assert.Equal(1, propagation.FailedCount);
        Assert.Equal(1, propagations.Propagations[propagation.Id].FailedCount);
    }

    [Fact]
    public async Task Giving_away_lowers_the_spare_count()
    {
        var propagation = Stored(p => p.SpareCount = 3);
        var form = Forms().GiveAway(propagation);
        form.Count = 2;
        form.To = "A friend";

        var result = await form.SaveAsync();

        Assert.Null(result.LaterError);
        Assert.Equal(2, propagation.GivenAwayCount);
        Assert.Equal(1, propagation.SpareCount);
        Assert.Contains(timeline.Entries, e => e.SubjectId == propagation.Id && e.Text == "Gave 2 to A friend");
    }

    [Fact]
    public async Task A_failed_give_away_leaves_the_counts_and_a_second_one_counts_once()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored(p => p.SpareCount = 3);
        var form = Forms().GiveAway(propagation);
        form.Count = 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());

        Assert.Equal(0, propagation.GivenAwayCount);
        Assert.Equal(3, propagation.SpareCount);

        await form.SaveAsync();

        Assert.Equal(2, propagation.GivenAwayCount);
        Assert.Equal(1, propagation.SpareCount);
    }

    [Fact]
    public async Task A_history_failure_after_giving_away_is_a_later_error_with_the_count_stored_once()
    {
        timelineRepository = new FailingOnceTimeline(timeline, _ => true);
        var propagation = Stored();
        var form = Forms().GiveAway(propagation);

        var result = await form.SaveAsync();

        Assert.NotNull(result.LaterError);
        Assert.Equal(1, propagation.GivenAwayCount);
        Assert.Equal(1, propagations.Propagations[propagation.Id].GivenAwayCount);
    }

    [Fact]
    public async Task Finishing_the_propagation_changes_the_callers_object_only_once_saved()
    {
        propagationRepository = new FailingOncePropagations(propagations);
        var propagation = Stored();
        var form = Forms().Failed(propagation);
        form.Count = 4;

        await Assert.ThrowsAsync<InvalidOperationException>(() => form.SaveAsync());
        Assert.Equal(PropagationStage.Started, propagation.Stage);
        Assert.Null(propagation.FinishedOn);

        await form.SaveAsync();
        Assert.Equal(PropagationStage.Failed, propagation.Stage);
        Assert.Equal(Today, propagation.FinishedOn);
    }
}
