using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.SoilMixes;

/// <summary>A mix and how many plants are in it, which is what makes editing one a decision.</summary>
public sealed record MixUse(SoilMix Mix, int Plants)
{
    /// <summary>Every mix with its plants counted, the ones still being mixed first.</summary>
    public static IReadOnlyList<MixUse> List(IEnumerable<SoilMix> mixes, IEnumerable<Plant> plants)
    {
        var alive = plants.Where(p => !p.IsDeleted).ToList();

        return mixes
            .Where(mix => !mix.IsDeleted)
            .Select(mix => new MixUse(mix, alive.Count(p => p.SoilMixId == mix.Id)))
            .OrderBy(use => use.Mix.IsRetired)
            .ThenBy(use => use.Mix.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>The plants still in the collection that are in a mix, by name.</summary>
    public static IReadOnlyList<Plant> PlantsIn(Guid mixId, IEnumerable<Plant> plants) => plants
        .Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && p.SoilMixId == mixId)
        .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
}

/// <summary>The mixes, with the plants counted against them.</summary>
public sealed class SoilMixService(ISoilMixRepository mixes, IPlantRepository plants)
{
    public async Task<IReadOnlyList<MixUse>> GetAllAsync() =>
        MixUse.List(await mixes.GetAllAsync(), await plants.GetAllAsync());

    /// <summary>How many plants are in a mix, for the line before changing or deleting it.</summary>
    public async Task<int> PlantsInAsync(Guid mixId) =>
        (await plants.GetAllAsync()).Count(p => p.SoilMixId == mixId);

    /// <summary>The plants in a mix that are still in the collection, sorted by name.</summary>
    public async Task<IReadOnlyList<Plant>> PlantsListAsync(Guid mixId) =>
        MixUse.PlantsIn(mixId, await plants.GetAllAsync());
}
