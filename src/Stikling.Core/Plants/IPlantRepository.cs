using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>
/// Storage for plants. The app uses an IndexedDB implementation on the device;
/// a later sync version could swap in one that talks to a web API.
/// Plants that name a deleted plant as parent keep the link.
/// </summary>
public interface IPlantRepository : IRepository<Plant>
{
    /// <summary>True when a plant with this id is stored and soft-deleted.</summary>
    Task<bool> IsDeletedAsync(Guid id);
}
