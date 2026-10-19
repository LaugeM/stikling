using Stikling.Core.Models;
using Stikling.Core.Sellers;

namespace Stikling.Core.Pots;

/// <summary>A container's name as typed: spaces tidied, compared without caring about case.</summary>
public static class ContainerName
{
    /// <summary>Trimmed, with runs of spaces collapsed to one. Null when nothing is written.</summary>
    public static string? Clean(string? name) => SellerName.Clean(name);

    public static bool Same(string? a, string? b) => SellerName.Same(a, b);

    /// <summary>
    /// The id a container made from this name gets, so two devices that adopt the same Container
    /// text make the same container. Must never change.
    /// </summary>
    public static Guid IdFor(string cleanedName) => NameBasedId.For("container:" + cleanedName.ToLowerInvariant());
}

/// <summary>
/// Every pot, for turning the container a propagation points at into a pot. Deleted pots are
/// kept, so a deleted container can be told apart from one never seen.
/// </summary>
public sealed class ContainerList
{
    private readonly Dictionary<Guid, Pot> byId = [];

    public ContainerList(IEnumerable<Pot> pots)
    {
        foreach (var pot in pots)
            byId[pot.Id] = pot;
    }

    public static ContainerList None { get; } = new([]);

    /// <summary>The containers for propagating that aren't deleted, oldest first.</summary>
    public IEnumerable<Pot> Live =>
        byId.Values.Where(IsLive).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id);

    private static bool IsLive(Pot pot) => !pot.IsDeleted && pot.ForPropagation;

    /// <summary>A live container by its name, ignoring case and spacing. The oldest wins if two share it.</summary>
    public Pot? Named(string? name) => Live.FirstOrDefault(p => ContainerName.Same(p.Name, name));

    /// <summary>True when a pot with this id is stored, live or deleted.</summary>
    public bool Has(Guid id) => byId.ContainsKey(id);

    /// <summary>
    /// The container a propagation is in. Its ContainerPotId when it has one: null then if that pot
    /// is gone or isn't a container. Without one, the live container named like its Container text,
    /// else the one at the id the text makes (a container that was renamed). Nothing is saved: a
    /// propagation only gets a ContainerPotId when the person edits it.
    /// </summary>
    public Pot? For(Guid? potId, string? text)
    {
        if (potId is { } id)
            return byId.TryGetValue(id, out var pot) && IsLive(pot) ? pot : null;
        if (ContainerName.Clean(text) is not { } name)
            return null;
        return Named(name) ?? (byId.TryGetValue(ContainerName.IdFor(name), out var stand) && IsLive(stand) ? stand : null);
    }
}
