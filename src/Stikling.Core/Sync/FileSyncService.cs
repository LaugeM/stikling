using Stikling.Core.People;

namespace Stikling.Core.Sync;

/// <summary>
/// The attached files on this device whose bytes may not have reached the server yet, listed by
/// file id. They come back in the same order every time, so a sync can carry on after the last id it got.
/// There is no list to download: a file from another device is fetched when someone opens it.
/// </summary>
public interface IFileSyncStore
{
    Task<IReadOnlyList<Guid>> GetFileUploadsAsync(Guid? after, int max);

    Task RemoveFileUploadsAsync(IReadOnlyCollection<Guid> ids);
}

/// <summary>
/// The attached files' bytes on the server, as the signed-in person. The bytes go straight between
/// the server and the device's storage, so the app itself never holds them.
/// </summary>
public interface IFileServer
{
    /// <summary>Which of these files have their bytes on the server.</summary>
    Task<IReadOnlyList<Guid>> GetStoredFilesAsync(Guid collectionId, IReadOnlyList<Guid> ids);

    /// <summary>Sends the bytes kept on this device. <see cref="UploadOutcome.Refused"/> when the server will never take them.</summary>
    Task<UploadOutcome> UploadFileAsync(Guid collectionId, Guid fileId);

    /// <summary>Fetches the bytes and keeps them on this device. False when the server doesn't have them.</summary>
    Task<bool> DownloadFileAsync(Guid collectionId, Guid fileId);
}

/// <param name="Uploaded">Files whose bytes reached the server.</param>
/// <param name="CollectionFull">True when uploads stopped because the collection has no space left.</param>
public sealed record FileSyncResult(int Uploaded, bool CollectionFull);

/// <summary>
/// Sends attached files' bytes to the server, after <see cref="SyncService"/> has synced the records,
/// since the server only takes the bytes of a file whose record it has. Only the device a file was
/// added on sends it. Other devices fetch it when it is opened.
/// </summary>
public sealed class FileSyncService(IFileSyncStore store, IFileServer server)
{
    /// <param name="me">The person's place in the collection. Only an editor adds files, so only an editor sends them.</param>
    public async Task<FileSyncResult> SyncAsync(Guid collectionId, MemberAccess me)
    {
        if (!me.CanEdit)
            return new FileSyncResult(0, false);

        var uploaded = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await store.GetFileUploadsAsync(after, FileRules.BatchSize);
            if (batch.Count == 0)
                break;
            after = batch[^1];

            // A file already there only comes off the list
            var stored = (await server.GetStoredFilesAsync(collectionId, batch)).ToHashSet();
            var done = new List<Guid>(stored);
            foreach (var id in batch.Where(id => !stored.Contains(id)))
            {
                var outcome = await server.UploadFileAsync(collectionId, id);
                if (outcome == UploadOutcome.Full)
                {
                    await store.RemoveFileUploadsAsync(done);
                    return new FileSyncResult(uploaded, true);
                }

                // Not there yet stays on the list until its record has reached the server. Everything
                // else is final: sent, gone from this device, deleted, or never going to be taken.
                if (outcome != UploadOutcome.NotYet)
                    done.Add(id);
                if (outcome == UploadOutcome.Uploaded)
                    uploaded++;
            }

            await store.RemoveFileUploadsAsync(done);
            if (batch.Count < FileRules.BatchSize)
                break;
        }

        return new FileSyncResult(uploaded, false);
    }
}
