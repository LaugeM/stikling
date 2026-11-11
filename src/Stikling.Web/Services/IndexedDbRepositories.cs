using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Fields;
using Stikling.Core.Flowering;
using Stikling.Core.Leaves;
using Stikling.Core.Lights;
using Stikling.Core.Orders;
using Stikling.Core.Models;
using Stikling.Core.Sync;
using Stikling.Core.Pests;
using Stikling.Core.Problems;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Shelves;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Sharing;
using Stikling.Core.SoilMixes;
using Stikling.Core.Stock;
using Stikling.Core.Today;
using Stikling.Core.Wishlist;

namespace Stikling.Web.Services;

public sealed class IndexedDbPlantRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Plant>(db, time, RecordKinds.Plants), IPlantRepository
{
    protected override IReadOnlyList<string> Validate(Plant plant) => plant.Validate(Today);
}

public sealed class IndexedDbPotRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Pot>(db, time, RecordKinds.Pots), IPotRepository
{
    // Deleted pots are kept, so a deleted container's name isn't made again
    public async Task<ContainerList> GetContainersAsync() => new(await GetStoredAsync());

    protected override IReadOnlyList<string> Validate(Pot pot) => pot.Validate();
}

public sealed class IndexedDbSoilMixRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<SoilMix>(db, time, RecordKinds.SoilMixes), ISoilMixRepository
{
    protected override IReadOnlyList<string> Validate(SoilMix mix) => mix.Validate();
}

public sealed class IndexedDbProductRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Product>(db, time, RecordKinds.Products), IProductRepository
{
    protected override IReadOnlyList<string> Validate(Product product) => product.Validate();
}

public sealed class IndexedDbFeedRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Feed>(db, time, RecordKinds.Feeds), IFeedRepository
{
    protected override IReadOnlyList<string> Validate(Feed feed) => feed.Validate();
}

public sealed class IndexedDbSettingsRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<UserSettings>(db, time, RecordKinds.Settings), ISettingsRepository
{
    protected override IReadOnlyList<string> Validate(UserSettings settings) => [];
}

public sealed class IndexedDbPutOffRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PutOff>(db, time, RecordKinds.PutOffs), IPutOffRepository
{
    protected override IReadOnlyList<string> Validate(PutOff putOff) =>
        string.IsNullOrWhiteSpace(putOff.Key) ? ["A put-off needs a key."] : [];
}

public sealed class IndexedDbGrowLightRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<GrowLight>(db, time, RecordKinds.GrowLights), IGrowLightRepository
{
    protected override IReadOnlyList<string> Validate(GrowLight light) => light.Validate();
}

public sealed class IndexedDbAwayStretchRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<AwayStretch>(db, time, RecordKinds.AwayStretches), IAwayStretchRepository
{
    protected override IReadOnlyList<string> Validate(AwayStretch stretch) => stretch.Validate();
}

public sealed class IndexedDbFloweringMarkRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<FloweringMark>(db, time, RecordKinds.FloweringMarks), IFloweringMarkRepository
{
    protected override IReadOnlyList<string> Validate(FloweringMark mark) => mark.Validate(Today);
}

public sealed class IndexedDbProblemRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Problem>(db, time, RecordKinds.Problems), IProblemRepository
{
    protected override IReadOnlyList<string> Validate(Problem problem) => problem.Validate(Today);
}

public sealed class IndexedDbProblemUpdateRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<ProblemUpdate>(db, time, RecordKinds.ProblemUpdates), IProblemUpdateRepository
{
    protected override IReadOnlyList<string> Validate(ProblemUpdate update) => update.Validate(Today);
}

public sealed class IndexedDbWishRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Wish>(db, time, RecordKinds.Wishes), IWishRepository
{
    protected override IReadOnlyList<string> Validate(Wish wish) => wish.Validate(Today);
}

public sealed class IndexedDbSitterSheetRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<SitterSheet>(db, time, RecordKinds.SitterSheets), ISitterSheetRepository
{
    protected override IReadOnlyList<string> Validate(SitterSheet sheet) => [];
}

public sealed class IndexedDbReceivedHistoryRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<ReceivedHistory>(db, time, RecordKinds.ReceivedHistories), IReceivedHistoryRepository
{
    protected override IReadOnlyList<string> Validate(ReceivedHistory history) => [];

    public async Task<ReceivedHistory?> GetForAsync(Guid subjectId) =>
        (await GetAllAsync()).Where(h => h.SubjectId == subjectId).OrderByDescending(h => h.UpdatedAt).FirstOrDefault();
}

public sealed class IndexedDbTreatmentRecipeRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<TreatmentRecipe>(db, time, RecordKinds.TreatmentRecipes), ITreatmentRecipeRepository
{
    protected override IReadOnlyList<string> Validate(TreatmentRecipe recipe) => recipe.Validate();
}

public sealed class IndexedDbPropagationRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Propagation>(db, time, RecordKinds.Propagations), IPropagationRepository
{
    protected override IReadOnlyList<string> Validate(Propagation propagation) => propagation.Validate(Today);
}

