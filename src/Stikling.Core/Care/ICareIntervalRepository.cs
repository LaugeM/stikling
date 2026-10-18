using Stikling.Core.Models;

namespace Stikling.Core.Care;

public interface ICareIntervalRepository
{
    /// <summary>All intervals that aren't deleted.</summary>
    Task<IReadOnlyList<CareInterval>> GetAllAsync();

    Task<CareInterval?> GetAsync(Guid id);

    /// <summary>Adds or updates an interval. Throws if it isn't valid.</summary>
    Task SaveAsync(CareInterval interval);

    /// <summary>Soft-deletes an interval. Nothing logged is changed.</summary>
    Task DeleteAsync(Guid id);
}
