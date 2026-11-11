using Stikling.Core.Models;
using Stikling.Core.Today;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Pests;

/// <summary>
/// Where a case is in the treatment plan it follows. A step is done when a treatment on the
/// case points at it, logged or skipped, and the current step is the first one not done. Each
/// step is due a number of days after the step before was done, so a late spray moves the rest
/// along, and an extra treatment without a step doesn't move anything.
/// </summary>
public static class PlanProgress
{
    /// <summary>The round a treatment belongs to. 0 is every treatment from before rounds, so round 1.</summary>
    public static int RoundOf(PestTreatment treatment) => Math.Max(1, treatment.PlanRound);

    /// <summary>The plan starts over after its last step, round after round.</summary>
    public static bool Repeats(PestCase item) => item.PlanRepeatAfterDays is >= 1;

    /// <summary>The live treatments on the case made for one step in a round, newest first.</summary>
    public static IReadOnlyList<PestTreatment> ForStep(PestCase item, IEnumerable<PestTreatment> treatments, Guid stepId, int round = 1) =>
        PestService.TreatmentsFor(treatments, item.Id).Where(t => t.PlanStepId == stepId && RoundOf(t) == round).ToList();

    public static bool IsDone(PestCase item, IEnumerable<PestTreatment> treatments, Guid stepId, int round = 1) =>
        ForStep(item, treatments, stepId, round).Count > 0;

    private static bool RoundComplete(PestCase item, List<PestTreatment> list, int round) =>
        item.PlanSteps.Count > 0 && item.PlanSteps.All(s => IsDone(item, list, s.Id, round));

    /// <summary>
    /// The round being worked on: the first one with a step not done. Always 1 for a plan that
    /// doesn't repeat.
    /// </summary>
    public static int CurrentRound(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        if (!Repeats(item))
            return 1;

        var list = treatments.ToList();
        var round = 1;
        while (RoundComplete(item, list, round))
            round++;

        return round;
    }

    /// <summary>The index of the first step not done in the current round, or -1 when they all are or there is no plan.</summary>
    public static int CurrentIndex(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        var list = treatments.ToList();
        var round = CurrentRound(item, list);
        for (var i = 0; i < item.PlanSteps.Count; i++)
        {
            if (!IsDone(item, list, item.PlanSteps[i].Id, round))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// A plan was started on the case and every step has a treatment or a skip. A plan that
    /// starts over is never finished, it goes on until the case is watched or resolved.
    /// </summary>
    public static bool IsFinished(PestCase item, IEnumerable<PestTreatment> treatments) =>
        !Repeats(item) && item.PlanStartedOn is not null && item.PlanSteps.Count > 0 && CurrentIndex(item, treatments) < 0;

    /// <summary>
    /// The day the step at the index is due in the current round: the day it was moved to, else
    /// the start day for the first step of round 1, else the day the last step of the round before
    /// was done plus the days before it starts over, else the day the step before was last done
    /// plus this step's days after.
    /// </summary>
    public static DateOnly? DueOn(PestCase item, IEnumerable<PestTreatment> treatments, int index, PutOffs? putOffs = null)
    {
        if (item.PlanStartedOn is not { } started || index < 0 || index >= item.PlanSteps.Count)
            return null;

        var list = treatments.ToList();
        var round = CurrentRound(item, list);
        var step = item.PlanSteps[index];

        if (putOffs?.EndedOn(PutOffs.PlanStep(item.Id, step.Id, round)) is { } moved)
            return moved;

        if (index == 0)
        {
            if (round == 1)
                return started.AddDays(step.DaysAfter);

            var lastOfBefore = ForStep(item, list, item.PlanSteps[^1].Id, round - 1);
            return lastOfBefore.Count == 0 || item.PlanRepeatAfterDays is not { } again
                ? null
                : lastOfBefore.Max(t => t.OccurredOn).AddDays(again);
        }

        var before = ForStep(item, list, item.PlanSteps[index - 1].Id, round);
        return before.Count == 0 ? null : before.Max(t => t.OccurredOn).AddDays(step.DaysAfter);
    }

    /// <summary>
    /// When the question after the last step is due: the day the last step was last done plus
    /// the case's interval. Null unless the plan is finished and hasn't been stopped.
    /// </summary>
    public static DateOnly? DoneOn(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        if (!item.PlanRunning)
            return null;

        var list = treatments.ToList();
        if (!IsFinished(item, list))
            return null;

        var last = ForStep(item, list, item.PlanSteps[^1].Id);
        return last.Max(t => t.OccurredOn).AddDays(item.IntervalDays);
    }

    /// <summary>The treatment for a step, filled in from the step and the recipe if it still exists.</summary>
    public static PestTreatment StartStep(PestCase item, PlanStep step, DateOnly today, IEnumerable<TreatmentRecipe>? recipes = null, int round = 1)
    {
        var draft = new PestTreatment { CaseId = item.Id, OccurredOn = today, PlanStepId = step.Id, PlanRound = round };

        if (step.RecipeId is { } id && recipes?.FirstOrDefault(r => r.Id == id && !r.IsDeleted) is { } recipe)
            draft.Use(recipe);
        else
            draft.What = Clean(step.What);

        return draft;
    }

    /// <summary>A skipped step: a treatment with nothing in it but the day.</summary>
    public static PestTreatment Skip(PestCase item, PlanStep step, DateOnly today, int round = 1) =>
        new() { CaseId = item.Id, OccurredOn = today, PlanStepId = step.Id, PlanRound = round, Skipped = true };
}
