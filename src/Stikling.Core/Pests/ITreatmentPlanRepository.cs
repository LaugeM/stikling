using Stikling.Core.Models;

namespace Stikling.Core.Pests;

/// <summary>Cases following a deleted plan keep their own copy of the steps.</summary>
public interface ITreatmentPlanRepository : IRepository<TreatmentPlan>
{
}
