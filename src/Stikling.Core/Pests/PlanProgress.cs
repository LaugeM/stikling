using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Pests;

/// <summary>
/// Where a case is in the treatment plan it follows. A step is done when a treatment on the
/// case points at it, logged or skipped, and the current step is the first one not done. Each
/// step is due a number of days after the step before was done, so a late spray moves the rest
/// along, and an extra treatment without a step doesn't move anything.
/// </summary>
public static class PlanProgress
{
    /// <summary>The live treatments on the case made for one step, newest first.</summary>
    public static IReadOnlyList<PestTreatment> ForStep(PestCase item, IEnumerable<PestTreatment> treatments, Guid stepId) =>
        PestService.TreatmentsFor(treatments, item.Id).Where(t => t.PlanStepId == stepId).ToList();

    public static bool IsDone(PestCase item, IEnumerable<PestTreatment> treatments, Guid stepId) =>
        ForStep(item, treatments, stepId).Count > 0;

    /// <summary>The index of the first step that isn't done, or -1 when they all are or there is no plan.</summary>
    public static int CurrentIndex(PestCase item, IEnumerable<PestTreatment> treatments)
    {
        var list = treatments.ToList();
        for (var i = 0; i < item.PlanSteps.Count; i++)
        {
            if (!IsDone(item, list, item.PlanSteps[i].Id))
                return i;
        }

        return -1;
    }

    /// <summary>A plan was started on the case and every step has a treatment or a skip.</summary>
    public static bool IsFinished(PestCase item, IEnumerable<PestTreatment> treatments) =>
        item.PlanStartedOn is not null && item.PlanSteps.Count > 0 && CurrentIndex(item, treatments) < 0;

    /// <summary>
    /// The day the step at the index is due: the day it was moved to, else the start day for the
    /// first step, else the day the step before was last done plus this step's days after.
    /// </summary>
    public static DateOnly? DueOn(PestCase item, IEnumerable<PestTreatment> treatments, int index, PutOffs? putOffs = null)
    {
        if (item.PlanStartedOn is not { } started || index < 0 || index >= item.PlanSteps.Count)
            return null;

        var step = item.PlanSteps[index];

        if (putOffs?.EndedOn(PutOffs.PlanStep(item.Id, step.Id)) is { } moved)
            return moved;

        if (index == 0)
            return started.AddDays(step.DaysAfter);

        var before = ForStep(item, treatments, item.PlanSteps[index - 1].Id);
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
    public static PestTreatment StartStep(PestCase item, PlanStep step, DateOnly today, IEnumerable<TreatmentRecipe>? recipes = null)
    {
        var draft = new PestTreatment { CaseId = item.Id, OccurredOn = today, PlanStepId = step.Id };

        if (step.RecipeId is { } id && recipes?.FirstOrDefault(r => r.Id == id && !r.IsDeleted) is { } recipe)
            draft.Use(recipe);
        else
            draft.What = string.IsNullOrWhiteSpace(step.What) ? null : step.What.Trim();

        return draft;
    }

    /// <summary>A skipped step: a treatment with nothing in it but the day.</summary>
    public static PestTreatment Skip(PestCase item, PlanStep step, DateOnly today) =>
        new() { CaseId = item.Id, OccurredOn = today, PlanStepId = step.Id, Skipped = true };
}
