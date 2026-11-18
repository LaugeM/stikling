using Stikling.Core.Examples;
using Stikling.Core.Models;

namespace Stikling.Core.People;

/// <summary>How many plants and cuttings a collection has, for what an invite says about it. Never names or photos.</summary>
public static class CollectionCounts
{
    /// <summary>
    /// Plants that are here, not deleted and not an example, and cuttings that are still going, not deleted and not an example.
    /// </summary>
    public static (int Plants, int Cuttings) Of(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations) =>
        (plants.Count(p => !p.IsDeleted && p.InCollection && !ExampleFamily.PlantIds.Contains(p.Id)),
         propagations.Count(p => !p.IsDeleted && p.IsActive && !ExampleFamily.PropagationIds.Contains(p.Id)));
}
