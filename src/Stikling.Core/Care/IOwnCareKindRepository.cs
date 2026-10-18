using Stikling.Core.Models;

namespace Stikling.Core.Care;

public interface IOwnCareKindRepository
{
    /// <summary>The kinds that aren't deleted.</summary>
    Task<IReadOnlyList<OwnCareKind>> GetAllAsync();

    /// <summary>Every kind stored, deleted ones too, so adding a name that was removed can bring it back.</summary>
    Task<IReadOnlyList<OwnCareKind>> GetStoredAsync();

    Task<OwnCareKind?> GetAsync(Guid id);

    /// <summary>Adds or updates a kind. Throws if it isn't valid.</summary>
    Task SaveAsync(OwnCareKind kind);

    /// <summary>Soft-deletes a kind. Care entries keep their own copy of its name.</summary>
    Task DeleteAsync(Guid id);
}
