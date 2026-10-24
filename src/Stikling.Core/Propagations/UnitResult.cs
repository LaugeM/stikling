using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>What saving the pot up, failed or gave away sheet came to.</summary>
/// <param name="Plants">The plants pot up made. Empty for failed and given away, and when a later step failed (see <paramref name="LaterError"/>).</param>
/// <param name="LaterError">The count was stored but a step after it failed, like the history line. The change happened, so it is logged, not a failed save.</param>
public sealed record UnitResult(IReadOnlyList<Plant> Plants, Exception? LaterError);
