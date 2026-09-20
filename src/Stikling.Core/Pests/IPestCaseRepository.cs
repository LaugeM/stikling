using Stikling.Core.Models;

namespace Stikling.Core.Pests;

public interface IPestCaseRepository
{
    /// <summary>All cases that aren't deleted.</summary>
    Task<IReadOnlyList<PestCase>> GetAllAsync();

    Task<PestCase?> GetAsync(Guid id);

    /// <summary>Adds or updates a case. Throws if it isn't valid.</summary>
    Task SaveAsync(PestCase item);

    /// <summary>Soft-deletes a case.</summary>
    Task DeleteAsync(Guid id);
}
