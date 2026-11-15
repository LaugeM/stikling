using Stikling.Core.Models;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Pests;

/// <summary>
/// The day's round of treating a case with several plants: one treatment with a list of ticked
/// plants, started from the case page. It is saved at the first tick, and the page finds it again
/// the same day to carry on.
/// </summary>
public static class TreatmentRound
{
    /// <summary>
    /// The case's round from today, if one has been started. With two (two devices can each make
    /// one) the newest wins.
    /// </summary>
    public static PestTreatment? Today(PestCase item, IEnumerable<PestTreatment> treatments, DateOnly today) =>
        treatments
            .Where(t => !t.IsDeleted
                && t.CaseId == item.Id
                && t.Kind == PestTreatmentKind.Treated
                && !t.IsRelease
                && !t.Skipped
                && t.OccurredOn == today
                && t.TreatedPlantIds is not null)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.UpdatedAt)
            .FirstOrDefault();

    /// <summary>
    /// Today's round if there is one, else a new unsaved one with nothing ticked, carrying the
    /// last recipe forward. Pass the step only when the case's plan is running.
    /// </summary>
    public static PestTreatment Start(
        PestCase item,
        IReadOnlyList<PestTreatment> treatments,
        DateOnly today,
        IEnumerable<TreatmentRecipe> recipes,
        PlanStep? step)
    {
        if (Today(item, treatments, today) is { } existing)
            return existing;

        var draft = step is null
            ? PestService.StartTreatment(item, treatments, today, recipes)
            : PlanProgress.StartStep(item, step, today, recipes, PlanProgress.CurrentRound(item, treatments));
        draft.TreatedPlantIds = [];
        draft.NextDueOn = null;
        return draft;
    }

    /// <summary>
    /// Sets what is used: a saved recipe, copied onto the treatment, or typed text. Switching
    /// leaves nothing of the other behind.
    /// </summary>
    public static void UseRecipe(PestTreatment treatment, TreatmentRecipe? recipe, string? what)
    {
        if (recipe is not null)
        {
            treatment.Use(recipe);
            return;
        }

        treatment.RecipeId = null;
        treatment.Ingredients = [];
        treatment.Method = null;
        treatment.What = Clean(what);
    }

    public static bool AllTicked(ChecklistGroup group) => group.Rows.Count > 0 && group.Rows.All(r => r.Ticked);

    /// <summary>Ticks or unticks every plant of the group, leaving the other ids in the list alone.</summary>
    public static void TickAll(PestTreatment treatment, ChecklistGroup group, bool on)
    {
        var list = treatment.TreatedPlantIds ?? [];
        var ids = group.Rows.Select(r => r.Plant.Id).ToHashSet();
        list.RemoveAll(ids.Contains);
        if (on)
            list.AddRange(ids);
        treatment.TreatedPlantIds = list;
    }

    /// <summary>Today's round with plants still to tick, for the case page. Null when there is none or it is finished.</summary>
    public static (int Ticked, int Total)? InProgress(
        PestCase item, IReadOnlyList<PestTreatment> treatments, IReadOnlyList<Plant> covered, DateOnly today)
    {
        if (Today(item, treatments, today) is not { } round)
            return null;

        var (ticked, total) = TreatmentChecklist.Progress(round, covered);
        return ticked < total ? (ticked, total) : null;
    }
}
