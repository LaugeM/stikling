using Stikling.Core.Models;

namespace Stikling.Core.Pests;

public interface ITreatmentRecipeRepository
{
    Task<IReadOnlyList<TreatmentRecipe>> GetAllAsync();

    Task<TreatmentRecipe?> GetAsync(Guid id);

    Task SaveAsync(TreatmentRecipe recipe);

    /// <summary>Soft-deletes the recipe. Treatments keep their own copy of what was in it.</summary>
    Task DeleteAsync(Guid id);
}
