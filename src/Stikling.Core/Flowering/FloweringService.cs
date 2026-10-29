using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Flowering;

/// <summary>
/// Marking when a plant flowers. A mark is a record of its own, so devices never fight over a
/// single flowering. Marks made now also write a line on the plant's history.
/// </summary>
public sealed class FloweringService(IFloweringMarkRepository marks, ITimelineRepository timeline, TimeProvider time)
{
    private DateOnly Today => time.Today();

    public async Task<IReadOnlyList<FloweringMark>> ForPlantAsync(Guid plantId) =>
        (await marks.GetAllAsync()).Where(m => m.PlantId == plantId).ToList();

    /// <summary>Marks a stage on a day and writes it on the plant's history.</summary>
    public async Task<FloweringMark> MarkAsync(Plant plant, FloweringStage stage, DateOnly day)
    {
        var mark = new FloweringMark { PlantId = plant.Id, Stage = stage, On = LooseDate.Of(day) };
        Check(mark);

        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Change,
            Event = TimelineEvent.Flowering,
            Text = stage switch
            {
                FloweringStage.Buds => "Spike or buds",
                FloweringStage.InFlower => "In flower",
                _ => "Done flowering"
            },
            OccurredAt = time.MomentOn(day)
        };
        await timeline.AddAsync(entry);

        mark.TimelineEntryId = entry.Id;
        await marks.SaveAsync(mark);
        return mark;
    }

    /// <summary>
    /// "Still in flower? Yes." Dated today and with no history line, since nothing happened. It
    /// only brings forward when the flowering was last confirmed.
    /// </summary>
    public async Task StillAsync(Plant plant, FloweringStage stage)
    {
        var mark = new FloweringMark { PlantId = plant.Id, Stage = stage, On = LooseDate.Of(Today) };
        Check(mark);
        await marks.SaveAsync(mark);
    }

    /// <summary>
    /// Adds a flowering from the past, e.g. "it flowered in May 2025". Nothing is written on the
    /// history, since the dates may be only a month or a year. Throws if a date is in the future
    /// or they are out of order.
    /// </summary>
    public async Task<IReadOnlyList<FloweringMark>> AddPastAsync(
        Plant plant, LooseDate? budsFrom, LooseDate inFlowerFrom, LooseDate? until)
    {
        var errors = PastErrors(budsFrom, inFlowerFrom, until);
        if (errors.Count > 0)
            throw new InvalidOperationException(errors[0]);

        var added = new List<FloweringMark>();
        foreach (var (stage, on) in new (FloweringStage, LooseDate?)[]
                 {
                     (FloweringStage.Buds, budsFrom), (FloweringStage.InFlower, inFlowerFrom), (FloweringStage.Done, until)
                 })
        {
            if (on is null)
                continue;
            var mark = new FloweringMark { PlantId = plant.Id, Stage = stage, On = on };
            Check(mark);
            await marks.SaveAsync(mark);
            added.Add(mark);
        }
        return added;
    }

    /// <summary>What is wrong with the dates of a past flowering, for showing in the form.</summary>
    public IReadOnlyList<string> PastErrors(LooseDate? budsFrom, LooseDate inFlowerFrom, LooseDate? until)
    {
        var errors = new List<string>();
        var today = Today;
        if (new[] { budsFrom, inFlowerFrom, until }.Any(d => d is not null && d.Start > today))
            errors.Add(Strings.Pests.FloweringInFuture);
        if (budsFrom is not null && budsFrom.Start > inFlowerFrom.Start)
            errors.Add(Strings.Pests.FloweringBudsBeforeFlowers);
        if (until is not null && until.Start < inFlowerFrom.Start)
            errors.Add(Strings.Pests.FloweringEndBeforeBegin);
        return errors;
    }

    /// <summary>Takes a mark off, and the history line it wrote.</summary>
    public async Task RemoveAsync(FloweringMark mark)
    {
        await marks.DeleteAsync(mark.Id);
        if (mark.TimelineEntryId is { } entryId)
            await timeline.DeleteAsync(entryId);
    }

    private void Check(FloweringMark mark)
    {
        if (mark.Validate(Today).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
    }
}
