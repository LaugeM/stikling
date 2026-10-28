using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Propagations;

/// <summary>Makes the forms for the pot up, failed and gave away sheets on a propagation's page.</summary>
public sealed class UnitForms(PropagationService propagationService, RoomService roomService, IPlantRepository plants, TimeProvider time)
{
    internal PropagationService PropagationService => propagationService;

    internal RoomService RoomService => roomService;

    internal TimeProvider Time => time;

    /// <summary>True when any of these plants is stored, so a pot up that failed may have written some.</summary>
    internal async Task<bool> AnyStoredAsync(IReadOnlyList<Guid> ids)
    {
        foreach (var id in ids)
        {
            if (await plants.GetAsync(id) is not null)
                return true;
        }
        return false;
    }

    /// <param name="places">To fill in where it is now.</param>
    /// <param name="label">Gives the labels for the history.</param>
    public PotUpForm PotUp(Propagation propagation, Places places, Func<Enum, string> label) =>
        new(this, propagation, places, label);

    public FailedForm Failed(Propagation propagation) => new(this, propagation);

    public GiveAwayForm GiveAway(Propagation propagation) => new(this, propagation);

    /// <summary>
    /// Runs a save and tells the two failures apart. The service says when the count was stored and a
    /// later step failed, with a <see cref="PartlySavedException"/>. That comes back as the result's
    /// later error. Any other exception means nothing was counted, and it goes on.
    /// </summary>
    internal static async Task<UnitResult> RunAsync(Func<Task<IReadOnlyList<Plant>>> save)
    {
        try
        {
            return new UnitResult(await save(), null);
        }
        catch (PartlySavedException ex)
        {
            return new UnitResult([], ex.InnerException ?? ex);
        }
    }
}
