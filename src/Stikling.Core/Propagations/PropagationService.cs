using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using System.Globalization;
using Stikling.Core.Text;
using Stikling.Core.Timeline;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Propagations;

/// <summary>What to do when potting up units of a propagation as plants.</summary>
/// <param name="PlantIds">Ids for the new plants, in order. Given again on a retry, the plants are saved over themselves
/// instead of made twice. There has to be one for each plant the request makes.</param>
public sealed record PotUpRequest(
    int Count,
    DateOnly Date,
    string? Nickname = null,
    Guid? PlaceId = null,
    GrowingMedium Medium = GrowingMedium.Soil,
    Guid? PotId = null,
    Guid? SoilMixId = null,
    bool OnePot = false,
    Guid? IntoPlantId = null,
    IReadOnlyList<Guid>? PlantIds = null);

/// <summary>
/// Propagation operations that keep the counts, the new plants and the history of both in step.
/// </summary>
public sealed class PropagationService(
    IPropagationRepository propagations,
    IPlantRepository plants,
    ITimelineRepository timeline,
    IPropagationCareLogRepository propagationCare,
    ICareLogRepository plantCare,
    IPhotoRepository photos,
    TimeProvider time)
{
    private DateOnly Today => time.Today();

    /// <summary>A new propagation from a plant, with its names and room filled in.</summary>
    public Propagation StartFrom(Plant parent) => new()
    {
        ParentPlantId = parent.Id,
        // Keep the nickname only when there's no genus, so the propagation still has a name
        Nickname = string.IsNullOrWhiteSpace(parent.Genus) ? parent.Nickname : null,
        Genus = parent.Genus,
        Species = parent.Species,
        Cultivar = parent.Cultivar,
        NameQualifier = parent.NameQualifier,
        NameNote = parent.NameNote,
        PlaceId = parent.PlaceId,
        StartedOn = Today
    };

    /// <summary>
    /// Saves a new propagation and records it on its own history and the parent's. Photos taken
    /// while starting it go on its history on the days they were taken, and the newest one
    /// becomes the cover. A create that failed partway can be run again: the record is saved over
    /// itself, and a history entry that is already there isn't added twice.
    /// Run again with the same photos, so the ones whose entries are missing get them.
    /// </summary>
    public async Task CreateAsync(Propagation propagation, Func<Enum, string> label, IReadOnlyList<Photo>? photos = null)
    {
        propagation.ClearMixUnlessSoil();
        if (photos is { Count: > 0 })
            propagation.CoverPhotoId ??= PhotoEntries.Cover(photos).Id;
        // Only a create run again has history to look through, so a first one doesn't read the whole store
        var again = await propagations.GetAsync(propagation.Id) is not null;
        await propagations.SaveAsync(propagation);

        IReadOnlyList<TimelineEntry> history = again ? await timeline.GetForAsync(propagation.Id) : [];
        var batch = BatchLine(propagation);
        var occurredAt = time.MomentOn(propagation.StartedOn);
        if (!history.Any(e => !e.IsDeleted && e.Kind == TimelineKind.Created))
        {
            var started = new TimelineEntry
            {
                SubjectType = SubjectType.Propagation,
                SubjectId = propagation.Id,
                Kind = TimelineKind.Created,
                OccurredAt = PhotoEntries.StartedAt(occurredAt, photos ?? [], time)
            };
            HistoryText.Set(started, [HistoryText.Line("Started", [.. batch.Args])], label);
            await timeline.AddAsync(started);
        }

        var shown = history.Where(e => !e.IsDeleted).SelectMany(e => e.PhotoIds).ToHashSet();
        foreach (var entry in PhotoEntries.For(SubjectType.Propagation, propagation.Id, photos ?? [], time))
        {
            if (!entry.PhotoIds.All(shown.Contains))
                await timeline.AddAsync(entry);
        }

        if (propagation.ParentPlantId is { } parentId && await plants.GetAsync(parentId) is not null
            && !(again && (await timeline.GetForAsync(parentId)).Any(e => !e.IsDeleted && e.Kind == TimelineKind.Propagated && e.RelatedId == propagation.Id)))
        {
            var taken = new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = parentId,
                Kind = TimelineKind.Propagated,
                OccurredAt = occurredAt,
                RelatedType = SubjectType.Propagation,
                RelatedId = propagation.Id
            };
            HistoryText.Set(taken, [batch], label);
            await timeline.AddAsync(taken);
        }
    }

    /// <summary>
    /// Saves an edited propagation and records what changed. A failure in the history after the
    /// save comes as a <see cref="PartlySavedException"/>, since running it again would record the
    /// change twice.
    /// </summary>
    /// <param name="placeName">Turns a room or spot id into its name, so the history can say where it went.</param>
    public async Task UpdateAsync(
        Propagation before,
        Propagation after,
        Func<Enum, string> label,
        Func<Guid, string?>? placeName = null)
    {
        after.ClearMixUnlessSoil();
        after.SyncStageWithCounts(Today);
        after.NoteFirstRoot();
        after.NoteRooted(Today);
        await propagations.SaveAsync(after);

        await PartlySavedException.AfterSaveAsync(async () =>
        {
            var changes = PropagationChanges.Lines(before, after, placeName);
            if (changes.Count > 0)
                await AddChangeAsync(after, changes, label: label);
            if (PlantNames.WasIdentified(before.NameQualifier, after.NameQualifier))
                await AddChangeAsync(after, [HistoryText.Line("IdentifiedAs", HistoryArg.Of(after.BotanicalName ?? after.DisplayName))], @event: TimelineEvent.Identified);
        });
    }

    /// <summary>Marks a propagation dormant from the given day, or woken up again with null.</summary>
    public async Task SetDormantAsync(Propagation propagation, DateOnly? since, Func<Enum, string> label)
    {
        if (propagation.DormantSince == since)
            return;
        var before = propagation.Copy();
        propagation.DormantSince = since;
        await UpdateAsync(before, propagation, label);
    }

    /// <summary>Records that the first root or leaf showed today.</summary>
    public async Task RecordMilestoneAsync(Propagation propagation, Milestone milestone, Func<Enum, string> label)
    {
        var before = propagation.Copy();
        propagation.RecordMilestone(milestone, Today);
        await UpdateAsync(before, propagation, label);
    }

    /// <summary>
    /// Flags a propagation as needing something, e.g. "Change the water", or clears the flag
    /// with null. It's only a reminder for Today, so nothing goes on the history. With
    /// <paramref name="from"/> (a month or a day) it stays off Today until then.
    /// </summary>
    public async Task SetAttentionAsync(Propagation propagation, string? reason, LooseDate? from = null)
    {
        propagation.Attention = Attention.Change(propagation.Attention, reason, Today);
        propagation.AttentionLater = AttentionLater.For(propagation.Attention, from);
        await propagations.SaveAsync(propagation);
    }

    /// <param name="seenToday">Set when the stage was picked on the propagation's page: reaching
    /// Rooting there also records the first root (or seedling) as today. See <see cref="Propagation.NoteFirstSign"/>.</param>
    public async Task SetStageAsync(Propagation propagation, PropagationStage stage, Func<Enum, string> label, bool seenToday = false)
    {
        if (propagation.Stage == stage)
            return;
        var before = propagation.Copy();
        propagation.SetStage(stage);
        // Stepping back from Rooted is a correction, not something seen today
        if (seenToday && stage > before.Stage)
            propagation.NoteFirstSign(Today);
        await UpdateAsync(before, propagation, label);
    }

    /// <summary>
    /// Turns units of the propagation into plants. The plants keep the lineage (parent plant and
    /// propagation) and the propagation finishes when nothing is left. The count is recorded on a copy
    /// and the caller's propagation only changes once that is saved. With <see cref="PotUpRequest.PlantIds"/> it can be
    /// run again after a failure before the count was stored: the plants are saved over themselves and each
    /// gets one Created entry. A failure in the steps after the count (watering, potting together, the
    /// history line) comes as a <see cref="PartlySavedException"/>: the pot up is done and isn't run again.
    /// </summary>
    public async Task<IReadOnlyList<Plant>> PotUpAsync(Propagation propagation, PotUpRequest request, Func<Enum, string>? label = null)
    {
        label ??= e => e.ToString();
        Plant? into = null;
        if (request.IntoPlantId is { } intoId)
        {
            into = await plants.GetAsync(intoId);
            if (into is not { InCollection: true })
                throw new InvalidOperationException(Strings.Propagations.ErrPotWithMissing);
            if (request.Date > Today)
                throw new InvalidOperationException(Strings.Propagations.ErrDateCantBeFuture);
        }
        // One plant with several in the pot, or all of them in with another plant
        var oneRecord = (request.OnePot || into is not null) && request.Count > 1;
        var oneAny = request.OnePot || into is not null;
        // The day it was potted up also finishes the propagation when nothing is left
        if (request.Date < propagation.StartedOn)
            throw new InvalidOperationException(Strings.Propagations.ErrDateBeforeStart);
        IReadOnlyList<string?> names = oneAny
            ? [string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim()]
            : PotUpNames(request.Nickname, request.Count, propagation.PottedUpCount);
        // Worked out from the count as it is now, so a retry gives the same ones
        var codes = CollectionNumbers.PottedUpCodes(propagation, request.Count);
        if (request.PlantIds is { } ids && ids.Count < names.Count)
            throw new ArgumentException(Strings.Propagations.ErrIdForEachPlant, nameof(request));
        var newPlants = names.Select((name, i) => new Plant
        {
            Id = request.PlantIds is { } given ? given[i] : Guid.NewGuid(),
            Nickname = name,
            CollectionNumber = codes[i],
            Genus = propagation.Genus,
            Species = propagation.Species,
            Cultivar = propagation.Cultivar,
            NameQualifier = propagation.NameQualifier,
            NameNote = propagation.NameNote,
            Origin = propagation.Type == PropagationType.Seed ? PlantOrigin.GrownFromSeed : PlantOrigin.Propagated,
            AcquiredOn = LooseDate.Of(request.Date),
            Source = propagation.Source,
            SellerId = propagation.SellerId,
            PlaceId = request.PlaceId,
            Medium = request.Medium,
            InnerPotId = request.PotId,
            SoilMixId = request.Medium == GrowingMedium.Soil ? request.SoilMixId : null,
            ParentPlantId = propagation.ParentPlantId,
            FromPropagationId = propagation.Id,
            Count = oneRecord ? request.Count : null
        }).ToList();

        // They start with what the plant they came from likes
        if (propagation.ParentPlantId is { } parentId && await plants.GetAsync(parentId) is { } parentPlant)
        {
            foreach (var plant in newPlants)
                CareLikes.Copy(parentPlant, plant, Enum.GetValues<LikesField>());
        }

        // Check everything before saving anything
        if (newPlants.SelectMany(p => p.Validate(Today)).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        // They start with the cutting's cover, so its photos don't vanish from view
        var carried = CarriedPhotos.Cover(propagation, await photos.GetForAsync(propagation.Id), request.Date, time);
        foreach (var plant in newPlants)
            plant.CoverPhotoId = carried;
        // Fails here, before anything is saved, when the count doesn't fit
        propagation.Copy().RecordPottedUp(request.Count, request.Date);

        var occurredAt = time.MomentOn(request.Date);
        foreach (var plant in newPlants)
        {
            // Only a plant a retry already saved can have the entry, so a new one doesn't read the whole history
            var again = await plants.GetAsync(plant.Id) is not null;
            await plants.SaveAsync(plant);
            if (again && (await timeline.GetForAsync(plant.Id)).Any(e => !e.IsDeleted && e.Kind == TimelineKind.Created))
                continue;
            var created = new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = plant.Id,
                Kind = TimelineKind.Created,
                OccurredAt = occurredAt,
                RelatedType = SubjectType.Propagation,
                RelatedId = propagation.Id
            };
            HistoryText.Set(created, [HistoryText.Line("PottedUpFromPropagation")]);
            await timeline.AddAsync(created);
        }

        await CountAsync(propagation, p => p.RecordPottedUp(request.Count, request.Date));

        await PartlySavedException.AfterSaveAsync(async () =>
        {
            await CarryWateringOverAsync(propagation, newPlants);

            if (into is not null)
                await new PlantService(plants, timeline, time).PotTogetherAsync(newPlants[0], into, request.Date, label);

            var line = HistoryText.Line("PottedUp", HistoryArg.Of(request.Count), HistoryArg.Of(string.Join(", ", newPlants.Select(p => p.DisplayName))));
            await AddChangeAsync(propagation, WithOutcome(line, propagation), occurredAt,
                newPlants.Count == 1 ? newPlants[0].Id : null, TimelineEvent.PottedUp);
        });

        return newPlants;
    }

    /// <summary>
    /// Gives plants that were potted up before covers were carried over the cover of the cutting
    /// they came from. It only sets a cover where there is none, and every device reaches the same
    /// value. It is meant to run once per device: a cover someone removes later would come back.
    /// A deleted cutting gives nothing. Returns how many plants changed.
    /// </summary>
    public async Task<int> CarryCoversAsync()
    {
        var changed = 0;
        foreach (var plant in await plants.GetAllAsync())
        {
            if (plant.IsDeleted || plant.CoverPhotoId is not null || plant.FromPropagationId is not { } fromId)
                continue;
            if (await propagations.GetAsync(fromId) is not { IsDeleted: false } propagation)
                continue;

            var day = CarriedPhotos.PotUpDay(plant, await timeline.GetForAsync(plant.Id), time);
            if (CarriedPhotos.Cover(propagation, await photos.GetForAsync(propagation.Id), day, time) is not { } cover)
                continue;
            plant.CoverPhotoId = cover;
            await plants.SaveAsync(plant);
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// The newest watering or water change on the propagation goes with the new plants, so they
    /// don't start with no watering at all. Only a watering counts: a plant in soil wasn't watered
    /// by a change of water.
    /// </summary>
    private async Task CarryWateringOverAsync(Propagation propagation, IReadOnlyList<Plant> newPlants)
    {
        var newest = PropagationCareService.For(await propagationCare.GetAllAsync(), propagation.Id)
            .FirstOrDefault(l => l.Kind is PropagationCareKind.Watered or PropagationCareKind.ChangedWater);
        if (newest is not { Kind: PropagationCareKind.Watered })
            return;

        foreach (var plant in newPlants)
        {
            await plantCare.SaveAsync(new CareLog
            {
                PlantId = plant.Id,
                Kind = CareKind.Watered,
                OccurredOn = newest.OccurredOn
            });
        }
    }

    /// <summary>
    /// Records units that didn't make it, with an optional reason and note ("2 failed: rotted, left too wet"). They failed on
    /// <paramref name="date"/>, today unless given, which can't be in the future or before the start.
    /// The caller's propagation only gets the new counts once they are saved, and a failed history line
    /// after that comes as a <see cref="PartlySavedException"/>.
    /// </summary>
    public async Task MarkFailedAsync(Propagation propagation, int count, FailReason? reason = null, string? note = null, DateOnly? date = null)
    {
        var day = date ?? Today;
        if (day > Today)
            throw new InvalidOperationException(Strings.Propagations.ErrDateCantBeFuture);
        if (day < propagation.StartedOn)
            throw new InvalidOperationException(Strings.Propagations.ErrDateBeforeStart);
        await CountAsync(propagation, p => p.RecordFailed(count, day));

        var line = (reason, Clean(note)) switch
        {
            ({ } picked, { } said) => HistoryText.Line("FailedWithReasonNote", HistoryArg.Of(count), HistoryArg.Of(picked), HistoryArg.Of(said)),
            ({ } picked, null) => HistoryText.Line("FailedWithReason", HistoryArg.Of(count), HistoryArg.Of(picked)),
            (null, { } said) => HistoryText.Line("FailedWithNote", HistoryArg.Of(count), HistoryArg.Of(said)),
            _ => HistoryText.Line("Failed", HistoryArg.Of(count))
        };
        await PartlySavedException.AfterSaveAsync(() => AddChangeAsync(propagation, WithOutcome(line, propagation), time.MomentOn(day), null,
            TimelineEvent.Failed, failReason: reason, count: count));
    }

    /// <summary>
    /// Records units given to someone, with an optional name for who got them. They went on
    /// <paramref name="date"/>, today unless given, which can't be in the future or before the start.
    /// The caller's propagation only gets the new counts once they are saved, and a failed history line
    /// after that comes as a <see cref="PartlySavedException"/>.
    /// </summary>
    public async Task GiveAwayAsync(Propagation propagation, int count, string? to = null, DateOnly? date = null)
    {
        var day = date ?? Today;
        if (day > Today)
            throw new InvalidOperationException(Strings.Propagations.ErrDateCantBeFuture);
        if (day < propagation.StartedOn)
            throw new InvalidOperationException(Strings.Propagations.ErrDateBeforeStart);
        await CountAsync(propagation, p => p.RecordGivenAway(count, day));

        var line = Clean(to) is { } who
            ? HistoryText.Line("GaveToWho", HistoryArg.Of(count), HistoryArg.Of(who))
            : HistoryText.Line("GaveAway", HistoryArg.Of(count));
        await PartlySavedException.AfterSaveAsync(() =>
            AddChangeAsync(propagation, WithOutcome(line, propagation), time.MomentOn(day), null, TimelineEvent.GaveAway));
    }

    // Records the count on a copy and saves that, then the same on the caller's propagation, so it only
    // changes once the count is stored. It gets the UpdatedAt the repository set on saving too.
    private async Task CountAsync(Propagation propagation, Action<Propagation> record)
    {
        var counted = propagation.Copy();
        record(counted);
        await propagations.SaveAsync(counted);
        record(propagation);
        propagation.UpdatedAt = counted.UpdatedAt;
    }

    /// <summary>
    /// Sets how many units are offered for swap, never more than what is left. Zero or null clears it.
    /// </summary>
    public async Task SetSpareAsync(Propagation propagation, int? count)
    {
        int? value = count is > 0 ? Math.Min(count.Value, Math.Max(0, propagation.RemainingCount)) : null;
        if (value == 0)
            value = null;
        if (propagation.SpareCount == value)
            return;
        propagation.SpareCount = value;
        await propagations.SaveAsync(propagation);
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

    /// <summary>"2 potted up, 1 given away, 1 failed", for showing.</summary>
    public static string Outcome(Propagation propagation)
    {
        var parts = new List<string>(3);
        if (propagation.PottedUpCount > 0)
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Propagations.OutcomePottedUp, propagation.PottedUpCount));
        if (propagation.GivenAwayCount > 0)
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Propagations.OutcomeGivenAway, propagation.GivenAwayCount));
        if (propagation.FailedCount > 0)
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Propagations.OutcomeFailed, propagation.FailedCount));
        return parts.Count == 0 ? Strings.Propagations.OutcomeNothingYet : string.Join(", ", parts);
    }

    /// <summary>The batch as a history line: "3× corm in LECA".</summary>
    private static HistoryLine BatchLine(Propagation propagation) =>
        HistoryText.Line("BatchOf", HistoryArg.Of(propagation.InitialCount), HistoryArg.Of(propagation.Type), HistoryArg.Of(propagation.Medium));

    // A finished propagation adds a line with what became of the units
    private static IReadOnlyList<HistoryLine> WithOutcome(HistoryLine line, Propagation propagation)
    {
        if (propagation.IsActive)
            return [line];

        var (potted, given, failed) = (propagation.PottedUpCount, propagation.GivenAwayCount, propagation.FailedCount);
        HistoryArg[] Counts(params int[] counts) => [.. counts.Select(c => HistoryArg.Of(c))];
        var finished = (potted > 0, given > 0, failed > 0) switch
        {
            (true, true, true) => HistoryText.Line("FinishedAll", Counts(potted, given, failed)),
            (true, true, false) => HistoryText.Line("FinishedPottedGiven", Counts(potted, given)),
            (true, false, true) => HistoryText.Line("FinishedPottedFailed", Counts(potted, failed)),
            (false, true, true) => HistoryText.Line("FinishedGivenFailed", Counts(given, failed)),
            (true, false, false) => HistoryText.Line("FinishedPotted", Counts(potted)),
            (false, true, false) => HistoryText.Line("FinishedGiven", Counts(given)),
            (false, false, true) => HistoryText.Line("FinishedFailed", Counts(failed)),
            _ => HistoryText.Line("FinishedNothing")
        };
        return [line, finished];
    }

    // The label turns the enums in the lines into the English words that are saved in Text
    private Task AddChangeAsync(Propagation propagation, IReadOnlyList<HistoryLine> lines, DateTimeOffset? occurredAt = null, Guid? plantId = null, TimelineEvent? @event = null,
        FailReason? failReason = null, int? count = null, Func<Enum, string>? label = null)
    {
        var entry = new TimelineEntry
        {
            SubjectType = SubjectType.Propagation,
            SubjectId = propagation.Id,
            Kind = TimelineKind.Change,
            OccurredAt = occurredAt ?? time.GetUtcNow(),
            Event = @event,
            FailReason = failReason,
            Count = count,
            RelatedType = plantId is null ? null : SubjectType.Plant,
            RelatedId = plantId
        };
        HistoryText.Set(entry, lines, label ?? (e => e.ToString()));
        return timeline.AddAsync(entry);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
