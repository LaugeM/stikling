using Stikling.Core.Care;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Timeline;

namespace Stikling.Core.Plants;

/// <summary>
/// Plant operations that also write to the timeline, so every place in the app records
/// history the same way.
/// </summary>
public sealed class PlantService(IPlantRepository plants, ITimelineRepository timeline, TimeProvider time)
{
    /// <summary>
    /// Saves a new plant and records "Added to collection". Photos taken while adding it start
    /// its history on the days they were taken, and the newest one becomes the cover. A create that
    /// failed partway can be run again with the same photos: the plant is saved over itself, and a
    /// history entry that is already there isn't added twice.
    /// </summary>
    public async Task CreateAsync(Plant plant, IReadOnlyList<Photo>? photos = null)
    {
        if (photos is { Count: > 0 })
            plant.CoverPhotoId ??= PhotoEntries.Cover(photos).Id;

        plant.Tags = PlantTags.Normalize(plant.Tags);
        Normalize(plant);
        // Only a create run again has history to look through, so a first one doesn't read the whole store
        var again = await plants.GetAsync(plant.Id) is not null;
        await plants.SaveAsync(plant);

        List<TimelineEntry> history = again ? [.. (await timeline.GetForAsync(plant.Id)).Where(e => !e.IsDeleted)] : [];
        if (!history.Any(e => e.Kind == TimelineKind.Created))
        {
            await timeline.AddAsync(new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = plant.Id,
                Kind = TimelineKind.Created,
                OccurredAt = AddedAt(plant, photos ?? []),
                Text = FirstEntryText(plant)
            });
        }

