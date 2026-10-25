using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// The rules of the Mark failed sheet, kept apart from the page so they can be tested. The page binds
/// to the properties and calls <see cref="SaveAsync"/>.
/// </summary>
public sealed class FailedForm
{
    private readonly UnitForms deps;

    internal FailedForm(UnitForms deps, Propagation propagation)
    {
        this.deps = deps;
        Propagation = propagation;
        Date = deps.Time.Today();
    }

    /// <summary>
    /// The propagation it records on. The page sets the one it reloaded after a sync, so a save never
    /// writes the copy the sheet was opened with over a newer one.
    /// </summary>
    public Propagation Propagation { get; set; }

    public int Count { get; set; } = 1;

    public FailReason? Reason { get; set; }

    public string? Note { get; set; }

    public DateOnly Date { get; set; }

    public bool CountIsValid => Count >= 1 && Count <= Propagation.RemainingCount;

    /// <summary>These are all that is left, so marking them finishes the propagation.</summary>
    public bool Finishes => Count >= Propagation.RemainingCount;

    /// <summary>
    /// Records the failed units. An exception means nothing was counted, and saving again is safe. When
    /// the count was stored and a later step failed, that comes back as <see cref="UnitResult.LaterError"/>.
    /// </summary>
    public Task<UnitResult> SaveAsync() => UnitForms.RunAsync(
        () => Propagation.FailedCount,
        async () =>
        {
            await deps.PropagationService.MarkFailedAsync(Propagation, Count, Reason, Note, Date);
            return [];
        },
        _ => { });
}
