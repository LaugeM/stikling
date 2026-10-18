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
            errors.Add("Give the plan a name.");

        if (Steps.Count == 0)
        {
            errors.Add("Add at least one step.");
            return errors;
        }

        if (Steps[0].DaysAfter != 0 || Steps.Skip(1).Any(s => s.DaysAfter < 1))
            errors.Add("A step after the first comes at least a day later.");

        if (Steps.Any(s => string.IsNullOrWhiteSpace(s.What) && s.RecipeId is null))
            errors.Add("Say what each step uses.");

        return errors;
    }

    /// <summary>"3 steps over 13 days", or "1 step" for a plan of one.</summary>
    public string Summary()
    {
        var days = Steps.Sum(s => s.DaysAfter);
        var steps = Steps.Count == 1 ? "1 step" : $"{Steps.Count} steps";
        return days > 0 ? $"{steps} over {(days == 1 ? "1 day" : $"{days} days")}" : steps;
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
