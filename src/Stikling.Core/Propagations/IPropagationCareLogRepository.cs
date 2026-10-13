using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

public interface IPropagationCareLogRepository
{
    /// <summary>All care entries that aren't deleted.</summary>
    Task<IReadOnlyList<PropagationCareLog>> GetAllAsync();

    Task<PropagationCareLog?> GetAsync(Guid id);

    /// <summary>Adds or updates a care entry. Throws if it isn't valid.</summary>
    Task SaveAsync(PropagationCareLog entry);

    /// <summary>Soft-deletes a care entry.</summary>
    Task DeleteAsync(Guid id);
}
