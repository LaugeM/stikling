using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>One batch in an experiment, with the numbers it's compared on.</summary>
public sealed record ExperimentEntry(Propagation Propagation, int DaysRunning, int? DaysToRoot);

/// <summary>Propagations started together under the same name, to be compared side by side.</summary>
/// <param name="StartedOn">The day the first of them was started.</param>
public sealed record ExperimentGroup(string Name, DateOnly StartedOn, IReadOnlyList<ExperimentEntry> Entries)
{
    public int RootedCount => Entries.Count(e => e.DaysToRoot is not null);

    /// <summary>Still worth watching: at least one batch hasn't finished.</summary>
    public bool IsActive => Entries.Any(e => e.Propagation.IsActive);
}

/// <summary>
/// Grouping propagations that were started together to compare them, e.g. the same corms in
/// LECA, perlite, sphagnum and on a riser. The name is free text so an experiment can be
/// started without setting anything up first.
/// </summary>
public static class Experiments
{
    /// <summary>Distinct, sorted experiment names, e.g. to suggest while typing.</summary>
    public static IReadOnlyList<string> Names(IEnumerable<Propagation> propagations) =>
        propagations
            .Where(p => !p.IsDeleted)
            .Select(p => p.Experiment)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Every propagation that has an experiment name, grouped by it. The newest experiment comes
    /// first, and inside a group the ones that have rooted come first, fastest at the top.
    /// </summary>
    public static IReadOnlyList<ExperimentGroup> Group(IEnumerable<Propagation> propagations, DateOnly today) =>
        propagations
            .Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Experiment))
            .GroupBy(p => p.Experiment!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExperimentGroup(
                // The spelling used by whichever batch was started first
                group.OrderBy(p => p.StartedOn).ThenBy(p => p.CreatedAt).First().Experiment!.Trim(),
                group.Min(p => p.StartedOn),
                group
                    .OrderBy(p => p.DaysToRoot is null)
                    .ThenBy(p => p.DaysToRoot ?? 0)
                    .ThenBy(p => p.StartedOn)
                    .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(p => new ExperimentEntry(p, p.DaysSinceStart(today), p.DaysToRoot))
                    .ToList()))
            .OrderByDescending(g => g.StartedOn)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
