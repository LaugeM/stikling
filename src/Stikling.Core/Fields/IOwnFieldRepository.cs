using Stikling.Core.Models;

namespace Stikling.Core.Fields;

/// <summary>Plants keep their values for a deleted field.</summary>
public interface IOwnFieldRepository : IRepository<OwnField>
{
    /// <summary>Every field stored, deleted ones too, so adding a name that was removed can bring it back.</summary>
    Task<IReadOnlyList<OwnField>> GetStoredAsync();
}
