using Stikling.Core.Models;

namespace Stikling.Core.Shelves;

public interface IPhotoMarkRepository
{
    /// <summary>The marks on one photo, not deleted.</summary>
    Task<IReadOnlyList<PhotoMark>> GetForPhotoAsync(Guid photoId);

    /// <summary>The marks of one plant or propagation, on any photo, not deleted.</summary>
    Task<IReadOnlyList<PhotoMark>> GetForSubjectAsync(Guid subjectId);

    /// <summary>All marks that aren't deleted.</summary>
    Task<IReadOnlyList<PhotoMark>> GetAllAsync();

    /// <summary>Adds or updates a mark. Throws if it isn't valid.</summary>
    Task SaveAsync(PhotoMark mark);

    /// <summary>Soft-deletes a mark.</summary>
    Task DeleteAsync(Guid id);
}
