using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;

namespace Stikling.Core.Sellers;

/// <summary>The seller a form's "Where from" text came to, and the text to keep in Source beside it.</summary>
public sealed record SellerChoice(Guid? SellerId, string? Source);

/// <summary>
/// Sellers: finding or making the one a form typed in, renaming, merging and deleting, and
/// turning the Source text of older plants into sellers. A plant keeps the seller's name in its
/// Source text as well, so older versions and the CSV show it.
/// </summary>
public sealed class SellerService(ISellerRepository sellers, IPlantRepository plants, IPropagationRepository propagations)
{
    // Start-up, a restore and a sync can all ask at once, and two runs shouldn't both make a seller
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<SellerList> GetSellersAsync() => sellers.GetSellersAsync();

    /// <summary>
    /// The seller for the text in a "Where from" field, made if there is none with that name.
    /// Nothing typed gives no seller and no Source. The seller the record has already counts when
    /// its name is what was typed.
    /// </summary>
    public async Task<SellerChoice> ChooseAsync(string? text, SellerKind kindIfNew, Guid? currentId = null)
    {
        if (SellerName.Clean(text) is not { } name)
            return new SellerChoice(null, null);

        var all = await sellers.GetSellersAsync();
        var seller = all.Find(currentId) is { } current && SellerName.Same(current.Name, name) ? current
            : all.Named(name) ?? await AddAsync(all, name, kindIfNew);
        return new SellerChoice(seller.Id, seller.Name);
    }

    private async Task<Seller> AddAsync(SellerList all, string name, SellerKind kind)
    {
        // The same id on every device, unless that id is taken by a seller that was renamed or deleted
        var id = SellerName.IdFor(name);
        var seller = new Seller { Id = all.Has(id) ? Guid.NewGuid() : id, Name = name, Kind = kind };
        await sellers.SaveAsync(seller);
        return seller;
    }

    /// <summary>
    /// Gives every plant and propagation that has Source text but no seller one, the seller of that
    /// name. Sellers are found by name first, then by the id the name makes, so a seller renamed on
    /// another device is still the one used. Source is not changed. Safe to run again, and cheap
    /// when there is nothing to do. Returns how many records got a seller.
    /// </summary>
    public async Task<int> AdoptSourcesAsync()
    {
        await gate.WaitAsync();
        try
        {
            var plantList = (await plants.GetAllAsync()).Where(p => p.SellerId is null && SellerName.Clean(p.Source) is not null).ToList();
            var propagationList = (await propagations.GetAllAsync()).Where(p => p.SellerId is null && SellerName.Clean(p.Source) is not null).ToList();
            if (plantList.Count == 0 && propagationList.Count == 0)
                return 0;

            var all = await sellers.GetSellersAsync();
            // One lookup per name, null for a seller that was deleted on purpose, which stays deleted
            var resolved = new Dictionary<string, Seller?>(StringComparer.OrdinalIgnoreCase);

            async Task<Seller?> ResolveAsync(string text, SellerKind kind)
            {
                var name = SellerName.Clean(text)!;
                if (resolved.TryGetValue(name, out var known))
                    return known;

                var seller = all.Named(name);
                if (seller is null)
                {
                    var id = SellerName.IdFor(name);
                    if (all.Has(id))
                        seller = all.Find(id);
                    else
                    {
                        seller = new Seller { Id = id, Name = name, Kind = kind };
                        await sellers.SaveAsync(seller);
                    }
                }
                return resolved[name] = seller;
            }

            var adopted = 0;
            foreach (var plant in plantList)
            {
                if (await ResolveAsync(plant.Source!, SellerName.Guess(plant.Origin)) is not { } seller)
                    continue;
                plant.SellerId = seller.Id;
                if (await TrySaveAsync(() => plants.SaveAsync(plant)))
                    adopted++;
            }
            foreach (var propagation in propagationList)
            {
                if (await ResolveAsync(propagation.Source!, SellerKind.Other) is not { } seller)
                    continue;
                propagation.SellerId = seller.Id;
                if (await TrySaveAsync(() => propagations.SaveAsync(propagation)))
                    adopted++;
            }
            return adopted;
        }
        finally
        {
            gate.Release();
        }
    }

