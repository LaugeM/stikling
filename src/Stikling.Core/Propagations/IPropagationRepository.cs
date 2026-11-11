using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

public interface IPropagationRepository : IRepository<Propagation>
{
    /// <summary>True when a propagation with this id is stored and soft-deleted.</summary>
    Task<bool> IsDeletedAsync(Guid id);
}
