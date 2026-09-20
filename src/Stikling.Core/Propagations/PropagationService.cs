using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;

namespace Stikling.Core.Propagations;

/// <summary>What to do when potting up units of a propagation as plants.</summary>
public sealed record PotUpRequest(
    int Count,
    DateOnly Date,
    string? Nickname = null,
    string? Location = null,
    GrowingMedium Medium = GrowingMedium.Soil,
    Guid? PotId = null,
    Guid? SoilMixId = null);

/// <summary>
/// Propagation operations that keep the counts, the new plants and the history of both in step.
/// </summary>
public sealed class PropagationService(
    IPropagationRepository propagations,
    IPlantRepository plants,
    ITimelineRepository timeline,
    TimeProvider time)
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>A new propagation from a plant, with its names and room filled in.</summary>
    public Propagation StartFrom(Plant parent) => new()
    {
        ParentPlantId = parent.Id,
        // Keep the nickname only when there's no genus, so the propagation still has a name
        Nickname = string.IsNullOrWhiteSpace(parent.Genus) ? parent.Nickname : null,
        Genus = parent.Genus,
        Species = parent.Species,
        Cultivar = parent.Cultivar,
        Location = parent.Location,
        StartedOn = Today
    };

    /// <summary>Saves a new propagation and records it on its own history and the parent's.</summary>
    public async Task CreateAsync(Propagation propagation, Func<Enum, string> label)
    {
        await propagations.SaveAsync(propagation);

        var what = Describe(propagation, label);
        var occurredAt = MomentOf(propagation.StartedOn);
        await timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = propagation.Id,
            Kind = TimelineKind.Created,
            OccurredAt = occurredAt,
            Text = $"Started {what}"
        });

        if (propagation.ParentPlantId is { } parentId && await plants.GetAsync(parentId) is not null)
        {
            await timeline.AddAsync(new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = parentId,
                Kind = TimelineKind.Propagated,
                OccurredAt = occurredAt,
                Text = what,
                RelatedType = SubjectType.Propagation,
                RelatedId = propagation.Id
            });
        }
    }

    /// <summary>Saves an edited propagation and records what changed.</summary>
    public async Task UpdateAsync(Propagation before, Propagation after, Func<Enum, string> label)
    {
        after.SyncStageWithCounts();
        after.NoteRooted(Today);
        await propagations.SaveAsync(after);

        var changes = PropagationChanges.Describe(before, after, label);
        if (changes.Count > 0)
            await AddChangeAsync(after, string.Join("\n", changes));
    }

    public async Task SetStageAsync(Propagation propagation, PropagationStage stage, Func<Enum, string> label)
    {
        if (propagation.Stage == stage)
            return;
        var before = propagation.Copy();
        propagation.SetStage(stage);
        await UpdateAsync(before, propagation, label);
    }

    /// <summary>
    /// Turns units of the propagation into plants. The plants keep the lineage (parent plant and
    /// propagation) and the propagation finishes when nothing is left.
    /// </summary>
    public async Task<IReadOnlyList<Plant>> PotUpAsync(Propagation propagation, PotUpRequest request)
    {
        var names = PotUpNames(request.Nickname, request.Count, propagation.PottedUpCount);
        var newPlants = names.Select(name => new Plant
        {
            Nickname = name,
            Genus = propagation.Genus,
            Species = propagation.Species,
            Cultivar = propagation.Cultivar,
            Origin = propagation.Type == PropagationType.Seed ? PlantOrigin.GrownFromSeed : PlantOrigin.Propagated,
            AcquiredOn = LooseDate.Of(request.Date),
            Source = propagation.Source,
            Location = Clean(request.Location),
            Medium = request.Medium,
            InnerPotId = request.PotId,
            SoilMixId = request.SoilMixId,
            ParentPlantId = propagation.ParentPlantId,
            FromPropagationId = propagation.Id
        }).ToList();

        // Check everything before saving anything
        if (newPlants.SelectMany(p => p.Validate(Today)).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        propagation.RecordPottedUp(request.Count);

        var occurredAt = MomentOf(request.Date);
        foreach (var plant in newPlants)
        {
            await plants.SaveAsync(plant);
            await timeline.AddAsync(new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = plant.Id,
                Kind = TimelineKind.Created,
                OccurredAt = occurredAt,
                Text = "Potted up from a propagation",
                RelatedType = SubjectType.Propagation,
                RelatedId = propagation.Id
            });
        }

        await propagations.SaveAsync(propagation);

        var text = $"Potted up {request.Count}: {string.Join(", ", newPlants.Select(p => p.DisplayName))}";
        await AddChangeAsync(propagation, WithOutcome(text, propagation), occurredAt,
            newPlants.Count == 1 ? newPlants[0].Id : null);

        return newPlants;
    }

    /// <summary>Records units that didn't make it, with an optional reason ("rotted").</summary>
    public async Task MarkFailedAsync(Propagation propagation, int count, string? reason = null)
    {
        propagation.RecordFailed(count);
        await propagations.SaveAsync(propagation);

        var text = Clean(reason) is { } r ? $"{count} failed: {r}" : $"{count} failed";
        await AddChangeAsync(propagation, WithOutcome(text, propagation));
    }

    public Task DeleteAsync(Guid id) => propagations.DeleteAsync(id);

    /// <summary>
    /// Nicknames for potted-up plants. Several plants from one batch are numbered so they can be
    /// told apart ("Coleus 2", "Coleus 3"), continuing after any potted up earlier.
    /// </summary>
    public static IReadOnlyList<string?> PotUpNames(string? nickname, int count, int alreadyPottedUp)
    {
        if (Clean(nickname) is not { } name)
            return Enumerable.Repeat<string?>(null, count).ToList();
        if (count == 1 && alreadyPottedUp == 0)
            return [name];
        return Enumerable.Range(alreadyPottedUp + 1, count).Select(n => (string?)$"{name} {n}").ToList();
    }

    /// <summary>"3× corm in LECA".</summary>
    public static string Describe(Propagation propagation, Func<Enum, string> label) =>
        $"{propagation.InitialCount}× {label(propagation.Type).ToLowerInvariant()} in {label(propagation.Medium)}";

    /// <summary>"2 potted up, 1 failed".</summary>
    public static string Outcome(Propagation propagation)
    {
        var parts = new List<string>(2);
        if (propagation.PottedUpCount > 0)
            parts.Add($"{propagation.PottedUpCount} potted up");
        if (propagation.FailedCount > 0)
            parts.Add($"{propagation.FailedCount} failed");
        return parts.Count == 0 ? "nothing yet" : string.Join(", ", parts);
    }

    private static string WithOutcome(string text, Propagation propagation) =>
        propagation.IsActive ? text : $"{text}\nFinished: {Outcome(propagation)}";

    private Task AddChangeAsync(Propagation propagation, string text, DateTimeOffset? occurredAt = null, Guid? plantId = null) =>
        timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = propagation.Id,
            Kind = TimelineKind.Change,
            OccurredAt = occurredAt ?? time.GetUtcNow(),
            Text = text,
            RelatedType = plantId is null ? null : SubjectType.Plant,
            RelatedId = plantId
        });

    // A date without a time: "now" for today, otherwise noon so it lands on the right day
    private DateTimeOffset MomentOf(DateOnly date) =>
        date == Today ? time.GetUtcNow() : new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
