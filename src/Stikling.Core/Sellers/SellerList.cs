using Stikling.Core.Models;

namespace Stikling.Core.Sellers;

/// <summary>A seller's name as typed: spaces tidied, compared without caring about case.</summary>
public static class SellerName
{
    /// <summary>Trimmed, with runs of spaces collapsed to one. Null when nothing is written.</summary>
    public static string? Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool Same(string? a, string? b) =>
        Clean(a) is { } x && string.Equals(x, Clean(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The id a seller made from this name gets, so two devices that adopt the same Source text
    /// make the same seller. Must never change.
    /// </summary>
    public static Guid IdFor(string cleanedName) => NameBasedId.For("seller:" + cleanedName.ToLowerInvariant());

    /// <summary>The kind a plant's origin suggests.</summary>
    public static SellerKind Guess(PlantOrigin origin) => origin switch
    {
        PlantOrigin.Purchased => SellerKind.Shop,
        PlantOrigin.Gift or PlantOrigin.Swap => SellerKind.Person,
        _ => SellerKind.Other
    };
}

/// <summary>
/// Every seller, for turning the one a plant points at into a name. Sellers merged into another
/// are kept, and following one leads to the seller it went into. A seller deleted without a merge
/// is kept too, so it can be told apart from one never seen.
/// </summary>
public sealed class SellerList
{
    private readonly Dictionary<Guid, Seller> byId = [];

    public SellerList(IEnumerable<Seller> sellers)
    {
        foreach (var seller in sellers)
            byId[seller.Id] = seller;
    }

    public static SellerList None { get; } = new([]);

    /// <summary>The sellers nothing else stands in for, oldest first, so the first of two with one name stays first.</summary>
    public IEnumerable<Seller> Live =>
        byId.Values.Where(s => !s.IsDeleted).OrderBy(s => s.CreatedAt).ThenBy(s => s.Id);

    /// <summary>
    /// The seller with this id, or the one it was merged into. Null for no id, an unknown one,
    /// and a seller that was deleted without being merged.
    /// </summary>
    /// <remarks>
    /// Two devices can merge the same two sellers in opposite directions, so each can end up deleted
    /// and pointing at the other. The oldest in such a loop is the one kept, which every device
    /// works out the same way.
    /// </remarks>
    public Seller? Find(Guid? id)
    {
        var path = new List<Seller>();
        while (id is { } current)
        {
            if (!byId.TryGetValue(current, out var seller))
                return null;
            if (!seller.IsDeleted)
                return seller;
            if (seller.MergedIntoId is null)
                return null;

            var seen = path.IndexOf(seller);
            if (seen >= 0)
                return path[seen..].OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).First();

            path.Add(seller);
            id = seller.MergedIntoId;
        }
        return null;
    }

    /// <summary>A live seller by its name, ignoring case and spacing. The oldest wins if two share it.</summary>
    public Seller? Named(string? name) => Live.FirstOrDefault(s => SellerName.Same(s.Name, name));

    /// <summary>True when a record with this id is stored, live, merged or deleted.</summary>
    public bool Has(Guid id) => byId.ContainsKey(id);

    public string? NameOf(Guid? id) => Find(id)?.Name;

    /// <summary>
    /// The seller a plant or propagation comes from. Its SellerId when it has one, following a
    /// merge; null then if that seller is gone. Without one, the live seller named like its Source,
    /// else the one at the id the Source makes (a seller that was renamed). Null when none, and the
    /// Source text is all there is to show. Nothing is saved: a record only gets a SellerId when
    /// the person edits it.
    /// </summary>
    public Seller? For(Guid? sellerId, string? source)
    {
        if (sellerId is not null)
            return Find(sellerId);
        if (SellerName.Clean(source) is not { } name)
            return null;
        return Named(name) ?? Find(SellerName.IdFor(name));
    }
}
