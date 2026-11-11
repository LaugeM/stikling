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

/// <summary>One medium of an experiment, added up over the batches that grew in it.</summary>
/// <param name="Rooted">How many rooted: a batch with a rooted date counts what it started with minus what failed, one without counts none.</param>
/// <param name="AverageDays">The rounded average days to root over the batches that have rooted, or null.</param>
/// <param name="PhotoId">The cover photo of the fastest rooted batch that has one, else of the first batch with one. Never a reference photo.</param>
public sealed record ExperimentResultRow(GrowingMedium Medium, int Rooted, int Total, int? AverageDays, Guid? PhotoId, PhotoFrame? Frame);

/// <summary>An experiment's result, the mediums side by side, for a picture.</summary>
public sealed record ExperimentResult(string Name, DateOnly StartedOn, bool StillRunning, IReadOnlyList<ExperimentResultRow> Rows);

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

    /// <summary>
    /// How each medium of an experiment did. The app keeps one rooted date for a batch, not for each
    /// cutting, so a batch with a date counts as rooted less what failed, and one without counts none.
    /// Best first: most rooted, then fastest, then by medium name.
    /// </summary>
    /// <param name="photos">Used to skip reference photos and to find how a cover photo is framed.</param>
    public static ExperimentResult Result(ExperimentGroup group, IEnumerable<Photo> photos)
    {
        var byId = new Dictionary<Guid, Photo>();
        foreach (var photo in photos)
            byId[photo.Id] = photo;

        Photo? UsablePhoto(Propagation p) =>
            p.CoverPhotoId is { } id && byId.TryGetValue(id, out var photo) && !photo.IsReference ? photo : null;

        var rows = group.Entries
            .GroupBy(e => e.Propagation.Medium)
            .Select(batches =>
            {
                var rootedBatches = batches.Where(e => e.DaysToRoot is not null).ToList();
                var rooted = rootedBatches.Sum(e => Math.Max(0, e.Propagation.StartedCount - e.Propagation.FailedCount));
                int? average = rootedBatches.Count == 0
                    ? null
                    : (int)Math.Round(rootedBatches.Average(e => e.DaysToRoot!.Value), MidpointRounding.AwayFromZero);

                // The fastest rooted batch with a photo, else the first batch with one
                var photo = rootedBatches.OrderBy(e => e.DaysToRoot).Select(e => UsablePhoto(e.Propagation)).FirstOrDefault(p => p is not null)
                    ?? batches.Select(e => UsablePhoto(e.Propagation)).FirstOrDefault(p => p is not null);

                return new ExperimentResultRow(batches.Key, rooted, batches.Sum(e => e.Propagation.StartedCount), average, photo?.Id, photo?.Frame);
            })
            .OrderByDescending(r => r.Total > 0 ? r.Rooted / (double)r.Total : 0)
            .ThenBy(r => r.AverageDays is null)
            .ThenBy(r => r.AverageDays ?? 0)
            .ThenBy(r => r.Medium.ToString(), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ExperimentResult(group.Name, group.StartedOn, group.IsActive, rows);
    }
}
