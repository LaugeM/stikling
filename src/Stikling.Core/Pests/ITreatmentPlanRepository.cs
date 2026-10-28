using Stikling.Core.Models;

namespace Stikling.Core.Pests;

public interface ITreatmentPlanRepository
{
    Task<IReadOnlyList<TreatmentPlan>> GetAllAsync();

    Task<TreatmentPlan?> GetAsync(Guid id);

    Task SaveAsync(TreatmentPlan plan);

    /// <summary>Soft-deletes the plan. Cases following it keep their own copy of the steps.</summary>
    Task DeleteAsync(Guid id);
}
