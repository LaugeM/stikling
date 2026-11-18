using Azure;
using Azure.Storage.Blobs;
using Stikling.Api.Data;
using Stikling.Api.Photos;

namespace Stikling.Api.Files;

/// <summary>
/// The bytes of attached files in Blob Storage, one blob each, in the photos container under
/// <c>{collectionId}/files/{fileId}</c>. Sharing the container and the collection's prefix means
/// <see cref="PhotoStorage.DeleteCollectionAsync"/> erases them with the collection or the account.
/// <see cref="StoredFile"/> rows say which are there.
/// </summary>
public sealed class FileStorage(BlobServiceClient blobs)
{
    private readonly BlobContainerClient container = blobs.GetBlobContainerClient(PhotoStorage.ContainerName);
    private bool ready;

    public static string BlobName(Guid collectionId, Guid fileId) => $"{collectionId}/files/{fileId}";

    public async Task SaveAsync(Guid collectionId, Guid fileId, BinaryData bytes)
    {
        await ReadyAsync();
        await container.GetBlobClient(BlobName(collectionId, fileId)).UploadAsync(bytes, overwrite: true);
    }

    /// <summary>The bytes, or null when they aren't there.</summary>
    public async Task<Stream?> OpenAsync(Guid collectionId, Guid fileId)
    {
        await ReadyAsync();
        try
        {
            var download = await container.GetBlobClient(BlobName(collectionId, fileId)).DownloadStreamingAsync();
            return download.Value.Content;
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid collectionId, Guid fileId)
    {
        await ReadyAsync();
        await container.GetBlobClient(BlobName(collectionId, fileId)).DeleteIfExistsAsync();
    }

    /// <summary>
    /// Deletes the bytes whose rows were just removed. One that can't be deleted now is left behind
    /// rather than failing a change that is already saved. Without its row it no longer counts towards
    /// the collection's space, and the collection's prefix is erased with it.
    /// </summary>
    public async Task DeleteAsync(IEnumerable<StoredFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                await DeleteAsync(file.CollectionId, file.FileId);
            }
            catch (RequestFailedException)
            {
            }
        }
    }

    private async Task ReadyAsync()
    {
        if (ready)
            return;

        await container.CreateIfNotExistsAsync();
        ready = true;
    }
}
