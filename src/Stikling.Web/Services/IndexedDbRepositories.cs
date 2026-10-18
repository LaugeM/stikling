using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Flowering;
using Stikling.Core.Lights;
using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Problems;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Sharing;
using Stikling.Core.SoilMixes;
using Stikling.Core.Today;
using Stikling.Core.Wishlist;

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

public sealed class IndexedDbSettingsRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<UserSettings>(db, time, Stores.Settings), ISettingsRepository
{
    protected override IReadOnlyList<string> Validate(UserSettings settings) => [];
}

public sealed class IndexedDbPutOffRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PutOff>(db, time, Stores.PutOffs), IPutOffRepository
{
    protected override IReadOnlyList<string> Validate(PutOff putOff) =>
        string.IsNullOrWhiteSpace(putOff.Key) ? ["A put-off needs a key."] : [];
}

public sealed class IndexedDbGrowLightRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<GrowLight>(db, time, Stores.GrowLights), IGrowLightRepository
{
    protected override IReadOnlyList<string> Validate(GrowLight light) => light.Validate();
}

public sealed class IndexedDbAwayStretchRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<AwayStretch>(db, time, Stores.AwayStretches), IAwayStretchRepository
{
    protected override IReadOnlyList<string> Validate(AwayStretch stretch) => stretch.Validate();
}

public sealed class IndexedDbFloweringMarkRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<FloweringMark>(db, time, Stores.FloweringMarks), IFloweringMarkRepository
{
    protected override IReadOnlyList<string> Validate(FloweringMark mark) => mark.Validate(Today);
}

public sealed class IndexedDbProblemRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Problem>(db, time, Stores.Problems), IProblemRepository
{
    protected override IReadOnlyList<string> Validate(Problem problem) => problem.Validate(Today);
}

public sealed class IndexedDbProblemUpdateRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<ProblemUpdate>(db, time, Stores.ProblemUpdates), IProblemUpdateRepository
{
    protected override IReadOnlyList<string> Validate(ProblemUpdate update) => update.Validate(Today);
}

public sealed class IndexedDbWishRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Wish>(db, time, Stores.Wishes), IWishRepository
{
    protected override IReadOnlyList<string> Validate(Wish wish) => wish.Validate(Today);
}

public sealed class IndexedDbSitterSheetRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<SitterSheet>(db, time, Stores.SitterSheets), ISitterSheetRepository
{
    protected override IReadOnlyList<string> Validate(SitterSheet sheet) => [];
}

public sealed class IndexedDbReceivedHistoryRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<ReceivedHistory>(db, time, Stores.ReceivedHistories), IReceivedHistoryRepository
{
    protected override IReadOnlyList<string> Validate(ReceivedHistory history) => [];

    public async Task<ReceivedHistory?> GetForAsync(Guid subjectId) =>
        (await GetAllAsync()).Where(h => h.SubjectId == subjectId).OrderByDescending(h => h.UpdatedAt).FirstOrDefault();
}

public sealed class IndexedDbTreatmentRecipeRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<TreatmentRecipe>(db, time, Stores.TreatmentRecipes), ITreatmentRecipeRepository
{
    protected override IReadOnlyList<string> Validate(TreatmentRecipe recipe) => recipe.Validate();
}

public sealed class IndexedDbPropagationRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Propagation>(db, time, Stores.Propagations), IPropagationRepository
{
    protected override IReadOnlyList<string> Validate(Propagation propagation) => propagation.Validate(Today);
}

public sealed class IndexedDbPlaceRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Place>(db, time, Stores.Places), IPlaceRepository
{
    // Merged places are deleted, but they're kept so what still points at them can find where they went
    public async Task<Places> GetPlacesAsync() =>
        new((await GetStoredAsync()).Where(p => !p.IsDeleted || p.MergedIntoId is not null));

    protected override IReadOnlyList<string> Validate(Place place) => place.Validate();
}

public sealed class IndexedDbSellerRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Seller>(db, time, Stores.Sellers), ISellerRepository
{
    // Merged and deleted sellers are kept, so what still points at them can be followed or skipped
    public async Task<SellerList> GetSellersAsync() => new(await GetStoredAsync());

    protected override IReadOnlyList<string> Validate(Seller seller) => seller.Validate();
}

public sealed class IndexedDbOwnCareKindRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<OwnCareKind>(db, time, Stores.OwnCareKinds), IOwnCareKindRepository
{
    // Deleted kinds are kept in the list so a removed name can be brought back
    async Task<IReadOnlyList<OwnCareKind>> IOwnCareKindRepository.GetStoredAsync() => await GetStoredAsync();

    protected override IReadOnlyList<string> Validate(OwnCareKind kind) => kind.Validate();
}

public sealed class IndexedDbPropagationCareLogRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PropagationCareLog>(db, time, Stores.PropagationCareLogs), IPropagationCareLogRepository
{
    protected override IReadOnlyList<string> Validate(PropagationCareLog entry) => entry.Validate();
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
