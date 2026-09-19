using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Plants;

/// <summary>
/// Plant operations that also write to the timeline, so every place in the app records
/// history the same way.
/// </summary>
public sealed class PlantService(IPlantRepository plants, ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>Saves a new plant and records "Added to collection".</summary>
    public async Task CreateAsync(Plant plant)
    {
        await plants.SaveAsync(plant);
        await timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Created,
            OccurredAt = StartOf(plant.AcquiredOn) ?? time.GetUtcNow(),
            Text = plant.Origin == PlantOrigin.Propagated ? "Added as a propagated plant" : "Added to collection"
        });
    }

    /// <summary>Saves an edited plant and records what changed (status, room, medium, pot).</summary>
    public async Task UpdateAsync(Plant before, Plant after, Func<Enum, string> label)
    {
        await plants.SaveAsync(after);

        var changes = PlantChanges.Describe(before, after, label);
        if (changes.Count > 0)
            await AddChangeAsync(after.Id, string.Join("\n", changes));
    }

    /// <summary>Sets the same status on several plants, recording it on each.</summary>
    public async Task SetStatusAsync(IEnumerable<Plant> selected, PlantStatus status, Func<Enum, string> label)
    {
        foreach (var plant in selected.Where(p => p.Status != status))
        {
            var before = plant.Copy();
            plant.Status = status;
            await UpdateAsync(before, plant, label);
        }
    }

    /// <summary>Adds the same note to several plants, e.g. "Sprayed for thrips".</summary>
    public async Task AddNoteAsync(IEnumerable<Plant> selected, string text, DateTimeOffset? occurredAt = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A note needs some text.", nameof(text));

        foreach (var plant in selected)
        {
            await timeline.AddAsync(new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = plant.Id,
                Kind = TimelineKind.Note,
                OccurredAt = occurredAt ?? time.GetUtcNow(),
                Text = text.Trim()
            });
        }
    }

    private Task AddChangeAsync(Guid plantId, string text) =>
        timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plantId,
            Kind = TimelineKind.Change,
            OccurredAt = time.GetUtcNow(),
            Text = text
        });

    // Acquisition dates have no time of day; use noon so the entry lands on the right day in any timezone
    private static DateTimeOffset? StartOf(DateOnly? date) =>
        date is { } d ? new DateTimeOffset(d.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero) : null;
}
