using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Pests;

/// <summary>
/// Photos on a pest case. They are ordinary plant photos on a line in that plant's history: a
/// treatment or check gets a line of its own, and the photos picked when the case is opened go on
/// the line for where the pest was found. It is the only place that writes the lines that carry a
/// treatment's photos. Photos are never deleted here, since they are pictures of the plant: removing
/// a treatment or deleting a case turns its line into a plain photo entry that stays on the history.
/// </summary>
public sealed class PestPhotoService(
    ITimelineRepository timeline, IPestTreatmentRepository treatments, IPestCaseRepository cases, TimeProvider time)
{
    /// <summary>
    /// Writes a line on the plant's history for a treatment or check that has photos, dated the day it was done.
    /// Does nothing without photos. The <paramref name="english"/> label turns the pest into its English
    /// name for the text saved with the line.
    /// </summary>
    public async Task AddLineAsync(
        PestCase item, PestTreatment treatment, Guid plantId, IReadOnlyList<Guid> photoIds, Func<Enum, string>? english = null)
    {
        if (photoIds.Count == 0)
            return;

        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plantId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Pest,
            PestCaseId = item.Id,
            PestTreatmentId = treatment.Id,
            OccurredAt = time.MomentOn(treatment.OccurredOn),
            PhotoIds = [.. photoIds]
        };

        var what = treatment.What?.Trim();
        var line = treatment.Kind == PestTreatmentKind.Checked
            ? HistoryText.Line("PestChecked", HistoryArg.Of(item.Pest))
            : !string.IsNullOrEmpty(what)
                ? HistoryText.Line("PestTreatedWith", HistoryArg.Of(item.Pest), HistoryArg.Of(what))
                : HistoryText.Line("PestTreated", HistoryArg.Of(item.Pest));

        HistoryText.Set(entry, [line], english ?? (e => e.ToString()));
        await timeline.AddAsync(entry);
    }

    /// <summary>
    /// Puts photos on the line for where the pest was found on a plant, without repeating ones it
    /// already has. When the plant has no such line yet, one is written, dated the day the case started.
    /// </summary>
    public async Task AddToFoundLineAsync(
        PestCase item, Guid plantId, IReadOnlyList<Guid> photoIds, Func<Enum, string>? english = null)
    {
        if (photoIds.Count == 0)
            return;

        var found = (await timeline.GetForAsync(plantId)).FirstOrDefault(e =>
            !e.IsDeleted && e.PestCaseId == item.Id && e.Event == TimelineEvent.Pest && e.PestTreatmentId is null);

        if (found is not null)
        {
            var added = photoIds.Where(id => !found.PhotoIds.Contains(id)).Distinct().ToList();
            if (added.Count == 0)
                return;
            found.PhotoIds.AddRange(added);
            await timeline.UpdateAsync(found);
            return;
        }

        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plantId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Pest,
            PestCaseId = item.Id,
            OccurredAt = time.MomentOn(item.StartedOn),
            PhotoIds = [.. photoIds.Distinct()]
        };
        HistoryText.Set(entry, [HistoryText.Line("PestFound", HistoryArg.Of(item.Pest))], english ?? (e => e.ToString()));
        await timeline.AddAsync(entry);
    }

    /// <summary>The lines with photos that belong to a case, newest first.</summary>
    public async Task<IReadOnlyList<TimelineEntry>> LinesForAsync(Guid caseId) =>
        [.. (await timeline.GetPlantChangesAsync())
            .Where(e => !e.IsDeleted && e.PestCaseId == caseId && e.PhotoIds.Count > 0)
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.CreatedAt)];

    /// <summary>How many photos a treatment has on the plants' histories.</summary>
    public async Task<int> PhotoCountAsync(PestTreatment treatment) =>
        (await timeline.GetPlantChangesAsync())
            .Where(e => !e.IsDeleted && e.PestTreatmentId == treatment.Id)
            .SelectMany(e => e.PhotoIds)
            .Distinct()
            .Count();

    /// <summary>
    /// Deletes a treatment, check or trap count. Its photos stay: the line they were on becomes a
    /// plain photo entry, and a line without photos is deleted.
    /// </summary>
    public async Task RemoveTreatmentAsync(PestTreatment treatment)
    {
        await treatments.DeleteAsync(treatment.Id);

        var lines = (await timeline.GetPlantChangesAsync())
            .Where(e => !e.IsDeleted && e.PestTreatmentId == treatment.Id)
            .ToList();

        foreach (var entry in lines)
        {
            if (entry.PhotoIds.Count > 0)
            {
                entry.Kind = TimelineKind.Photo;
                entry.Event = null;
                entry.Text = null;
                entry.Lines = null;
                entry.PestCaseId = null;
                entry.PestTreatmentId = null;
                await timeline.UpdateAsync(entry);
            }
            else
            {
                await timeline.DeleteAsync(entry.Id);
            }
        }
    }

    /// <summary>
    /// Deletes a case and its treatments. Photos stay on the plants, and so do the lines for where the
    /// pest was found, since it was found there.
    /// </summary>
    public async Task DeleteCaseAsync(PestCase item)
    {
        foreach (var treatment in PestService.TreatmentsFor(await treatments.GetAllAsync(), item.Id))
            await RemoveTreatmentAsync(treatment);

        await cases.DeleteAsync(item.Id);
    }
}
