using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Feeds;

/// <summary>A feed, how many plants have had it, and when it was last given.</summary>
public sealed record FeedUse(Feed Feed, int Plants, DateOnly? LastUsed)
{
    /// <summary>Every feed with its use counted from the care log, by plants rather than entries.</summary>
    public static IReadOnlyList<FeedUse> List(IEnumerable<Feed> feeds, IEnumerable<CareLog> logs)
    {
        var given = logs
            .Where(l => !l.IsDeleted && l.FeedId is not null)
            .Select(l => (l.FeedId, l.PlantId, l.OccurredOn))
            .ToList();

        return feeds
            .Where(f => !f.IsDeleted)
            .Select(feed =>
            {
                var used = given.Where(g => g.FeedId == feed.Id).ToList();
                return new FeedUse(
                    feed,
                    used.Select(g => g.PlantId).Distinct().Count(),
                    used.Count == 0 ? null : used.Max(g => g.OccurredOn));
            })
            .OrderBy(use => use.Feed.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>A plant that got a feed, with the days it was given, newest first.</summary>
public sealed record FeedPlant(Plant Plant, IReadOnlyList<DateOnly> Dates, int More)
{
    /// <summary>How many of the newest days are kept on each plant.</summary>
    public const int MaxDates = 5;

    /// <summary>
    /// The plants that got a feed, most recently fed first. Deleted entries and deleted plants
    /// are left out. Plants that died or left stay in, since the point is to see if a mix worked.
    /// </summary>
    public static IReadOnlyList<FeedPlant> For(Guid feedId, IEnumerable<CareLog> logs, IEnumerable<Plant> plants)
    {
        var byId = plants.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);

        return logs
            .Where(l => !l.IsDeleted && l.FeedId == feedId && byId.ContainsKey(l.PlantId))
            .GroupBy(l => l.PlantId)
            .Select(g =>
            {
                var days = g.Select(l => l.OccurredOn).OrderByDescending(d => d).ToList();
                return new FeedPlant(byId[g.Key], days.Take(MaxDates).ToList(), Math.Max(0, days.Count - MaxDates));
            })
            .OrderByDescending(f => f.Dates[0])
            .ThenBy(f => f.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>The feeds, with their use read from the care log.</summary>
public sealed class FeedService(IFeedRepository feeds, ICareLogRepository logs, IPlantRepository plants)
{
    public async Task<IReadOnlyList<FeedUse>> GetAllAsync() =>
        FeedUse.List(await feeds.GetAllAsync(), await logs.GetAllAsync());

    /// <summary>The plants that got one feed, and on which days.</summary>
    public async Task<IReadOnlyList<FeedPlant>> GetPlantsAsync(Guid feedId) =>
        FeedPlant.For(feedId, await logs.GetAllAsync(), await plants.GetAllAsync());
}
