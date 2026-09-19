using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Web.Services;

public sealed class IndexedDbPlantRepository(IndexedDb db, TimeProvider time) : IPlantRepository
{
    public async Task<IReadOnlyList<Plant>> GetAllAsync() =>
        (await db.GetAllAsync<Plant>(Stores.Plants)).Where(p => !p.IsDeleted).ToList();

    public async Task<Plant?> GetAsync(Guid id)
    {
        var plant = await db.GetAsync<Plant>(Stores.Plants, id);
        return plant is { IsDeleted: false } ? plant : null;
    }

    public async Task SaveAsync(Plant plant)
    {
        var errors = plant.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var now = time.GetUtcNow();
        if (plant.CreatedAt == default)
            plant.CreatedAt = now;
        plant.UpdatedAt = now;

        await db.PutAsync(Stores.Plants, plant);
    }

    public async Task DeleteAsync(Guid id)
    {
        var plant = await db.GetAsync<Plant>(Stores.Plants, id);
        if (plant is null || plant.IsDeleted)
            return;

        plant.DeletedAt = plant.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(Stores.Plants, plant);
    }
}
