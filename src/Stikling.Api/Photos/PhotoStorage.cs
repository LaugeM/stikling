using Azure;
using Azure.Storage.Blobs;
using Stikling.Api.Data;
using Stikling.Core.Sync;

namespace Stikling.Api.Photos;

public sealed class PhotoOptions
{
    /// <summary>How much space one collection's photo images may take on the server.</summary>
    public long MaxBytesPerCollection { get; set; } = 1024L * 1024 * 1024;
}

/// <summary>
/// The photo images in Blob Storage, one blob per image, named by collection, photo and size.
/// Locally that's Azurite in Docker. <see cref="PhotoImage"/> rows say which images are there.
/// </summary>
public sealed class PhotoStorage(BlobServiceClient blobs)
{
    public const string ContainerName = "photos";

    private readonly BlobContainerClient container = blobs.GetBlobContainerClient(ContainerName);
    private bool ready;

    public static string BlobName(Guid collectionId, Guid photoId, PhotoSize size) =>
        $"{collectionId}/{photoId}/{size.PathName()}";

    public async Task SaveAsync(Guid collectionId, Guid photoId, PhotoSize size, BinaryData image)
    {
        await ReadyAsync();
        await container.GetBlobClient(BlobName(collectionId, photoId, size)).UploadAsync(image, overwrite: true);
    }

    /// <summary>The image, or null when it isn't there.</summary>
    public async Task<Stream?> OpenAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        await ReadyAsync();
        try
        {
            var download = await container.GetBlobClient(BlobName(collectionId, photoId, size)).DownloadStreamingAsync();
            return download.Value.Content;
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid collectionId, Guid photoId, PhotoSize size)
    {
        await ReadyAsync();
        await container.GetBlobClient(BlobName(collectionId, photoId, size)).DeleteIfExistsAsync();
    }

    /// <summary>
    /// Deletes the images whose rows were just removed. One that can't be deleted now is left
    /// behind rather than failing a change that is already saved. Without its row it no longer
    /// counts towards the collection's space.
    /// </summary>
    public async Task DeleteAsync(IEnumerable<PhotoImage> images)
    {
        foreach (var image in images)
        {
            try
            {
                await DeleteAsync(image.CollectionId, image.PhotoId, image.Size);
            }
            catch (RequestFailedException)
            {
            }
        }
    }

    // The container is made the first time it's needed, so the API starts even if storage can't be reached
    private async Task ReadyAsync()
    {
        if (ready)
            return;

        await container.CreateIfNotExistsAsync();
        ready = true;
    }
}
