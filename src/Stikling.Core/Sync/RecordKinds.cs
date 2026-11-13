using Stikling.Core.Models;

namespace Stikling.Core.Sync;

/// <summary>Who a kind of record belongs to: the collection, shared with everyone in it, or the person.</summary>
public enum RecordOwner
{
    Collection,
    Person,
}

/// <summary>
/// One kind of record the app keeps: the IndexedDB store it lives in on the device, which is also
/// its kind in sync and its name in a backup.
/// </summary>
public abstract class RecordKind
{
    private protected RecordKind(string name, RecordOwner owner, bool isContent)
    {
        Name = name;
        Owner = owner;
        IsContent = isContent;
    }

    /// <summary>The store's name. Saved on every device and the server, so it never changes.</summary>
    public string Name { get; }

    public abstract Type Type { get; }

    public RecordOwner Owner { get; }

    /// <summary>
    /// True when a record of this kind is something a restore could remove or overwrite, so the
    /// device is saved first. Settings, what's put off on Today and sitter sheets aren't, since a
    /// fresh device can have those before anything has been added.
    /// </summary>
    public bool IsContent { get; }

    public override string ToString() => Name;
}

public sealed class RecordKind<T> : RecordKind where T : Entity
{
    internal RecordKind(string name, RecordOwner owner = RecordOwner.Collection, bool isContent = true)
        : base(name, owner, isContent)
    {
    }

    public override Type Type => typeof(T);
}

/// <summary>
/// Every kind of record, in one list. Sync, backups and the stores on the device all go by it, so a
/// new kind of record is added here once. <c>docs/dev/new-record.md</c> has the other steps.
/// </summary>
public static class RecordKinds
{
    public static readonly RecordKind<Plant> Plants = new("plants");
    public static readonly RecordKind<Propagation> Propagations = new("propagations");
    public static readonly RecordKind<PropagationCareLog> PropagationCareLogs = new("propagationCareLogs");
    public static readonly RecordKind<TimelineEntry> Timeline = new("timeline");
    public static readonly RecordKind<Photo> Photos = new(SyncKinds.Photos);
    public static readonly RecordKind<CareLog> CareLogs = new("careLogs");
    public static readonly RecordKind<PestCase> PestCases = new("pestCases");
    public static readonly RecordKind<PestTreatment> PestTreatments = new("pestTreatments");
    public static readonly RecordKind<Pot> Pots = new("pots");
    public static readonly RecordKind<SoilMix> SoilMixes = new("soilMixes");
    public static readonly RecordKind<Product> Products = new("products");
    public static readonly RecordKind<Feed> Feeds = new("feeds");
    public static readonly RecordKind<TreatmentRecipe> TreatmentRecipes = new("treatmentRecipes");
    public static readonly RecordKind<Place> Places = new("places");
    public static readonly RecordKind<PutOff> PutOffs = new("putOffs", isContent: false);
    public static readonly RecordKind<GrowLight> GrowLights = new("growLights");
    public static readonly RecordKind<ReceivedHistory> ReceivedHistories = new("receivedHistories");
    public static readonly RecordKind<AwayStretch> AwayStretches = new("awayStretches");
    public static readonly RecordKind<SitterSheet> SitterSheets = new("sitterSheets", isContent: false);
    public static readonly RecordKind<FloweringMark> FloweringMarks = new("floweringMarks");
    public static readonly RecordKind<Problem> Problems = new("problems");
    public static readonly RecordKind<ProblemUpdate> ProblemUpdates = new("problemUpdates");
    public static readonly RecordKind<Wish> Wishes = new("wishes");
    public static readonly RecordKind<Seller> Sellers = new("sellers");
    public static readonly RecordKind<OwnCareKind> OwnCareKinds = new("ownCareKinds");
    public static readonly RecordKind<OwnField> OwnFields = new("ownFields");
    public static readonly RecordKind<CareInterval> CareIntervals = new("careIntervals");
    public static readonly RecordKind<TreatmentPlan> TreatmentPlans = new("treatmentPlans");
    public static readonly RecordKind<Order> Orders = new("orders");
    public static readonly RecordKind<OrderItem> OrderItems = new("orderItems");
    public static readonly RecordKind<Leaf> Leaves = new("leaves");
    public static readonly RecordKind<PhotoMark> PhotoMarks = new("photoMarks");
    public static readonly RecordKind<Supply> Supplies = new("supplies");
    public static readonly RecordKind<SupplyEntry> SupplyEntries = new("supplyEntries");
    public static readonly RecordKind<ShoppingItem> ShoppingItems = new("shoppingItems");
    public static readonly RecordKind<Measurement> Measurements = new("measurements");
    public static readonly RecordKind<OwnMeasureKind> OwnMeasureKinds = new("ownMeasureKinds");

    public static readonly RecordKind<UserSettings> Settings = new(SyncKinds.Settings, RecordOwner.Person, isContent: false);

    /// <summary>
    /// In the order sync has always listed them. A device keeps the list it last synced with and
    /// fetches everything again when it changes, so a new kind goes at the end of the collection's.
    /// </summary>
    public static readonly IReadOnlyList<RecordKind> All =
    [
        Plants,
        Propagations,
        PropagationCareLogs,
        Timeline,
        Photos,
        CareLogs,
        PestCases,
        PestTreatments,
        Pots,
        SoilMixes,
        Products,
        Feeds,
        TreatmentRecipes,
        Places,
        PutOffs,
        GrowLights,
        ReceivedHistories,
        AwayStretches,
        SitterSheets,
        FloweringMarks,
        Problems,
        ProblemUpdates,
        Wishes,
        Sellers,
        OwnCareKinds,
        OwnFields,
        CareIntervals,
        TreatmentPlans,
        Orders,
        OrderItems,
        Leaves,
        PhotoMarks,
        Supplies,
        SupplyEntries,
        ShoppingItems,
        Measurements,
        OwnMeasureKinds,
        Settings,
    ];
}

/// <summary>
/// The records on this device, read and written by kind. <c>IndexedDb</c> in the Web project is
/// the one on the device.
/// </summary>
public interface IRecordStore
{
    /// <summary>Every record of the kind, deleted ones too.</summary>
    Task<List<T>> GetAllAsync<T>(RecordKind<T> kind) where T : Entity;

    /// <summary>Writes the record as it is, without touching its dates.</summary>
    Task PutAsync<T>(RecordKind<T> kind, T value) where T : Entity;
}
