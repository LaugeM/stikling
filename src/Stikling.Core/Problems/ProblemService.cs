using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Problems;

/// <summary>A problem whose look again day has come, with the plant it is on.</summary>
public sealed record LookAgain(Problem Problem, Plant Plant, DateOnly Since);

/// <summary>A closed problem like another one: what was shared, what was done and how it ended.</summary>
public sealed record PastProblem(
    Problem Problem, Plant Plant, IReadOnlyList<ProblemSign> Shared, IReadOnlyList<ProblemAction> Did, ProblemOutcome Outcome);

/// <summary>What a line in the problem's log is.</summary>
public enum ProblemLogKind
{
    Started,
    Update,
    Change,
    Closed
}

/// <summary>
/// One line of a problem's log. The update is set for <see cref="ProblemLogKind.Update"/> and the
/// history line for a plain change. The photos are the ones on the history line it wrote.
/// </summary>
public sealed record ProblemLogItem(
    ProblemLogKind Kind, DateOnly Day, ProblemUpdate? Update, TimelineEntry? Entry, IReadOnlyList<Guid> PhotoIds);

/// <summary>
/// Problems on plants. A problem and each update are records of their own, and each writes a
/// line on the plant's history, so removing one takes its line off again.
/// </summary>
public sealed class ProblemService(
    IProblemRepository problems, IProblemUpdateRepository updates, ITimelineRepository timeline, TimeProvider time)
{
    private DateOnly Today => time.Today();

    public async Task<IReadOnlyList<Problem>> AllAsync() =>
        (await problems.GetAllAsync()).Where(p => !p.IsDeleted).ToList();

    public async Task<IReadOnlyList<ProblemUpdate>> AllUpdatesAsync() =>
        (await updates.GetAllAsync()).Where(u => !u.IsDeleted).ToList();

    /// <summary>The plant's problems, open ones first, then the newest start first.</summary>
    public async Task<IReadOnlyList<Problem>> ForPlantAsync(Guid plantId) =>
        (await AllAsync()).Where(p => p.SubjectId == plantId)
            .OrderBy(p => p.IsOpen ? 0 : 1).ThenByDescending(p => p.StartedOn).ToList();

    /// <summary>The problem's updates, oldest first.</summary>
    public async Task<IReadOnlyList<ProblemUpdate>> UpdatesForAsync(Guid problemId) =>
        (await AllUpdatesAsync()).Where(u => u.ProblemId == problemId)
            .OrderBy(u => u.OccurredOn).ThenBy(u => u.CreatedAt).ToList();

    /// <summary>Opens a problem on a plant and writes it on the plant's history.</summary>
    public async Task<Problem> OpenAsync(Plant plant, Problem draft, IReadOnlyList<Guid> photoIds)
    {
        draft.SubjectType = SubjectType.Plant;
        draft.SubjectId = plant.Id;
        draft.Signs = draft.Signs.Distinct().ToList();
        draft.Note = Clean(draft.Note);
        Check(draft);

        var entry = NewEntry(draft, ProblemText.OpenedLines(draft), draft.StartedOn, photoIds);
        await timeline.AddAsync(entry);

        draft.TimelineEntryId = entry.Id;
        await problems.SaveAsync(draft);
        return draft;
    }

    /// <summary>Adds an update to a problem and writes it on the plant's history.</summary>
    public async Task<ProblemUpdate> AddUpdateAsync(Problem problem, ProblemUpdate draft, IReadOnlyList<Guid> photoIds)
    {
        Prepare(problem, draft, photoIds.Count);

        var entry = NewEntry(problem, ProblemText.UpdateLines(draft), draft.OccurredOn, photoIds);
        await timeline.AddAsync(entry);

        draft.TimelineEntryId = entry.Id;
        await updates.SaveAsync(draft);
        return draft;
    }

    /// <summary>Saves a problem whose signs, note, start or look again day were changed, and rewrites its opening line.</summary>
    public async Task EditAsync(Problem problem)
    {
        problem.Signs = problem.Signs.Distinct().ToList();
        problem.Note = Clean(problem.Note);
        Check(problem);

        await Rewrite(problem, problem.TimelineEntryId, ProblemText.OpenedLines(problem), problem.StartedOn);
        await problems.SaveAsync(problem);
    }

    /// <summary>Saves a corrected update and rewrites its line. The photos on the line are kept.</summary>
    public async Task EditUpdateAsync(Problem problem, ProblemUpdate update)
    {
        var line = update.TimelineEntryId is { } id
            ? (await timeline.GetForAsync(problem.SubjectId)).FirstOrDefault(e => e.Id == id)
            : null;
        Prepare(problem, update, line?.PhotoIds.Count ?? 0);

        await Rewrite(problem, update.TimelineEntryId, ProblemText.UpdateLines(update), update.OccurredOn);
        await updates.SaveAsync(update);
    }

    /// <summary>
    /// Takes an update off, and the history line it wrote. Photos on the line are never deleted:
    /// a line that has some stays on the history as a plain photo entry.
    /// </summary>
    public async Task RemoveUpdateAsync(ProblemUpdate update)
    {
        await updates.DeleteAsync(update.Id);
        var subject = (await problems.GetAsync(update.ProblemId))?.SubjectId;
        await TakeOffLine(update.TimelineEntryId, subject);
    }

    /// <summary>Closes a problem on a day and writes how it ended on the history.</summary>
    public async Task CloseAsync(Problem problem, ProblemOutcome outcome, DateOnly day)
    {
        var closed = problem.Copy();
        closed.ClosedOn = day;
        closed.Outcome = outcome;
        Check(closed);

        var entry = NewEntry(problem, ProblemText.ClosedLines(outcome), day, []);
        await timeline.AddAsync(entry);

        problem.ClosedOn = day;
        problem.Outcome = outcome;
        problem.ClosedTimelineEntryId = entry.Id;
        await problems.SaveAsync(problem);
    }

    /// <summary>
    /// Opens a closed problem again. The line about it ending stays on the history, since it
    /// happened, and a new line says it came back.
    /// </summary>
    public async Task ReopenAsync(Problem problem)
    {
        await timeline.AddAsync(NewEntry(problem, ProblemText.ReopenedLines, Today, []));

        problem.ClosedOn = null;
        problem.Outcome = null;
        problem.ClosedTimelineEntryId = null;
        problem.ReopenedOn = Today;
        problem.ReopenedAt = time.GetUtcNow();
        problem.LookAgainOn = null;
        await problems.SaveAsync(problem);
    }

    /// <summary>
    /// Deletes a problem, its updates and the history lines they wrote. A line with photos stays
    /// as a plain photo entry, so the photos stay on the plant's history.
    /// </summary>
    public async Task RemoveAsync(Problem problem)
    {
        foreach (var update in await UpdatesForAsync(problem.Id))
            await RemoveUpdateAsync(update);

        await problems.DeleteAsync(problem.Id);
        foreach (var entryId in new[] { problem.TimelineEntryId, problem.ClosedTimelineEntryId })
            await TakeOffLine(entryId, problem.SubjectId);
    }

    /// <summary>How many photos are on the history lines of an update, or of the whole problem.</summary>
    public async Task<int> PhotoCountAsync(Problem problem, ProblemUpdate? update = null)
    {
        var lineIds = new List<Guid?>();
        if (update is not null)
        {
            lineIds.Add(update.TimelineEntryId);
        }
        else
        {
            lineIds.Add(problem.TimelineEntryId);
            lineIds.Add(problem.ClosedTimelineEntryId);
            lineIds.AddRange((await UpdatesForAsync(problem.Id)).Select(u => u.TimelineEntryId));
        }

        var wanted = lineIds.OfType<Guid>().ToHashSet();
        return (await timeline.GetForAsync(problem.SubjectId))
            .Where(e => wanted.Contains(e.Id)).SelectMany(e => e.PhotoIds).Distinct().Count();
    }

    private async Task TakeOffLine(Guid? entryId, Guid? subjectId)
    {
        if (entryId is not { } id)
            return;
        var entry = subjectId is { } subject ? (await timeline.GetForAsync(subject)).FirstOrDefault(e => e.Id == id) : null;
        if (entry is { PhotoIds.Count: > 0 })
        {
            entry.Kind = TimelineKind.Photo;
            entry.Event = null;
            entry.Text = null;
            entry.Lines = null;
            await timeline.UpdateAsync(entry);
        }
        else
        {
            await timeline.DeleteAsync(id);
        }
    }

    /// <summary>
    /// The day to look at an open problem again, null when it is closed or nothing is planned.
    /// It comes from the newest record, the problem itself or one of its updates, even when that
    /// record has no day. So an update that leaves Look again empty ends the reminders.
    /// </summary>
    public static DateOnly? LookAgainOn(Problem problem, IEnumerable<ProblemUpdate> updates) =>
        problem.IsOpen ? Newest(problem, updates).Again : null;

    /// <summary>The choices for Look again, in days from the day of the problem or update. No is left out.</summary>
    public static readonly IReadOnlyList<int> LookAgainChoices = [3, 7, 14];

    /// <summary>
    /// The gap the current look again day is from the newest record, as the closest of
    /// <see cref="LookAgainChoices"/>, so the next update can start on the same gap. Null when
    /// nothing is planned.
    /// </summary>
    public static int? LookAgainGap(Problem problem, IEnumerable<ProblemUpdate> updates) =>
        problem.IsOpen && Newest(problem, updates) is { Again: { } again } newest
            ? Closest(again.DayNumber - newest.On.DayNumber)
            : null;

    /// <summary>The closest of <see cref="LookAgainChoices"/> to a number of days.</summary>
    public static int Closest(int days) => LookAgainChoices.MinBy(c => Math.Abs(c - days));

    private static (DateOnly On, DateTimeOffset At, DateOnly? Again) Newest(Problem problem, IEnumerable<ProblemUpdate> updates)
    {
        // Opened again, the problem counts from then, so updates from before it closed lose to it
        var newest = problem.ReopenedOn is { } reopened
            ? (On: reopened, At: problem.ReopenedAt ?? problem.CreatedAt, Again: problem.LookAgainOn)
            : (On: problem.StartedOn, At: problem.CreatedAt, Again: problem.LookAgainOn);
        foreach (var update in updates.Where(u => !u.IsDeleted))
        {
            var isNewer = update.OccurredOn > newest.On
                || (update.OccurredOn == newest.On && update.CreatedAt >= newest.At);
            if (isNewer)
                newest = (update.OccurredOn, update.CreatedAt, update.LookAgainOn);
        }
        return newest;
    }

    /// <summary>
    /// Open problems on plants that are still here and active, whose look again day has come.
    /// The one waiting longest comes first.
    /// </summary>
    public static IReadOnlyList<LookAgain> LookAgainDue(
        IEnumerable<Problem> problems, IEnumerable<ProblemUpdate> updates, IEnumerable<Plant> plants, DateOnly today)
    {
        var updateList = updates.ToList();
        var due = new List<LookAgain>();
        foreach (var problem in problems.Where(p => !p.IsDeleted && p.IsOpen))
        {
            var plant = plants.FirstOrDefault(p => p.Id == problem.SubjectId);
            if (plant is not { IsDeleted: false, InCollection: true })
                continue;
            var own = updateList.Where(u => u.ProblemId == problem.Id);
            if (LookAgainOn(problem, own) is { } day && day <= today)
                due.Add(new LookAgain(problem, plant, day));
        }
        return due.OrderBy(d => d.Since).ToList();
    }

    /// <summary>How the problem looks now: the trend of its newest update that has one.</summary>
    public static ProblemTrend? LatestTrend(IEnumerable<ProblemUpdate> updatesOfOneProblem) =>
        updatesOfOneProblem.Where(u => !u.IsDeleted && u.Trend is not null)
            .OrderByDescending(u => u.OccurredOn).ThenByDescending(u => u.CreatedAt)
            .Select(u => u.Trend).FirstOrDefault();

    /// <summary>
    /// What else happened to the plant while the problem lasted: its plain history changes
    /// (repots, prunes, moves, status changes) from the start day to the closing day, or today
    /// when it is open. Nothing new is stored for this, the lines are the ones the app already wrote.
    /// </summary>
    public IReadOnlyList<TimelineEntry> DuringProblem(Problem problem, IEnumerable<TimelineEntry> plantEntries)
    {
        var to = problem.ClosedOn ?? Today;
        return plantEntries
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change && TimelineEvents.Of(e) is null)
            .Where(e => time.LocalDay(e.OccurredAt) >= problem.StartedOn && time.LocalDay(e.OccurredAt) <= to)
            .OrderBy(e => e.OccurredAt).ToList();
    }

    /// <summary>
    /// Closed problems that had a sign in common with this one, so someone can see what they did
    /// last time and how it went. It is the person's own history, never advice. Another problem
    /// on the same plant counts. A sign of "something else" alone doesn't match.
    /// </summary>
    public static IReadOnlyList<PastProblem> PastOfSameKind(
        Problem problem, IEnumerable<Problem> all, IEnumerable<ProblemUpdate> updates, IEnumerable<Plant> plants)
    {
        var mine = problem.KnownSigns.Where(s => s != ProblemSign.Other).Distinct().ToList();
        var updateList = updates.Where(u => !u.IsDeleted).ToList();
        var found = new List<PastProblem>();
        foreach (var other in all.Where(p => !p.IsDeleted && p.Id != problem.Id && !p.IsOpen && p.Outcome is not null))
        {
            var otherSigns = other.KnownSigns;
            var shared = mine.Where(otherSigns.Contains).ToList();
            var plant = plants.FirstOrDefault(p => p.Id == other.SubjectId && !p.IsDeleted);
            if (shared.Count == 0 || plant is null)
                continue;

            var did = updateList.Where(u => u.ProblemId == other.Id)
                .OrderBy(u => u.OccurredOn).ThenBy(u => u.CreatedAt)
                .SelectMany(u => u.KnownDid).Distinct().ToList();
            found.Add(new PastProblem(other, plant, shared, did, other.Outcome!.Value));
        }
        return found.OrderByDescending(p => p.Problem.ClosedOn).ToList();
    }

    /// <summary>
    /// What happened with the problem, oldest first: how it started, each update, the plain
    /// changes made meanwhile (see <see cref="DuringProblem"/>) and how it ended.
    /// </summary>
    public IReadOnlyList<ProblemLogItem> Log(
        Problem problem, IEnumerable<ProblemUpdate> problemUpdates, IReadOnlyList<TimelineEntry> plantEntries)
    {
        IReadOnlyList<Guid> PhotosOf(Guid? lineId) =>
            plantEntries.FirstOrDefault(e => e.Id == lineId && !e.IsDeleted)?.PhotoIds ?? [];

        var items = new List<(ProblemLogItem Item, int Rank, DateTimeOffset At)>
        {
            (new(ProblemLogKind.Started, problem.StartedOn, null, null, PhotosOf(problem.TimelineEntryId)), 0, problem.CreatedAt)
        };
        foreach (var update in problemUpdates.Where(u => !u.IsDeleted && u.ProblemId == problem.Id))
            items.Add((new(ProblemLogKind.Update, update.OccurredOn, update, null, PhotosOf(update.TimelineEntryId)), 1, update.CreatedAt));
        foreach (var entry in DuringProblem(problem, plantEntries))
            items.Add((new(ProblemLogKind.Change, time.LocalDay(entry.OccurredAt), null, entry, []), 1, entry.OccurredAt));
        if (problem.ClosedOn is { } closed)
            items.Add((new(ProblemLogKind.Closed, closed, null, null, PhotosOf(problem.ClosedTimelineEntryId)), 2, DateTimeOffset.MaxValue));

        return items.OrderBy(i => i.Item.Day).ThenBy(i => i.Rank).ThenBy(i => i.At).Select(i => i.Item).ToList();
    }

    private void Prepare(Problem problem, ProblemUpdate update, int photoCount)
    {
        update.ProblemId = problem.Id;
        update.Did = update.Did.Distinct().ToList();
        update.Note = Clean(update.Note);
        if (update.Trend is null && update.Did.Count == 0 && update.Note is null && photoCount == 0)
            throw new ArgumentException(Strings.Pests.UpdateNeedsSomething);
        if (update.OccurredOn < problem.StartedOn)
            throw new ArgumentException(Strings.Pests.UpdateBeforeProblem);
        if (update.Validate(Today).FirstOrDefault() is { } error)
            throw new ArgumentException(error);
    }

    private void Check(Problem problem)
    {
        if (problem.Validate(Today).FirstOrDefault() is { } error)
            throw new ArgumentException(error);
    }

    private TimelineEntry NewEntry(Problem problem, IReadOnlyList<HistoryLine> lines, DateOnly day, IReadOnlyList<Guid> photoIds)
    {
        var entry = new TimelineEntry
        {
            SubjectType = problem.SubjectType,
            SubjectId = problem.SubjectId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Problem,
            OccurredAt = time.MomentOn(day),
            PhotoIds = [.. photoIds]
        };
        HistoryText.Set(entry, lines);
        return entry;
    }

    /// <summary>Changes the text and day of a line already on the history. A line that is gone is left.</summary>
    private async Task Rewrite(Problem problem, Guid? entryId, IReadOnlyList<HistoryLine> lines, DateOnly day)
    {
        if (entryId is not { } id)
            return;
        var entry = (await timeline.GetForAsync(problem.SubjectId)).FirstOrDefault(e => e.Id == id);
        if (entry is null)
            return;
        HistoryText.Set(entry, lines);
        entry.OccurredAt = time.MomentOn(day);
        await timeline.UpdateAsync(entry);
    }

    private static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
