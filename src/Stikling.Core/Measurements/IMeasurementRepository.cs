using Stikling.Core.Models;

namespace Stikling.Core.Measurements;

public interface IMeasurementRepository : IRepository<Measurement>
{
    /// <summary>Every measurement stored, deleted ones too.</summary>
    Task<IReadOnlyList<Measurement>> GetStoredAsync();
}

/// <summary>Measurements keep their own copy of a deleted kind's name.</summary>
public interface IOwnMeasureKindRepository : IRepository<OwnMeasureKind>
{
    /// <summary>Every kind stored, deleted ones too, so adding a name that was removed can bring it back.</summary>
    Task<IReadOnlyList<OwnMeasureKind>> GetStoredAsync();
}
