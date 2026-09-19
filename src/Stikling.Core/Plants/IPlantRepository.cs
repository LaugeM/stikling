using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>
/// Storage for plants. The app uses an IndexedDB implementation on the device;
/// a later sync version could swap in one that talks to a web API.
/// </summary>
public interface IPlantRepository
{
    /// <summary>All plants that haven't been deleted.</summary>
    Task<IReadOnlyList<Plant>> GetAllAsync();

    /// <summary>The plant with this id, or null if it doesn't exist or was deleted.</summary>
    Task<Plant?> GetAsync(Guid id);

    /// <summary>Creates or updates the plant and sets its timestamps.</summary>
    Task SaveAsync(Plant plant);

    /// <summary>Soft-deletes the plant. Plants that name it as parent keep the link.</summary>
    Task DeleteAsync(Guid id);
}
