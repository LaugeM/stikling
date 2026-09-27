using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>Whether something was used on the plants, or they were only looked over.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PestTreatmentKind>))]
public enum PestTreatmentKind
{
    Treated,

    /// <summary>Looked the plants over while the case is being watched.</summary>
    Checked
}

/// <summary>
/// One treatment on a case: a spray, a wipe-down, a soil drench. Or, once the case is only being
/// watched, a check that the pests haven't come back. It belongs to the case rather than to a
/// plant, because a case is treated as a whole.
/// </summary>
public sealed class PestTreatment : Entity
{
    public Guid CaseId { get; set; }

    public PestTreatmentKind Kind { get; set; } = PestTreatmentKind.Treated;

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

        if (Kind == PestTreatmentKind.Checked && (What is not null || NextDueOn is not null))
            errors.Add("A check only has a date and notes.");

        return errors;
    }
}
