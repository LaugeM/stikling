using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Wishlist;

/// <summary>What a page does with wishes. Everything goes through the repository, so dates are set.</summary>
public sealed class WishService(IWishRepository wishes, IPhotoRepository photos, TimeProvider time)
{
    public Task<IReadOnlyList<Wish>> GetAllAsync() => wishes.GetAllAsync();

    public Task SaveAsync(Wish wish) => wishes.SaveAsync(wish);

    /// <summary>
    /// Soft-deletes the wish and its photos, the way a pot goes with its photos. Returns the photos
    /// deleted, whose image data the caller frees on the device.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> DeleteAsync(Guid id)
    {
        var own = await photos.GetForAsync(id);
        foreach (var photo in own)
            await photos.DeleteAsync(photo.Id);
        await wishes.DeleteAsync(id);
        return own.Select(p => p.Id).ToList();
    }

    /// <summary>A wish's photos, newest first.</summary>
    public Task<IReadOnlyList<Photo>> GetPhotosAsync(Guid wishId) => photos.GetForAsync(wishId);

    /// <summary>The newest photo of each wish that has one, by the wish's id.</summary>
    public async Task<IReadOnlyDictionary<Guid, Photo>> GetNewestPhotosAsync() =>
        (await photos.GetOfKindAsync(SubjectType.Wish))
            .GroupBy(p => p.SubjectId)
            .ToDictionary(g => g.Key, g => g.MaxBy(p => p.TakenAt)!);

    /// <summary>
    /// Moves a wish to Got, with the plant or propagation it became. A wish that is gone or already
    /// got is left as it is, so a form saved twice, or on two devices, doesn't change it again.
    /// </summary>
    public async Task<bool> MarkGotAsync(Guid wishId, Guid? plantId, Guid? propagationId)
    {
        if (await wishes.GetAsync(wishId) is not { IsGot: false } wish)
            return false;

        wish.GotOn = time.Today();
        wish.PlantId = plantId;
        wish.PropagationId = propagationId;
        await wishes.SaveAsync(wish);

        // The photos were of the plant before it was bought, so they go with it as reference photos
        var subjectType = plantId is not null ? SubjectType.Plant : SubjectType.Propagation;
        if ((plantId ?? propagationId) is { } subjectId)
        {
            foreach (var photo in await photos.GetForAsync(wishId))
            {
                photo.SubjectType = subjectType;
                photo.SubjectId = subjectId;
                photo.IsReference = true;
                await photos.UpdateAsync(photo);
            }
        }

        return true;
    }

    /// <summary>Puts a got wish back among the open ones. What it became is not touched.</summary>
    public async Task PutBackAsync(Guid wishId)
    {
        if (await wishes.GetAsync(wishId) is not { IsGot: true } wish)
            return;

        wish.GotOn = null;
        wish.PlantId = null;
        wish.PropagationId = null;
        await wishes.SaveAsync(wish);
    }
}