    // A record that no longer validates (old data) is left as it was, and tried again next time
    private static async Task<bool> TrySaveAsync(Func<Task> save)
    {
        try
        {
            await save();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Changes a seller. A new name goes into the Source text of everything from it.</summary>
    public async Task UpdateAsync(Guid id, string? name, SellerKind kind, string? notes)
    {
        var all = await sellers.GetSellersAsync();
        if (all.Find(id) is not { } seller || seller.Id != id)
            throw new ArgumentException("That seller isn't there any more.", nameof(id));

        var cleaned = SellerName.Clean(name);
        var changed = new Seller { Name = cleaned ?? "" };
        if (changed.Validate().FirstOrDefault() is { } error)
            throw new ArgumentException(error, nameof(name));

        var renamed = seller.Name != cleaned;
        seller.Name = cleaned!;
        seller.Kind = kind;
        seller.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await sellers.SaveAsync(seller);

        if (renamed)
            await PointAtAsync(all, seller.Id, seller.Id, seller.Name);
    }

    /// <summary>
    /// Merges a seller into another: everything from it now comes from the other, with its name in
    /// Source, and this one is deleted but kept, pointing at where it went.
    /// </summary>
    public async Task MergeAsync(Guid fromId, Guid intoId)
    {
        var all = await sellers.GetSellersAsync();
        if (all.Find(fromId) is not { } from || from.Id != fromId)
            throw new ArgumentException("That seller isn't there any more.", nameof(fromId));
        if (all.Find(intoId) is not { } into || into.Id != intoId)
            throw new ArgumentException("That seller isn't there any more.", nameof(intoId));
        if (from.Id == into.Id)
            throw new ArgumentException("Pick another seller to merge into.", nameof(intoId));

        // What points at it (or at one merged into it) is worked out before it stops being live
        await PointAtAsync(all, from.Id, into.Id, into.Name);

        from.MergedIntoId = into.Id;
        await sellers.SaveAsync(from);
        await sellers.DeleteAsync(from.Id);
    }

    /// <summary>Deletes a seller without a merge. Plants from it lose the link and keep their Source text.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var all = await sellers.GetSellersAsync();
        if (all.Find(id) is not { } seller || seller.Id != id)
            return;

        foreach (var plant in (await plants.GetAllAsync()).Where(p => all.Find(p.SellerId)?.Id == id))
        {
            plant.SellerId = null;
            await TrySaveAsync(() => plants.SaveAsync(plant));
        }
        foreach (var propagation in (await propagations.GetAllAsync()).Where(p => all.Find(p.SellerId)?.Id == id))
        {
            propagation.SellerId = null;
            await TrySaveAsync(() => propagations.SaveAsync(propagation));
        }

        await sellers.DeleteAsync(id);

        // Adopting looks for a seller by the id its Source text makes. When this one has another
        // id (it was renamed), a deleted stand-in there keeps the text from making it again.
        var standIn = SellerName.IdFor(seller.Name);
        if (standIn != id && !all.Has(standIn))
        {
            await sellers.SaveAsync(new Seller { Id = standIn, Name = seller.Name });
            await sellers.DeleteAsync(standIn);
        }
    }

    // Plants and propagations that come from the seller get the other one's id and name
    private async Task PointAtAsync(SellerList all, Guid fromId, Guid toId, string name)
    {
        foreach (var plant in (await plants.GetAllAsync()).Where(p => all.Find(p.SellerId)?.Id == fromId))
        {
            if (plant.SellerId == toId && plant.Source == name)
                continue;
            plant.SellerId = toId;
            plant.Source = name;
            await TrySaveAsync(() => plants.SaveAsync(plant));
        }
        foreach (var propagation in (await propagations.GetAllAsync()).Where(p => all.Find(p.SellerId)?.Id == fromId))
        {
            if (propagation.SellerId == toId && propagation.Source == name)
                continue;
            propagation.SellerId = toId;
            propagation.Source = name;
            await TrySaveAsync(() => propagations.SaveAsync(propagation));
        }
    }
}
