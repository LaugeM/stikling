using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>Deleting an interval doesn't change anything logged.</summary>
public interface ICareIntervalRepository : IRepository<CareInterval>
{
}
