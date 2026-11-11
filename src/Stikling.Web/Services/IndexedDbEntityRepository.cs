using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// Shared storage logic for every kind of record: validation, timestamps and soft deletes.
/// </summary>
public abstract class IndexedDbEntityRepository<T>(IndexedDb db, TimeProvider time, RecordKind<T> kind) where T : Entity
{
    /// <summary>For subclasses whose validation depends on the date.</summary>
    protected DateOnly Today => time.Today();

    protected abstract IReadOnlyList<string> Validate(T entity);

    public async Task<IReadOnlyList<T>> GetAllAsync() =>
        (await db.GetAllAsync<T>(kind)).Where(e => !e.IsDeleted).ToList();

    /// <summary>Everything of the kind, deleted items too.</summary>
    protected Task<List<T>> GetStoredAsync() => db.GetAllAsync<T>(kind);

    public async Task<T?> GetAsync(Guid id)
    {
        var entity = await db.GetAsync<T>(kind, id);
        return entity is { IsDeleted: false } ? entity : null;
    }

    /// <summary>True when a record with this id is stored and soft-deleted.</summary>
    public async Task<bool> IsDeletedAsync(Guid id) =>
        await db.GetAsync<T>(kind, id) is { IsDeleted: true };

    public async Task SaveAsync(T entity)
    {
        var errors = Validate(entity);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var now = time.GetUtcNow();
        if (entity.CreatedAt == default)
            entity.CreatedAt = now;
        entity.UpdatedAt = now;

        await db.PutAsync(kind, entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await db.GetAsync<T>(kind, id);
        if (entity is null || entity.IsDeleted)
            return;

        entity.DeletedAt = entity.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(kind, entity);
    }
}
