namespace Stikling.Core.Sync;

/// <summary>
/// The photos on this device that sync has to move, kept as two lists by photo id: the ones whose
/// images may not have reached the server yet, and the ones that have no thumbnail here yet. Both
/// come back in the same order every time, so a sync can carry on after the last id it got.
/// </summary>
public interface IPhotoSyncStore
{
    Task<IReadOnlyList<Guid>> GetUploadsAsync(Guid? after, int max);

    Task RemoveUploadsAsync(IReadOnlyCollection<Guid> ids);

    Task<IReadOnlyList<Guid>> GetDownloadsAsync(Guid? after, int max);

    Task RemoveDownloadsAsync(IReadOnlyCollection<Guid> ids);
}

/// <summary>What happened to one image sent to the server.</summary>
public enum UploadOutcome
{
    Uploaded,

    /// <summary>The image isn't on this device, e.g. the photo was deleted here.</summary>
    NotHere,

    /// <summary>The server doesn't have the photo's record yet, so it can't take the image. It's tried again next time.</summary>
    NotYet,

    /// <summary>The photo was deleted, so the server doesn't want its image.</summary>
    Gone,

    /// <summary>The collection's photos have used all the space it has on the server.</summary>
    Full,

    /// <summary>The server will never take this image, e.g. it isn't a WebP or a JPEG. Sending it again wouldn't help.</summary>
    Refused,
}

/// <summary>
/// The photo images on the server, as the signed-in person. The image data goes straight between
/// the server and the device's storage, so the app itself never holds it.
/// </summary>
public interface IPhotoServer
{
    Task<IReadOnlyList<StoredPhoto>> GetStoredAsync(Guid collectionId, IReadOnlyList<Guid> ids);

    /// <summary>Sends the image kept on this device.</summary>
    Task<UploadOutcome> UploadAsync(Guid collectionId, Guid photoId, PhotoSize size);

    /// <summary>Fetches the image and keeps it on this device. False when the server doesn't have it.</summary>
    Task<bool> DownloadAsync(Guid collectionId, Guid photoId, PhotoSize size);
}

/// <param name="Uploaded">Photos whose images reached the server.</param>
/// <param name="Downloaded">Thumbnails fetched, so the screen can show them.</param>
/// <param name="CollectionFull">True when uploads stopped because the collection has no space left.</param>
public sealed record PhotoSyncResult(int Uploaded, int Downloaded, bool CollectionFull);

/// <summary>
/// Moves photo images between this device and the server, after <see cref="SyncService"/> has
/// synced the records. Every device keeps all the thumbnails, so lists look complete offline. A
/// full-size image only goes up from the device it was added on, and is fetched by other devices
/// when it's opened.
/// </summary>
public sealed class PhotoSyncService(IPhotoSyncStore store, IPhotoServer server)
{
    /// <summary>How many thumbnails are fetched at the same time.</summary>
    internal const int DownloadsAtOnce = 6;

    /// <param name="canEdit">False for someone who can only view the collection. They only fetch.</param>
    public async Task<PhotoSyncResult> SyncAsync(Guid collectionId, bool canEdit)
    {
        var (uploaded, full) = canEdit ? await UploadAsync(collectionId) : (0, false);
        var downloaded = await DownloadAsync(collectionId);
        return new PhotoSyncResult(uploaded, downloaded, full);
    }

    private async Task<(int Uploaded, bool Full)> UploadAsync(Guid collectionId)
    {
        var uploaded = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await store.GetUploadsAsync(after, PhotoRules.BatchSize);
            if (batch.Count == 0)
                break;
            after = batch[^1];

            // A photo whose images are already there only comes off the list
            var stored = (await server.GetStoredAsync(collectionId, batch)).ToDictionary(p => p.Id);
            var done = new List<Guid>();
            foreach (var id in batch)
            {
                stored.TryGetValue(id, out var there);
                var keep = false;
                var sent = false;

                // The thumbnail first, since that's what the other devices fetch straight away
                foreach (var (size, isThere) in new[] { (PhotoSize.Thumbnail, there?.Thumbnail == true), (PhotoSize.Full, there?.Full == true) })
                {
                    if (isThere)
                        continue;

                    var outcome = await server.UploadAsync(collectionId, id, size);
                    if (outcome == UploadOutcome.Full)
                    {
                        await store.RemoveUploadsAsync(done);
                        return (uploaded, true);
                    }

                    if (outcome == UploadOutcome.Gone)
                        break;
                    if (outcome == UploadOutcome.NotYet)
                        keep = true;
                    else if (outcome == UploadOutcome.Uploaded)
                        sent = true;
                }

                if (!keep)
                    done.Add(id);
                if (sent)
                    uploaded++;
            }

            await store.RemoveUploadsAsync(done);
            if (batch.Count < PhotoRules.BatchSize)
                break;
        }

        return (uploaded, false);
    }

    private async Task<int> DownloadAsync(Guid collectionId)
    {
        var downloaded = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await store.GetDownloadsAsync(after, PhotoRules.BatchSize);
            if (batch.Count == 0)
                break;
            after = batch[^1];

            // One not on the server yet stays on the list until the device it was taken on sends it.
            // A few are fetched at a time, since a device where someone just signed in has them all to get.
            var done = new List<Guid>();
            var there = (await server.GetStoredAsync(collectionId, batch)).Where(p => p.Thumbnail);
            foreach (var group in there.Chunk(DownloadsAtOnce))
            {
                var fetched = await Task.WhenAll(group.Select(async photo =>
                    (photo.Id, Ok: await server.DownloadAsync(collectionId, photo.Id, PhotoSize.Thumbnail))));
                foreach (var (id, ok) in fetched)
                {
                    if (ok)
                    {
                        done.Add(id);
                        downloaded++;
                    }
                }
            }

            await store.RemoveDownloadsAsync(done);
            if (batch.Count < PhotoRules.BatchSize)
                break;
        }

        return downloaded;
    }
}
