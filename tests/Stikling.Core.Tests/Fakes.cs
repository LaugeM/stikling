using System.Text.Json;
using Stikling.Core.Care;
using Stikling.Core.Feeds;
using Stikling.Core.Models;
using Stikling.Core.Names;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Pots;
using Stikling.Core.Products;
using Stikling.Core.Propagations;
using Stikling.Core.Rooms;
using Stikling.Core.Settings;
using Stikling.Core.SoilMixes;
using Stikling.Core.Sync;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

/// <summary>In-memory repositories for testing services without a browser database.</summary>
internal sealed class FakePlantRepository : IPlantRepository
{
    public Dictionary<Guid, Plant> Plants { get; } = [];

    public Task<IReadOnlyList<Plant>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Plant>>(Plants.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Plant?> GetAsync(Guid id) =>
        Task.FromResult(Plants.TryGetValue(id, out var p) && !p.IsDeleted ? p : null);

    public Task SaveAsync(Plant plant)
    {
        // DateOnly.MaxValue: the fake has no clock, and the future-date rule is tested on the model itself
        if (plant.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid plant");
        Plants[plant.Id] = plant;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Plants.TryGetValue(id, out var p))
            p.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakePropagationRepository : IPropagationRepository
{
    public Dictionary<Guid, Propagation> Propagations { get; } = [];

    public Task<IReadOnlyList<Propagation>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Propagation>>(Propagations.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Propagation?> GetAsync(Guid id) =>
        Task.FromResult(Propagations.TryGetValue(id, out var p) && !p.IsDeleted ? p : null);

    public Task SaveAsync(Propagation propagation)
    {
        if (propagation.Validate(DateOnly.MaxValue).Count > 0)
            throw new InvalidOperationException("Invalid propagation");
        Propagations[propagation.Id] = propagation;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Propagations.TryGetValue(id, out var p))
            p.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
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

internal sealed class FakeTimelineRepository : ITimelineRepository
{
    public List<TimelineEntry> Entries { get; } = [];

    public Task<IReadOnlyList<TimelineEntry>> GetForAsync(Guid subjectId) =>
        Task.FromResult(TimelineOrder.NewestFirst(Entries.Where(e => e.SubjectId == subjectId)));

    public Task<IReadOnlyDictionary<Guid, TimelineEntry>> GetLatestPerSubjectAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, TimelineEntry>>(
            TimelineOrder.NewestFirst(Entries).GroupBy(e => e.SubjectId).ToDictionary(g => g.Key, g => g.First()));

    public Task<IReadOnlyList<TimelineEntry>> GetRecentAsync(int count, IReadOnlySet<Guid> subjectIds) =>
        Task.FromResult(TodayBoard.RecentActivity(Entries, subjectIds, count));

    public Task AddAsync(TimelineEntry entry)
    {
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

    public Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPerSubjectAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, Photo>>(
            Photos.Values.Where(p => !p.IsDeleted)
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

internal sealed class FakePotRepository : IPotRepository
{
    public Dictionary<Guid, Pot> Pots { get; } = [];

    public Task<IReadOnlyList<Pot>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Pot>>(Pots.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Pot?> GetAsync(Guid id) =>
        Task.FromResult(Pots.TryGetValue(id, out var pot) && !pot.IsDeleted ? pot : null);

    public Task SaveAsync(Pot pot)
    {
        if (pot.Validate().Count > 0)
            throw new InvalidOperationException("Invalid pot");
        Pots[pot.Id] = pot;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Pots.TryGetValue(id, out var pot))
            pot.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
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

internal sealed class FakeSoilMixRepository : ISoilMixRepository
{
    public Dictionary<Guid, SoilMix> Mixes { get; } = [];

    public Task<IReadOnlyList<SoilMix>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<SoilMix>>(Mixes.Values.Where(m => !m.IsDeleted).ToList());

    public Task<SoilMix?> GetAsync(Guid id) =>
        Task.FromResult(Mixes.TryGetValue(id, out var mix) && !mix.IsDeleted ? mix : null);

    public Task SaveAsync(SoilMix mix)
    {
        if (mix.Validate().Count > 0)
            throw new InvalidOperationException("Invalid mix");
        Mixes[mix.Id] = mix;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Mixes.TryGetValue(id, out var mix))
            mix.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeProductRepository : IProductRepository
{
    public Dictionary<Guid, Product> Products { get; } = [];

    public Task<IReadOnlyList<Product>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Product>>(Products.Values.Where(p => !p.IsDeleted).ToList());

    public Task<Product?> GetAsync(Guid id) =>
        Task.FromResult(Products.TryGetValue(id, out var product) && !product.IsDeleted ? product : null);

    public Task SaveAsync(Product product)
    {
        if (product.Validate().Count > 0)
            throw new InvalidOperationException("Invalid product");
        Products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Products.TryGetValue(id, out var product))
            product.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeFeedRepository : IFeedRepository
{
    public Dictionary<Guid, Feed> Feeds { get; } = [];

    public Task<IReadOnlyList<Feed>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Feed>>(Feeds.Values.Where(f => !f.IsDeleted).ToList());

    public Task<Feed?> GetAsync(Guid id) =>
        Task.FromResult(Feeds.TryGetValue(id, out var feed) && !feed.IsDeleted ? feed : null);

    public Task SaveAsync(Feed feed)
    {
        if (feed.Validate().Count > 0)
            throw new InvalidOperationException("Invalid feed");
        Feeds[feed.Id] = feed;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Feeds.TryGetValue(id, out var feed))
            feed.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTreatmentRecipeRepository : ITreatmentRecipeRepository
{
    public Dictionary<Guid, TreatmentRecipe> Recipes { get; } = [];

    public Task<IReadOnlyList<TreatmentRecipe>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<TreatmentRecipe>>(Recipes.Values.Where(r => !r.IsDeleted).ToList());

    public Task<TreatmentRecipe?> GetAsync(Guid id) =>
        Task.FromResult(Recipes.TryGetValue(id, out var recipe) && !recipe.IsDeleted ? recipe : null);

    public Task SaveAsync(TreatmentRecipe recipe)
    {
        if (recipe.Validate().Count > 0)
            throw new InvalidOperationException("Invalid recipe");
        Recipes[recipe.Id] = recipe;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Recipes.TryGetValue(id, out var recipe))
            recipe.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakePestCaseRepository : IPestCaseRepository
{
    public Dictionary<Guid, PestCase> Cases { get; } = [];

    public Task<IReadOnlyList<PestCase>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<PestCase>>(Cases.Values.Where(c => !c.IsDeleted).ToList());

    public Task<PestCase?> GetAsync(Guid id) =>
        Task.FromResult(Cases.TryGetValue(id, out var item) && !item.IsDeleted ? item : null);

    public Task SaveAsync(PestCase item)
    {
        Cases[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        if (Cases.TryGetValue(id, out var item))
            item.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }
}

internal sealed class FakePlantNameSource(PlantNameData data) : IPlantNameSource
{
    /// <summary>Acts like the file can't be read, e.g. offline before it was ever cached.</summary>
    public bool Fail { get; set; }

    public int Loads { get; private set; }

    public Task<PlantNameData> LoadAsync()
    {
        if (Fail)
            throw new HttpRequestException("Offline");
        Loads++;
        return Task.FromResult(data);
    }
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
            var key = (collectionId, record.Kind, stamp.Id);
            if (!records.TryGetValue(key, out var current))
                records[key] = (++lastVersion, record.Data);
            else
            {
                RecordStamp.TryRead(current.Data, out var here);
                if (SyncRules.Replaces(stamp, record.Data.GetRawText(), here, current.Data.GetRawText()))
                    records[key] = (++lastVersion, record.Data);
                else if (SyncRules.Replaces(here, current.Data.GetRawText(), stamp, record.Data.GetRawText()))
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
