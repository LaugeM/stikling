using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Pests;

/// <summary>
/// Marking the plants a pest was seen on. It is the only place that changes
/// <see cref="PestCase.FoundOnPlantIds"/>, so the line each marked plant has on its history always
/// matches the case. Deleting or resolving a case leaves the lines, since the pest was found there.
/// </summary>
public sealed class PestFoundService(IPestCaseRepository cases, ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>
    /// Marks the pest as seen on a plant and writes it on the plant's history. Marking twice does
    /// nothing. On a case on picked plants the plant joins the case, and on a case being watched
    /// the case goes back to treating (pass the case's <paramref name="treatments"/> so a plan is handled right).
    /// The <paramref name="english"/> label turns the pest into its English name for the text saved with the line.
    /// </summary>
    public async Task MarkAsync(
        PestCase item, Plant plant, DateOnly on, IEnumerable<PestTreatment>? treatments = null, Func<Enum, string>? english = null)
    {
        if (item.FoundOnPlantIds.Contains(plant.Id))
            return;

        item.FoundOnPlantIds.Add(plant.Id);
        if (item.Scope == PestScope.PickedPlants && !item.PlantIds.Contains(plant.Id))
            item.PlantIds.Add(plant.Id);

        await AddLine(item, plant.Id, on, english);
        Resume(item, treatments);
        await cases.SaveAsync(item);
    }

    /// <summary>Takes the mark off a plant and the line it wrote on the plant's history.</summary>
    public async Task UnmarkAsync(PestCase item, Guid plantId)
    {
        item.FoundOnPlantIds.Remove(plantId);
        await TakeOffLine(item, plantId);
        await cases.SaveAsync(item);
    }

    /// <summary>
    /// Puts a case being watched back to treating, as when pests are found again. Does nothing for
    /// a case in any other state. Saves the case.
    /// </summary>
    public async Task ResumeAsync(PestCase item, IEnumerable<PestTreatment>? treatments = null)
    {
        if (Resume(item, treatments))
            await cases.SaveAsync(item);
    }

    /// <summary>
    /// Saves a case from the form and marks or unmarks the difference between the plants it had
    /// found before and <see cref="PestCase.FoundOnPlantIds"/> now. On picked plants a plant taken
    /// out of the case is unmarked too, so what was seen is always inside the case. The lines are
    /// dated the day the case started when it is new, and today otherwise.
    /// </summary>
    public async Task SaveAsync(
        PestCase item, IReadOnlyCollection<Guid> foundBefore, IReadOnlyList<Plant> plants, Func<Enum, string>? english = null)
    {
        var isNew = await cases.GetAsync(item.Id) is null;
        var day = isNew ? item.StartedOn : time.Today();
        var known = plants.Where(p => !p.IsDeleted).Select(p => p.Id).ToHashSet();

        var wanted = item.FoundOnPlantIds
            .Where(id => item.Scope != PestScope.PickedPlants || item.PlantIds.Contains(id))
            .Where(id => known.Contains(id) || foundBefore.Contains(id))
            .Distinct().ToList();
        item.FoundOnPlantIds = wanted;

        await cases.SaveAsync(item);

        foreach (var id in wanted.Where(id => !foundBefore.Contains(id)))
            await AddLine(item, id, day, english);
        foreach (var id in foundBefore.Where(id => !wanted.Contains(id)).Distinct())
            await TakeOffLine(item, id);
    }

    private bool Resume(PestCase item, IEnumerable<PestTreatment>? treatments)
    {
        if (item.Status != PestCaseStatus.Monitoring)
            return false;

        var from = item.Status;
        item.Status = PestCaseStatus.Active;
        PestService.StatusChanged(item, from, time.Today(), treatments);
        return true;
    }

    private async Task AddLine(PestCase item, Guid plantId, DateOnly day, Func<Enum, string>? english)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plantId,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Pest,
            PestCaseId = item.Id,
            OccurredAt = time.MomentOn(day)
        };
        HistoryText.Set(entry, [HistoryText.Line("PestFound", HistoryArg.Of(item.Pest))], english ?? (e => e.ToString()));
        await timeline.AddAsync(entry);
    }

    /// <summary>A line with photos stays on the history as a plain photo entry, the way a problem's does.</summary>
    private async Task TakeOffLine(PestCase item, Guid plantId)
    {
        var lines = (await timeline.GetForAsync(plantId))
            .Where(e => !e.IsDeleted && e.PestCaseId == item.Id && e.Event == TimelineEvent.Pest && e.PestTreatmentId is null)
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
                await timeline.UpdateAsync(entry);
            }
            else
            {
                await timeline.DeleteAsync(entry.Id);
            }
        }
    }
}
