using Stikling.Core.Models;
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
    /// its history on the days they were taken, and the newest one becomes the cover.
    /// </summary>
    public async Task CreateAsync(Plant plant, IReadOnlyList<Photo>? photos = null)
    {
        if (photos is { Count: > 0 })
            plant.CoverPhotoId ??= PhotoEntries.Cover(photos).Id;

        plant.Tags = PlantTags.Normalize(plant.Tags);
        await plants.SaveAsync(plant);
        await timeline.AddAsync(new TimelineEntry
        {
            SubjectType = SubjectType.Plant,
            SubjectId = plant.Id,
            Kind = TimelineKind.Created,
            OccurredAt = AddedAt(plant, photos ?? []),
            Text = FirstEntryText(plant)
        });

        foreach (var entry in PhotoEntries.For(SubjectType.Plant, plant.Id, photos ?? [], time))
            await timeline.AddAsync(entry);
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

    /// <summary>Saves an edited plant and records what changed (status, room, medium, pot).</summary>
    /// <param name="potName">Turns a pot id into its name, so the history can say which pot.</param>
    /// <param name="placeName">The same for the room or spot, so the history can say where it went.</param>
    public async Task UpdateAsync(
        Plant before,
        Plant after,
        Func<Enum, string> label,
        Func<Guid, string?>? potName = null,
        Func<Guid, string?>? mixName = null,
        Func<Guid, string?>? placeName = null)
    {
        after.Tags = PlantTags.Normalize(after.Tags);
        // Only a plant that left can take pots, and only the ones it has
        after.PotsTaken = after.HasLeft ? after.Fit(after.PotsTaken) : PotsTaken.None;
        // Only a plant still in the collection can be resting, kept apart or need something done
        if (after.Status != PlantStatus.Active)
        {
            after.DormantSince = null;
            after.QuarantinedSince = null;
            after.QuarantineDays = null;
            after.Attention = null;
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

        var changes = PlantChanges.Describe(before, after, label, potName, mixName, placeName);
        if (changes.Count > 0)
            await AddChangeAsync(after.Id, string.Join("\n", changes));
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
        foreach (var plant in selected.Where(p => p.Status != status))
        {
            var before = plant.Copy();
            plant.Status = status;
            plant.PotsTaken = potsTaken;
            await UpdateAsync(before, plant, label);
        }
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

    /// <summary>Puts a plant in quarantine from the given day, or takes it out with null.</summary>
    public async Task SetQuarantineAsync(Plant plant, DateOnly? since, Func<Enum, string> label)
    {
        if (plant.QuarantinedSince == since)
            return;

        var before = plant.Copy();
        plant.QuarantinedSince = since;
        plant.QuarantineDays = since is null ? null : plant.QuarantineDays ?? Plant.DefaultQuarantineDays;
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
    /// only a reminder for Today, so nothing goes on the history.
    /// </summary>
    public async Task SetAttentionAsync(Plant plant, string? reason)
    {
        plant.Attention = Attention.Change(plant.Attention, reason, time.Today());
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

    // A day without a time is recorded at noon, or now for today, so a photo from earlier that
    // day would come before the plant was added. The plant comes in just before its first photo
    // then, but never on the day before
    private DateTimeOffset AddedAt(Plant plant, IReadOnlyList<Photo> photos)
    {
        var added = plant.AcquiredOn is { } acquired ? time.MomentOn(acquired.Start) : time.GetUtcNow();
        var day = time.LocalDay(added);
        var first = photos.Where(p => time.LocalDay(p.TakenAt) == day).Select(p => p.TakenAt).DefaultIfEmpty(added).Min();
        if (first >= added)
            return added;
        var startOfDay = time.MomentAt(day.ToDateTime(TimeOnly.MinValue));
        return first.AddSeconds(-1) < startOfDay ? first : first.AddSeconds(-1);
    }

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
