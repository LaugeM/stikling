using Microsoft.JSInterop;

namespace Stikling.Web.Services;

/// <summary>Names of the IndexedDB object stores (see wwwroot/js/db.js).</summary>
public static class Stores
{
    public const string Plants = "plants";
    public const string Propagations = "propagations";
    public const string Timeline = "timeline";
    public const string Photos = "photos";
    public const string PhotoBlobs = "photoBlobs";
    public const string CareLogs = "careLogs";
    public const string PestCases = "pestCases";
    public const string PestTreatments = "pestTreatments";
    public const string Pots = "pots";
}

public sealed record StorageEstimate(long Usage, long Quota, bool Persisted);

/// <summary>
/// Typed access to the IndexedDB module. Objects are serialised to JSON on the way in and
/// back to C# types on the way out by Blazor's JS interop.
/// </summary>
public sealed class IndexedDb(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./js/db.js";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    public async Task<List<T>> GetAllAsync<T>(string store) =>
        await (await Module).InvokeAsync<List<T>>("getAll", store);

    public async Task<T?> GetAsync<T>(string store, Guid id) where T : class =>
        await (await Module).InvokeAsync<T?>("get", store, id);

    public async Task PutAsync<T>(string store, T value) =>
        await (await Module).InvokeVoidAsync("put", store, value);

    public async Task DeleteAsync(string store, Guid id) =>
        await (await Module).InvokeVoidAsync("remove", store, id);

    public async Task<bool> RequestPersistenceAsync() =>
        await (await Module).InvokeAsync<bool>("requestPersistence");

    public async Task<StorageEstimate?> EstimateAsync() =>
        await (await Module).InvokeAsync<StorageEstimate?>("estimate");

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
