using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// The rules of the Gave some away sheet, kept apart from the page so they can be tested. The page binds
/// to the properties and calls <see cref="SaveAsync"/>.
/// </summary>
public sealed class GiveAwayForm
{
    private readonly UnitForms deps;

    internal GiveAwayForm(UnitForms deps, Propagation propagation)
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

    public string? To { get; set; }

    public DateOnly Date { get; set; }

    public bool CountIsValid => Count >= 1 && Count <= Propagation.RemainingCount;

    /// <summary>These are all that is left, so giving them away finishes the propagation.</summary>
    public bool Finishes => Count >= Propagation.RemainingCount;

    /// <summary>
    /// Records the units given away. An exception means nothing was counted, and saving again is safe. When
    /// the count was stored and a later step failed, that comes back as <see cref="UnitResult.LaterError"/>.
    /// </summary>
    public Task<UnitResult> SaveAsync() => UnitForms.RunAsync(
        () => Propagation.GivenAwayCount,
        async () =>
        {
            await deps.PropagationService.GiveAwayAsync(Propagation, Count, To, Date);
            return [];
        },
        _ => { });
}
