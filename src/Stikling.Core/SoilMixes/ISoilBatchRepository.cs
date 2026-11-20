using Stikling.Core.Models;

namespace Stikling.Core.SoilMixes;

/// <summary>Plants in a deleted batch keep the link, the way a deleted mix works.</summary>
public interface ISoilBatchRepository : IRepository<SoilBatch>
{
}

public interface IBatchMixingRepository : IRepository<BatchMixing>
{
}
