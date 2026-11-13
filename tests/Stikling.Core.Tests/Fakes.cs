using System.Text.Json;
using Stikling.Core.Away;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Fields;
using Stikling.Core.Flowering;
using Stikling.Core.Wishlist;
using Stikling.Core.Lights;
using Stikling.Core.Measurements;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Leaves;
using Stikling.Core.Shelves;
using Stikling.Core.Orders;
using Stikling.Core.Photos;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Problems;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Sellers;
using Stikling.Core.Settings;
using Stikling.Core.Sharing;
using Stikling.Core.SoilMixes;
using Stikling.Core.Stock;
using Stikling.Core.Sync;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>
/// The four methods every in-memory repository shares. A fake keeps its records in <see cref="Records"/>,
/// and overrides <see cref="Check"/> to throw for a record that isn't valid.
/// </summary>
internal abstract class FakeRepository<T> : IRepository<T> where T : Entity
{
    // Fakes that stamp dates give each save and delete a moment a second after the last
    private readonly bool stampsDates;
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    protected FakeRepository(bool stampsDates = false) => this.stampsDates = stampsDates;

    protected Dictionary<Guid, T> Records { get; } = [];

    /// <summary>How many times a record was saved.</summary>
    public int Saves { get; private set; }

    /// <summary>Throws when the record can't be saved. Does nothing unless a fake overrides it.</summary>
    protected virtual void Check(T item)
    {
    }

    public Task<IReadOnlyList<T>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<T>>(Records.Values.Where(r => !r.IsDeleted).ToList());

    public Task<T?> GetAsync(Guid id) =>
        Task.FromResult(Records.TryGetValue(id, out var item) && !item.IsDeleted ? item : null);

