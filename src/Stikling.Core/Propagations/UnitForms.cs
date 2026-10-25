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
    /// Runs a save and tells the two failures apart. The service only changes the caller's count once
    /// it is stored, so a count that is unchanged after an exception means nothing was counted and the
    /// exception goes on. A changed count means the change happened and a later step failed.
    /// </summary>
    /// <param name="counted">Told whether the count was stored, whether the save succeeded or not.</param>
    internal static async Task<UnitResult> RunAsync(
        Func<int> count,
        Func<Task<IReadOnlyList<Plant>>> save,
        Action<bool> counted)
    {
        var before = count();
        try
        {
            var plants = await save();
            counted(true);
            return new UnitResult(plants, null);
        }
        catch (Exception ex)
        {
            if (count() == before)
            {
                counted(false);
                throw;
            }

            counted(true);
            return new UnitResult([], ex);
        }
    }
}
