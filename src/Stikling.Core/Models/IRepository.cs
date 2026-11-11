namespace Stikling.Core.Models;

/// <summary>
/// The four methods every kind of stored record has. Each store's own interface adds what is
/// particular to it.
/// </summary>
public interface IRepository<T> where T : Entity
{
    /// <summary>All records that aren't deleted.</summary>
    Task<IReadOnlyList<T>> GetAllAsync();

    /// <summary>The record with this id, or null if it doesn't exist or was deleted.</summary>
    Task<T?> GetAsync(Guid id);

    /// <summary>Adds or updates the record and sets its dates. Throws if it isn't valid.</summary>
    Task SaveAsync(T item);

    /// <summary>Soft-deletes the record. Nothing happens if it doesn't exist or is already deleted.</summary>
    Task DeleteAsync(Guid id);
}
