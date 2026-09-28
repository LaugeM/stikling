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
using Stikling.Core.SoilMixes;
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
