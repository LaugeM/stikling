using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>What the Mark failed and Gave some away sheets share: how many of what is left, and on which day.</summary>
public abstract class CountForm
{
    private protected CountForm(UnitForms deps, Propagation propagation)
    {
        Deps = deps;
        Propagation = propagation;
        Date = deps.Time.Today();
    }

    private protected UnitForms Deps { get; }

    /// <summary>
    /// The propagation it records on. The page sets the one it reloaded after a sync, so a save never
    /// writes the copy the sheet was opened with over a newer one.
    /// </summary>
    public Propagation Propagation { get; set; }

    public int Count { get; set; } = 1;

    public DateOnly Date { get; set; }

    public bool CountIsValid => Propagation.CanTake(Count);

    /// <summary>These are all that is left, so saving them finishes the propagation.</summary>
    public bool Finishes => Count >= Propagation.RemainingCount;

    /// <summary>
    /// Records the units. An exception means nothing was counted, and saving again is safe. When the
    /// count was stored and a later step failed, that comes back as <see cref="UnitResult.LaterError"/>.
    /// </summary>
    public Task<UnitResult> SaveAsync()
    {
        // Taken now, so a reload during the save can't change which copy it records on
        var propagation = Propagation;
        return UnitForms.RunAsync(async () =>
        {
            await RecordAsync(propagation);
            return [];
        });
    }

    private protected abstract Task RecordAsync(Propagation propagation);
}
