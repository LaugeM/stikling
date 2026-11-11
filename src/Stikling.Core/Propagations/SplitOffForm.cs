using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// The rules of the Split off sheet, kept apart from the page so they can be tested. The page binds to the
/// properties and calls <see cref="SaveAsync"/>. A retry after a failure saves over the same new propagation.
/// </summary>
public sealed class SplitOffForm
{
    private readonly UnitForms deps;
    private readonly Guid newId = Guid.NewGuid();

    internal SplitOffForm(UnitForms deps, Propagation propagation)
    {
        this.deps = deps;
        Propagation = propagation;
        Date = deps.Time.Today();
        Stage = DefaultStage(propagation.Stage);
    }

    /// <summary>
    /// The propagation it splits from. The page sets the one it reloaded after a sync, so a save never
    /// writes the copy the sheet was opened with over a newer one.
    /// </summary>
    public Propagation Propagation { get; set; }

    public int Count { get; set; } = 1;

    /// <summary>The stage the split off units have reached.</summary>
    public PropagationStage Stage { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The most that can be split off: at least one unit stays in the batch.</summary>
    public int MaxCount => Propagation.RemainingCount - 1;

    public bool CountIsValid => Count >= 1 && Count <= MaxCount;

    /// <summary>The stages the split off units can have reached.</summary>
    public static IReadOnlyList<PropagationStage> Stages { get; } =
        [PropagationStage.Started, PropagationStage.Rooting, PropagationStage.Rooted];

    /// <summary>One step past the batch's stage, at most Rooted.</summary>
    public static PropagationStage DefaultStage(PropagationStage batch) => batch switch
    {
        PropagationStage.Started => PropagationStage.Rooting,
        _ => PropagationStage.Rooted
    };

    /// <summary>Splits them off and gives back the new propagation. Saving again after an exception is safe.</summary>
    public Task<Propagation> SaveAsync() =>
        deps.PropagationService.SplitOffAsync(Propagation, Count, Stage, Date, newId);
}
