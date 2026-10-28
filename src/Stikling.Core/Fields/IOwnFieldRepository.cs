using Stikling.Core.Models;

namespace Stikling.Core.Fields;

public interface IOwnFieldRepository
{
    /// <summary>The fields that aren't deleted.</summary>
    Task<IReadOnlyList<OwnField>> GetAllAsync();

    /// <summary>Every field stored, deleted ones too, so adding a name that was removed can bring it back.</summary>
    Task<IReadOnlyList<OwnField>> GetStoredAsync();

    Task<OwnField?> GetAsync(Guid id);

    /// <summary>Adds or updates a field. Throws if it isn't valid.</summary>
    Task SaveAsync(OwnField field);

    /// <summary>Soft-deletes a field. Plants keep their values for it.</summary>
    Task DeleteAsync(Guid id);
}
