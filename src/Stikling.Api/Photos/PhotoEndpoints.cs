using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Core.Sync;

namespace Stikling.Api.Photos;

/// <summary>
/// The images of a collection's photos. A photo's record syncs through the records endpoints
/// first, and its images can only be sent once the server has it.
/// </summary>
public static class PhotoEndpoints
{
    public static IEndpointRouteBuilder MapPhotos(this IEndpointRouteBuilder app)
    {
        var photos = app.MapGroup("/collections/{collectionId:guid}/photos");

        // POST: which of these photos have images here. A POST, since a list of ids is too long for an address
        photos.MapPost("/stored", async (Guid collectionId, StoredPhotosRequest request, StiklingDbContext db) =>
        {
            var ids = request.Ids ?? [];
            if (ids.Count > PhotoRules.BatchSize)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ids"] = [$"Ask about at most {PhotoRules.BatchSize} photos at a time."],
                });
            }

            var images = await db.PhotoImages
                .Where(i => i.CollectionId == collectionId && ids.Contains(i.PhotoId))
                .Select(i => new { i.PhotoId, i.Size })
                .ToListAsync();

            return Results.Ok(new StoredPhotosResponse(images
                .GroupBy(i => i.PhotoId)
                .Select(g => new StoredPhoto(g.Key, g.Any(i => i.Size == PhotoSize.Full), g.Any(i => i.Size == PhotoSize.Thumbnail)))
                .ToList()));
        })
        .RequireAuthorization(CollectionPolicies.View);

        photos.MapGet("/usage", async (Guid collectionId, StiklingDbContext db, IOptions<PhotoOptions> options) =>
            new PhotoUsage(await UsedAsync(db, collectionId), options.Value.MaxBytesPerCollection))
        .RequireAuthorization(CollectionPolicies.View);

        photos.MapGet("/{photoId:guid}/{size}", async (Guid collectionId, Guid photoId, string size, StiklingDbContext db, PhotoStorage storage) =>
        {
            if (ParseSize(size) is not { } which)
                return Results.NotFound();

            var there = await db.PhotoImages.AnyAsync(i => i.CollectionId == collectionId && i.PhotoId == photoId && i.Size == which);
            if (!there || await storage.OpenAsync(collectionId, photoId, which) is not { } image)
                return Results.NotFound();

            return Results.Stream(image, "image/jpeg");
        })
        .RequireAuthorization(CollectionPolicies.View);

        // PUT: the image as it is stored on the device, a JPEG, as the body
        photos.MapPut("/{photoId:guid}/{size}", async (
            Guid collectionId, Guid photoId, string size, HttpRequest request,
            StiklingDbContext db, PhotoStorage storage, IOptions<PhotoOptions> options, TimeProvider clock) =>
        {
            if (ParseSize(size) is not { } which)
                return Results.NotFound();

            // The record says whether the photo is still wanted
            if (await PhotoProblemAsync(db, collectionId, photoId) is { } problem)
                return problem;

            if (await ReadImageAsync(request) is not { } image)
                return Results.Problem($"An image can be at most {PhotoRules.MaxImageBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);
            if (!PhotoRules.LooksLikeJpeg(image))
                return Results.Problem("The image needs to be a JPEG.", statusCode: StatusCodes.Status415UnsupportedMediaType);

            var limit = options.Value.MaxBytesPerCollection;
            if (await UsedAfterAsync(db, collectionId, photoId, which, image.Length) > limit)
                return Full(limit);

            await storage.SaveAsync(collectionId, photoId, which, BinaryData.FromBytes(image));

            // Checked again while no other change to the collection can happen, since two devices
            // can send images at once, and the photo may have been deleted in the meantime
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);

            var existing = await db.PhotoImages.FindAsync(collectionId, photoId, which);
            var nowProblem = await PhotoProblemAsync(db, collectionId, photoId);
            var tooBig = await UsedAfterAsync(db, collectionId, photoId, which, image.Length) > limit;
            if (nowProblem is not null || tooBig)
            {
                // The one just saved only replaced an image here when there was a row for it
                if (existing is null)
                    await storage.DeleteAsync(collectionId, photoId, which);
                return nowProblem ?? Full(limit);
            }

            if (existing is null)
            {
                db.PhotoImages.Add(new PhotoImage
                {
                    CollectionId = collectionId,
                    PhotoId = photoId,
                    Size = which,
                    Bytes = image.Length,
                    UploadedAt = clock.GetUtcNow(),
                });
            }
            else
            {
                existing.Bytes = image.Length;
                existing.UploadedAt = clock.GetUtcNow();
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        return app;
    }

    private static PhotoSize? ParseSize(string size) => size switch
    {
        "full" => PhotoSize.Full,
        "thumb" => PhotoSize.Thumbnail,
        _ => null,
    };

    private static Task<long> UsedAsync(StiklingDbContext db, Guid collectionId) =>
        db.PhotoImages.Where(i => i.CollectionId == collectionId).SumAsync(i => i.Bytes);

    /// <summary>What the collection would use with this image, counting a replaced one only once.</summary>
    private static async Task<long> UsedAfterAsync(StiklingDbContext db, Guid collectionId, Guid photoId, PhotoSize size, long bytes)
    {
        var replaced = await db.PhotoImages
            .Where(i => i.CollectionId == collectionId && i.PhotoId == photoId && i.Size == size)
            .SumAsync(i => i.Bytes);
        return await UsedAsync(db, collectionId) - replaced + bytes;
    }

    /// <summary>Why the photo can't have images, or null when it can.</summary>
    private static async Task<IResult?> PhotoProblemAsync(StiklingDbContext db, Guid collectionId, Guid photoId)
    {
        var record = await db.Records
            .Where(r => r.CollectionId == collectionId && r.Kind == SyncKinds.Photos && r.Id == photoId)
            .Select(r => new { r.DeletedAt })
            .SingleOrDefaultAsync();

        if (record is null)
            return Results.Problem("Send the photo's record before its images.", statusCode: StatusCodes.Status404NotFound);
        if (record.DeletedAt is not null)
            return Results.Problem("The photo was deleted.", statusCode: StatusCodes.Status410Gone);
        return null;
    }

    private static IResult Full(long limit) =>
        Results.Problem(
            $"The collection's photos have used all of their {limit / (1024 * 1024)} MB.",
            statusCode: StatusCodes.Status507InsufficientStorage);

    /// <summary>The body, or null when it's larger than an image may be.</summary>
    private static async Task<byte[]?> ReadImageAsync(HttpRequest request)
    {
        if (request.ContentLength > PhotoRules.MaxImageBytes)
            return null;

        using var image = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            image.Write(chunk, 0, read);
            if (image.Length > PhotoRules.MaxImageBytes)
                return null;
        }

        return image.ToArray();
    }
}
