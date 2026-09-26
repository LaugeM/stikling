using Stikling.Core.Models;

namespace Stikling.Core.Feeds;

public interface IFeedRepository
{
    Task<IReadOnlyList<Feed>> GetAllAsync();

    Task<Feed?> GetAsync(Guid id);

    Task SaveAsync(Feed feed);

    /// <summary>Soft-deletes the feed. Care entries keep their own copy of its name and doses.</summary>
    Task DeleteAsync(Guid id);
}
