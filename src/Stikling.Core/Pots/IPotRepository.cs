using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>Plants that point at a deleted pot keep the link, the way a deleted parent works.</summary>
public interface IPotRepository : IRepository<Pot>
{
    /// <summary>
    /// The containers for propagating, with the deleted pots kept so a name can be told apart from
    /// one never seen.
    /// </summary>
    Task<ContainerList> GetContainersAsync();
}