    public virtual Task SaveAsync(T item)
    {
        Check(item);
        Saves++;
        if (stampsDates)
        {
            if (item.CreatedAt == default)
                item.CreatedAt = clock;
            item.UpdatedAt = clock = clock.AddSeconds(1);
        }
        Records[item.Id] = item;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(Guid id)
    {
        if (Records.TryGetValue(id, out var item))
        {
            if (stampsDates)
                item.DeletedAt = item.UpdatedAt = clock = clock.AddSeconds(1);
            else
                item.DeletedAt = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }
}

/// <summary>In-memory repositories for testing services without a browser database.</summary>
internal sealed class FakePlantRepository : FakeRepository<Plant>, IPlantRepository
{
    public Dictionary<Guid, Plant> Plants => Records;

    public Task<bool> IsDeletedAsync(Guid id) =>
        Task.FromResult(Plants.TryGetValue(id, out var p) && p.IsDeleted);

    protected override void Check(Plant plant)
    {
        // DateOnly.MaxValue: the fake has no clock, and the future-date rule is tested on the model itself
        if (plant.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid plant");
    }
}

internal sealed class FakePropagationRepository : FakeRepository<Propagation>, IPropagationRepository
{
    public Dictionary<Guid, Propagation> Propagations => Records;

    public Task<bool> IsDeletedAsync(Guid id) =>
        Task.FromResult(Propagations.TryGetValue(id, out var p) && p.IsDeleted);

    protected override void Check(Propagation propagation)
    {
        if (propagation.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid propagation");
    }
}

internal sealed class FakeCareLogRepository : ICareLogRepository
{
    /// <summary>In the order they were saved, which the multi-plant tests rely on.</summary>
    public List<CareLog> Logs { get; } = [];

    public Task<IReadOnlyList<CareLog>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<CareLog>>(Logs.Where(l => !l.IsDeleted).ToList());

    public Task<CareLog?> GetAsync(Guid id) =>
        Task.FromResult(Logs.FirstOrDefault(l => l.Id == id && !l.IsDeleted));

    public Task SaveAsync(CareLog entry)
    {
        var index = Logs.FindIndex(l => l.Id == entry.Id);
        if (index >= 0)
            Logs[index] = entry;
        else
            Logs.Add(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Logs.FirstOrDefault(l => l.Id == id) is { } entry)
            entry.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeOwnCareKindRepository : FakeRepository<OwnCareKind>, IOwnCareKindRepository
{
    public Dictionary<Guid, OwnCareKind> Kinds => Records;

    public Task<IReadOnlyList<OwnCareKind>> GetStoredAsync() =>
        Task.FromResult<IReadOnlyList<OwnCareKind>>(Kinds.Values.ToList());

    protected override void Check(OwnCareKind kind)
    {
        if (kind.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}

internal sealed class FakeMeasurementRepository : FakeRepository<Measurement>, IMeasurementRepository
{
    public FakeMeasurementRepository() : base(stampsDates: true)
    {
    }

    public Dictionary<Guid, Measurement> Measurements => Records;

    public Task<IReadOnlyList<Measurement>> GetStoredAsync() =>
        Task.FromResult<IReadOnlyList<Measurement>>(Measurements.Values.ToList());

    protected override void Check(Measurement measurement)
    {
        if (measurement.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}

internal sealed class FakeOwnMeasureKindRepository : FakeRepository<OwnMeasureKind>, IOwnMeasureKindRepository
{
    public Dictionary<Guid, OwnMeasureKind> Kinds => Records;

    public Task<IReadOnlyList<OwnMeasureKind>> GetStoredAsync() =>
        Task.FromResult<IReadOnlyList<OwnMeasureKind>>(Kinds.Values.ToList());

    protected override void Check(OwnMeasureKind kind)
    {
        if (kind.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}

internal sealed class FakeOwnFieldRepository : FakeRepository<OwnField>, IOwnFieldRepository
{
    public Dictionary<Guid, OwnField> Fields => Records;

    public Task<IReadOnlyList<OwnField>> GetStoredAsync() =>
        Task.FromResult<IReadOnlyList<OwnField>>(Fields.Values.ToList());

    protected override void Check(OwnField field)
    {
        if (field.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}

internal sealed class FakePropagationCareLogRepository : IPropagationCareLogRepository
{
    public List<PropagationCareLog> Logs { get; } = [];

    public Task<IReadOnlyList<PropagationCareLog>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PropagationCareLog>>(Logs.Where(l => !l.IsDeleted).ToList());

    public Task<PropagationCareLog?> GetAsync(Guid id) =>
        Task.FromResult(Logs.FirstOrDefault(l => l.Id == id && !l.IsDeleted));

    public Task SaveAsync(PropagationCareLog entry)
    {
        if (entry.Validate().Count > 0)
            throw new InvalidOperationException("Invalid care entry");
        var index = Logs.FindIndex(l => l.Id == entry.Id);
        if (index >= 0)
            Logs[index] = entry;
        else
            Logs.Add(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Logs.FirstOrDefault(l => l.Id == id) is { } entry)
            entry.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTimelineRepository : ITimelineRepository
{
    public List<TimelineEntry> Entries { get; } = [];

    public Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) =>
        Task.FromResult(TimelineOrder.NewestFirst(Entries.Where(e => e.SubjectId == subjectId)));

    public Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, TimelineEntry>>(
            TimelineOrder.NewestFirst(Entries).GroupBy(e => e.SubjectId).ToDictionary(g => g.Key, g => g.First()));

    public Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(
        int count, IReadOnlySet<Guid> subjectIds, DateTimeOffset since, IReadOnlySet<Guid>? referencePhotoIds = null) =>
        Task.FromResult(TodayBoard.RecentActivity(Entries, subjectIds, since, count, referencePhotoIds));

    public Task<IReadOnlyList<TimelineEntry>> GetPlantChangesAsync() =>
        Task.FromResult<IReadOnlyList<TimelineEntry>>(Entries
            .Where(e => !e.IsDeleted && e.Kind == TimelineKind.Change && e.SubjectType == SubjectType.Plant
                && TimelineEvents.Of(e) is not (TimelineEvent.LookedOver or TimelineEvent.Measured)).ToList());

    public Task<IReadOnlyList<TimelineEntry>> GetComingBackAsync() =>
        Task.FromResult<IReadOnlyList<TimelineEntry>>(Entries.Where(NotesComingBack.IsWaiting).ToList());

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLastActivityBeforeAsync(DateTimeOffset before) =>
        Task.FromResult(TimelineActivity.LastBefore(Entries, before));

    public Task<IReadOnlyList<TimelineEntry>> GetSinceAsync(DateTimeOffset since) =>
        Task.FromResult<IReadOnlyList<TimelineEntry>>(Entries.Where(e => !e.IsDeleted && e.OccurredAt >= since).ToList());

    public Task AddAsync(TimelineEntry entry)
    {
        // An entry added again with an id that is stored replaces it, like the device's store does
        Entries.RemoveAll(e => e.Id == entry.Id);
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TimelineEntry entry)
    {
        Updated.Add(entry.Id);
        return Task.CompletedTask;
    }

    /// <summary>Ids of the entries saved through <see cref="UpdateAsync"/>.</summary>
    public List<Guid> Updated { get; } = [];

    public Task DeleteAsync(Guid id)
    {
        Entries.First(e => e.Id == id).DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakePhotoRepository : IPhotoRepository
{
    public Dictionary<Guid, Photo> Photos { get; } = [];

    public Task<IReadOnlyList<Photo>> GetForAsync(Guid subjectId) =>
        Task.FromResult<IReadOnlyList<Photo>>(
            Photos.Values.Where(p => p.SubjectId == subjectId && !p.IsDeleted).OrderByDescending(p => p.TakenAt).ToList());

    public Task<Photo?> GetAsync(Guid id) =>
        Task.FromResult(Photos.GetValueOrDefault(id) is { IsDeleted: false } photo ? photo : null);

    public Task<IReadOnlyList<Photo>> GetOfKindAsync(SubjectType subjectType) =>
        Task.FromResult<IReadOnlyList<Photo>>(
            Photos.Values.Where(p => p.SubjectType == subjectType && !p.IsDeleted).OrderByDescending(p => p.TakenAt).ToList());

    public Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPerSubjectAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, Photo>>(
            Photos.Values.Where(p => !p.IsDeleted && !p.IsReference)
                .GroupBy(p => p.SubjectId)
                .ToDictionary(g => g.Key, g => g.MaxBy(p => p.TakenAt)!));

    public Task AddAsync(Photo photo)
    {
        Photos[photo.Id] = photo;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Photo photo)
    {
        Photos[photo.Id] = photo;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        Photos[id].DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

/// <summary>A clock that always returns the same moment.</summary>
/// <param name="offset">The device's time zone. UTC unless a test is about time zones.</param>
/// <param name="zone">A real time zone instead, for tests about summer time.</param>
internal sealed class FixedTime(DateTimeOffset now, TimeSpan offset = default, TimeZoneInfo? zone = null) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    // A fixed zone keeps "today" the same on every machine the tests run on
    public override TimeZoneInfo LocalTimeZone { get; } = zone ?? (offset == TimeSpan.Zero
        ? TimeZoneInfo.Utc
        : TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test"));
}

internal sealed class FakePotRepository : FakeRepository<Pot>, IPotRepository
{
    public Dictionary<Guid, Pot> Pots => Records;

    public Task<ContainerList> GetContainersAsync() => Task.FromResult(new ContainerList(Pots.Values));

    protected override void Check(Pot pot)
    {
        if (pot.Validate().Count > 0)
            throw new InvalidOperationException("Invalid pot");
    }
}

internal sealed class FakePlaceRepository : IPlaceRepository
{
    // Each new place is a second newer than the last, so "oldest first" is the order they were made
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, Place> Places { get; } = [];

    public Task<Places> GetPlacesAsync() =>
        Task.FromResult(new Places(Places.Values.Where(p => !p.IsDeleted || p.MergedIntoId is not null)));

    public Task SaveAsync(Place place)
    {
        if (place.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        if (place.CreatedAt == default)
            place.CreatedAt = clock = clock.AddSeconds(1);
        Places[place.Id] = place;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Places.TryGetValue(id, out var place))
            place.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    /// <summary>Every place as it stands now.</summary>
    public Places Current => GetPlacesAsync().Result;

    /// <summary>The place written out, "Living room / Windowsill", made the first time it's named.</summary>
    public Guid? IdOf(string? place) =>
        new RoomService(this, new FakePlantRepository(), new FakePropagationRepository()).PlaceIdAsync(place).Result;

    /// <summary>Adds a room, or a spot when a room is given, and returns it.</summary>
    public Place Add(string name, Place? room = null)
    {
        var place = new Place { Name = name, RoomId = room?.Id };
        SaveAsync(place);
        return place;
    }
}

internal sealed class FakeTreatmentPlanRepository : FakeRepository<TreatmentPlan>, ITreatmentPlanRepository
{
    public Dictionary<Guid, TreatmentPlan> Plans => Records;

    protected override void Check(TreatmentPlan plan)
    {
        if (plan.Validate().Count > 0)
            throw new InvalidOperationException("Invalid plan");
    }
}

internal sealed class FakeSellerRepository : ISellerRepository
{
    // Each new seller is a second newer than the last, so "oldest first" is the order they were made
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, Seller> Sellers { get; } = [];

    public Task<SellerList> GetSellersAsync() => Task.FromResult(new SellerList(Sellers.Values));

    public Task SaveAsync(Seller seller)
    {
        if (seller.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        if (seller.CreatedAt == default)
            seller.CreatedAt = clock = clock.AddSeconds(1);
        Sellers[seller.Id] = seller;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Sellers.TryGetValue(id, out var seller))
            seller.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSoilMixRepository : FakeRepository<SoilMix>, ISoilMixRepository
{
    public Dictionary<Guid, SoilMix> Mixes => Records;

    protected override void Check(SoilMix mix)
    {
        if (mix.Validate().Count > 0)
            throw new InvalidOperationException("Invalid mix");
    }
}

internal sealed class FakeProductRepository : FakeRepository<Product>, IProductRepository
{
    public Dictionary<Guid, Product> Products => Records;

    protected override void Check(Product product)
    {
        if (product.Validate().Count > 0)
            throw new InvalidOperationException("Invalid product");
    }
}

internal sealed class FakeFeedRepository : FakeRepository<Feed>, IFeedRepository
{
    public Dictionary<Guid, Feed> Feeds => Records;

    protected override void Check(Feed feed)
    {
        if (feed.Validate().Count > 0)
            throw new InvalidOperationException("Invalid feed");
    }
}

internal sealed class FakeAwayStretchRepository() : FakeRepository<AwayStretch>(stampsDates: true), IAwayStretchRepository
{
    public Dictionary<Guid, AwayStretch> Stretches => Records;

    protected override void Check(AwayStretch stretch)
    {
        if (stretch.Validate().Count > 0)
            throw new InvalidOperationException("Invalid stretch");
    }
}

internal sealed class FakeSitterSheetRepository : FakeRepository<SitterSheet>, ISitterSheetRepository
{
    public Dictionary<Guid, SitterSheet> Sheets => Records;
}

internal sealed class FakeGrowLightRepository : FakeRepository<GrowLight>, IGrowLightRepository
{
    public Dictionary<Guid, GrowLight> Lights => Records;

    protected override void Check(GrowLight light)
    {
        if (light.Validate().Count > 0)
            throw new InvalidOperationException("Invalid grow light");
    }
}

internal sealed class FakeReceivedHistoryRepository : FakeRepository<ReceivedHistory>, IReceivedHistoryRepository
{
    public Dictionary<Guid, ReceivedHistory> Histories => Records;

    public Task<ReceivedHistory?> GetForAsync(Guid subjectId) =>
        Task.FromResult(Histories.Values.Where(h => h.SubjectId == subjectId && !h.IsDeleted).OrderByDescending(h => h.UpdatedAt).FirstOrDefault());
}

internal sealed class FakeTreatmentRecipeRepository : FakeRepository<TreatmentRecipe>, ITreatmentRecipeRepository
{
    public Dictionary<Guid, TreatmentRecipe> Recipes => Records;

    protected override void Check(TreatmentRecipe recipe)
    {
        if (recipe.Validate().Count > 0)
            throw new InvalidOperationException("Invalid recipe");
    }
}

internal sealed class FakeCareIntervalRepository : FakeRepository<CareInterval>, ICareIntervalRepository
{
    public Dictionary<Guid, CareInterval> Intervals => Records;

    protected override void Check(CareInterval interval)
    {
        if (interval.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}

internal sealed class FakePestCaseRepository : FakeRepository<PestCase>, IPestCaseRepository
{
    public Dictionary<Guid, PestCase> Cases => Records;
}

internal class FakePestTreatmentRepository : FakeRepository<PestTreatment>, IPestTreatmentRepository
{
    public Dictionary<Guid, PestTreatment> Treatments => Records;
}

internal sealed class FakePlantNameSource(PlantNameData data, params EverydayNameData[] everyday) : IPlantNameSource
{
    /// <summary>Acts like the files can't be read, e.g. offline before they were ever cached.</summary>
    public bool Fail { get; set; }

    public int Loads { get; private set; }

    /// <summary>The languages whose everyday names were read, in order.</summary>
    public List<string> EverydayLoads { get; } = [];

    /// <summary>Everyday names that can't be read, as if that one file was never cached.</summary>
    public HashSet<string> Missing { get; } = [];

    public Task<PlantNameData> LoadAsync()
    {
        if (Fail)
            throw new HttpRequestException("Offline");
        Loads++;
        return Task.FromResult(data);
    }

    public Task<EverydayNameData> LoadEverydayAsync(string language)
    {
        if (Fail || Missing.Contains(language))
            throw new HttpRequestException("Offline");
        EverydayLoads.Add(language);
        return Task.FromResult(everyday.FirstOrDefault(e => e.Language == language) ?? EverydayNameData.Empty);
    }
}

internal sealed class FakeDeviceLanguages(params string[] languages) : IDeviceLanguages
{
    public Task<IReadOnlyList<string>> GetAsync() => Task.FromResult<IReadOnlyList<string>>(languages);
}

internal sealed class FakeSettingsRepository : ISettingsRepository
{
    public Dictionary<Guid, UserSettings> Settings { get; } = [];

    /// <summary>How many times something was saved.</summary>
    public int Saves { get; private set; }

    public Task<UserSettings?> GetAsync(Guid id) =>
        Task.FromResult(Settings.TryGetValue(id, out var settings) && !settings.IsDeleted ? settings : null);

    public Task SaveAsync(UserSettings settings)
    {
        Settings[settings.Id] = settings;
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePutOffRepository : IPutOffRepository
{
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, PutOff> PutOffs { get; } = [];

    public Task<IReadOnlyList<PutOff>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PutOff>>(PutOffs.Values.Where(p => !p.IsDeleted).ToList());

    public Task<PutOff?> GetAsync(Guid id) =>
        Task.FromResult(PutOffs.TryGetValue(id, out var putOff) && !putOff.IsDeleted ? putOff : null);

    public Task SaveAsync(PutOff putOff)
    {
        if (putOff.CreatedAt == default)
            putOff.CreatedAt = clock;
        putOff.UpdatedAt = clock = clock.AddSeconds(1);
        PutOffs[putOff.Id] = putOff;
        return Task.CompletedTask;
    }
}

/// <summary>
/// The records on one device, with the change list every write there adds to, as the browser
/// database keeps them. Records are stored as JSON the way the app writes them.
/// </summary>
internal sealed class FakeSyncStore : ISyncStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Dictionary<(string Kind, Guid Id), JsonElement> Records { get; } = [];

    /// <summary>The change list: the updatedAt each record had when it was changed.</summary>
    public Dictionary<(string Kind, Guid Id), string> Pending { get; } = [];

    public SyncState? State { get; set; }

    /// <summary>Runs just before records from the server are saved, to change something mid-sync.</summary>
    public Action? BeforeSavingFromServer { get; set; }

    /// <summary>A change made on this device.</summary>
    public void Save(string kind, Entity entity)
    {
        Keep(kind, entity);
        Pending[(kind, entity.Id)] = entity.UpdatedAt.ToString("O");
    }

    /// <summary>A record that was on the device before it ever synced, so it isn't on the change list.</summary>
    public void Keep(string kind, Entity entity) =>
        Records[(kind, entity.Id)] = JsonSerializer.SerializeToElement(entity, entity.GetType(), Json);

    public T? Get<T>(string kind, Guid id) where T : Entity =>
        Records.TryGetValue((kind, id), out var data) ? data.Deserialize<T>(Json) : null;

    public Task<SyncState?> GetStateAsync() => Task.FromResult(State);

    public Task SaveStateAsync(SyncState state)
    {
        State = state;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingRecord>> GetPendingAsync(IReadOnlyCollection<string> kinds, int max) =>
        Task.FromResult<IReadOnlyList<PendingRecord>>(Pending
            .Where(p => kinds.Contains(p.Key.Kind))
            .Take(max)
            .Select(p => new PendingRecord(p.Key.Kind, p.Key.Id, Records.TryGetValue(p.Key, out var data) ? data : null, p.Value))
            .ToList());

    public Task MarkSentAsync(IReadOnlyList<PendingRecord> sent)
    {
        foreach (var record in sent)
        {
            if (Pending.TryGetValue((record.Kind, record.Id), out var mark) && mark == record.Mark)
                Pending.Remove((record.Kind, record.Id));
        }
        return Task.CompletedTask;
    }

    public Task QueueAllAsync()
    {
        foreach (var (key, data) in Records)
            Pending[key] = UpdatedAt(data)!;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JsonElement>> GetAsync(string kind, IReadOnlyCollection<Guid> ids) =>
        Task.FromResult<IReadOnlyList<JsonElement>>(ids
            .Where(id => Records.ContainsKey((kind, id)))
            .Select(id => Records[(kind, id)])
            .ToList());

    public Task SaveFromServerAsync(string kind, IReadOnlyList<ServerCopy> records)
    {
        BeforeSavingFromServer?.Invoke();
        foreach (var copy in records)
        {
            var key = (kind, copy.Data.GetProperty("id").GetGuid());
            var here = Records.TryGetValue(key, out var current) ? UpdatedAt(current) : null;
            if (here != copy.Replaces)
                continue;

            Records[key] = copy.Data;

            // The change here lost to a newer one, so there's nothing left to send
            if (Pending.TryGetValue(key, out var mark) && mark == copy.Replaces)
                Pending.Remove(key);
        }
        return Task.CompletedTask;
    }

    private static string? UpdatedAt(JsonElement data) => data.GetProperty("updatedAt").GetString();
}

/// <summary>The sync API for one person, following the same rules as the real one.</summary>
internal sealed class FakeSyncServer : ISyncServer
{
    private long lastVersion;
    private readonly Dictionary<(Guid Collection, string Kind, Guid Id), (long Version, JsonElement Data)> records = [];
    private JsonElement? settings;

    public int PageSize { get; set; } = SyncRules.BatchSize;

    /// <summary>The server's clock, for refusing records changed in the future.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public List<SyncRecord> Pushed { get; } = [];

    /// <summary>Runs as an upload arrives, for another device's change landing mid-sync.</summary>
    public Action? BeforePush { get; set; }

    /// <summary>A record sent by some other device.</summary>
    public void Add(Guid collectionId, string kind, JsonElement data) =>
        records[(collectionId, kind, data.GetProperty("id").GetGuid())] = (++lastVersion, data);

    public JsonElement? Find(Guid collectionId, string kind, Guid id) =>
        records.TryGetValue((collectionId, kind, id), out var record) ? record.Data : null;

    /// <summary>As if the server's database had been put back to an empty copy.</summary>
    public void Forget()
    {
        records.Clear();
        lastVersion = 0;
    }

    public Task<PullResponse> PullAsync(Guid collectionId, long after)
    {
        if (after > lastVersion)
            return Task.FromResult(new PullResponse([], 0, More: false, StartOver: true));

        var page = records
            .Where(r => r.Key.Collection == collectionId && r.Value.Version > after)
            .OrderBy(r => r.Value.Version)
            .Take(PageSize + 1)
            .ToList();
        var more = page.Count > PageSize;
        if (more)
            page.RemoveAt(page.Count - 1);

        return Task.FromResult(new PullResponse(
            page.Select(r => new SyncRecord(r.Key.Kind, r.Value.Data)).ToList(),
            page.Count > 0 ? page[^1].Value.Version : after,
            more));
    }

    public Task<PushResponse> PushAsync(Guid collectionId, IReadOnlyList<SyncRecord> sent)
    {
        BeforePush?.Invoke();
        BeforePush = null;

        var newer = new List<SyncRecord>();
        var refused = new List<RefusedRecord>();
        for (var i = 0; i < sent.Count; i++)
        {
            var record = sent[i];
            if (SyncRules.Problem(record.Data, Now) is { } problem)
            {
                refused.Add(new RefusedRecord(i, problem));
                continue;
            }

            Pushed.Add(record);
            RecordStamp.TryRead(record.Data, out var stamp);
            var stored = JsonSerializer.Deserialize<JsonElement>(SyncRules.AsStored(record.Data));
            var key = (collectionId, record.Kind, stamp.Id);
            if (!records.TryGetValue(key, out var current))
                records[key] = (++lastVersion, stored);
            else
            {
                RecordStamp.TryRead(current.Data, out var here);
                if (SyncRules.Replaces(stamp, stored.GetRawText(), here, current.Data.GetRawText()))
                    records[key] = (++lastVersion, stored);
                else if (SyncRules.Replaces(here, current.Data.GetRawText(), stamp, stored.GetRawText()))
                    newer.Add(new SyncRecord(record.Kind, current.Data));
            }
        }
        return Task.FromResult(new PushResponse(newer, refused));
    }

    public Task<JsonElement?> GetSettingsAsync() => Task.FromResult(settings);

    public Task<JsonElement?> PutSettingsAsync(JsonElement sent)
    {
        if (SyncRules.Problem(sent, Now) is not null)
            return Task.FromResult<JsonElement?>(null);

        RecordStamp.TryRead(sent, out var stamp);
        if (settings is not { } current
            || (RecordStamp.TryRead(current, out var here) && SyncRules.Replaces(stamp, sent.GetRawText(), here, current.GetRawText())))
            settings = sent;
        return Task.FromResult(settings);
    }
}

/// <summary>
/// The photo images on one device and its two lists, as the browser database keeps them. Ids are
/// listed in the order of their text, like IndexedDB keys.
/// </summary>
internal sealed class FakePhotoSyncStore : IPhotoSyncStore
{
    private static readonly IComparer<Guid> ByText = Comparer<Guid>.Create((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));

    public SortedSet<Guid> Uploads { get; } = new(ByText);
    public SortedSet<Guid> Downloads { get; } = new(ByText);
    public HashSet<(Guid Id, PhotoSize Size)> Images { get; } = [];

    /// <summary>A photo taken on this device: both images, and on the list to send.</summary>
    public Guid Take(Guid? id = null)
    {
        var photo = id ?? Guid.NewGuid();
        Images.Add((photo, PhotoSize.Full));
        Images.Add((photo, PhotoSize.Thumbnail));
        Uploads.Add(photo);
        return photo;
    }

    public Task<IReadOnlyList<Guid>> GetUploadsAsync(Guid? after, int max) => Task.FromResult(Page(Uploads, after, max));

    public Task RemoveUploadsAsync(IReadOnlyCollection<Guid> ids)
    {
        Uploads.ExceptWith(ids);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetDownloadsAsync(Guid? after, int max) => Task.FromResult(Page(Downloads, after, max));

    public Task RemoveDownloadsAsync(IReadOnlyCollection<Guid> ids)
    {
        Downloads.ExceptWith(ids);
        return Task.CompletedTask;
    }

    private static IReadOnlyList<Guid> Page(SortedSet<Guid> list, Guid? after, int max) =>
        list.Where(id => after is null || ByText.Compare(id, after.Value) > 0).Take(max).ToList();
}

/// <summary>
/// The photo images of one collection on the server, and which photo records it has. Each image
/// counts as <see cref="ImageBytes"/> towards <see cref="Limit"/>.
/// </summary>
internal sealed class FakePhotoServer(FakePhotoSyncStore device) : IPhotoServer
{
    public const int ImageBytes = 100;

    public HashSet<(Guid Id, PhotoSize Size)> Images { get; } = [];
    public HashSet<Guid> Records { get; } = [];
    public HashSet<Guid> Deleted { get; } = [];
    public long Limit { get; set; } = long.MaxValue;
    public List<(Guid Id, PhotoSize Size)> Sent { get; } = [];
    public List<(Guid Id, PhotoSize Size)> Fetched { get; } = [];

    /// <summary>A photo from another device, with its record and the images it has sent.</summary>
    public Guid Add(bool full = true, bool thumbnail = true)
    {
        var id = Guid.NewGuid();
        Records.Add(id);
        if (full) Images.Add((id, PhotoSize.Full));
        if (thumbnail) Images.Add((id, PhotoSize.Thumbnail));
        return id;
    }

    public Task<IReadOnlyList<StoredPhoto>> GetStoredAsync(Guid collectionId, IReadOnlyList<Guid> ids)
    {
        Assert.True(ids.Count <= PhotoRules.BatchSize);
        IReadOnlyList<StoredPhoto> stored = ids
            .Where(id => Images.Contains((id, PhotoSize.Full)) || Images.Contains((id, PhotoSize.Thumbnail)))
            .Select(id => new StoredPhoto(id, Images.Contains((id, PhotoSize.Full)), Images.Contains((id, PhotoSize.Thumbnail))))
            .ToList();
        return Task.FromResult(stored);
    }

    public Task<UploadOutcome> UploadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        UploadOutcome outcome;
        if (!device.Images.Contains((photoId, size)))
            outcome = UploadOutcome.NotHere;
        else if (Deleted.Contains(photoId))
            outcome = UploadOutcome.Gone;
        else if (!Records.Contains(photoId))
            outcome = UploadOutcome.NotYet;
        else if ((long)(Images.Count + 1) * ImageBytes > Limit)
            outcome = UploadOutcome.Full;
        else
        {
            Images.Add((photoId, size));
            Sent.Add((photoId, size));
            outcome = UploadOutcome.Uploaded;
        }

        return Task.FromResult(outcome);
    }

    public Task<bool> DownloadAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        if (!Images.Contains((photoId, size)))
            return Task.FromResult(false);

        device.Images.Add((photoId, size));
        Fetched.Add((photoId, size));
        return Task.FromResult(true);
    }
}

/// <summary>The share link endpoints for one collection, keeping its links in memory.</summary>
internal sealed class FakeShareLinkServer : IShareLinkServer
{
    private readonly List<ShareLinkInfo> links = [];

    /// <summary>When set, every call fails with this problem.</summary>
    public string? Problem { get; set; }

    /// <summary>Makes a failure look like the server couldn't be reached.</summary>
    public bool Offline { get; set; }

    public List<UpdateShareLinkRequest> Updates { get; } = [];

    public List<CreateShareLinkRequest> Creates { get; } = [];

    /// <summary>A link made on another device.</summary>
    public ShareLinkInfo Add(SubjectType type, Guid subjectId)
    {
        var link = new ShareLinkInfo(Guid.NewGuid(), type, subjectId, $"https://share.example/{links.Count}", true, null, null, [], DateTimeOffset.UnixEpoch);
        links.Add(link);
        return link;
    }

    private ShareOutcome<T>? Failed<T>() =>
        Problem is null ? null : ShareOutcome<T>.Failure(Problem, Offline);

    public Task<ShareOutcome<IReadOnlyList<ShareLinkInfo>>> ListAsync(Guid collectionId) =>
        Task.FromResult(Failed<IReadOnlyList<ShareLinkInfo>>() ?? ShareOutcome<IReadOnlyList<ShareLinkInfo>>.Success(links.ToList()));

    public Task<ShareOutcome<ShareLinkInfo>> CreateAsync(Guid collectionId, CreateShareLinkRequest request)
    {
        if (Failed<ShareLinkInfo>() is { } failed)
            return Task.FromResult(failed);

        Creates.Add(request);
        var link = links.FirstOrDefault(l => l.SubjectId == request.SubjectId) ?? Add(request.SubjectType, request.SubjectId);
        return Task.FromResult(ShareOutcome<ShareLinkInfo>.Success(link));
    }

    public Task<ShareOutcome<ShareLinkInfo>> UpdateAsync(Guid collectionId, Guid id, UpdateShareLinkRequest request)
    {
        if (Failed<ShareLinkInfo>() is { } failed)
            return Task.FromResult(failed);

        Updates.Add(request);
        var index = links.FindIndex(l => l.Id == id);
        links[index] = links[index] with { ShowNotes = request.ShowNotes, Name = request.Name, Line = request.Line, LeftOutPhotoIds = request.LeftOutPhotoIds };
        return Task.FromResult(ShareOutcome<ShareLinkInfo>.Success(links[index]));
    }

    public Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId, Guid id)
    {
        if (Failed<bool>() is { } failed)
            return Task.FromResult(failed);

        links.RemoveAll(l => l.Id == id);
        return Task.FromResult(ShareOutcome<bool>.Success(true));
    }
}

/// <summary>The public page endpoints for one collection, keeping its page in memory.</summary>
internal sealed class FakePublicPageServer : IPublicPageServer
{
    private int made;

    /// <summary>The page that is on, or null.</summary>
    public PublicPageInfo? Page { get; set; }

    /// <summary>When set, every call fails with this status.</summary>
    public int? FailWith { get; set; }

    /// <summary>Makes a failure look like the server couldn't be reached.</summary>
    public bool Offline { get; set; }

    public List<UpdatePublicPageRequest> Updates { get; } = [];

    public List<SetPublicPageNameRequest> Names { get; } = [];

    public List<TurnOnPublicPageRequest> TurnedOn { get; } = [];

    private ShareOutcome<T>? Failed<T>() =>
        FailWith is { } status ? ShareOutcome<T>.Failure($"Status {status}", Offline, Offline ? 0 : status) : null;

    /// <summary>A page made on another device.</summary>
    public PublicPageInfo Add() => Page = new PublicPageInfo(
        Guid.NewGuid(), $"https://share.example/token{made++}", null, null, null, null,
        PublicPageLayout.Shelves, PublicPageOrder.Newest, true, false, false, false, false, "en", DateTimeOffset.UnixEpoch);

    public Task<ShareOutcome<PublicPageInfo?>> GetAsync(Guid collectionId) =>
        Task.FromResult(Failed<PublicPageInfo?>() ?? ShareOutcome<PublicPageInfo?>.Success(Page));

    public Task<ShareOutcome<PublicPageInfo>> TurnOnAsync(Guid collectionId, TurnOnPublicPageRequest request)
    {
        if (Failed<PublicPageInfo>() is { } failed)
            return Task.FromResult(failed);

        TurnedOn.Add(request);
        return Task.FromResult(ShareOutcome<PublicPageInfo>.Success(Page ?? (Page = Add() with { Language = request.Language ?? "en" })));
    }

    public Task<ShareOutcome<PublicPageInfo>> UpdateAsync(Guid collectionId, UpdatePublicPageRequest request)
    {
        if (Failed<PublicPageInfo>() is { } failed)
            return Task.FromResult(failed);

        Updates.Add(request);
        Page = Page! with
        {
            Title = request.Title,
            Line = request.Line,
            Layout = request.Layout,
            Order = request.Order,
            ShowPropagations = request.ShowPropagations,
            ShowSwap = request.ShowSwap,
            ShowWishlist = request.ShowWishlist,
            ShowStories = request.ShowStories,
            Searchable = request.Searchable,
            Language = request.Language ?? Page.Language
        };
        return Task.FromResult(ShareOutcome<PublicPageInfo>.Success(Page));
    }

    public Task<ShareOutcome<PublicPageInfo>> SetNameAsync(Guid collectionId, SetPublicPageNameRequest request)
    {
        if (Failed<PublicPageInfo>() is { } failed)
            return Task.FromResult(failed);

        Names.Add(request);
        Page = Page! with { Name = request.Name, NamedUrl = request.Name is null ? null : $"https://share.example/@{request.Name}" };
        return Task.FromResult(ShareOutcome<PublicPageInfo>.Success(Page));
    }

    public Task<ShareOutcome<bool>> TurnOffAsync(Guid collectionId)
    {
        if (Failed<bool>() is { } failed)
            return Task.FromResult(failed);

        Page = null;
        return Task.FromResult(ShareOutcome<bool>.Success(true));
    }
}

internal sealed class FakeFloweringMarkRepository() : FakeRepository<FloweringMark>(stampsDates: true), IFloweringMarkRepository
{
    public Dictionary<Guid, FloweringMark> Marks => Records;

    protected override void Check(FloweringMark mark)
    {
        if (mark.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid mark");
    }
}

internal sealed class FakeLeafRepository() : FakeRepository<Leaf>(stampsDates: true), ILeafRepository
{
    public Dictionary<Guid, Leaf> Leaves => Records;

    protected override void Check(Leaf leaf)
    {
        if (leaf.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid leaf");
    }
}

internal sealed class FakeProblemRepository() : FakeRepository<Problem>(stampsDates: true), IProblemRepository
{
    public Dictionary<Guid, Problem> Problems => Records;

    protected override void Check(Problem problem)
    {
        if (problem.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid problem");
    }
}

internal sealed class FakeProblemUpdateRepository() : FakeRepository<ProblemUpdate>(stampsDates: true), IProblemUpdateRepository
{
    public Dictionary<Guid, ProblemUpdate> Updates => Records;

    protected override void Check(ProblemUpdate update)
    {
        if (update.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid update");
    }
}

internal sealed class FakeWishRepository() : FakeRepository<Wish>(stampsDates: true), IWishRepository
{
    public Dictionary<Guid, Wish> Wishes => Records;

    protected override void Check(Wish wish)
    {
        if (wish.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid wish");
    }
}

internal sealed class FakeOrderRepository() : FakeRepository<Order>(stampsDates: true), IOrderRepository
{
    public Dictionary<Guid, Order> Orders => Records;

    protected override void Check(Order order)
    {
        if (order.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid order");
    }
}

internal sealed class FakeOrderItemRepository : FakeRepository<OrderItem>, IOrderItemRepository
{
    // Each item is a second newer than the last, so "in the order they were made" can be told apart
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, OrderItem> Items => Records;

    public override Task SaveAsync(OrderItem item)
    {
        if (item.Validate().Count > 0)
            throw new InvalidOperationException("Invalid order item");
        if (item.CreatedAt == default)
            item.CreatedAt = clock = clock.AddSeconds(1);
        item.UpdatedAt = clock.AddSeconds(1);
        Items[item.Id] = item;
        return Task.CompletedTask;
    }

    public override Task DeleteAsync(Guid id)
    {
        if (Items.TryGetValue(id, out var item))
            item.DeletedAt = item.UpdatedAt = clock = clock.AddSeconds(1);
        return Task.CompletedTask;
    }
}

internal sealed class FakePhotoMarkRepository : IPhotoMarkRepository
{
    // Each mark is a second newer than the last, so "the one made first" can be told apart
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, PhotoMark> Marks { get; } = [];

    public Task<IReadOnlyList<PhotoMark>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PhotoMark>>(Marks.Values.Where(m => !m.IsDeleted).ToList());

    public Task<IReadOnlyList<PhotoMark>> GetForPhotoAsync(Guid photoId) =>
        Task.FromResult<IReadOnlyList<PhotoMark>>(Marks.Values.Where(m => !m.IsDeleted && m.PhotoId == photoId).ToList());

    public Task<IReadOnlyList<PhotoMark>> GetForSubjectAsync(Guid subjectId) =>
        Task.FromResult<IReadOnlyList<PhotoMark>>(Marks.Values.Where(m => !m.IsDeleted && m.SubjectId == subjectId).ToList());

    public Task SaveAsync(PhotoMark mark)
    {
        if (mark.Validate().Count > 0)
            throw new InvalidOperationException("Invalid mark");
        if (mark.CreatedAt == default)
            mark.CreatedAt = clock = clock.AddSeconds(1);
        mark.UpdatedAt = clock.AddSeconds(1);
        Marks[mark.Id] = mark;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Marks.TryGetValue(id, out var mark))
            mark.DeletedAt = mark.UpdatedAt = clock = clock.AddSeconds(1);
        return Task.CompletedTask;
    }
}


internal sealed class FakePhotoMarkupRepository : IPhotoMarkupRepository
{
    // Each mark is a second newer than the last, so "the one made first" can be told apart
    private DateTimeOffset clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, PhotoMarkup> Marks { get; } = [];

    public Task<IReadOnlyList<PhotoMarkup>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PhotoMarkup>>(Marks.Values.Where(m => !m.IsDeleted).ToList());

    public Task<IReadOnlyList<PhotoMarkup>> GetForPhotoAsync(Guid photoId) =>
        Task.FromResult<IReadOnlyList<PhotoMarkup>>(Marks.Values.Where(m => !m.IsDeleted && m.PhotoId == photoId).ToList());

    public Task SaveAsync(PhotoMarkup markup)
    {
        if (markup.Validate().Count > 0)
            throw new InvalidOperationException("Invalid mark");
        if (markup.CreatedAt == default)
            markup.CreatedAt = clock = clock.AddSeconds(1);
        markup.UpdatedAt = clock.AddSeconds(1);
        Marks[markup.Id] = markup;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Marks.TryGetValue(id, out var markup))
            markup.DeletedAt = markup.UpdatedAt = clock = clock.AddSeconds(1);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSupplyRepository() : FakeRepository<Supply>(stampsDates: true), ISupplyRepository
{
    public Dictionary<Guid, Supply> Supplies => Records;

    protected override void Check(Supply supply)
    {
        if (supply.Validate().Count > 0)
            throw new InvalidOperationException("Invalid supply");
    }
}

internal sealed class FakeSupplyEntryRepository() : FakeRepository<SupplyEntry>(stampsDates: true), ISupplyEntryRepository
{
    public Dictionary<Guid, SupplyEntry> Entries => Records;

    protected override void Check(SupplyEntry entry)
    {
        if (entry.Validate().Count > 0)
            throw new InvalidOperationException("Invalid supply entry");
    }
}

internal sealed class FakeShoppingItemRepository() : FakeRepository<ShoppingItem>(stampsDates: true), IShoppingItemRepository
{
    public Dictionary<Guid, ShoppingItem> Items => Records;

    protected override void Check(ShoppingItem item)
    {
        if (item.Validate().Count > 0)
            throw new InvalidOperationException("Invalid shopping item");
    }
}
