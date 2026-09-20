using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Web.Services;

public sealed class IndexedDbPlantRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Plant>(db, time, Stores.Plants), IPlantRepository
{
    protected override IReadOnlyList<string> Validate(Plant plant) => plant.Validate();
}

public sealed class IndexedDbPropagationRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Propagation>(db, time, Stores.Propagations), IPropagationRepository
{
    protected override IReadOnlyList<string> Validate(Propagation propagation) => propagation.Validate();
}

public sealed class IndexedDbCareLogRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<CareLog>(db, time, Stores.CareLogs), ICareLogRepository
{
    protected override IReadOnlyList<string> Validate(CareLog entry) => entry.Validate(Today);
}
