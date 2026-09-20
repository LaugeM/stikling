using Stikling.Core.Models;

namespace Stikling.Core.SoilMixes;

public interface ISoilMixRepository
{
    Task<IReadOnlyList<SoilMix>> GetAllAsync();

    Task<SoilMix?> GetAsync(Guid id);

    Task SaveAsync(SoilMix mix);

    /// <summary>Soft-deletes the mix. Plants in it keep the link, the way a deleted pot works.</summary>
    Task DeleteAsync(Guid id);
}
