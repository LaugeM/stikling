using Stikling.Core.Models;

namespace Stikling.Core.Pots;

public interface IPotRepository
{
    Task<IReadOnlyList<Pot>> GetAllAsync();

    /// <summary>
    /// The containers for propagating, with the deleted pots kept so a name can be told apart from
    /// one never seen.
    /// </summary>
    Task<ContainerList> GetContainersAsync();

    Task<Pot?> GetAsync(Guid id);

    Task SaveAsync(Pot pot);

    /// <summary>Soft-deletes the pot. Plants that point at it keep the link, the way a deleted parent works.</summary>
    Task DeleteAsync(Guid id);
}
