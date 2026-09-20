namespace Stikling.Core.Models;

/// <summary>
/// One treatment on a case: a spray, a wipe-down, a soil drench. It belongs to the case rather
/// than to a plant, because a case is treated as a whole.
/// </summary>
public sealed class PestTreatment : Entity
{
    public Guid CaseId { get; set; }

    /// <summary>The day it was done, which can be earlier than when it was written down.</summary>
    public DateOnly OccurredOn { get; set; }

    /// <summary>What was used, e.g. "alcohol spray, both sides of the leaves".</summary>
    public string? What { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// When the next treatment is due, when it shouldn't simply be the case's interval. Left
    /// empty the interval decides, so this is only filled in when you want a different gap.
    /// </summary>
    public DateOnly? NextDueOn { get; set; }

    public PestTreatment Copy() => (PestTreatment)MemberwiseClone();

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();

        if (CaseId == Guid.Empty)
            errors.Add("A treatment has to belong to a case.");

        if (OccurredOn > today)
            errors.Add("That date is in the future.");

        if (NextDueOn is { } next && next < OccurredOn)
            errors.Add("The next treatment can't be due before this one happened.");

        return errors;
    }
}
