using Stikling.Core.Models;

namespace Stikling.Core.Pests;

/// <summary>Treatments keep their own copy of what was in a deleted recipe.</summary>
public interface ITreatmentRecipeRepository : IRepository<TreatmentRecipe>
{
}
