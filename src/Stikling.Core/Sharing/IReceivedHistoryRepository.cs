using Stikling.Core.Models;

namespace Stikling.Core.Sharing;

public interface IReceivedHistoryRepository
{
    /// <summary>The newest history of this plant or propagation that isn't deleted, or null.</summary>
    Task<ReceivedHistory?> GetForAsync(Guid subjectId);

    Task<ReceivedHistory?> GetAsync(Guid id);

    /// <summary>Adds or updates a history.</summary>
    Task SaveAsync(ReceivedHistory history);

    /// <summary>Soft-deletes a history.</summary>
    Task DeleteAsync(Guid id);
}
