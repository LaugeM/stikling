using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

public interface IPropagationRepository
{
    /// <summary>All propagations that aren't deleted.</summary>
    Task<IReadOnlyList<Propagation>> GetAllAsync();

    Task<Propagation?> GetAsync(Guid id);

    /// <summary>Adds or updates a propagation. Throws if it isn't valid.</summary>
    Task SaveAsync(Propagation propagation);

    /// <summary>Soft-deletes a propagation.</summary>
    Task DeleteAsync(Guid id);
}
