using Stikling.Core.Models;

namespace Stikling.Core.Leaves;

public interface ILeafRepository
{
    /// <summary>All leaves that aren't deleted.</summary>
    Task<IReadOnlyList<Leaf>> GetAllAsync();

    Task<Leaf?> GetAsync(Guid id);

    /// <summary>Adds or updates a leaf. Throws if it isn't valid.</summary>
    Task SaveAsync(Leaf leaf);

    /// <summary>Soft-deletes a leaf.</summary>
    Task DeleteAsync(Guid id);
}
