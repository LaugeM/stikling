using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Pots;

/// <summary>What points at a pot: plants sitting in it, and propagations in it as their container.</summary>
public sealed record PotLinks(int Plants, int Propagations);

/// <summary>The container a form's text came to, and the text to keep in Container beside it.</summary>
public sealed record ContainerChoice(Guid? PotId, string? Name);

/// <summary>The pot library, with the plants counted against it. Mirrors how rooms are served.</summary>
public sealed class PotService(IPotRepository pots, IPlantRepository plants, IPhotoRepository photos, IPropagationRepository propagations)
{
    // Start-up, a restore and a sync can all ask at once, and two runs shouldn't both make a container
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<PotUse>> GetAllAsync() =>
        PotUse.List(await pots.GetAllAsync(), await plants.GetAllAsync(), await propagations.GetAllAsync());

    /// <summary>How many plants and propagations point at a pot, for the line before deleting it.</summary>
    public async Task<PotLinks> InUseAsync(Guid potId) =>
        new((await plants.GetAllAsync()).Count(p => p.InnerPotId == potId || p.OuterPotId == potId),
            (await propagations.GetAllAsync()).Count(p => p.ContainerPotId == potId));

    /// <summary>
    /// The container for the text in a propagation's Container field, made if there is none with
    /// that name. Nothing typed gives no container and no text. The container the propagation is
    /// already in counts when its name is what was typed.
    /// </summary>
    public async Task<ContainerChoice> ChooseContainerAsync(string? text, Guid? currentId = null)
    {
        if (ContainerName.Clean(text) is not { } name)
            return new ContainerChoice(null, null);

        var all = await pots.GetContainersAsync();
        if (all.For(currentId, null) is { } current && ContainerName.Same(current.Name, name))
            return new ContainerChoice(current.Id, current.Name);
        if (all.Named(name) is { } named)
            return new ContainerChoice(named.Id, named.Name);

        // The same id on every device, unless that id is taken by a pot that was renamed or deleted
        var id = ContainerName.IdFor(name);
        var made = new Pot
        {
            Id = all.Has(id) ? Guid.NewGuid() : id,
            Group = PotGroup.Standalone,
            ForPropagation = true,
            Name = name,
            Owned = 1,
            Material = PotMaterial.Plastic
        };
        await pots.SaveAsync(made);
        return new ContainerChoice(made.Id, made.Name);
    }

    /// <summary>
    /// Makes sure a container exists for each name written in the Container of propagations that
    /// have none linked. It only makes containers: no propagation is saved, since two devices doing
    /// that would overwrite each other's edits. A container made from a name gets the id the name
    /// makes, so every device makes the same one, and owns as many as there are propagations still
    /// going in it. A name that already has a live container, or whose id belongs to a deleted pot,
    /// is left alone. Safe to run again, and cheap when there is nothing to do. Returns how many
    /// containers it made.
    /// </summary>
    public async Task<int> AdoptContainersAsync()
    {
        await gate.WaitAsync();
        try
        {
            var unlinked = (await propagations.GetAllAsync())
                .Where(p => !p.IsDeleted && p.ContainerPotId is null && ContainerName.Clean(p.Container) is not null)
                .ToList();
            if (unlinked.Count == 0)
                return 0;

            var all = await pots.GetContainersAsync();
            var made = 0;
            foreach (var group in unlinked.GroupBy(p => ContainerName.Clean(p.Container)!, StringComparer.OrdinalIgnoreCase))
            {
                var name = group.Key;
                var id = ContainerName.IdFor(name);
                if (all.For(null, name) is not null || all.Has(id))
                    continue;
                await pots.SaveAsync(new Pot
                {
                    Id = id,
                    Group = PotGroup.Standalone,
                    ForPropagation = true,
                    Name = name,
                    Owned = Math.Max(1, group.Count(p => p.IsActive)),
                    Material = PotMaterial.Plastic
                });
                made++;
            }
            return made;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The photo that stands for each pot that has one, by the pot's id.</summary>
    public async Task<IReadOnlyDictionary<Guid, Photo>> GetCoversAsync() =>
        PotPhotos.Covers(await pots.GetAllAsync(), await photos.GetOfKindAsync(SubjectType.Pot));

    /// <summary>A pot's photos, newest first.</summary>
    public Task<IReadOnlyList<Photo>> GetPhotosAsync(Guid potId) => photos.GetForAsync(potId);

    /// <summary>Shows this photo for the pot from now on.</summary>
    public async Task SetCoverAsync(Pot pot, Photo photo)
    {
        pot.CoverPhotoId = photo.Id;
        await pots.SaveAsync(pot);
    }

    /// <summary>
    /// Soft-deletes the pot and its photos, so they stop taking up photo space. Plants that point
    /// at the pot keep the link, the way a deleted parent works. Returns the photos deleted, whose
    /// image data the caller frees on the device.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> DeleteAsync(Guid potId)
    {
        var own = await photos.GetForAsync(potId);
        foreach (var photo in own)
            await photos.DeleteAsync(photo.Id);
        await pots.DeleteAsync(potId);
        return own.Select(p => p.Id).ToList();
    }
}
