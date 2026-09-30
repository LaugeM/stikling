namespace Stikling.Core.Sync;

/// <summary>
/// The two images kept for each photo. The photo's record syncs like any other, and the images
/// travel on their own, under <c>/collections/{id}/photos/{photoId}/full</c> and <c>/thumb</c>.
/// </summary>
public enum PhotoSize
{
    Full,
    Thumbnail,
}

/// <summary>What the server and the devices agree on about photo images.</summary>
public static class PhotoRules
{
    /// <summary>
    /// The largest image the server takes. The app shrinks a photo to 1600 pixels before saving it,
    /// which is well under this.
    /// </summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>The most photos asked about in one <see cref="StoredPhotosRequest"/>.</summary>
    public const int BatchSize = 200;

    /// <summary>The name of a size in the address of its image.</summary>
    public static string PathName(this PhotoSize size) => size == PhotoSize.Full ? "full" : "thumb";

    /// <summary>
    /// What kind of image the bytes are, from how they start, or null when it isn't one the app
    /// saves. The app saves WebP, and JPEG in a browser that can't make WebP and for photos saved
    /// before it did.
    /// </summary>
    public static ImageFormat? FormatOf(ReadOnlySpan<byte> bytes) =>
        bytes is [0xFF, 0xD8, 0xFF, ..] ? ImageFormat.Jpeg
        : bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? ImageFormat.WebP
        : null;
}

/// <summary>A kind of image the app saves, with its media type and the file extension it gets in a download.</summary>
public sealed record ImageFormat(string ContentType, string Extension)
{
    public static readonly ImageFormat Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageFormat WebP = new("image/webp", "webp");
}

/// <summary><c>POST /collections/{id}/photos/stored</c>: which of these photos have their images on the server.</summary>
public sealed record StoredPhotosRequest(List<Guid> Ids);

/// <param name="Photos">The photos asked about that have at least one image on the server.</param>
public sealed record StoredPhotosResponse(List<StoredPhoto> Photos);

public sealed record StoredPhoto(Guid Id, bool Full, bool Thumbnail);

/// <summary><c>GET /collections/{id}/photos/usage</c>: how much space the collection's photos take.</summary>
public sealed record PhotoUsage(long UsedBytes, long LimitBytes);