public sealed class IndexedDbPlaceRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Place>(db, time, RecordKinds.Places), IPlaceRepository
{
    // Merged places are deleted, but they're kept so what still points at them can find where they went
    public async Task<Places> GetPlacesAsync() =>
        new((await GetStoredAsync()).Where(p => !p.IsDeleted || p.MergedIntoId is not null));

    protected override IReadOnlyList<string> Validate(Place place) => place.Validate();
}

public sealed class IndexedDbTreatmentPlanRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<TreatmentPlan>(db, time, RecordKinds.TreatmentPlans), ITreatmentPlanRepository
{
    protected override IReadOnlyList<string> Validate(TreatmentPlan plan) => plan.Validate();
}

public sealed class IndexedDbSellerRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Seller>(db, time, RecordKinds.Sellers), ISellerRepository
{
    // Merged and deleted sellers are kept, so what still points at them can be followed or skipped
    public async Task<SellerList> GetSellersAsync() => new(await GetStoredAsync());

    protected override IReadOnlyList<string> Validate(Seller seller) => seller.Validate();
}

public sealed class IndexedDbOwnCareKindRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<OwnCareKind>(db, time, RecordKinds.OwnCareKinds), IOwnCareKindRepository
{
    // Deleted kinds are kept in the list so a removed name can be brought back
    async Task<IReadOnlyList<OwnCareKind>> IOwnCareKindRepository.GetStoredAsync() => await GetStoredAsync();

    protected override IReadOnlyList<string> Validate(OwnCareKind kind) => kind.Validate();
}

public sealed class IndexedDbCareIntervalRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<CareInterval>(db, time, RecordKinds.CareIntervals), ICareIntervalRepository
{
    protected override IReadOnlyList<string> Validate(CareInterval interval) => interval.Validate();
}

public sealed class IndexedDbOwnFieldRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<OwnField>(db, time, RecordKinds.OwnFields), IOwnFieldRepository
{
    // Deleted fields are kept in the list so a removed name can be brought back
    async Task<IReadOnlyList<OwnField>> IOwnFieldRepository.GetStoredAsync() => await GetStoredAsync();

    protected override IReadOnlyList<string> Validate(OwnField field) => field.Validate();
}

public sealed class IndexedDbOrderRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Order>(db, time, RecordKinds.Orders), IOrderRepository
{
    protected override IReadOnlyList<string> Validate(Order order) => order.Validate(Today);
}

public sealed class IndexedDbOrderItemRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<OrderItem>(db, time, RecordKinds.OrderItems), IOrderItemRepository
{
    protected override IReadOnlyList<string> Validate(OrderItem item) => item.Validate();
}

public sealed class IndexedDbLeafRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Leaf>(db, time, RecordKinds.Leaves), ILeafRepository
{
    protected override IReadOnlyList<string> Validate(Leaf leaf) => leaf.Validate(Today);
}

public sealed class IndexedDbPhotoMarkRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PhotoMark>(db, time, RecordKinds.PhotoMarks), IPhotoMarkRepository
{
    public async Task<IReadOnlyList<PhotoMark>> GetForPhotoAsync(Guid photoId) =>
        (await GetAllAsync()).Where(m => m.PhotoId == photoId).ToList();

    public async Task<IReadOnlyList<PhotoMark>> GetForSubjectAsync(Guid subjectId) =>
        (await GetAllAsync()).Where(m => m.SubjectId == subjectId).ToList();

    protected override IReadOnlyList<string> Validate(PhotoMark mark) => mark.Validate();
}

public sealed class IndexedDbPropagationCareLogRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PropagationCareLog>(db, time, RecordKinds.PropagationCareLogs), IPropagationCareLogRepository
{
    protected override IReadOnlyList<string> Validate(PropagationCareLog entry) => entry.Validate();
}

public sealed class IndexedDbCareLogRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<CareLog>(db, time, RecordKinds.CareLogs), ICareLogRepository
{
    protected override IReadOnlyList<string> Validate(CareLog entry) => entry.Validate(Today);
}

public sealed class IndexedDbPestCaseRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PestCase>(db, time, RecordKinds.PestCases), IPestCaseRepository
{
    protected override IReadOnlyList<string> Validate(PestCase item) => item.Validate(Today);
}

public sealed class IndexedDbPestTreatmentRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<PestTreatment>(db, time, RecordKinds.PestTreatments), IPestTreatmentRepository
{
    protected override IReadOnlyList<string> Validate(PestTreatment treatment) => treatment.Validate(Today);
}

public sealed class IndexedDbSupplyRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<Supply>(db, time, RecordKinds.Supplies), ISupplyRepository
{
    protected override IReadOnlyList<string> Validate(Supply supply) => supply.Validate();
}

public sealed class IndexedDbSupplyEntryRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<SupplyEntry>(db, time, RecordKinds.SupplyEntries), ISupplyEntryRepository
{
    protected override IReadOnlyList<string> Validate(SupplyEntry entry) => entry.Validate();
}

public sealed class IndexedDbShoppingItemRepository(IndexedDb db, TimeProvider time)
    : IndexedDbEntityRepository<ShoppingItem>(db, time, RecordKinds.ShoppingItems), IShoppingItemRepository
{
    protected override IReadOnlyList<string> Validate(ShoppingItem item) => item.Validate();
}
