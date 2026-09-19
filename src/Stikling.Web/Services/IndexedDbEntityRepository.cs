using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>
/// Shared storage logic for plants and propagations: validation, timestamps and soft deletes.
/// </summary>
public abstract class IndexedDbEntityRepository<T>(IndexedDb db, TimeProvider time, string store) where T : Entity
{
    protected abstract IReadOnlyList<string> Validate(T entity);

    public async Task<IReadOnlyList<T>> GetAllAsync() =>
        (await db.GetAllAsync<T>(store)).Where(e => !e.IsDeleted).ToList();

    public async Task<T?> GetAsync(Guid id)
    {
        var entity = await db.GetAsync<T>(store, id);
        return entity is { IsDeleted: false } ? entity : null;
    }

    public async Task SaveAsync(T entity)
    {
        var errors = Validate(entity);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var now = time.GetUtcNow();
        if (entity.CreatedAt == default)
            entity.CreatedAt = now;
        entity.UpdatedAt = now;

        await db.PutAsync(store, entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await db.GetAsync<T>(store, id);
        if (entity is null || entity.IsDeleted)
            return;

        entity.DeletedAt = entity.UpdatedAt = time.GetUtcNow();
        await db.PutAsync(store, entity);
    }
}
