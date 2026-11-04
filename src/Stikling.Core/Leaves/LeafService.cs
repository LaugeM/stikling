using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Leaves;

/// <summary>A leaf in a log. AsCutting is set for a leaf logged on the cutting a plant was potted up from.</summary>
public sealed record LoggedLeaf(Leaf Leaf, bool AsCutting);

/// <summary>
/// The leaf log of plants and cuttings. Each leaf is a record of its own and writes a line on the
/// history, so removing a leaf takes its line off again. Nothing is copied from a cutting to the
/// plants it becomes: the leaves stay on the cutting and the plant reads them from there.
/// </summary>
public sealed class LeafService(ILeafRepository leaves, ITimelineRepository timeline, TimeProvider time)
{
    private DateOnly Today => time.Today();

    /// <summary>The leaves of one plant or cutting, newest first.</summary>
    public async Task<IReadOnlyList<Leaf>> ForAsync(Guid subjectId) =>
        Newest((await leaves.GetAllAsync()).Where(l => !l.IsDeleted && l.SubjectId == subjectId)).ToList();

    /// <summary>
    /// The plant's own leaves, and when it was potted up from a cutting, that cutting's leaves
    /// from before it was potted up (all of them when the day isn't known). Newest first.
    /// </summary>
    public async Task<IReadOnlyList<LoggedLeaf>> ForPlantAsync(Plant plant)
    {
        var all = (await leaves.GetAllAsync()).Where(l => !l.IsDeleted).ToList();
        var found = all.Where(l => l.SubjectId == plant.Id).Select(l => new LoggedLeaf(l, false)).ToList();
        if (plant.FromPropagationId is { } propagationId)
        {
            var pottedUp = plant.AcquiredOn?.Start;
            found.AddRange(all.Where(l => l.SubjectId == propagationId && (pottedUp is null || l.On <= pottedUp))
                .Select(l => new LoggedLeaf(l, true)));
        }
        return [.. found.OrderByDescending(l => l.Leaf.On).ThenByDescending(l => l.Leaf.CreatedAt)];
    }

    /// <summary>The cutting's own leaves, newest first.</summary>
    public async Task<IReadOnlyList<LoggedLeaf>> ForPropagationAsync(Guid propagationId) =>
        [.. (await ForAsync(propagationId)).Select(l => new LoggedLeaf(l, false))];

    /// <summary>Logs a leaf and writes it on the history, with any photos taken for it.</summary>
    public async Task<Leaf> AddAsync(
        SubjectType subjectType, Guid subjectId, Variegation rating, DateOnly day, string? note, IReadOnlyList<Guid> photoIds)
    {
        var leaf = new Leaf { SubjectType = subjectType, SubjectId = subjectId, Rating = rating, On = day, Note = Clean(note) };
        Check(leaf);

        var entry = new TimelineEntry
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.NewLeaf,
            OccurredAt = time.MomentOn(day),
            PhotoIds = [.. photoIds]
        };
        HistoryText.Set(entry, LeafText.Lines(leaf));
        await timeline.AddAsync(entry);

        leaf.TimelineEntryId = entry.Id;
        await leaves.SaveAsync(leaf);
        return leaf;
    }

    /// <summary>Saves a corrected leaf and rewrites its line. The photos on the line are kept.</summary>
    public async Task EditAsync(Leaf leaf)
    {
        leaf.Note = Clean(leaf.Note);
        Check(leaf);

        if (leaf.TimelineEntryId is { } id
            && (await timeline.GetForAsync(leaf.SubjectId)).FirstOrDefault(e => e.Id == id) is { } entry)
        {
            HistoryText.Set(entry, LeafText.Lines(leaf));
            entry.OccurredAt = time.MomentOn(leaf.On);
            await timeline.UpdateAsync(entry);
        }
        await leaves.SaveAsync(leaf);
    }

    /// <summary>
    /// Takes a leaf off, and the history line it wrote. Photos on the line are never deleted:
    /// a line that has some stays on the history as a plain photo entry.
    /// </summary>
    public async Task RemoveAsync(Leaf leaf)
    {
        await leaves.DeleteAsync(leaf.Id);
        if (leaf.TimelineEntryId is not { } id)
            return;
        var entry = (await timeline.GetForAsync(leaf.SubjectId)).FirstOrDefault(e => e.Id == id);
        if (entry is { PhotoIds.Count: > 0 })
        {
            entry.Kind = TimelineKind.Photo;
            entry.Event = null;
            entry.Text = null;
            entry.Lines = null;
            await timeline.UpdateAsync(entry);
        }
        else
        {
            await timeline.DeleteAsync(id);
        }
    }

    private static IEnumerable<Leaf> Newest(IEnumerable<Leaf> found) =>
        found.OrderByDescending(l => l.On).ThenByDescending(l => l.CreatedAt);

    private void Check(Leaf leaf)
    {
        if (leaf.Validate(Today).FirstOrDefault() is { } error)
            throw new ArgumentException(error);
    }

    private static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
