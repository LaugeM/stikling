using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Problems;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class ProblemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static int made;

    private sealed class Setup
    {
        public FakeProblemRepository Problems { get; } = new();
        public FakeProblemUpdateRepository Updates { get; } = new();
        public FakeTimelineRepository Timeline { get; } = new();
        public ProblemService Service { get; }
        public Plant Plant { get; } = new() { Nickname = "Monstera" };

        public Setup() => Service = new ProblemService(Problems, Updates, Timeline, new FixedTime(Now));

        public Task<Problem> Open(DateOnly? started = null, string[]? signs = null, DateOnly? lookAgain = null, string? note = null) =>
            Service.OpenAsync(Plant, new Problem
            {
                StartedOn = started ?? Today.AddDays(-5),
                Signs = [.. signs ?? [nameof(ProblemSign.YellowLeaves)]],
                LookAgainOn = lookAgain,
                Note = note
            }, []);

        public IEnumerable<TimelineEntry> Lines => Timeline.Entries.Where(e => !e.IsDeleted);
    }

    private static Plant APlant(PlantStatus status = PlantStatus.Active, bool deleted = false) =>
        new() { Nickname = "Plant", Status = status, DeletedAt = deleted ? Now : null };

    private static Problem AProblem(Guid plant, DateOnly started, DateOnly? lookAgain = null, string[]? signs = null) =>
        new()
        {
            SubjectId = plant, StartedOn = started, LookAgainOn = lookAgain,
            Signs = [.. signs ?? [nameof(ProblemSign.YellowLeaves)]],
            CreatedAt = Now.AddSeconds(made++)
        };

    private static Problem Closed(Guid plant, ProblemOutcome outcome, DateOnly closedOn, params ProblemSign[] signs)
    {
        var problem = AProblem(plant, closedOn.AddDays(-10), signs: [.. signs.Select(s => s.ToString())]);
        problem.ClosedOn = closedOn;
        problem.Outcome = outcome;
        return problem;
    }

    private static ProblemUpdate AnUpdate(Guid problem, DateOnly on, DateOnly? lookAgain = null, params ProblemAction[] did) =>
        new()
        {
            ProblemId = problem, OccurredOn = on, LookAgainOn = lookAgain,
            Did = [.. did.Select(a => a.ToString())],
            CreatedAt = Now.AddSeconds(made++)
        };

    // Opening

    [Fact]
    public async Task Opening_writes_one_line_with_the_signs_and_photos_and_stores_its_id()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();

        var problem = await s.Service.OpenAsync(s.Plant, new Problem
        {
            StartedOn = Today.AddDays(-2),
            Signs = ["YellowLeaves", "Drooping", "Drooping"],
            Note = "  Lower leaves first  "
        }, [photo]);

        var line = Assert.Single(s.Lines);
        Assert.Equal("Something's wrong: yellow leaves, drooping\nLower leaves first", line.Text);
        Assert.Equal([photo], line.PhotoIds);
        Assert.Equal(TimelineEvent.Problem, line.Event);
        Assert.Equal(TimelineKind.Change, line.Kind);
        Assert.Equal(s.Plant.Id, line.SubjectId);
        Assert.Equal(Today.AddDays(-2), DateOnly.FromDateTime(line.OccurredAt.DateTime));
        Assert.Equal(line.Id, problem.TimelineEntryId);
        Assert.Equal(["YellowLeaves", "Drooping"], problem.Signs);
        Assert.Equal("Lower leaves first", problem.Note);
        Assert.Equal(s.Plant.Id, problem.SubjectId);
        Assert.Equal(SubjectType.Plant, problem.SubjectType);
        Assert.True(problem.IsOpen);
        Assert.Single(await s.Service.AllAsync());
    }

    [Fact]
    public async Task A_problem_without_signs_or_with_a_future_start_is_refused_and_writes_nothing()
    {
        var s = new Setup();

        var none = await Assert.ThrowsAsync<ArgumentException>(() => s.Open(signs: []));
        var future = await Assert.ThrowsAsync<ArgumentException>(() => s.Open(started: Today.AddDays(1)));

        Assert.Equal("Pick what you see.", none.Message);
        Assert.Equal("That start date is in the future.", future.Message);
        Assert.Empty(s.Timeline.Entries);
        Assert.Empty(s.Problems.Problems);
    }

    [Fact]
    public void Validation_covers_the_closing_and_the_look_again_day()
    {
        var plant = Guid.NewGuid();
        var started = Today.AddDays(-3);

        var future = AProblem(plant, started);
        future.ClosedOn = Today.AddDays(1);
        future.Outcome = ProblemOutcome.GotBetter;
        var before = AProblem(plant, started);
        before.ClosedOn = started.AddDays(-1);
        before.Outcome = ProblemOutcome.GotBetter;
        var noOutcome = AProblem(plant, started);
        noOutcome.ClosedOn = Today;
        var noDay = AProblem(plant, started);
        noDay.Outcome = ProblemOutcome.Died;

        Assert.Contains("That date is in the future.", future.Validate(Today));
        Assert.Contains("A problem can't end before it started.", before.Validate(Today));
        // A newer version's outcome reads as null, and the problem still saves
        Assert.DoesNotContain("Say how it ended.", noOutcome.Validate(Today));
        Assert.Contains("Say how it ended.", noDay.Validate(Today));
        Assert.Contains("Look again after it started.", AProblem(plant, started, started.AddDays(-1)).Validate(Today));
        Assert.Contains("A problem belongs to a plant.", AProblem(Guid.Empty, started).Validate(Today));
    }

    [Fact]
    public void Signs_this_version_does_not_know_survive_a_round_trip_and_are_skipped()
    {
        var problem = JsonSerializer.Deserialize<Problem>(
            """{"signs":["YellowLeaves","Thrips","Drooping"],"outcome":"Vanished","startedOn":"2026-10-01"}""", Web)!;

        Assert.Equal([ProblemSign.YellowLeaves, ProblemSign.Drooping], problem.KnownSigns);
        Assert.Null(problem.Outcome);
        Assert.Contains("\"signs\":[\"YellowLeaves\",\"Thrips\",\"Drooping\"]", JsonSerializer.Serialize(problem, Web));
        Assert.Equal(["YellowLeaves", "Thrips", "Drooping"], problem.Copy().Signs);
        Assert.NotSame(problem.Signs, problem.Copy().Signs);
    }

    [Fact]
    public void An_unknown_trend_reads_as_null_and_unknown_actions_are_kept()
    {
        var update = JsonSerializer.Deserialize<ProblemUpdate>(
            """{"trend":"Gone","did":["MovedIt","Misted"],"occurredOn":"2026-10-01"}""", Web)!;

        Assert.Null(update.Trend);
        Assert.Equal([ProblemAction.MovedIt], update.KnownDid);
        Assert.Contains("\"did\":[\"MovedIt\",\"Misted\"]", JsonSerializer.Serialize(update, Web));
        Assert.Contains("\"trend\":null", JsonSerializer.Serialize(update, Web));
    }

    // Updates

    [Fact]
    public async Task An_update_writes_a_line_with_the_trend_and_what_was_done()
    {
        var s = new Setup();
        var problem = await s.Open();
        var photo = Guid.NewGuid();

        var update = await s.Service.AddUpdateAsync(problem, new ProblemUpdate
        {
            OccurredOn = Today.AddDays(-1),
            Trend = ProblemTrend.Better,
            Did = ["MovedIt", "WateredLess"],
            Note = "  Looks perkier "
        }, [photo]);

        var line = s.Lines.Single(l => l.Id == update.TimelineEntryId);
        Assert.Equal("Better · Moved it, watered less\nLooks perkier", line.Text);
        Assert.Equal([photo], line.PhotoIds);
        Assert.Equal(problem.Id, update.ProblemId);
        Assert.Equal(2, s.Lines.Count());
    }

    [Fact]
    public async Task An_empty_update_or_one_before_the_start_is_refused()
    {
        var s = new Setup();
        var problem = await s.Open(started: Today.AddDays(-3));

        var empty = await Assert.ThrowsAsync<ArgumentException>(() =>
            s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Note = "  " }, []));
        var early = await Assert.ThrowsAsync<ArgumentException>(() =>
            s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today.AddDays(-4), Trend = ProblemTrend.Same }, []));
        var photoOnly = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today }, [Guid.NewGuid()]);

        Assert.Equal("Say how it looks, what you did, or add a note or a photo.", empty.Message);
        Assert.Equal("An update can't be before the problem started.", early.Message);
        Assert.Equal("Update on the problem", s.Lines.Single(l => l.Id == photoOnly.TimelineEntryId).Text);
        Assert.Single(s.Updates.Updates);
    }

    [Fact]
    public async Task Editing_a_problem_rewrites_its_opening_line_and_keeps_the_photos()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();
        var problem = await s.Service.OpenAsync(s.Plant, new Problem { StartedOn = Today.AddDays(-5), Signs = ["Spots"] }, [photo]);

        problem.Signs = ["Spots", "LeafDrop"];
        problem.StartedOn = Today.AddDays(-7);
        problem.Note = "Brown edges";
        await s.Service.EditAsync(problem);

        var line = Assert.Single(s.Lines);
        Assert.Equal("Something's wrong: spots on leaves, dropping leaves\nBrown edges", line.Text);
        Assert.Equal(Today.AddDays(-7), DateOnly.FromDateTime(line.OccurredAt.DateTime));
        Assert.Equal([photo], line.PhotoIds);
        Assert.Contains(line.Id, s.Timeline.Updated);
    }

    [Fact]
    public async Task Editing_with_its_line_gone_still_saves()
    {
        var s = new Setup();
        var problem = await s.Open();
        s.Timeline.Entries.Clear();

        problem.Note = "More";
        await s.Service.EditAsync(problem);

        Assert.Equal("More", s.Problems.Problems[problem.Id].Note);
        Assert.Empty(s.Timeline.Entries);
    }

    [Fact]
    public async Task Editing_an_update_rewrites_its_line_and_counts_the_photos_already_on_it()
    {
        var s = new Setup();
        var problem = await s.Open();
        var update = await s.Service.AddUpdateAsync(problem,
            new ProblemUpdate { OccurredOn = Today.AddDays(-2), Trend = ProblemTrend.Same }, [Guid.NewGuid()]);

        update.Trend = null;
        update.Did = ["Repotted"];
        update.OccurredOn = Today.AddDays(-1);
        await s.Service.EditUpdateAsync(problem, update);

        var line = s.Lines.Single(l => l.Id == update.TimelineEntryId);
        Assert.Equal("Repotted", line.Text);
        Assert.Single(line.PhotoIds);
        Assert.Equal(Today.AddDays(-1), DateOnly.FromDateTime(line.OccurredAt.DateTime));

        // Emptied out, but the photo on its line still counts
        update.Did = [];
        await s.Service.EditUpdateAsync(problem, update);
        Assert.Equal("Update on the problem", s.Lines.Single(l => l.Id == update.TimelineEntryId).Text);

        update.OccurredOn = problem.StartedOn.AddDays(-1);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.EditUpdateAsync(problem, update));
    }

    [Fact]
    public async Task Removing_an_update_deletes_it_and_its_line()
    {
        var s = new Setup();
        var problem = await s.Open();
        var update = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Trend = ProblemTrend.Worse }, []);

        await s.Service.RemoveUpdateAsync(update);

        Assert.Empty(await s.Service.UpdatesForAsync(problem.Id));
        Assert.Single(s.Lines);
        Assert.Equal(problem.TimelineEntryId, s.Lines.Single().Id);
    }

    // Closing

    [Fact]
    public async Task Closing_sets_the_outcome_and_writes_a_line_and_reopening_keeps_that_line()
    {
        var s = new Setup();
        var problem = await s.Open(started: Today.AddDays(-5));

        await s.Service.CloseAsync(problem, ProblemOutcome.GotBetter, Today.AddDays(-1));

        Assert.False(problem.IsOpen);
        Assert.Equal(ProblemOutcome.GotBetter, problem.Outcome);
        Assert.Equal(Today.AddDays(-1), problem.ClosedOn);
        var closing = s.Lines.Single(l => l.Id == problem.ClosedTimelineEntryId);
        Assert.Equal("Problem over: got better", closing.Text);

        await s.Service.ReopenAsync(problem);

        Assert.True(problem.IsOpen);
        Assert.Null(problem.Outcome);
        Assert.Null(problem.ClosedTimelineEntryId);
        Assert.Contains(closing, s.Lines);
        Assert.Contains(s.Lines, l => l.Text == "The problem came back" && l.Event == TimelineEvent.Problem);
        Assert.Equal(3, s.Lines.Count());
    }

    [Fact]
    public async Task Reopening_does_not_bring_back_a_look_again_day_from_before_it_closed()
    {
        var s = new Setup();
        var problem = await s.Open(started: Today.AddDays(-20));
        var update = await s.Service.AddUpdateAsync(problem,
            new ProblemUpdate { OccurredOn = Today.AddDays(-10), Trend = ProblemTrend.Same, LookAgainOn = Today.AddDays(-7) }, []);
        await s.Service.CloseAsync(problem, ProblemOutcome.GotBetter, Today.AddDays(-5));

        await s.Service.ReopenAsync(problem);

        Assert.Equal(Today, problem.ReopenedOn);
        Assert.Null(ProblemService.LookAgainOn(problem, [update]));

        var later = await s.Service.AddUpdateAsync(problem,
            new ProblemUpdate
            {
                OccurredOn = Today, Trend = ProblemTrend.Worse, LookAgainOn = Today.AddDays(3),
                // The fake keeps its own clock, while the real repositories use the app's
                CreatedAt = problem.ReopenedAt!.Value.AddSeconds(1)
            }, []);
        Assert.Equal(Today.AddDays(3), ProblemService.LookAgainOn(problem, [update, later]));
    }

    [Fact]
    public void A_problem_with_an_outcome_or_signs_this_version_does_not_know_still_reads()
    {
        var problem = new Problem { SubjectId = Guid.NewGuid(), StartedOn = Today.AddDays(-23), ClosedOn = Today, Signs = ["Mould"] };

        Assert.Equal("Over after 3 weeks", ProblemText.OverAfter(problem));
        Assert.Equal("A problem", ProblemText.Title(problem));
    }

    [Fact]
    public async Task Closing_before_the_start_or_in_the_future_is_refused_and_changes_nothing()
    {
        var s = new Setup();
        var problem = await s.Open(started: Today.AddDays(-5));

        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CloseAsync(problem, ProblemOutcome.Died, Today.AddDays(-6)));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CloseAsync(problem, ProblemOutcome.Died, Today.AddDays(1)));

        Assert.True(problem.IsOpen);
        Assert.Null(problem.Outcome);
        Assert.Single(s.Lines);
    }

    [Fact]
    public async Task Removing_a_problem_deletes_its_updates_and_every_line()
    {
        var s = new Setup();
        var problem = await s.Open();
        await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today.AddDays(-1), Trend = ProblemTrend.Same }, []);
        await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Trend = ProblemTrend.Better }, []);
        await s.Service.CloseAsync(problem, ProblemOutcome.WentAway, Today);

        await s.Service.RemoveAsync(problem);

        Assert.Empty(await s.Service.AllAsync());
        Assert.Empty(await s.Service.AllUpdatesAsync());
        Assert.Empty(s.Lines);
    }

    [Fact]
    public async Task A_plants_problems_list_open_ones_first_then_the_newest()
    {
        var s = new Setup();
        var oldOpen = await s.Open(started: Today.AddDays(-20));
        var newOpen = await s.Open(started: Today.AddDays(-2));
        var done = await s.Open(started: Today.AddDays(-1));
        await s.Service.CloseAsync(done, ProblemOutcome.GotBetter, Today);
        var other = new Plant();
        await s.Service.OpenAsync(other, new Problem { StartedOn = Today, Signs = ["Spots"] }, []);

        var list = await s.Service.ForPlantAsync(s.Plant.Id);

        Assert.Equal([newOpen.Id, oldOpen.Id, done.Id], list.Select(p => p.Id));
    }

    [Fact]
    public async Task Updates_come_oldest_first()
    {
        var s = new Setup();
        var problem = await s.Open();
        var late = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Trend = ProblemTrend.Better }, []);
        var early = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today.AddDays(-2), Trend = ProblemTrend.Worse }, []);

        var list = await s.Service.UpdatesForAsync(problem.Id);

        Assert.Equal([early.Id, late.Id], list.Select(u => u.Id));
        Assert.Equal(ProblemTrend.Better, ProblemService.LatestTrend(list));
        Assert.Null(ProblemService.LatestTrend([]));
    }

    // Look again

    [Fact]
    public void Look_again_comes_from_the_problem_until_a_newer_update_replaces_it()
    {
        var plant = Guid.NewGuid();
        var problem = AProblem(plant, Today.AddDays(-5), Today.AddDays(3));
        Assert.Equal(Today.AddDays(3), ProblemService.LookAgainOn(problem, []));

        var newer = AnUpdate(problem.Id, Today.AddDays(-1), Today.AddDays(6));
        Assert.Equal(Today.AddDays(6), ProblemService.LookAgainOn(problem, [newer]));

        var older = AnUpdate(problem.Id, Today.AddDays(-9), Today.AddDays(20));
        Assert.Equal(Today.AddDays(3), ProblemService.LookAgainOn(problem, [older]));
    }

    [Fact]
    public void A_newer_update_without_a_day_ends_the_reminders_and_a_closed_problem_has_none()
    {
        var problem = AProblem(Guid.NewGuid(), Today.AddDays(-5), Today.AddDays(3));
        var cleared = AnUpdate(problem.Id, Today.AddDays(-1));
        var removed = AnUpdate(problem.Id, Today, Today.AddDays(9));
        removed.DeletedAt = Now;

        Assert.Null(ProblemService.LookAgainOn(problem, [cleared, removed]));

        problem.ClosedOn = Today;
        problem.Outcome = ProblemOutcome.GotBetter;
        Assert.Null(ProblemService.LookAgainOn(problem, []));
    }

    [Fact]
    public void Look_again_is_due_on_the_day_and_after_and_the_longest_wait_comes_first()
    {
        var a = APlant();
        var b = APlant();
        var dueToday = AProblem(a.Id, Today.AddDays(-9), Today);
        var older = AProblem(b.Id, Today.AddDays(-9), Today.AddDays(-4));
        var notYet = AProblem(a.Id, Today.AddDays(-9), Today.AddDays(1));
        var noDay = AProblem(b.Id, Today.AddDays(-9));

        var due = ProblemService.LookAgainDue([dueToday, older, notYet, noDay], [], [a, b], Today);

        Assert.Equal([older.Id, dueToday.Id], due.Select(d => d.Problem.Id));
        Assert.Equal(Today.AddDays(-4), due[0].Since);
        Assert.Same(b, due[0].Plant);
    }

    [Fact]
    public void Look_again_leaves_out_plants_that_are_gone_deleted_or_not_active_and_closed_problems()
    {
        var gone = Guid.NewGuid();
        var deleted = APlant(deleted: true);
        var died = APlant(PlantStatus.Died);
        var closed = APlant();
        var closedProblem = AProblem(closed.Id, Today.AddDays(-9), Today.AddDays(-1));
        closedProblem.ClosedOn = Today;
        closedProblem.Outcome = ProblemOutcome.GotBetter;

        var due = ProblemService.LookAgainDue(
        [
            AProblem(gone, Today.AddDays(-9), Today),
            AProblem(deleted.Id, Today.AddDays(-9), Today),
            AProblem(died.Id, Today.AddDays(-9), Today),
            closedProblem
        ], [], [deleted, died, closed], Today);

        Assert.Empty(due);
    }

    // During a problem

    [Fact]
    public void Plain_changes_during_the_problem_are_listed_and_the_rest_is_left_out()
    {
        var s = new Setup();
        var problem = AProblem(s.Plant.Id, Today.AddDays(-10));
        problem.ClosedOn = Today.AddDays(-2);
        problem.Outcome = ProblemOutcome.GotBetter;
        TimelineEntry Entry(int daysAgo, string text, TimelineKind kind = TimelineKind.Change, TimelineEvent? ev = null) =>
            new() { Kind = kind, Text = text, Event = ev, OccurredAt = Now.AddDays(-daysAgo), SubjectId = s.Plant.Id };

        var repot = Entry(6, "Repotted");
        var firstDay = Entry(10, "Moved to Kitchen");
        var lastDay = Entry(2, "Pruned");
        var gone = Entry(5, "Repotted again");
        gone.DeletedAt = Now;

        var result = s.Service.DuringProblem(problem,
        [
            lastDay, repot, firstDay, gone,
            Entry(6, "Something's wrong: spots", ev: TimelineEvent.Problem),
            Entry(6, "In flower", ev: TimelineEvent.Flowering),
            Entry(6, "Looked over", ev: TimelineEvent.LookedOver),
            Entry(6, "A note", TimelineKind.Note),
            Entry(11, "Before"),
            Entry(1, "After")
        ]);

        Assert.Equal([firstDay, repot, lastDay], result);
    }

    [Fact]
    public void An_open_problem_runs_until_today()
    {
        var s = new Setup();
        var problem = AProblem(s.Plant.Id, Today.AddDays(-3));
        var today = new TimelineEntry { Kind = TimelineKind.Change, Text = "Repotted", OccurredAt = Now };

        Assert.Equal([today], s.Service.DuringProblem(problem, [today]));
    }

    // Past problems

    [Fact]
    public void A_shared_sign_matches_and_other_on_its_own_does_not()
    {
        var plant = APlant();
        var mine = AProblem(plant.Id, Today.AddDays(-2), signs: ["YellowLeaves", "Drooping", "Other"]);
        var yellow = Closed(plant.Id, ProblemOutcome.GotBetter, Today.AddDays(-30), ProblemSign.YellowLeaves, ProblemSign.Sunburn);
        var otherOnly = Closed(plant.Id, ProblemOutcome.Died, Today.AddDays(-20), ProblemSign.Other);
        var unrelated = Closed(plant.Id, ProblemOutcome.Died, Today.AddDays(-20), ProblemSign.RootRot);

        var past = ProblemService.PastOfSameKind(mine, [mine, yellow, otherOnly, unrelated], [], [plant]);

        var found = Assert.Single(past);
        Assert.Same(yellow, found.Problem);
        Assert.Equal([ProblemSign.YellowLeaves], found.Shared);
        Assert.Equal(ProblemOutcome.GotBetter, found.Outcome);
    }

    [Fact]
    public void Open_deleted_and_plantless_problems_are_left_out_and_the_newest_end_comes_first()
    {
        var plant = APlant();
        var deletedPlant = APlant(deleted: true);
        var mine = AProblem(plant.Id, Today.AddDays(-2), signs: ["Spots"]);
        var open = AProblem(plant.Id, Today.AddDays(-9), signs: ["Spots"]);
        var removed = Closed(plant.Id, ProblemOutcome.GotBetter, Today.AddDays(-9), ProblemSign.Spots);
        removed.DeletedAt = Now;
        var orphan = Closed(Guid.NewGuid(), ProblemOutcome.GotBetter, Today.AddDays(-9), ProblemSign.Spots);
        var onDeleted = Closed(deletedPlant.Id, ProblemOutcome.GotBetter, Today.AddDays(-9), ProblemSign.Spots);
        var older = Closed(plant.Id, ProblemOutcome.DidntHelp, Today.AddDays(-40), ProblemSign.Spots);
        var newer = Closed(plant.Id, ProblemOutcome.GotBetter, Today.AddDays(-10), ProblemSign.Spots);

        var past = ProblemService.PastOfSameKind(mine, [mine, open, removed, orphan, onDeleted, older, newer], [], [plant, deletedPlant]);

        Assert.Equal([newer.Id, older.Id], past.Select(p => p.Problem.Id));
    }

    [Fact]
    public void The_actions_of_a_past_problem_come_in_the_order_they_were_first_done()
    {
        var plant = APlant();
        var mine = AProblem(plant.Id, Today, signs: ["RootRot"]);
        var before = Closed(plant.Id, ProblemOutcome.GotBetter, Today.AddDays(-3), ProblemSign.RootRot);
        var later = AnUpdate(before.Id, Today.AddDays(-5), null, ProblemAction.WateredLess, ProblemAction.MovedIt);
        var earlier = AnUpdate(before.Id, Today.AddDays(-8), null, ProblemAction.Repotted, ProblemAction.WateredLess);
        var removed = AnUpdate(before.Id, Today.AddDays(-7), null, ProblemAction.MoreLight);
        removed.DeletedAt = Now;
        var elsewhere = AnUpdate(Guid.NewGuid(), Today.AddDays(-7), null, ProblemAction.LessLight);

        var past = Assert.Single(ProblemService.PastOfSameKind(mine, [mine, before], [later, earlier, removed, elsewhere], [plant]));

        Assert.Equal([ProblemAction.Repotted, ProblemAction.WateredLess, ProblemAction.MovedIt], past.Did);
    }

    // Words

    [Fact]
    public void Signs_and_actions_read_as_a_list_with_the_first_one_capitalised()
    {
        Assert.Equal("Yellow leaves, drooping, root rot",
            ProblemText.Signs([ProblemSign.YellowLeaves, ProblemSign.Drooping, ProblemSign.RootRot]));
        Assert.Equal("Moved it, watered less", ProblemText.Actions([ProblemAction.MovedIt, ProblemAction.WateredLess]));
        Assert.Equal("Sunburn", ProblemText.Signs([ProblemSign.Sunburn]));
        Assert.Equal("", ProblemText.Signs([]));
        Assert.Equal("Brown or crispy tips, bumps under the leaves",
            ProblemText.CauseOfDeath(new Problem { Signs = ["BrownTips", "Gone", "Edema"] }));
    }

    [Fact]
    public void The_history_lines_read_plainly()
    {
        Assert.Equal("Something's wrong: yellow leaves, drooping",
            ProblemText.OpenedLine(new Problem { Signs = ["YellowLeaves", "Drooping"], Note = "  " }));
        Assert.Equal("Something's wrong: sunburn\nOn the south sill",
            ProblemText.OpenedLine(new Problem { Signs = ["Sunburn"], Note = " On the south sill " }));
        Assert.Equal("Better · Moved it, watered less",
            ProblemText.UpdateLine(new ProblemUpdate { Trend = ProblemTrend.Better, Did = ["MovedIt", "WateredLess"] }));
        Assert.Equal("About the same", ProblemText.UpdateLine(new ProblemUpdate { Trend = ProblemTrend.Same }));
        Assert.Equal("Moved it\nNew spot", ProblemText.UpdateLine(new ProblemUpdate { Did = ["MovedIt"], Note = "New spot" }));
        Assert.Equal("Update on the problem", ProblemText.UpdateLine(new ProblemUpdate()));
        Assert.Equal("Problem over: got better", ProblemText.ClosedLine(ProblemOutcome.GotBetter));
        Assert.Equal("Problem over: it died", ProblemText.ClosedLine(ProblemOutcome.Died));
        Assert.Equal("Problem over: went away by itself", ProblemText.ClosedLine(ProblemOutcome.WentAway));
        Assert.Equal("Problem over: nothing helped", ProblemText.ClosedLine(ProblemOutcome.DidntHelp));
        Assert.Equal("The problem came back", ProblemText.ReopenedLine);
    }

    // The pieces the screens use

    [Fact]
    public void Look_again_gap_is_the_closest_choice_and_follows_the_newest_record()
    {
        var plant = Guid.NewGuid();
        var problem = AProblem(plant, Today.AddDays(-10), lookAgain: Today.AddDays(-4));
        Assert.Equal(7, ProblemService.LookAgainGap(problem, []));

        var update = AnUpdate(problem.Id, Today.AddDays(-2), lookAgain: Today.AddDays(1));
        Assert.Equal(3, ProblemService.LookAgainGap(problem, [update]));

        var endsIt = AnUpdate(problem.Id, Today.AddDays(-1));
        Assert.Null(ProblemService.LookAgainGap(problem, [update, endsIt]));

        var wide = AnUpdate(problem.Id, Today.AddDays(-1), lookAgain: Today.AddDays(20));
        Assert.Equal(14, ProblemService.LookAgainGap(problem, [wide]));
        Assert.Null(ProblemService.LookAgainGap(Closed(plant, ProblemOutcome.GotBetter, Today), []));
    }

    [Fact]
    public async Task Photo_counts_come_from_the_lines_of_the_update_or_the_whole_problem()
    {
        var s = new Setup();
        var problem = await s.Service.OpenAsync(s.Plant, new Problem
        {
            StartedOn = Today.AddDays(-3), Signs = [nameof(ProblemSign.Spots)]
        }, [Guid.NewGuid()]);
        var update = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today.AddDays(-1) }, [Guid.NewGuid(), Guid.NewGuid()]);

        Assert.Equal(2, await s.Service.PhotoCountAsync(problem, update));
        Assert.Equal(3, await s.Service.PhotoCountAsync(problem));
    }

    [Fact]
    public async Task Removing_an_update_keeps_the_photos_on_its_line_as_a_plain_photo_entry()
    {
        var s = new Setup();
        var problem = await s.Open();
        var photo = Guid.NewGuid();
        var update = await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Trend = ProblemTrend.Worse }, [photo]);

        await s.Service.RemoveUpdateAsync(update);

        Assert.Empty(await s.Service.UpdatesForAsync(problem.Id));
        var kept = s.Lines.Single(l => l.Id == update.TimelineEntryId);
        Assert.Equal(TimelineKind.Photo, kept.Kind);
        Assert.Null(kept.Event);
        Assert.Null(kept.Text);
        Assert.Equal([photo], kept.PhotoIds);
    }

    [Fact]
    public async Task Removing_a_problem_keeps_lines_with_photos_and_deletes_the_rest()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();
        var problem = await s.Service.OpenAsync(s.Plant, new Problem
        {
            StartedOn = Today.AddDays(-3), Signs = [nameof(ProblemSign.Spots)]
        }, [photo]);
        await s.Service.AddUpdateAsync(problem, new ProblemUpdate { OccurredOn = Today, Trend = ProblemTrend.Better }, []);

        await s.Service.RemoveAsync(problem);

        Assert.Empty(await s.Service.AllAsync());
        var kept = Assert.Single(s.Lines);
        Assert.Equal(problem.TimelineEntryId, kept.Id);
        Assert.Equal(TimelineKind.Photo, kept.Kind);
        Assert.Equal([photo], kept.PhotoIds);
    }

    [Fact]
    public async Task The_log_is_oldest_first_with_the_changes_made_meanwhile_and_the_close()
    {
        var s = new Setup();
        var photo = Guid.NewGuid();
        var problem = await s.Open(Today.AddDays(-6), note: "Lower leaves");
        var update = await s.Service.AddUpdateAsync(problem,
            new ProblemUpdate { OccurredOn = Today.AddDays(-4), Trend = ProblemTrend.Worse }, [photo]);
        s.Timeline.Entries.Add(new TimelineEntry
        {
            SubjectId = s.Plant.Id, Kind = TimelineKind.Change, Text = "Repotted", OccurredAt = Now.AddDays(-5)
        });
        s.Timeline.Entries.Add(new TimelineEntry
        {
            SubjectId = s.Plant.Id, Kind = TimelineKind.Change, Text = "Repotted long before", OccurredAt = Now.AddDays(-30)
        });
        await s.Service.CloseAsync(problem, ProblemOutcome.GotBetter, Today.AddDays(-1));

        var entries = await s.Timeline.GetForAsync(s.Plant.Id);
        var log = s.Service.Log(problem, await s.Service.UpdatesForAsync(problem.Id), entries);

        Assert.Equal(
            [ProblemLogKind.Started, ProblemLogKind.Change, ProblemLogKind.Update, ProblemLogKind.Closed],
            log.Select(i => i.Kind));
        Assert.Equal("Repotted", log[1].Entry!.Text);
        Assert.Equal(update.Id, log[2].Update!.Id);
        Assert.Equal([photo], log[2].PhotoIds);
        Assert.Equal(Today.AddDays(-1), log[3].Day);
    }

    [Fact]
    public void Dates_and_spans_read_the_way_the_screens_show_them()
    {
        Assert.Equal("12 Oct", ProblemText.ShortDay(new DateOnly(2026, 10, 12)));
        Assert.Equal("Mar 2026", ProblemText.MonthYear(new DateOnly(2026, 3, 30)));
        Assert.Equal("1 day", ProblemText.Duration(1));
        Assert.Equal("13 days", ProblemText.Duration(13));
        Assert.Equal("3 weeks", ProblemText.Duration(21));
        Assert.Equal("2 months", ProblemText.Duration(70));
        Assert.Equal("2 years", ProblemText.Duration(800));

        var problem = Closed(Guid.NewGuid(), ProblemOutcome.GotBetter, new DateOnly(2026, 3, 30), ProblemSign.RootRot);
        Assert.Equal("Mar 2026 · 10 days · got better", ProblemText.PastLine(problem));
        Assert.Equal("Over after 10 days: got better", ProblemText.OverAfter(problem));
        Assert.Equal(9, ProblemText.DaysLasted(AProblem(Guid.NewGuid(), Today.AddDays(-9)), Today));
    }

    [Fact]
    public void The_trend_row_is_read_aloud_and_empty_without_trends()
    {
        Assert.Equal("Updates: worse, the same, better",
            ProblemText.TrendsLabel([ProblemTrend.Worse, ProblemTrend.Same, ProblemTrend.Better]));
        Assert.Null(ProblemText.TrendsLabel([]));
    }

    [Fact]
    public void An_earlier_problem_reads_with_what_was_done_and_how_it_ended()
    {
        var plant = APlant();
        var old = Closed(plant.Id, ProblemOutcome.DidntHelp, new DateOnly(2026, 3, 5), ProblemSign.YellowLeaves);
        var past = new PastProblem(old, plant, [ProblemSign.YellowLeaves], [ProblemAction.MovedIt, ProblemAction.WateredLess], ProblemOutcome.DidntHelp);

        Assert.Equal("Mar 2026 · Moved it, watered less · nothing helped", ProblemText.PastOfSameKindLine(past));
        Assert.Equal("Mar 2026 · got better",
            ProblemText.PastOfSameKindLine(past with { Did = [], Outcome = ProblemOutcome.GotBetter }));
    }

    [Fact]
    public void A_look_again_put_off_ends_when_a_new_day_is_picked()
    {
        var id = Guid.NewGuid();
        var putOffs = PutOffs.Empty();
        putOffs.PutOff(PutOffs.Problem(id, Today), Today.AddDays(3));

        Assert.True(putOffs.IsPutOff(PutOffs.Problem(id, Today), Today));
        Assert.False(putOffs.IsPutOff(PutOffs.Problem(id, Today.AddDays(7)), Today));
    }

    [Fact]
    public void The_plant_filter_can_be_limited_to_some_plants()
    {
        var a = new Plant { Nickname = "A" };
        var b = new Plant { Nickname = "B" };

        Assert.Equal([b], new PlantFilter(OnlyIds: new HashSet<Guid> { b.Id }).Apply([a, b]));
        Assert.Equal([a, b], new PlantFilter().Apply([a, b]));
    }
}
