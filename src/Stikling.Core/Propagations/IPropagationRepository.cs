using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

public interface IPropagationRepository
{
    /// <summary>All propagations that aren't deleted.</summary>
    Task<IReadOnlyList<Propagation>> GetAllAsync();

    Task<Propagation?> GetAsync(Guid id);

    /// <summary>True when a propagation with this id is stored and soft-deleted.</summary>
    Task<bool> IsDeletedAsync(Guid id);

    /// <summary>Adds or updates a propagation. Throws if it isn't valid.</summary>
    Task SaveAsync(Propagation propagation);

    /// <summary>Soft-deletes a propagation.</summary>
    Task DeleteAsync(Guid id);
}
