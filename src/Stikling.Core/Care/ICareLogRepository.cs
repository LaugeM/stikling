using Stikling.Core.Models;

namespace Stikling.Core.Care;

public interface ICareLogRepository
{
    /// <summary>All care entries that aren't deleted.</summary>
    Task<IReadOnlyList<CareLog>> GetAllAsync();

    Task<CareLog?> GetAsync(Guid id);

    /// <summary>Adds or updates a care entry. Throws if it isn't valid.</summary>
    Task SaveAsync(CareLog entry);

    /// <summary>Soft-deletes a care entry.</summary>
    Task DeleteAsync(Guid id);
}
