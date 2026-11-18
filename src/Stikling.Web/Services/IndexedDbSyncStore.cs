using System.Text.Json;
using Microsoft.JSInterop;
using Stikling.Core.Sync;

namespace Stikling.Web.Services;

/// <summary>
/// The records on this device as sync sees them: as the JSON they are stored as, so fields this
/// version of the app doesn't know travel too. The work is done in wwwroot/js/db.js, where the
/// change list is kept in the same transaction as each write, and the photo lists in the same
/// transaction as the images.
/// </summary>
public sealed class IndexedDbSyncStore(IJSRuntime js) : JsModule(js, IndexedDb.ModulePath), ISyncStore, IPhotoSyncStore
{
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

    public async Task DropPendingAsync(IReadOnlyList<PendingRecord> dropped)
    {
        // The same as after sending: it comes off the list unless it changed again since it was read
        if (dropped.Count > 0)
            await (await Module).InvokeVoidAsync("markSent", dropped.Select(p => new { p.Kind, p.Id, p.Mark }));
    }

    public async Task QueueAllAsync() =>
        await (await Module).InvokeVoidAsync("queueAll", SyncKinds.All);

    public async Task<IReadOnlyList<JsonElement>> GetAsync(string kind, IReadOnlyCollection<Guid> ids) =>
        ids.Count == 0 ? [] : await (await Module).InvokeAsync<List<JsonElement>>("getMany", kind, ids);

    public async Task SaveFromServerAsync(string kind, IReadOnlyList<ServerCopy> records) =>
        await (await Module).InvokeVoidAsync("saveFromServer", kind, records);

    public async Task<IReadOnlyList<Guid>> GetUploadsAsync(Guid? after, int max) =>
        await (await Module).InvokeAsync<List<Guid>>("getPhotoUploads", after, max);

    public async Task RemoveUploadsAsync(IReadOnlyCollection<Guid> ids) =>
        await (await Module).InvokeVoidAsync("removePhotoUploads", ids);

    public async Task<IReadOnlyList<Guid>> GetDownloadsAsync(Guid? after, int max) =>
        await (await Module).InvokeAsync<List<Guid>>("getPhotoDownloads", after, max);

    public async Task RemoveDownloadsAsync(IReadOnlyCollection<Guid> ids) =>
        await (await Module).InvokeVoidAsync("removePhotoDownloads", ids);

    /// <summary>How many photos taken or restored here haven't reached the server yet.</summary>
    public async Task<int> CountPhotoUploadsAsync() =>
        await (await Module).InvokeAsync<int>("countPhotoUploads");

    /// <summary>How many of these photos are still waiting to be sent to the server.</summary>
    public async Task<int> CountUploadsAmongAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
            return 0;

        var wanted = ids.ToHashSet();
        var waiting = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await GetUploadsAsync(after, 500);
            if (batch.Count == 0)
                return waiting;
            waiting += batch.Count(wanted.Contains);
            after = batch[^1];
        }
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetCreatedByAsync(IReadOnlyCollection<Guid> photoIds)
    {
        var found = new Dictionary<Guid, Guid?>();
        foreach (var photo in await GetAsync(SyncKinds.Photos, photoIds))
        {
            if (RecordStamp.TryRead(photo, out var stamp))
            {
                found[stamp.Id] = photo.TryGetProperty("createdBy", out var by) && by.ValueKind == JsonValueKind.String && by.TryGetGuid(out var who)
                    ? who
                    : null;
            }
        }
        return found;
    }

    /// <summary>
    /// Empties the main database, for signing out and removing everything from this device. The databases
    /// of shared collections are deleted by <see cref="CollectionsService"/>.
    /// </summary>
    public async Task ClearAllAsync() =>
        await (await Module).InvokeVoidAsync("clearAll");
}
