using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Fields;

/// <summary>The fields people add to their plants: adding, renaming, their choices, values and removing.</summary>
public sealed class OwnFieldService(IOwnFieldRepository fields, IPlantRepository plants)
{
    /// <summary>The fields that aren't deleted, by name.</summary>
    public async Task<IReadOnlyList<OwnField>> GetAllAsync() =>
        [.. (await fields.GetAllAsync()).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Adds a field, or returns the one that is already there. The same name twice is one field. A
    /// removed field with that name comes back with these settings, and the plants that had a value
    /// show it again. The id comes from the name, so two devices adding "Clone name" make the same
    /// record, unless a field was renamed and still holds that id, then the new one gets an id of its own.
    /// </summary>
    public async Task<OwnField> AddAsync(string name, OwnFieldKind kind, IEnumerable<string>? choices = null)
    {
        var trimmed = (name ?? "").Trim();
        var field = new OwnField { Name = trimmed, Kind = kind, Choices = TidyChoices(choices) };
        if (field.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        var stored = await fields.GetStoredAsync();
        if (stored.FirstOrDefault(f => !f.IsDeleted && SameName(f.Name, trimmed)) is { } existing)
            return existing;

        var id = OwnField.IdFor(trimmed);
        var holder = stored.FirstOrDefault(f => f.Id == id);

        // A removed field is found by the name it had when it was removed, which isn't the one its id
        // came from if it was renamed. One that synced from another device has lost its name, so the
        // removed field holding the id is taken to be it.
        var removed = stored.FirstOrDefault(f => f.IsDeleted && SameName(f.Name, trimmed))
            ?? (holder is { IsDeleted: true } && string.IsNullOrEmpty(holder.Name) ? holder : null);
        if (removed is not null)
        {
            removed.DeletedAt = null;
            removed.Name = trimmed;
            removed.Kind = kind;
            removed.Choices = field.Choices;
            await fields.SaveAsync(removed);
            return removed;
        }

        // A field holding the id has been renamed since, so this one can't share it
        field.Id = holder is null ? id : Guid.NewGuid();
        await fields.SaveAsync(field);
        return field;
    }

    /// <summary>Renames a field. It keeps its id, so the values on the plants stay with it.</summary>
    public async Task RenameAsync(Guid id, string name)
    {
        var field = await fields.GetAsync(id) ?? throw Gone();
        var trimmed = (name ?? "").Trim();
        if (field.Name == trimmed)
            return;
        var renamed = field.Copy();
        renamed.Name = trimmed;
        if (renamed.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        if ((await fields.GetAllAsync()).Any(f => f.Id != id && SameName(f.Name, trimmed)))
            throw new InvalidOperationException($"You already have a field called {trimmed}.");

        await fields.SaveAsync(renamed);
    }

    /// <summary>
    /// Changes what the field holds. Values on plants are never changed, so one that doesn't fit the
    /// new kind stays and shows as written. A field that becomes a choice with no choices yet gets
    /// the values already in use as its choices.
    /// </summary>
    public async Task SetKindAsync(Guid id, OwnFieldKind kind)
    {
        var field = await fields.GetAsync(id) ?? throw Gone();
        if (field.Kind == kind)
            return;
        var changed = field.Copy();
        changed.Kind = kind;
        if (kind == OwnFieldKind.Choice && changed.Choices.Count == 0)
        {
            changed.Choices = [.. (await plants.GetAllAsync())
                .Select(p => p.OwnFields.TryGetValue(id, out var value) ? value.Trim() : "")
                .Where(v => v.Length > 0 && v.Length <= OwnField.MaxNameLength)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase)
                .Take(OwnField.MaxChoices)];
        }
        await fields.SaveAsync(changed);
    }

    /// <summary>Adds a choice to a choice field. One that is already there (ignoring case) is left as it is.</summary>
    public async Task AddChoiceAsync(Guid id, string choice)
    {
        var field = await fields.GetAsync(id) ?? throw Gone();
        var trimmed = (choice ?? "").Trim();
        if (field.Choices.Any(c => SameName(c, trimmed)))
            return;
        var changed = field.Copy();
        changed.Choices.Add(trimmed);
        if (changed.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);
        await fields.SaveAsync(changed);
    }

    /// <summary>Takes a choice off the list. Plants that have it keep the value.</summary>
    public async Task RemoveChoiceAsync(Guid id, string choice)
    {
        var field = await fields.GetAsync(id) ?? throw Gone();
        var changed = field.Copy();
        if (changed.Choices.RemoveAll(c => SameName(c, choice)) == 0)
            return;
        await fields.SaveAsync(changed);
    }

    /// <summary>
    /// Renames a choice and rewrites it on every plant that has it. When the new name is already a
    /// choice, the old one is dropped from the list, so the two become one. Returns how many plants changed.
    /// </summary>
    public async Task<int> RenameChoiceAsync(Guid id, string from, string to)
    {
        var field = await fields.GetAsync(id) ?? throw Gone();
        var target = (to ?? "").Trim();
        if (target.Length == 0)
            throw new InvalidOperationException("A choice can't be empty.");
        if (target.Length > OwnField.MaxNameLength)
            throw new InvalidOperationException($"A choice can be at most {OwnField.MaxNameLength} characters.");

        var changed = field.Copy();
        var index = changed.Choices.FindIndex(c => SameName(c, from));
        var existing = changed.Choices.FindIndex(c => SameName(c, target));
        var merge = existing >= 0 && existing != index;
        if (index >= 0)
        {
            if (merge)
                changed.Choices.RemoveAt(index);
            else
                changed.Choices[index] = target;
            await fields.SaveAsync(changed);
        }

        // When the new name is already a choice, its own spelling is the one plants get
        var spelling = merge ? field.Choices[existing] : target;
        var count = 0;
        foreach (var plant in await plants.GetAllAsync())
        {
            if (!plant.OwnFields.TryGetValue(id, out var value) || !SameName(value, from) || value == spelling)
                continue;
            var rewritten = plant.Copy();
            rewritten.OwnFields[id] = spelling;
            await plants.SaveAsync(rewritten);
            count++;
        }
        return count;
    }

    /// <summary>Removes a field. The values on plants are kept, and show again if a field with this name is added.</summary>
    public Task RemoveAsync(Guid id) => fields.DeleteAsync(id);

    /// <summary>How many live plants have a value for the field.</summary>
    public async Task<int> UseCountAsync(Guid id) =>
        (await plants.GetAllAsync()).Count(p => p.OwnFields.TryGetValue(id, out var v) && v.Length > 0);

    /// <summary>How many live plants have a value that doesn't read as <paramref name="kind"/>.</summary>
    public async Task<int> NotFittingCountAsync(Guid id, OwnFieldKind kind) =>
        (await plants.GetAllAsync()).Count(p =>
            p.OwnFields.TryGetValue(id, out var v) && v.Length > 0 && !OwnFieldValues.Fits(kind, v));

    /// <summary>
    /// Sets the field on these plants, or clears it when the value is empty. The value is tidied for
    /// the kind of the field and throws if it doesn't fit. Returns how many plants changed.
    /// </summary>
    public async Task<int> SetValueAsync(IEnumerable<Guid> plantIds, Guid fieldId, string? value)
    {
        var field = await fields.GetAsync(fieldId) ?? throw Gone();
        var stored = OwnFieldValues.Normalize(field.Kind, value, out var error);
        if (error is not null)
            throw new InvalidOperationException(error);

        var ids = plantIds.ToHashSet();
        var changed = 0;
        foreach (var plant in await plants.GetAllAsync())
        {
            if (!ids.Contains(plant.Id))
                continue;
            plant.OwnFields.TryGetValue(fieldId, out var current);
            if (stored is null ? current is null : current == stored)
                continue;
            var rewritten = plant.Copy();
            if (stored is null)
                rewritten.OwnFields.Remove(fieldId);
            else
                rewritten.OwnFields[fieldId] = stored;
            await plants.SaveAsync(rewritten);
            changed++;
        }
        return changed;
    }

    /// <summary>Choices trimmed, with the empty ones and the repeats (ignoring case) dropped.</summary>
    public static List<string> TidyChoices(IEnumerable<string>? choices) =>
        [.. (choices ?? []).Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase)];

    private static InvalidOperationException Gone() => new("That field is gone.");

    private static bool SameName(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
