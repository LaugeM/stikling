using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>Care entries keep their own copy of a deleted kind's name.</summary>
public interface IOwnCareKindRepository : IRepository<OwnCareKind>
{
    /// <summary>Every kind stored, deleted ones too, so adding a name that was removed can bring it back.</summary>
    Task<IReadOnlyList<OwnCareKind>> GetStoredAsync();
}
