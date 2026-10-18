using Stikling.Core.Models;

namespace Stikling.Core.Care;

/// <summary>The own kinds of care: adding, renaming, what they count as, and removing.</summary>
public sealed class OwnCareKindService(IOwnCareKindRepository kinds, ICareLogRepository logs)
{
    /// <summary>The kinds that aren't deleted, by name.</summary>
    public async Task<IReadOnlyList<OwnCareKind>> GetAllAsync() =>
        [.. (await kinds.GetAllAsync()).OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Adds a kind, or returns the one that is already there. The same name twice is one kind. A
    /// removed kind with that name comes back with these settings. The id comes from the name, so
    /// two devices adding "Misted" make the same record, unless a kind was renamed and still holds
    /// that id, then the new one gets an id of its own.
    /// </summary>
    public async Task<OwnCareKind> AddAsync(string name, CareKind? countsAs, bool onHistory)
    {
        var trimmed = (name ?? "").Trim();
        var kind = new OwnCareKind { Name = trimmed, CountsAs = countsAs, OnHistory = onHistory };
        if (kind.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        var stored = await kinds.GetStoredAsync();
        if (stored.FirstOrDefault(k => !k.IsDeleted && SameName(k.Name, trimmed)) is { } existing)
            return existing;

        var id = OwnCareKind.IdFor(trimmed);
        var holder = stored.FirstOrDefault(k => k.Id == id);
        if (holder is { IsDeleted: true })
        {
            holder.DeletedAt = null;
            holder.Name = trimmed;
            holder.CountsAs = countsAs;
            holder.OnHistory = onHistory;
            await kinds.SaveAsync(holder);
            return holder;
        }

        // A live kind holding the id has been renamed since, so this one can't share it
        kind.Id = holder is null ? id : Guid.NewGuid();
        await kinds.SaveAsync(kind);
        return kind;
    }

    /// <summary>Renames a kind. It keeps its id, and entries logged before show the new name.</summary>
    public async Task RenameAsync(Guid id, string name)
    {
        var kind = await kinds.GetAsync(id) ?? throw new InvalidOperationException("That kind of care is gone.");
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Give the kind of care a name.");
        if (kind.Name == trimmed)
            return;
        if ((await kinds.GetAllAsync()).Any(k => k.Id != id && SameName(k.Name, trimmed)))
            throw new InvalidOperationException($"You already have a kind of care called {trimmed}.");

        kind.Name = trimmed;
        await kinds.SaveAsync(kind);
    }

    public async Task SetOnHistoryAsync(Guid id, bool onHistory)
    {
        var kind = await kinds.GetAsync(id) ?? throw new InvalidOperationException("That kind of care is gone.");
        if (kind.OnHistory == onHistory)
            return;
        kind.OnHistory = onHistory;
        await kinds.SaveAsync(kind);
    }

    /// <summary>
    /// Changes what a kind counts as. That only decides new entries, unless <paramref name="alsoChangeEntries"/>
    /// is set, then the live entries of this kind are rewritten too (through the repository, so they sync).
    /// An entry that carries products, a feed, water or a reading can't become a plain one, so it is left
    /// as it is. Returns how many entries were changed.
    /// </summary>
    public async Task<int> SetCountsAsAsync(Guid id, CareKind? countsAs, bool alsoChangeEntries)
    {
        var kind = await kinds.GetAsync(id) ?? throw new InvalidOperationException("That kind of care is gone.");
        var changedKind = kind.CountsAs != countsAs;
        kind.CountsAs = countsAs;
        if (kind.Validate().FirstOrDefault() is { } error)
            throw new InvalidOperationException(error);

        if (changedKind)
            await kinds.SaveAsync(kind);
        if (!alsoChangeEntries)
            return 0;

        var changed = 0;
        foreach (var entry in (await logs.GetAllAsync()).Where(l => l.OwnKindId == id && l.Kind != kind.LogKind))
        {
            var rewritten = entry.Copy();
            rewritten.Kind = kind.LogKind;
            if (rewritten.Validate(DateOnly.MaxValue).Count > 0)
                continue;
            await logs.SaveAsync(rewritten);
            changed++;
        }
        return changed;
    }

    /// <summary>How many live entries were logged with this kind.</summary>
    public async Task<int> EntryCountAsync(Guid id) =>
        (await logs.GetAllAsync()).Count(l => l.OwnKindId == id);

    /// <summary>Removes a kind. Entries already logged are left alone and show the name they were logged with.</summary>
    public Task RemoveAsync(Guid id) => kinds.DeleteAsync(id);

    private static bool SameName(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
