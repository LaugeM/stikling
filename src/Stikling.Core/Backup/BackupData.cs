using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Core.Backup;

/// <summary>
/// Everything the app stores, as it is written to data.json inside a backup ZIP.
/// The photo files sit next to it in the same ZIP, named after the photo ids.
/// </summary>
public sealed class BackupData
{
    /// <summary>Raised when the format changes in a way older versions can't read.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Written so a stray data.json can be recognised as Stikling's.</summary>
    public string App { get; set; } = "Stikling";

    public DateTimeOffset ExportedAt { get; set; }

    public List<Plant> Plants { get; set; } = [];
    public List<Propagation> Propagations { get; set; } = [];
    public List<PropagationCareLog> PropagationCareLogs { get; set; } = [];
    public List<TimelineEntry> Timeline { get; set; } = [];
    public List<Photo> Photos { get; set; } = [];
    public List<CareLog> CareLogs { get; set; } = [];
    public List<PestCase> PestCases { get; set; } = [];
    public List<PestTreatment> PestTreatments { get; set; } = [];
    public List<Pot> Pots { get; set; } = [];
    public List<SoilMix> SoilMixes { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Feed> Feeds { get; set; } = [];
    public List<TreatmentRecipe> TreatmentRecipes { get; set; } = [];
    public List<Place> Places { get; set; } = [];
    public List<UserSettings> Settings { get; set; } = [];
    public List<PutOff> PutOffs { get; set; } = [];
    public List<GrowLight> GrowLights { get; set; } = [];
    public List<ReceivedHistory> ReceivedHistories { get; set; } = [];
    public List<AwayStretch> AwayStretches { get; set; } = [];
    public List<SitterSheet> SitterSheets { get; set; } = [];
    public List<FloweringMark> FloweringMarks { get; set; } = [];
    public List<Problem> Problems { get; set; } = [];
    public List<ProblemUpdate> ProblemUpdates { get; set; } = [];
    public List<Wish> Wishes { get; set; } = [];
    public List<Seller> Sellers { get; set; } = [];
    public List<OwnCareKind> OwnCareKinds { get; set; } = [];
    public List<CareInterval> CareIntervals { get; set; } = [];
    public List<TreatmentPlan> TreatmentPlans { get; set; } = [];
    public List<OwnField> OwnFields { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<OrderItem> OrderItems { get; set; } = [];
    public List<Leaf> Leaves { get; set; } = [];
    public List<PhotoMark> PhotoMarks { get; set; } = [];
    public List<Supply> Supplies { get; set; } = [];
    public List<SupplyEntry> SupplyEntries { get; set; } = [];
    public List<ShoppingItem> ShoppingItems { get; set; } = [];
    public List<Measurement> Measurements { get; set; } = [];
    public List<OwnMeasureKind> OwnMeasureKinds { get; set; } = [];

    /// <summary>The photos' list on its own, since their images are saved and restored after the records.</summary>
    // Before Lists, which uses it: static fields are set in the order they are written
    public static BackupList<Photo> PhotoList { get; } = List(RecordKinds.Photos, d => d.Photos, (d, l) => d.Photos = l);

    /// <summary>
    /// Each kind of record with its list above, in the order of the lists. The property names are
    /// the backup's format, which is why they are written out rather than made from the kinds.
    /// </summary>
    public static readonly IReadOnlyList<BackupList> Lists =
    [
        List(RecordKinds.Plants, d => d.Plants, (d, l) => d.Plants = l),
        List(RecordKinds.Propagations, d => d.Propagations, (d, l) => d.Propagations = l),
        List(RecordKinds.PropagationCareLogs, d => d.PropagationCareLogs, (d, l) => d.PropagationCareLogs = l),
        List(RecordKinds.Timeline, d => d.Timeline, (d, l) => d.Timeline = l),
        PhotoList,
        List(RecordKinds.CareLogs, d => d.CareLogs, (d, l) => d.CareLogs = l),
        List(RecordKinds.PestCases, d => d.PestCases, (d, l) => d.PestCases = l),
        List(RecordKinds.PestTreatments, d => d.PestTreatments, (d, l) => d.PestTreatments = l),
        List(RecordKinds.Pots, d => d.Pots, (d, l) => d.Pots = l),
        List(RecordKinds.SoilMixes, d => d.SoilMixes, (d, l) => d.SoilMixes = l),
        List(RecordKinds.Products, d => d.Products, (d, l) => d.Products = l),
        List(RecordKinds.Feeds, d => d.Feeds, (d, l) => d.Feeds = l),
        List(RecordKinds.TreatmentRecipes, d => d.TreatmentRecipes, (d, l) => d.TreatmentRecipes = l),
        List(RecordKinds.Places, d => d.Places, (d, l) => d.Places = l),
        List(RecordKinds.Settings, d => d.Settings, (d, l) => d.Settings = l),
        List(RecordKinds.PutOffs, d => d.PutOffs, (d, l) => d.PutOffs = l),
        List(RecordKinds.GrowLights, d => d.GrowLights, (d, l) => d.GrowLights = l),
        List(RecordKinds.ReceivedHistories, d => d.ReceivedHistories, (d, l) => d.ReceivedHistories = l),
        List(RecordKinds.AwayStretches, d => d.AwayStretches, (d, l) => d.AwayStretches = l),
        List(RecordKinds.SitterSheets, d => d.SitterSheets, (d, l) => d.SitterSheets = l),
        List(RecordKinds.FloweringMarks, d => d.FloweringMarks, (d, l) => d.FloweringMarks = l),
        List(RecordKinds.Problems, d => d.Problems, (d, l) => d.Problems = l),
        List(RecordKinds.ProblemUpdates, d => d.ProblemUpdates, (d, l) => d.ProblemUpdates = l),
        List(RecordKinds.Wishes, d => d.Wishes, (d, l) => d.Wishes = l),
        List(RecordKinds.Sellers, d => d.Sellers, (d, l) => d.Sellers = l),
        List(RecordKinds.OwnCareKinds, d => d.OwnCareKinds, (d, l) => d.OwnCareKinds = l),
        List(RecordKinds.CareIntervals, d => d.CareIntervals, (d, l) => d.CareIntervals = l),
        List(RecordKinds.TreatmentPlans, d => d.TreatmentPlans, (d, l) => d.TreatmentPlans = l),
        List(RecordKinds.OwnFields, d => d.OwnFields, (d, l) => d.OwnFields = l),
        List(RecordKinds.Orders, d => d.Orders, (d, l) => d.Orders = l),
        List(RecordKinds.OrderItems, d => d.OrderItems, (d, l) => d.OrderItems = l),
        List(RecordKinds.Leaves, d => d.Leaves, (d, l) => d.Leaves = l),
        List(RecordKinds.PhotoMarks, d => d.PhotoMarks, (d, l) => d.PhotoMarks = l),
        List(RecordKinds.Supplies, d => d.Supplies, (d, l) => d.Supplies = l),
        List(RecordKinds.SupplyEntries, d => d.SupplyEntries, (d, l) => d.SupplyEntries = l),
        List(RecordKinds.ShoppingItems, d => d.ShoppingItems, (d, l) => d.ShoppingItems = l),
        List(RecordKinds.Measurements, d => d.Measurements, (d, l) => d.Measurements = l),
        List(RecordKinds.OwnMeasureKinds, d => d.OwnMeasureKinds, (d, l) => d.OwnMeasureKinds = l),
    ];

    private static BackupList<T> List<T>(RecordKind<T> kind, Func<BackupData, List<T>> get, Action<BackupData, List<T>> set)
        where T : Entity => new(kind, get, set);

    /// <summary>
    /// What the file contains, for the line shown before restoring. Deleted items are in the
    /// file too, so a restore can carry a delete over, but they aren't counted as content.
    /// </summary>
    public BackupCounts Counts => new(
        Plants.Count(p => !p.IsDeleted),
        Propagations.Count(p => !p.IsDeleted),
        Timeline.Count(e => !e.IsDeleted),
        Photos.Count(p => !p.IsDeleted),
        CareLogs.Count(l => !l.IsDeleted),
        PestCases.Count(c => !c.IsDeleted),
        PestTreatments.Count(t => !t.IsDeleted),
        Pots.Count(p => !p.IsDeleted),
        SoilMixes.Count(m => !m.IsDeleted),
        Products.Count(p => !p.IsDeleted),
        Feeds.Count(f => !f.IsDeleted),
        TreatmentRecipes.Count(r => !r.IsDeleted),
        Places.Count(p => !p.IsDeleted && !p.IsSpot),
        Settings.Count(s => !s.IsDeleted),
        PutOffs.Count(p => !p.IsDeleted),
        GrowLights.Count(l => !l.IsDeleted),
        ReceivedHistories.Count(h => !h.IsDeleted),
        AwayStretches.Count(s => !s.IsDeleted),
        SitterSheets.Count(s => !s.IsDeleted),
        PropagationCareLogs.Count(l => !l.IsDeleted),
        FloweringMarks.Count(m => !m.IsDeleted),
        Problems.Count(p => !p.IsDeleted),
        ProblemUpdates.Count(u => !u.IsDeleted),
        Wishes.Count(w => !w.IsDeleted),
        Sellers.Count(s => !s.IsDeleted),
        OwnCareKinds.Count(k => !k.IsDeleted),
        CareIntervals.Count(i => !i.IsDeleted),
        TreatmentPlans.Count(p => !p.IsDeleted),
        OwnFields.Count(f => !f.IsDeleted),
        Orders.Count(o => !o.IsDeleted),
        OrderItems.Count(i => !i.IsDeleted),
        Leaves.Count(l => !l.IsDeleted),
        PhotoMarks.Count(m => !m.IsDeleted),
        Supplies.Count(s => !s.IsDeleted),
        SupplyEntries.Count(e => !e.IsDeleted),
        ShoppingItems.Count(i => !i.IsDeleted),
        Measurements.Count(m => !m.IsDeleted),
        OwnMeasureKinds.Count(k => !k.IsDeleted));
}

public sealed record BackupCounts(
    int Plants,
    int Propagations,
    int Entries,
    int Photos,
    int CareEntries,
    int PestCases,
    int Treatments,
    int Pots,
    int SoilMixes,
    int Products,
    int Feeds,
    int Recipes,
    int Rooms,
    int Settings,
    int PutOffs,
    int GrowLights,
    int ReceivedHistories,
    int AwayStretches,
    int SitterSheets,
    int PropagationCareEntries,
    int FloweringMarks,
    int Problems,
    int ProblemUpdates,
    int Wishes,
    int Sellers,
    int OwnCareKinds,
    int CareIntervals,
    int TreatmentPlans,
    int OwnFields,
    int Orders,
    int OrderItems,
    int Leaves,
    int PhotoMarks,
    int Supplies,
    int SupplyEntries,
    int ShoppingItems,
    int Measurements,
    int OwnMeasureKinds);
