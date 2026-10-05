using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

public enum ProgressFilter
{
    Active,
    Finished,
    All
}

/// <summary>Search and filter rules for the propagations list.</summary>
/// <param name="Everyday">The everyday name shown for a propagation, like "Snake plant", which the search looks through too.</param>
public sealed record PropagationFilter(string? Search = null, ProgressFilter Progress = ProgressFilter.Active, Func<Propagation, string?>? Everyday = null)
{
    /// <summary>Active ones oldest first (they've waited longest), finished ones newest first.</summary>
    public IEnumerable<Propagation> Apply(IEnumerable<Propagation> propagations) =>
        propagations
            .Where(p => !p.IsDeleted)
            .Where(MatchesProgress)
            .Where(MatchesSearch)
            .OrderBy(p => p.IsActive ? 0 : 1)
            .ThenBy(p => p.IsActive ? p.StartedOn.DayNumber : -p.UpdatedAt.ToUnixTimeSeconds())
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Active propagations grouped by stage, in the order they move through them.</summary>
    public static IReadOnlyList<(PropagationStage Stage, IReadOnlyList<Propagation> Items)> ByStage(IEnumerable<Propagation> propagations) =>
        propagations
            .GroupBy(p => p.Stage)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, (IReadOnlyList<Propagation>)g.ToList()))
            .ToList();

    private bool MatchesProgress(Propagation p) => Progress switch
    {
        ProgressFilter.Active => p.IsActive,
        ProgressFilter.Finished => !p.IsActive,
        _ => true
    };

    private bool MatchesSearch(Propagation p)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return true;

        var haystack = string.Join(' ', p.Nickname, p.Genus, p.Species, p.Cultivar, p.Source, Everyday?.Invoke(p));
        return Search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }
}
