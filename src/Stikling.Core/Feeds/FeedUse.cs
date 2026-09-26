using Stikling.Core.Care;
using Stikling.Core.Models;

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

/// <summary>The feeds, with their use read from the care log.</summary>
public sealed class FeedService(IFeedRepository feeds, ICareLogRepository logs)
{
    public async Task<IReadOnlyList<FeedUse>> GetAllAsync() =>
        FeedUse.List(await feeds.GetAllAsync(), await logs.GetAllAsync());
}
