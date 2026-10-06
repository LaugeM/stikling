using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Timeline;

namespace Stikling.Core.Pots;

/// <summary>The pot library, with the plants counted against it. Mirrors how rooms are served.</summary>
public sealed class PotService(IPotRepository pots, IPlantRepository plants, IPhotoRepository photos)
{
    public async Task<IReadOnlyList<PotUse>> GetAllAsync() =>
        PotUse.List(await pots.GetAllAsync(), await plants.GetAllAsync());

    /// <summary>How many plants point at a pot, for the line before deleting it.</summary>
    public async Task<int> InUseAsync(Guid potId) =>
        (await plants.GetAllAsync()).Count(p => p.InnerPotId == potId || p.OuterPotId == potId);

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