        var shown = history.SelectMany(e => e.PhotoIds).ToHashSet();
        foreach (var entry in PhotoEntries.For(SubjectType.Plant, plant.Id, photos ?? [], time))
        {
            if (!entry.PhotoIds.All(shown.Contains))
                await timeline.AddAsync(entry);
        }
    }

    /// <summary>
    /// Sets the given needs on each of the other plants to what this plant has, and saves them.
    /// Writes nothing to the timeline.
    /// </summary>
    public async Task ApplyLikesAsync(Plant from, IReadOnlyList<LikesField> fields, IEnumerable<Plant> others)
    {
        foreach (var loaded in others)
        {
            // Read again, so a change that arrived since the page loaded isn't saved over
            if (await plants.GetAsync(loaded.Id) is not { IsDeleted: false } other)
                continue;
            CareLikes.Copy(from, other, fields);
            CareLikes.Normalize(other);
            await plants.SaveAsync(other);
        }
    }

    /// <summary>
    /// Renames a tag on every plant that has it, and merges it into another tag when the new
    /// name is one already in use. Plants that are gone keep their labels, so they change too.
    /// Returns how many plants changed. An empty new name does nothing.
    /// </summary>
    public async Task<int> RenameTagAsync(string from, string to)
    {
        if (PlantTags.Clean(to) is not { } name || PlantTags.Clean(from) is null)
            return 0;

        var changed = 0;
        foreach (var plant in await plants.GetAllAsync())
        {
            if (plant.IsDeleted || !PlantTags.Has(plant, from))
                continue;

            var renamed = PlantTags.Normalize(plant.Tags.Select(t => PlantTags.Same(t, from) ? name : t));
            if (renamed.SequenceEqual(plant.Tags))
                continue;

            plant.Tags = renamed;
            await plants.SaveAsync(plant);
            changed++;
        }

        return changed;
    }

    // What every stored plant follows. Safe to run again. A collection number is only tidied when it
    // was typed now, so an older one like "#117A" stays as it was until someone edits it
    private static void Normalize(Plant plant, bool numberChanged = true)
    {
        if (numberChanged)
            plant.CollectionNumber = CollectionNumbers.Normalize(plant.CollectionNumber);
        CareLikes.Normalize(plant);
        // One is never stored
        if (plant.Count == 1)
            plant.Count = null;
    }

    /// <summary>
    /// Saves an edited plant and records what changed (status, room, medium, pot). A failure in the
    /// history after the save comes as a <see cref="PartlySavedException"/>, since running it again
    /// would record the change twice.
    /// </summary>
    /// <param name="potName">Turns a pot id into its name, so the history can say which pot.</param>
    /// <param name="placeName">The same for the room or spot, so the history can say where it went.</param>
    /// <param name="pot">Finds a pot by id, to see whether a pot waters itself.</param>
    /// <param name="feedName">Turns a feed id into its name, for the feed given with the watering.</param>
    public async Task UpdateAsync(
        Plant before,
        Plant after,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null,
        Func<Guid, Pot?>? pot = null,
        Func<Guid, string?>? feedName = null)
    {
        after.Tags = PlantTags.Normalize(after.Tags);
        Normalize(after, numberChanged: after.CollectionNumber != before.CollectionNumber);
        // The day it died, left or came back is what the year in review reads
        if (before.Status != after.Status)
            after.StatusSince = time.Today();
        // What it learned about watering is from the way it was watered, so a new way starts over.
        // Without the pots only the medium and the water in the outer pot are seen.
        if (WateringForms.Of(before, pot) != WateringForms.Of(after, pot))
            after.WateringSince = time.Today();
        // Only a plant that left can take pots, and only the ones it has
        after.PotsTaken = after.HasLeft ? after.Fit(after.PotsTaken) : PotsTaken.None;
        // Only a plant still in the collection can be resting, kept apart or need something done
        if (!after.InCollection)
        {
            after.DormantSince = null;
            after.QuarantinedSince = null;
            after.QuarantineDays = null;
            after.Attention = null;
            after.AttentionLater = null;
        }
        // The cause only belongs to a plant that died
        after.CauseOfDeath = after.Status == PlantStatus.Died && !string.IsNullOrWhiteSpace(after.CauseOfDeath)
            ? after.CauseOfDeath.Trim()
            : null;
        // Who got it and what it went for only belong to a plant that was given away or sold
        var gone = after.Status is PlantStatus.GivenAway or PlantStatus.Sold;
        after.LeftTo = gone && !string.IsNullOrWhiteSpace(after.LeftTo) ? after.LeftTo.Trim() : null;
        after.LeftFor = gone && !string.IsNullOrWhiteSpace(after.LeftFor) ? after.LeftFor.Trim() : null;
        await plants.SaveAsync(after);

        await PartlySavedException.AfterSaveAsync(async () =>
        {
            var changes = PlantChanges.Describe(before, after, label, potName, mixName, placeName, feedName);
            if (changes.Count > 0)
                await AddChangeAsync(after.Id, string.Join("\n", changes));
            if (PlantNames.WasIdentified(before.NameQualifier, after.NameQualifier))
                await AddChangeAsync(after.Id, $"Identified as {after.BotanicalName ?? after.DisplayName}", TimelineEvent.Identified);
        });
    }

    /// <summary>Sets the same status on several plants, recording it on each.</summary>
    /// <param name="potsTaken">For plants given away or sold: which of their pots went with them.
    /// Each plant takes as much of that as its own pots allow.</param>
    public async Task SetStatusAsync(
        IEnumerable<Plant> selected,
        PlantStatus status,
        Func<Enum, string> label,
        PotsTaken potsTaken = PotsTaken.None)
    {
        var done = new HashSet<Guid>();
        foreach (var plant in selected.Where(p => p.Status != status))
        {
            if (!done.Add(plant.Id))
                continue;
            var before = plant.Copy();
            plant.Status = status;
            plant.PotsTaken = potsTaken;
            await UpdateAsync(before, plant, label);
            await CarryStatusToPottedWithAsync(plant, before.Status, label, done);
        }
    }

    /// <summary>
    /// The plants potted with this one get its status, so they leave and come back with it. They don't
    /// take pots with them. Used after the status of a plant was changed from <paramref name="statusBefore"/>.
    /// Only plants that had that status follow, so one that died on its own stays died when this one
    /// comes back.
    /// </summary>
    public async Task CarryStatusToPottedWithAsync(Plant plant, PlantStatus statusBefore, Func<Enum, string> label, ISet<Guid>? done = null)
    {
        foreach (var other in await plants.GetAllAsync())
        {
            if (other.PottedWithPlantId != plant.Id || other.Id == plant.Id || other.Status != statusBefore || other.Status == plant.Status)
                continue;
            if (done is not null && !done.Add(other.Id))
                continue;
            var before = other.Copy();
            other.Status = plant.Status;
            other.PotsTaken = PotsTaken.None;
            other.LeftTo = plant.LeftTo;
            other.LeftFor = plant.LeftFor;
            other.CauseOfDeath = plant.CauseOfDeath;
            await UpdateAsync(before, other, label);
        }
    }

    /// <summary>
    /// Deletes a plant. Plants potted with it are taken out first and get a place in the list of
    /// their own again, and a plant that is potted with another is taken out of that pot, so the
    /// other plant's count goes down.
    /// </summary>
    public async Task DeleteAsync(Plant plant, Func<Enum, string> label)
    {
        var today = time.Today();
        foreach (var other in (await plants.GetAllAsync()).Where(p => p.PottedWithPlantId == plant.Id && p.Id != plant.Id).ToList())
            await TakeOutAsync(other, today, null, label);
        if (await plants.GetAsync(plant.Id) is { PottedWithPlantId: not null } mine)
            await TakeOutAsync(mine, today, null, label);
        await plants.DeleteAsync(plant.Id);
    }

    /// <summary>
    /// Moves several plants to a room or spot, recording it on each like a room change on the
    /// edit form does. Plants that are already there or are deleted are left alone. Returns how
    /// many moved.
    /// </summary>
    /// <param name="placeName">Turns a place id into its name, so the history can say where from and to.</param>
    public async Task<int> MoveAsync(
        IEnumerable<Plant> selected,
        Guid? placeId,
        Func<Enum, string> label,
        Func<Guid, string?>? placeName = null)
    {
        var moved = 0;
        foreach (var plant in selected.Where(p => !p.IsDeleted && p.PlaceId != placeId))
        {
            var before = plant.Copy();
            plant.PlaceId = placeId;
            await UpdateAsync(before, plant, label, placeName: placeName);
            moved++;
        }

        return moved;
    }

    /// <summary>Whether a new plant starts in quarantine: the setting is on and it came from outside the home.</summary>
    public static bool StartsInQuarantine(PlantOrigin origin, bool settingOn) =>
        settingOn && origin is PlantOrigin.Purchased or PlantOrigin.Swap or PlantOrigin.Gift;

    /// <summary>True when a plant moved from an outdoor place to an indoor one.</summary>
    public static bool CameInside(Guid? from, Guid? to, Places places) =>
        places.IsOutdoors(from) && to is not null && !places.IsOutdoors(to);

    /// <summary>Puts a plant in quarantine from the given day, or takes it out with null.</summary>
    public async Task SetQuarantineAsync(Plant plant, DateOnly? since, Func<Enum, string> label, int? days = null)
    {
        if (plant.QuarantinedSince == since)
            return;

        var before = plant.Copy();
        plant.QuarantinedSince = since;
        plant.QuarantineDays = since is null ? null : plant.QuarantineDays ?? days ?? Plant.DefaultQuarantineDays;
        await UpdateAsync(before, plant, label);
    }

    /// <summary>True when changing the medium from one to the other is a move to semi-hydro.</summary>
    public static bool OffersTransition(GrowingMedium before, GrowingMedium after) =>
        before is GrowingMedium.Soil or GrowingMedium.Water && after is GrowingMedium.Leca or GrowingMedium.Pon;

    /// <summary>
    /// Starts tracking a move to LECA or PON. The steps start empty, so a second move starts clean.
    /// A check of the default length is stored as null.
    /// </summary>
    public async Task StartTransitionAsync(Plant plant, DateOnly since, GrowingMedium to, int? checkWeeks, Func<Enum, string> label)
    {
        var before = plant.Copy();
        plant.TransitionSince = since;
        plant.TransitionTo = to;
        plant.TransitionCheckWeeks = checkWeeks == Plant.DefaultTransitionCheckWeeks ? null : checkWeeks;
        plant.RootsWashedOn = null;
        plant.FirstWaterRootOn = null;
        plant.FirstNewLeafOn = null;
        await UpdateAsync(before, plant, label);
    }

    /// <summary>Sets the day of one step of the move, or clears it with null.</summary>
    public async Task SetTransitionStepAsync(Plant plant, TransitionStep step, DateOnly? on, Func<Enum, string> label)
    {
        var before = plant.Copy();
        switch (step)
        {
            case TransitionStep.RootsWashed: plant.RootsWashedOn = on; break;
            case TransitionStep.FirstWaterRoot: plant.FirstWaterRootOn = on; break;
            case TransitionStep.FirstNewLeaf: plant.FirstNewLeafOn = on; break;
        }

        if (before.RootsWashedOn == plant.RootsWashedOn && before.FirstWaterRootOn == plant.FirstWaterRootOn
            && before.FirstNewLeafOn == plant.FirstNewLeafOn)
            return;
        await UpdateAsync(before, plant, label);
    }

    /// <summary>Stops tracking a move that isn't settled, and clears its dates.</summary>
    public async Task StopTransitionAsync(Plant plant, Func<Enum, string> label)
    {
        if (!plant.InTransition)
            return;

        var before = plant.Copy();
        plant.TransitionSince = null;
        plant.TransitionTo = null;
        plant.TransitionCheckWeeks = null;
        plant.RootsWashedOn = null;
        plant.FirstWaterRootOn = null;
        plant.FirstNewLeafOn = null;
        await UpdateAsync(before, plant, label);
    }

    /// <summary>Marks a plant dormant from the given day, or woken up again with null.</summary>
    public async Task SetDormantAsync(Plant plant, DateOnly? since, Func<Enum, string> label)
    {
        if (plant.DormantSince == since)
            return;

        var before = plant.Copy();
        plant.DormantSince = since;
        await UpdateAsync(before, plant, label);
    }

    /// <summary>
    /// Flags a plant as needing something, e.g. "Repot soon", or clears the flag with null. It's
    /// only a reminder for Today, so nothing goes on the history. With <paramref name="from"/> (a
    /// month or a day) it stays off Today until then.
    /// </summary>
    public async Task SetAttentionAsync(Plant plant, string? reason, LooseDate? from = null)
    {
        plant.Attention = Attention.Change(plant.Attention, reason, time.Today());
        plant.AttentionLater = AttentionLater.For(plant.Attention, from);
        await plants.SaveAsync(plant);
    }

    /// <summary>Pins a plant to the top of the list, or unpins it. Nothing goes on the history.</summary>
    public async Task SetFavouriteAsync(Plant plant, bool favourite)
    {
        if (plant.Favourite == favourite)
            return;
        plant.Favourite = favourite;
        await plants.SaveAsync(plant);
    }

    /// <summary>
    /// Adds the same tags to several plants, e.g. "For swap" before a plant swap, and records
    /// it on each plant's history. Returns how many plants got something new.
    /// </summary>
    public async Task<int> AddTagsAsync(IEnumerable<Plant> selected, IEnumerable<string> tags)
    {
        var adding = PlantTags.Normalize(tags);
        var changed = 0;

        foreach (var plant in selected)
        {
            var updated = PlantTags.Normalize([.. plant.Tags, .. adding]);
            if (updated.Count == PlantTags.Normalize(plant.Tags).Count)
                continue;

            var added = new List<string>();
            PlantChanges.AddTags(added, plant.Tags, updated);
            plant.Tags = updated;
            await plants.SaveAsync(plant);
            await AddChangeAsync(plant.Id, string.Join("\n", added));
            changed++;
        }

        return changed;
    }

    /// <summary>
    /// Marks several plants as offered for swap, or clears the mark with null. Returns how many
    /// changed. Plants that are gone are left alone.
    /// </summary>
    public async Task<int> SetSpareAsync(IEnumerable<Plant> selected, SpareMark? mark)
    {
        var changed = 0;
        foreach (var plant in selected)
        {
            if (plant.IsDeleted || plant.Spare == mark)
                continue;
            plant.Spare = mark;
            await plants.SaveAsync(plant);
            changed++;
        }

        return changed;
    }

    /// <summary>
    /// Sets how several plants were raised, or clears it with null. Plants grown from seed or
    /// propagated here are left as they are, since how they were raised follows from that, and so are
    /// plants that are gone. Returns how many changed.
    /// </summary>
    public async Task<int> SetRaisedAsync(IEnumerable<Plant> selected, RaisedAs? raised)
    {
        var changed = 0;
        foreach (var plant in selected)
        {
            if (plant.IsDeleted || !plant.AsksHowRaised || plant.RaisedAs == raised)
                continue;
            plant.RaisedAs = raised;
            await plants.SaveAsync(plant);
            changed++;
        }

        return changed;
    }

    /// <summary>Plants tagged "For swap" (in any case) that have no spare mark yet, still here.</summary>
    public static IReadOnlyList<Plant> TaggedForSwap(IEnumerable<Plant> all) =>
        [.. all.Where(p => !p.IsDeleted && p.InCollection && p.Spare is null && PlantTags.Has(p, PlantTags.ForSwap))];

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

    /// <summary>The count to store: one is never stored.</summary>
    internal static int? Stored(int howMany) => howMany <= 1 ? null : howMany;

    private void CheckDate(DateOnly date)
    {
        if (date > time.Today())
            throw new InvalidOperationException("The date can't be in the future.");
    }

    private static void CheckSome(Plant plant, int count)
    {
        if (count < 1 || count >= plant.HowMany)
            throw new InvalidOperationException("Some of them has to be fewer than all of them, and at least one.");
    }

    /// <summary>
    /// Plant <paramref name="plant"/> goes into the pot of <paramref name="into"/>. It stays a record of its
    /// own with its history and lineage, but is no longer in the collection, and the other plant's count
    /// grows by its count. It takes the other plant's pot, medium, mix and room. Plants already potted with
    /// it are potted with the other plant now.
    /// </summary>
    public async Task PotTogetherAsync(
        Plant plant,
        Plant into,
        DateOnly date,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null)
    {
        CheckDate(date);
        if (plant.Id == into.Id)
            throw new InvalidOperationException("A plant can't be potted with itself.");
        if (!into.InCollection)
            throw new InvalidOperationException("The plant to pot it with isn't in the collection.");
        if (!plant.InCollection)
            throw new InvalidOperationException("Only a plant in the collection can be potted with another.");

        var before = plant.Copy();
        plant.PottedWithPlantId = into.Id;
        plant.PottedWithSince = date;
        plant.Medium = into.Medium;
        plant.InnerPotId = into.InnerPotId;
        plant.OuterPotId = into.OuterPotId;
        plant.WaterInOuterPot = into.WaterInOuterPot;
        plant.SoilMixId = into.SoilMixId;
        plant.PlaceId = into.PlaceId;
        await UpdateAsync(before, plant, label, potName, mixName, placeName);

        // Plants already potted with it are with the other plant now. Their count is inside its count.
        foreach (var other in await plants.GetAllAsync())
        {
            if (other.PottedWithPlantId != plant.Id || other.Id == plant.Id || other.Id == into.Id)
                continue;
            other.PottedWithPlantId = into.Id;
            await plants.SaveAsync(other);
        }

        into.Count = Stored(into.HowMany + plant.HowMany);
        await plants.SaveAsync(into);

        var at = time.MomentOn(date);
        await AddChangeAsync(plant.Id, $"Potted together with {into.DisplayName}", occurredAt: at, relatedPlantId: into.Id);
        await AddChangeAsync(into.Id, $"{plant.DisplayName} potted in with it, {into.HowMany} in the pot now", occurredAt: at, relatedPlantId: plant.Id);
    }

    /// <summary>
    /// Undoes <see cref="PotTogetherAsync"/>: the plant has its own pot again (<paramref name="innerPotId"/>,
    /// or none) and counts as a plant of its own. The other plant's count goes down by this one's, unless
    /// that plant is gone.
    /// </summary>
    public async Task TakeOutAsync(
        Plant plant,
        DateOnly date,
        Guid? innerPotId,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null)
    {
        CheckDate(date);
        if (plant.PottedWithPlantId is not { } otherId)
            throw new InvalidOperationException("It isn't potted with another plant.");
        if (plant.PottedWithSince is { } since && date < since)
            throw new InvalidOperationException("The date can't be before it was potted together.");

        // Only one step: a merge can leave two plants potted with each other
        var other = otherId == plant.Id ? null : await plants.GetAsync(otherId);

        var before = plant.Copy();
        plant.PottedWithPlantId = null;
        plant.PottedWithSince = null;
        plant.InnerPotId = innerPotId;
        plant.OuterPotId = null;
        plant.WaterInOuterPot = false;
        await UpdateAsync(before, plant, label, potName);

        var at = time.MomentOn(date);
        if (other is null)
        {
            await AddChangeAsync(plant.Id, "Taken out of the pot it shared", occurredAt: at);
            return;
        }

        other.Count = Stored(Math.Max(1, other.HowMany - plant.HowMany));
        await plants.SaveAsync(other);
        await AddChangeAsync(plant.Id, $"Taken out of {other.DisplayName}'s pot", occurredAt: at, relatedPlantId: other.Id);
        await AddChangeAsync(other.Id, $"{plant.DisplayName} taken out, {other.HowMany} in the pot now", occurredAt: at, relatedPlantId: plant.Id);
    }

    /// <summary>Some of the plants in the pot died. At least one is left.</summary>
    public async Task SomeDiedAsync(Plant plant, int count, DateOnly date)
    {
        CheckDate(date);
        CheckSome(plant, count);
        plant.Count = Stored(plant.HowMany - count);
        await plants.SaveAsync(plant);
        await AddChangeAsync(plant.Id, $"{count} died, {plant.HowMany} left", occurredAt: time.MomentOn(date));
    }

    /// <summary>Some of the plants in the pot were given away. At least one is left.</summary>
    public async Task GaveSomeAwayAsync(Plant plant, int count, string? to, DateOnly date)
    {
        CheckDate(date);
        CheckSome(plant, count);
        plant.Count = Stored(plant.HowMany - count);
        await plants.SaveAsync(plant);
        var text = string.IsNullOrWhiteSpace(to)
            ? $"Gave {count} away, {plant.HowMany} left"
            : $"Gave {count} to {to.Trim()}, {plant.HowMany} left";
        await AddChangeAsync(plant.Id, text, occurredAt: time.MomentOn(date));
    }

    /// <summary>
    /// Some of the plants in the pot got pots of their own. Each becomes a plant, with this one as its
    /// parent. At least one is left. Returns the new plants.
    /// </summary>
    public async Task<IReadOnlyList<Plant>> SplitOffAsync(Plant plant, int count, string? nickname, DateOnly date)
    {
        CheckDate(date);
        CheckSome(plant, count);

        var names = Propagations.PropagationService.PotUpNames(nickname, count, 0);
        var newPlants = names.Select(name =>
        {
            var split = plant.Duplicate();
            split.Nickname = name;
            split.ParentPlantId = plant.Id;
            split.AcquiredOn = LooseDate.Of(date);
            return split;
        }).ToList();
        if (newPlants.SelectMany(p => p.Validate(time.Today())).FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        var at = time.MomentOn(date);
        foreach (var split in newPlants)
        {
            await plants.SaveAsync(split);
            await timeline.AddAsync(new TimelineEntry
            {
                SubjectType = SubjectType.Plant,
                SubjectId = split.Id,
                Kind = TimelineKind.Created,
                OccurredAt = at,
                Text = $"Split off from {plant.DisplayName}",
                RelatedType = SubjectType.Plant,
                RelatedId = plant.Id
            });
        }

        plant.Count = Stored(plant.HowMany - count);
        await plants.SaveAsync(plant);
        await AddChangeAsync(plant.Id, $"Potted {count} up on their own: {string.Join(", ", newPlants.Select(p => p.DisplayName))}",
            occurredAt: at, relatedPlantId: newPlants.Count == 1 ? newPlants[0].Id : null);
        return newPlants;
    }

    private Task AddChangeAsync(Guid plantId, string text, TimelineEvent? @event = null, DateTimeOffset? occurredAt = null, Guid? relatedPlantId = null) =>
        timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plantId,
            Kind = TimelineKind.Change,
            OccurredAt = occurredAt ?? time.GetUtcNow(),
            Text = text,
            Event = @event,
            RelatedType = relatedPlantId is null ? null : SubjectType.Plant,
            RelatedId = relatedPlantId
        });

    // A day without a time is recorded at noon, or now for today, so a photo from earlier that
    // day would come before the plant was added. The plant comes in just before its first photo
    // then, but never on the day before
    private DateTimeOffset AddedAt(Plant plant, IReadOnlyList<Photo> photos) =>
        PhotoEntries.StartedAt(plant.AcquiredOn is { } acquired ? time.MomentOn(acquired.Start) : time.GetUtcNow(), photos, time);

    // A date that is only a month or a year cannot be read off the timeline's day headings,
    // so it is said in the text instead.
    private static string FirstEntryText(Plant plant)
    {
        var text = plant.Origin == PlantOrigin.Propagated ? "Added as a propagated plant" : "Added to collection";
        if (plant.AcquiredOn is { Precision: not DatePrecision.Day } acquired)
            text = $"{text}, {acquired.Text()}";
        return plant.InQuarantine ? $"{text}\nPut in quarantine" : text;
    }
}
