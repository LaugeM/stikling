using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tidy;

/// <summary>Two plants that may be the same one entered twice. The key is what "Not the same" puts off.</summary>
public sealed record SamePair(Plant First, Plant Second, string Key);

/// <summary>What the Tidy up page lists. Every list is empty when there is nothing to tidy in that part.</summary>
public sealed record TidyReport(
    IReadOnlyList<Plant> NoGenus,
    IReadOnlyList<Plant> NoPhoto,
    IReadOnlyList<Plant> NoRoom,
    IReadOnlyList<Propagation> ParentMissing,
    IReadOnlyList<SamePair> MaybeSame,
    IReadOnlyList<string> UnusedTags,
    IReadOnlyList<Product> UnusedProducts,
    IReadOnlyList<SoilMix> UnusedMixes)
{
    public bool IsEmpty =>
        NoGenus.Count == 0 && NoPhoto.Count == 0 && NoRoom.Count == 0 && ParentMissing.Count == 0
        && MaybeSame.Count == 0 && UnusedTags.Count == 0 && UnusedProducts.Count == 0 && UnusedMixes.Count == 0;
}

/// <summary>Everything the checks read, so they can be worked out without the stores.</summary>
public sealed record TidyInput(
    IReadOnlyList<Plant> Plants,
    IReadOnlyList<Propagation> Propagations,
    IReadOnlyList<Photo> PlantPhotos,
    Places Places,
    IReadOnlyList<Product> Products,
    IReadOnlyList<CareLog> CareLogs,
    IReadOnlyList<PropagationCareLog> PropagationCareLogs,
    IReadOnlyList<Feed> Feeds,
    IReadOnlyList<Supply> Supplies,
    IReadOnlyList<SoilMix> Mixes,
    IReadOnlyList<PutOff> PutOffs);

/// <summary>The checks behind the Tidy up page. Nothing here changes anything.</summary>
public static class TidyChecks
{
    /// <summary>The key that puts off "may be the same" for a pair, the same whichever plant is named first.</summary>
    public static string SameKey(Guid a, Guid b)
    {
        var (low, high) = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
        return $"tidy:same:{low:D}:{high:D}";
    }

    /// <summary>A plant that is in the collection's own list: not deleted, not gone, not one that lives with someone else.</summary>
    private static bool IsMine(Plant p) => !p.IsDeleted && p.Status == PlantStatus.Active && !p.LivesElsewhere;

    public static TidyReport Build(TidyInput input)
    {
        var mine = input.Plants.Where(IsMine).OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

        var withPhoto = input.PlantPhotos.Where(p => !p.IsDeleted).Select(p => p.SubjectId).ToHashSet();

        var parentIds = input.Plants.Where(p => !p.IsDeleted).Select(p => p.Id).ToHashSet();
        var parentMissing = input.Propagations
            .Where(p => !p.IsDeleted && p.IsActive && p.ParentPlantId is { } parent && !parentIds.Contains(parent))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new TidyReport(
            [.. mine.Where(p => string.IsNullOrWhiteSpace(p.Genus))],
            [.. mine.Where(p => !withPhoto.Contains(p.Id))],
            [.. mine.Where(p => input.Places.Find(p.PlaceId) is null)],
            parentMissing,
            MaybeSame(mine, input.Places, input.PutOffs),
            UnusedTags(input.Plants, mine),
            UnusedProducts(input),
            UnusedMixes(input));
    }

    private static IReadOnlyList<SamePair> MaybeSame(IReadOnlyList<Plant> mine, Places places, IReadOnlyList<PutOff> putOffs)
    {
        var dismissed = putOffs.Where(p => !p.IsDeleted && p.Until == DateOnly.MaxValue).Select(p => p.Key).ToHashSet();

        var pairs = new List<SamePair>();
        var groups = mine
            .Select(p => (Plant: p, Room: places.RoomOf(p.PlaceId)?.Id, Key: SameName(p)))
            .Where(x => x.Room is not null && x.Key is not null)
            .GroupBy(x => (x.Room, x.Key));

        foreach (var group in groups)
        {
            var plants = group.Select(x => x.Plant).OrderBy(p => p.Id).ToList();
            for (var i = 0; i < plants.Count; i++)
            {
                for (var j = i + 1; j < plants.Count; j++)
                {
                    var key = SameKey(plants[i].Id, plants[j].Id);
                    if (!dismissed.Contains(key))
                        pairs.Add(new SamePair(plants[i], plants[j], key));
                }
            }
        }

        return [.. pairs.OrderBy(p => p.First.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Second.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    // The nickname when it has one, otherwise the botanical name. Null when there is nothing to compare, so two plants
    // that were never named don't look like the same one.
    private static string? SameName(Plant p)
    {
        if (!string.IsNullOrWhiteSpace(p.Nickname))
            return "n:" + p.Nickname.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(p.Genus))
            return null;
        string Part(string? s) => (s ?? "").Trim().ToLowerInvariant();
        return $"b:{Part(p.Genus)}|{Part(p.Species)}|{Part(p.Cultivar)}";
    }

    // Tags that are on plants that left or died, but on none still in the collection
    private static IReadOnlyList<string> UnusedTags(IReadOnlyList<Plant> all, IReadOnlyList<Plant> mine) =>
        [.. PlantTags.InUse(all).Where(t => !mine.Any(p => PlantTags.Has(p, t.Name))).Select(t => t.Name)];

    /// <summary>The ids of the products a care log, a feed or a supply points at.</summary>
    public static HashSet<Guid> ProductsInUse(TidyInput input)
    {
        var ids = new HashSet<Guid>();
        foreach (var log in input.CareLogs.Where(l => !l.IsDeleted))
            ids.UnionWith(log.Products.Select(d => d.ProductId).OfType<Guid>());
        foreach (var log in input.PropagationCareLogs.Where(l => !l.IsDeleted))
            ids.UnionWith(log.Products.Select(d => d.ProductId).OfType<Guid>());
        foreach (var feed in input.Feeds.Where(f => !f.IsDeleted))
            ids.UnionWith(feed.Products.Select(d => d.ProductId).OfType<Guid>());
        foreach (var supply in input.Supplies.Where(s => !s.IsDeleted))
        {
            if (supply.ProductId is { } id)
                ids.Add(id);
        }
        return ids;
    }

    private static IReadOnlyList<Product> UnusedProducts(TidyInput input)
    {
        var used = ProductsInUse(input);
        return [.. input.Products.Where(p => !p.IsDeleted && !used.Contains(p.Id))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static IReadOnlyList<SoilMix> UnusedMixes(TidyInput input)
    {
        var used = input.Plants.Where(p => !p.IsDeleted).Select(p => p.SoilMixId)
            .Concat(input.Propagations.Where(p => !p.IsDeleted).Select(p => p.SoilMixId))
            .OfType<Guid>().ToHashSet();
        return [.. input.Mixes.Where(m => !m.IsDeleted && !used.Contains(m.Id))
            .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
