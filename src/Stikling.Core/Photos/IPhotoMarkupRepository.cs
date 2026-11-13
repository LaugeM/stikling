using Stikling.Core.Models;

namespace Stikling.Core.Photos;

public interface IPhotoMarkupRepository
{
    /// <summary>The marks on one photo, not deleted.</summary>
    Task<IReadOnlyList<PhotoMarkup>> GetForPhotoAsync(Guid photoId);

    /// <summary>All marks that aren't deleted.</summary>
    Task<IReadOnlyList<PhotoMarkup>> GetAllAsync();

    /// <summary>Adds or updates a mark. Throws if it isn't valid.</summary>
    Task SaveAsync(PhotoMarkup markup);

    /// <summary>Soft-deletes a mark.</summary>
    Task DeleteAsync(Guid id);
}
