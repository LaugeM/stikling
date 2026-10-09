using Stikling.Core.Models;

namespace Stikling.Core.Lights;

public interface IGrowLightRepository
{
    /// <summary>All grow lights that aren't deleted.</summary>
    Task<IReadOnlyList<GrowLight>> GetAllAsync();

    Task<GrowLight?> GetAsync(Guid id);

    /// <summary>Adds or updates a grow light. Throws if it isn't valid.</summary>
    Task SaveAsync(GrowLight light);

    /// <summary>Soft-deletes a grow light.</summary>
    Task DeleteAsync(Guid id);
}
