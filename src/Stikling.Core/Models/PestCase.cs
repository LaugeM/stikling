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

public static class PestTraits
{
    /// <summary>
    /// Whether the pest flies into a yellow or blue sticky trap, so counting one says something
    /// about how the outbreak is going. Spider mites, mealybugs and scale crawl and rarely end up
    /// on one, and most aphids indoors never grow wings.
    /// </summary>
    public static bool CaughtOnTrap(this Pest pest) =>
        pest is Pest.Thrips or Pest.FungusGnats or Pest.Whitefly;
}

/// <summary>
/// How far along a case is. Active means still treating, Monitoring means the treatments have
/// stopped but the plants are still checked now and then, Resolved means done.
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

    /// <summary>
    /// The day the case last went back to treating, or was opened again after being resolved.
    /// The clean count on a case being watched starts no earlier than this, since pests found
    /// again mean it wasn't clean. Null when that hasn't happened.
    /// </summary>
    public DateOnly? TreatingAgainOn { get; set; }

    public PestScope Scope { get; set; } = PestScope.Everywhere;

    /// <summary>The room or spot the case covers. Only on <see cref="PestScope.Room"/>.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>The plants the case covers. Only on <see cref="PestScope.PickedPlants"/>.</summary>
    public List<Guid> PlantIds { get; set; } = [];

    /// <summary>Days between treatments, e.g. 4 for "every 4 days".</summary>
    public int IntervalDays { get; set; } = 4;

    public string? Notes { get; set; }

    /// <summary>The plan this case was started on. Only to remember where the steps came from.</summary>
    public Guid? PlanId { get; set; }

    public string? PlanName { get; set; }

    /// <summary>
    /// The steps being followed, copied from the plan with ids of their own, so editing or
    /// deleting the plan leaves them alone. Older versions ignore these fields and use
    /// <see cref="IntervalDays"/>.
    /// </summary>
    public List<PlanStep> PlanSteps { get; set; } = [];

    /// <summary>The day the plan was started, which is when its first step is due.</summary>
    public DateOnly? PlanStartedOn { get; set; }

    /// <summary>The day the plan was stopped, or the day it was answered after the last step.</summary>
    public DateOnly? PlanStoppedOn { get; set; }

    /// <summary>A plan is running while it has been started, not stopped, and the case is still being treated.</summary>
    [JsonIgnore]
    public bool PlanRunning => PlanStartedOn is not null && PlanStoppedOn is null && Status == PestCaseStatus.Active;

    /// <summary>Copies the plan's steps onto the case with fresh ids and starts it on the given day.</summary>
    public void StartPlan(TreatmentPlan plan, DateOnly on)
    {
        PlanId = plan.Id;
        PlanName = plan.Name?.Trim();
        PlanSteps = [.. plan.Steps.Select(s => { var step = s.Copy(); step.Id = Guid.NewGuid(); return step; })];
        PlanStartedOn = on;
        PlanStoppedOn = null;
    }

    /// <summary>Active and monitoring cases are still worth showing; resolved ones aren't.</summary>
    [JsonIgnore]
    public bool IsOpen => Status != PestCaseStatus.Resolved;

    public PestCase Copy()
    {
        var copy = (PestCase)MemberwiseClone();
        copy.PlantIds = [.. PlantIds];
        copy.PlanSteps = [.. PlanSteps.Select(s => s.Copy())];
        return copy;
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();

        if (StartedOn > today)
            errors.Add("That start date is in the future.");

        if (IntervalDays < 1)
            errors.Add("Treat at least every day, so the interval has to be 1 or more.");

        if (Scope == PestScope.Room && PlaceId is null)
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
