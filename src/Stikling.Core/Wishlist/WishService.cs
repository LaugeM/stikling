using Stikling.Core.Models;

namespace Stikling.Core.Wishlist;

/// <summary>What a page does with wishes. Everything goes through the repository, so dates are set.</summary>
public sealed class WishService(IWishRepository wishes, TimeProvider time)
{
    public Task<IReadOnlyList<Wish>> GetAllAsync() => wishes.GetAllAsync();

    public Task SaveAsync(Wish wish) => wishes.SaveAsync(wish);

    public Task DeleteAsync(Guid id) => wishes.DeleteAsync(id);

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
