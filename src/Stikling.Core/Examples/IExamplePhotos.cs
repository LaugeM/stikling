namespace Stikling.Core.Examples;

/// <summary>The images that come with the example family. They are bundled with the app, not taken by the person.</summary>
public interface IExamplePhotos
{
    /// <summary>Stores the bundled image under this photo id and returns its size. Throws when it can't.</summary>
    Task<(int Width, int Height)> StoreAsync(Guid photoId, string fileName);

    /// <summary>Frees the image data of a photo.</summary>
    Task RemoveBytesAsync(Guid photoId);
}
