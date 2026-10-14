using Stikling.Core.Propagations;

namespace Stikling.Core.Plants;

/// <summary>Numbering the whole collection in one go, when the person asks for it.</summary>
public sealed class CollectionNumberService(IPlantRepository plants, IPropagationRepository propagations)
{
    /// <summary>What numbering would do now. Nothing is saved.</summary>
    public async Task<NumberAllPlan> PlanAsync() =>
        CollectionNumbers.Plan(await plants.GetAllAsync(), await propagations.GetAllAsync());

    /// <summary>Saves the plan through the repositories, so each record gets a new UpdatedAt.</summary>
    public async Task ApplyAsync(NumberAllPlan plan)
    {
        foreach (var item in plan.Plants)
        {
            item.Plant.CollectionNumber = item.Code;
            await plants.SaveAsync(item.Plant);
        }
        foreach (var item in plan.Cuttings)
        {
            item.Propagation.CollectionNumber = item.Code;
            await propagations.SaveAsync(item.Propagation);
        }
    }
}
