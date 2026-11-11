using Microsoft.JSInterop;
using Stikling.Core.Models;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// Names of the IndexedDB object stores that don't hold records (see wwwroot/js/db.js). The record
/// stores are in <c>RecordKinds</c> in Core.
/// </summary>
public static class Stores
{
    public const string PhotoBlobs = "photoBlobs";

    // Kept by sync, and not synced themselves
    public const string Changes = "changes";
    public const string SyncState = "syncState";
}

public sealed record StorageEstimate(long Usage, long Quota, bool Persisted);

/// <summary>
/// Typed access to the IndexedDB module. Objects are serialised to JSON on the way in and
/// back to C# types on the way out by Blazor's JS interop.
/// </summary>
public sealed class IndexedDb(IJSRuntime js) : JsModule(js, ModulePath), IRecordStore
{
    internal const string ModulePath = "./js/db.js";

    /// <summary>Raised after something is saved or deleted here, so sync can send it.</summary>
    public event Action? Changed;

    public async Task<List<T>> GetAllAsync<T>(string store) =>
        await (await Module).InvokeAsync<List<T>>("getAll", store);

    public Task<List<T>> GetAllAsync<T>(RecordKind<T> kind) where T : Entity => GetAllAsync<T>(kind.Name);

    public Task<T?> GetAsync<T>(RecordKind<T> kind, Guid id) where T : Entity => GetAsync<T>(kind.Name, id);

    public Task PutAsync<T>(RecordKind<T> kind, T value) where T : Entity => PutAsync(kind.Name, value);

    public async Task<T?> GetAsync<T>(string store, Guid id) where T : class =>
        await (await Module).InvokeAsync<T?>("get", store, id);

    public async Task PutAsync<T>(string store, T value)
    {
        await (await Module).InvokeVoidAsync("put", store, value);
        Changed?.Invoke();
    }

    public async Task DeleteAsync(string store, Guid id)
    {
        await (await Module).InvokeVoidAsync("remove", store, id);
        Changed?.Invoke();
    }

    public async Task<bool> RequestPersistenceAsync() =>
        await (await Module).InvokeAsync<bool>("requestPersistence");

    public async Task<StorageEstimate?> EstimateAsync() =>
        await (await Module).InvokeAsync<StorageEstimate?>("estimate");
}
