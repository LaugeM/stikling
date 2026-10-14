using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>The containers people have used for propagations, to suggest while typing.</summary>
public static class Containers
{
    /// <summary>
    /// One entry per name, ignoring case, using the spelling of the most recently updated propagation.
    /// The most recently used comes first.
    /// </summary>
    public static IReadOnlyList<string> Names(IEnumerable<Propagation> propagations) =>
        propagations
            .Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Container))
            .GroupBy(p => p.Container!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.UpdatedAt).First())
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => p.Container!.Trim())
            .ToList();
}
