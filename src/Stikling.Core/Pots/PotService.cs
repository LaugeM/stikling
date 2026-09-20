using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Pots;

/// <summary>The pot library, with the plants counted against it. Mirrors how rooms are served.</summary>
public sealed class PotService(IPotRepository pots, IPlantRepository plants)
{
    public async Task<IReadOnlyList<PotUse>> GetAllAsync() =>
        PotUse.List(await pots.GetAllAsync(), await plants.GetAllAsync());

    /// <summary>How many plants point at a pot, for the line before deleting it.</summary>
    public async Task<int> InUseAsync(Guid potId) =>
        (await plants.GetAllAsync()).Count(p => p.InnerPotId == potId || p.OuterPotId == potId);
}
