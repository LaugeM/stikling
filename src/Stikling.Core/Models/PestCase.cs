using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What you found. Stored as text like the other enums.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Pest>))]
public enum Pest
{
    SpiderMites,
    Thrips,
    FungusGnats,
    Mealybugs,
    Scale,
    Aphids,
    Whitefly,
    Other
}

/// <summary>
/// How far along a case is. Active means still treating, Monitoring means the treatments have
/// stopped but you're still watching, Resolved means done.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PestCaseStatus>))]
public enum PestCaseStatus
{
    Active,
    Monitoring,
    Resolved
}

/// <summary>
/// Which plants a case covers. Everywhere and Room are rules rather than a fixed list, so a
/// plant added to the room while the case is open is covered without anyone remembering to
/// add it. That matches how spraying actually works: you treat the room, not a list.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PestScope>))]
public enum PestScope
{
    /// <summary>Every plant in the collection.</summary>
    Everywhere,

    /// <summary>One room, including the spots inside it.</summary>
    Room,

    /// <summary>A hand-picked set of plants.</summary>
    PickedPlants
}

/// <summary>
/// One outbreak: a pest, when it started, which plants it covers and how often they're treated.
/// Treatments are logged against the case rather than against each plant, so one spray over
/// thirty plants is one entry.
/// </summary>
public sealed class PestCase : Entity
{
    public Pest Pest { get; set; } = Pest.SpiderMites;

    /// <summary>The day you found it, which can be earlier than when it was written down.</summary>
    public DateOnly StartedOn { get; set; }

    public PestCaseStatus Status { get; set; } = PestCaseStatus.Active;

    /// <summary>The day it was marked resolved, set when the status goes to Resolved.</summary>
    public DateOnly? ResolvedOn { get; set; }

    public PestScope Scope { get; set; } = PestScope.Everywhere;

    /// <summary>The room the case covers, e.g. "Living room". Only on <see cref="PestScope.Room"/>.</summary>
    public string? Room { get; set; }

    /// <summary>The plants the case covers. Only on <see cref="PestScope.PickedPlants"/>.</summary>
    public List<Guid> PlantIds { get; set; } = [];

    /// <summary>Days between treatments, e.g. 4 for "every 4 days".</summary>
    public int IntervalDays { get; set; } = 4;

    public string? Notes { get; set; }

    /// <summary>Active and monitoring cases are still worth showing; resolved ones aren't.</summary>
    [JsonIgnore]
    public bool IsOpen => Status != PestCaseStatus.Resolved;

    public PestCase Copy()
    {
        var copy = (PestCase)MemberwiseClone();
        copy.PlantIds = [.. PlantIds];
        return copy;
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();

        if (StartedOn > today)
            errors.Add("That start date is in the future.");

        if (IntervalDays < 1)
            errors.Add("Treat at least every day, so the interval has to be 1 or more.");

        if (Scope == PestScope.Room && string.IsNullOrWhiteSpace(Room))
            errors.Add("Pick the room the case covers.");

        if (Scope == PestScope.PickedPlants && PlantIds.Count == 0)
            errors.Add("Pick at least one plant.");

        if (ResolvedOn is { } resolved)
        {
            if (resolved > today)
                errors.Add("That resolved date is in the future.");
            else if (resolved < StartedOn)
                errors.Add("A case can't be resolved before it started.");
        }

        return errors;
    }
}
