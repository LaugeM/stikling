using Stikling.Core.Models;

namespace Stikling.Core.Pests;

public interface IPestTreatmentRepository
{
    /// <summary>All treatments that aren't deleted, across every case.</summary>
    Task<IReadOnlyList<PestTreatment>> GetAllAsync();

    Task<PestTreatment?> GetAsync(Guid id);

    /// <summary>Adds or updates a treatment. Throws if it isn't valid.</summary>
    Task SaveAsync(PestTreatment treatment);

    /// <summary>Soft-deletes a treatment.</summary>
    Task DeleteAsync(Guid id);
}
