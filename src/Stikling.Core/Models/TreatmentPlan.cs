using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A sequence of treatments saved under a name: spray, spray again after 3 days, release
/// predatory mites after 10 more. Starting it on a pest case copies the steps onto the case, so
/// editing or deleting the plan later leaves the cases that follow it alone.
/// </summary>
public sealed class TreatmentPlan : Entity
{
    public string? Name { get; set; }

    public string? Notes { get; set; }

    /// <summary>The steps in order.</summary>
    public List<PlanStep> Steps { get; set; } = [];

    /// <summary>A copy with its own step list, so editing a draft can't change the original.</summary>
    public TreatmentPlan Copy()
    {
        var copy = (TreatmentPlan)MemberwiseClone();
        copy.Steps = [.. Steps.Select(s => s.Copy())];
        return copy;
    }

    public void MoveUp(int index)
    {
        if (index > 0 && index < Steps.Count)
            (Steps[index - 1], Steps[index]) = (Steps[index], Steps[index - 1]);
    }

    public void MoveDown(int index)
    {
        if (index >= 0 && index < Steps.Count - 1)
            (Steps[index], Steps[index + 1]) = (Steps[index + 1], Steps[index]);
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Pests.PlanNeedsName);

        if (Steps.Count == 0)
        {
            errors.Add(Strings.Pests.PlanNeedsStep);
            return errors;
        }

        if (Steps[0].DaysAfter != 0 || Steps.Skip(1).Any(s => s.DaysAfter < 1))
            errors.Add(Strings.Pests.PlanStepsApart);

        if (Steps.Any(s => string.IsNullOrWhiteSpace(s.What) && s.RecipeId is null))
            errors.Add(Strings.Pests.PlanStepNeedsWhat);

        return errors;
    }

    /// <summary>"3 steps over 13 days", or "1 step" for a plan of one.</summary>
    public string Summary()
    {
        var days = Steps.Sum(s => s.DaysAfter);
        var steps = Plural.Of(Steps.Count, Strings.Pests.PlanSteps_One, Strings.Pests.PlanSteps_Other);
        return days > 0
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Strings.Pests.PlanSummary,
                steps,
                Plural.Of(days, Strings.Pests.DurationDay_One, Strings.Pests.DurationDay_Other))
            : steps;
    }
}

/// <summary>
/// One step of a plan. Not a record of its own: it lives inside the plan, and a copy lives on
/// each case that follows the plan.
/// </summary>
public sealed class PlanStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Days after the step before was done. The first step is always 0, the day the plan starts.
    /// </summary>
    public int DaysAfter { get; set; }

    /// <summary>What to do, e.g. "alcohol spray". With a recipe it's the recipe's name.</summary>
    public string? What { get; set; }

    /// <summary>A treatment recipe the step uses.</summary>
    public Guid? RecipeId { get; set; }

    public string? Notes { get; set; }

    public PlanStep Copy() => (PlanStep)MemberwiseClone();
}
