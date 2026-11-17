using Stikling.Core.Models;
using Stikling.Core.Pests;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Timeline;

/// <summary>
/// One place a plant or propagation was mentioned: a note on another plant's or propagation's history, or a
/// pest case update. Exactly one of the three records is set, the one the note is on.
/// </summary>
public sealed record MentionedLine(DateOnly Day, string Snippet, Plant? Plant, Propagation? Propagation, PestCase? PestCase);

/// <summary>Finds the notes that mention a record, for the "Mentioned in" card on its page.</summary>
public sealed class MentionService(
    ITimelineRepository timeline,
    IPlantRepository plants,
    IPropagationRepository propagations,
    IPestCaseRepository cases,
    IPestTreatmentRepository treatments,
    Pots.IPotRepository pots,
    TimeProvider time)
{
    /// <summary>The ids of the plants, propagations and pots that still exist, which is what a mention can link to.</summary>
    public async Task<IReadOnlySet<Guid>> LiveTargetsAsync()
    {
        var ids = new HashSet<Guid>();
        ids.UnionWith((await plants.GetAllAsync()).Where(p => !p.IsDeleted).Select(p => p.Id));
        ids.UnionWith((await propagations.GetAllAsync()).Where(p => !p.IsDeleted).Select(p => p.Id));
        ids.UnionWith((await pots.GetAllAsync()).Where(p => !p.IsDeleted).Select(p => p.Id));
        return ids;
    }

    /// <summary>
    /// The notes that mention the record, newest first. A note on a record that is deleted is left out, since
    /// there is nowhere to go from it, and so is a note on the record itself.
    /// </summary>
    public async Task<IReadOnlyList<MentionedLine>> ForAsync(Guid id)
    {
        var lines = new List<MentionedLine>();

        var entries = (await timeline.GetSinceAsync(DateTimeOffset.MinValue))
            .Where(e => !e.IsDeleted && e.SubjectId != id && e.Mentions is { } found && found.Any(m => m.Id == id)
                && Mentions.Keep(e.Text, found)?.Any(m => m.Id == id) == true)
            .ToList();

        if (entries.Count > 0)
        {
            var plantsById = (await plants.GetAllAsync()).ToDictionary(p => p.Id);
            var propagationsById = (await propagations.GetAllAsync()).ToDictionary(p => p.Id);
            foreach (var entry in entries)
            {
                var plant = entry.SubjectType == SubjectType.Plant ? plantsById.GetValueOrDefault(entry.SubjectId) : null;
                var propagation = entry.SubjectType == SubjectType.Propagation ? propagationsById.GetValueOrDefault(entry.SubjectId) : null;
                if (plant is null && propagation is null)
                    continue;
                lines.Add(new MentionedLine(time.LocalDay(entry.OccurredAt), Mentions.Snippet(entry.Text), plant, propagation, null));
            }
        }

        var mentioning = (await treatments.GetAllAsync())
            .Where(t => !t.IsDeleted && t.Mentions is { } found && found.Any(m => m.Id == id))
            .ToList();
        if (mentioning.Count > 0)
        {
            var casesById = (await cases.GetAllAsync()).ToDictionary(c => c.Id);
            foreach (var treatment in mentioning)
            {
                if (casesById.GetValueOrDefault(treatment.CaseId) is { } pestCase)
                    lines.Add(new MentionedLine(treatment.OccurredOn, Mentions.Snippet(treatment.Notes), null, null, pestCase));
            }
        }

        return [.. lines.OrderByDescending(l => l.Day)];
    }
}
