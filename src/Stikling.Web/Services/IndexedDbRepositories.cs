using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.SoilMixes;

namespace Stikling.Web.Services;

public sealed class IndexedDbPlantRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Plant>(db, time, Stores.Plants), IPlantRepository
{
    protected override IReadOnlyList<string> Validate(Plant plant) => plant.Validate(Today);
}

public sealed class IndexedDbPotRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Pot>(db, time, Stores.Pots), IPotRepository
{
    protected override IReadOnlyList<string> Validate(Pot pot) => pot.Validate();
}

public sealed class IndexedDbSoilMixRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<SoilMix>(db, time, Stores.SoilMixes), ISoilMixRepository
{
    protected override IReadOnlyList<string> Validate(SoilMix mix) => mix.Validate();
}

public sealed class IndexedDbProductRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Product>(db, time, Stores.Products), IProductRepository
{
    protected override IReadOnlyList<string> Validate(Product product) => product.Validate();
}

public sealed class IndexedDbFeedRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Feed>(db, time, Stores.Feeds), IFeedRepository
{
    protected override IReadOnlyList<string> Validate(Feed feed) => feed.Validate();
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

public sealed class IndexedDbPestCaseRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PestCase>(db, time, Stores.PestCases), IPestCaseRepository
{
    protected override IReadOnlyList<string> Validate(PestCase item) => item.Validate(Today);
}

public sealed class IndexedDbPestTreatmentRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PestTreatment>(db, time, Stores.PestTreatments), IPestTreatmentRepository
{
    protected override IReadOnlyList<string> Validate(PestTreatment treatment) => treatment.Validate(Today);
}
