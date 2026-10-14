using Stikling.Core.Models;

namespace Stikling.Core.Flowering;

public interface IFloweringMarkRepository
{
    /// <summary>All marks that aren't deleted.</summary>
    Task<IReadOnlyList<FloweringMark>> GetAllAsync();

    Task<FloweringMark?> GetAsync(Guid id);

    /// <summary>Adds or updates a mark. Throws if it isn't valid.</summary>
    Task SaveAsync(FloweringMark mark);

    /// <summary>Soft-deletes a mark.</summary>
    Task DeleteAsync(Guid id);
}
