using System.Text.Json;
using Microsoft.JSInterop;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// The records on this device as sync sees them: as the JSON they are stored as, so fields this
/// version of the app doesn't know travel too. The work is done in wwwroot/js/db.js, where the
/// change list is kept in the same transaction as each write.
/// </summary>
public sealed class IndexedDbSyncStore(IJSRuntime js) : ISyncStore, IAsyncDisposable
{
    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module =>
        module ??= js.InvokeAsync<IJSObjectReference>("import", IndexedDb.ModulePath).AsTask();

    public async Task<SyncState?> GetStateAsync() =>
        await (await Module).InvokeAsync<SyncState?>("getSyncState");

    public async Task SaveStateAsync(SyncState state) =>
        await (await Module).InvokeVoidAsync("saveSyncState", state);

    /// <summary>Forgets how far this device got, so the next sign-in starts over and merges.</summary>
    public async Task ForgetStateAsync() =>
        await (await Module).InvokeVoidAsync("forgetSyncState");

    public async Task<IReadOnlyList<PendingRecord>> GetPendingAsync(IReadOnlyCollection<string> kinds, int max) =>
        await (await Module).InvokeAsync<List<PendingRecord>>("getPending", kinds, max);

    /// <summary>How many changes here haven't reached the server yet.</summary>
    public async Task<int> CountPendingAsync() =>
        await (await Module).InvokeAsync<int>("countPending");

    public async Task MarkSentAsync(IReadOnlyList<PendingRecord> sent)
    {
        if (sent.Count > 0)
            await (await Module).InvokeVoidAsync("markSent", sent.Select(p => new { p.Kind, p.Id, p.Mark }));
    }

    public async Task QueueAllAsync() =>
        await (await Module).InvokeVoidAsync("queueAll", SyncKinds.All);

    public async Task<IReadOnlyList<JsonElement>> GetAsync(string kind, IReadOnlyCollection<Guid> ids) =>
        ids.Count == 0 ? [] : await (await Module).InvokeAsync<List<JsonElement>>("getMany", kind, ids);

    public async Task SaveFromServerAsync(string kind, IReadOnlyList<ServerCopy> records) =>
        await (await Module).InvokeVoidAsync("saveFromServer", kind, records);

    /// <summary>How many photos have their image on this device. Images don't sync yet.</summary>
    public async Task<int> CountPhotoImagesAsync() =>
        await (await Module).InvokeAsync<int>("countPhotoImages");

    /// <summary>Empties the database, for signing out and removing everything from this device.</summary>
    public async Task ClearAllAsync() =>
        await (await Module).InvokeVoidAsync("clearAll");

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
            await module.Result.DisposeAsync();
    }
}
