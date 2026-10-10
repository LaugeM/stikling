using Stikling.Core.Examples;

namespace Stikling.Web.Services;

/// <summary>The example family's images, fetched from the app's own files when someone loads the example.</summary>
public sealed class ExamplePhotos(PhotoService photos) : IExamplePhotos
{
    public Task<(int Width, int Height)> StoreAsync(Guid photoId, string fileName) =>
        photos.SaveFromUrlAsync($"example/{fileName}", photoId);

    public Task RemoveBytesAsync(Guid photoId) => photos.RemoveBytesAsync(photoId);
}
