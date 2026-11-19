using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.Photos;
using Stikling.Core.Sync;

namespace Stikling.Api.Files;

/// <summary>
/// The bytes of a collection's attached files. A file's record syncs through the records endpoints
/// first, and its bytes can only be sent once the server has it. Only an editor adds files, and
/// they count towards the same space as the photos. Share links and the public page never read them.
/// </summary>
public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFiles(this IEndpointRouteBuilder app)
    {
        var files = app.MapGroup("/collections/{collectionId:guid}/files");

        // POST: which of these files have their bytes here. A POST, since a list of ids is too long for an address
        files.MapPost("/stored", async (Guid collectionId, StoredFilesRequest request, StiklingDbContext db) =>
        {
            var ids = request.Ids ?? [];
            if (ids.Count > FileRules.BatchSize)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ids"] = [$"Ask about at most {FileRules.BatchSize} files at a time."],
                });
            }

            var there = await db.StoredFiles
                .Where(f => f.CollectionId == collectionId && ids.Contains(f.FileId))
                .Select(f => f.FileId)
                .ToListAsync();

            return Results.Ok(new StoredFilesResponse(there));
        })
        .RequireAuthorization(CollectionPolicies.View);

        files.MapGet("/{fileId:guid}", async (Guid collectionId, Guid fileId, HttpContext http, StiklingDbContext db, FileStorage storage) =>
        {
            var row = await db.StoredFiles.AsNoTracking()
                .Where(f => f.CollectionId == collectionId && f.FileId == fileId)
                .Select(f => new { f.ContentType })
                .SingleOrDefaultAsync();
            if (row is null || await storage.OpenAsync(collectionId, fileId) is not { } stream)
                return Results.NotFound();

            // At most MaxFileBytes, so it can be read whole
            using var memory = new MemoryStream();
            await using (stream)
                await stream.CopyToAsync(memory);
            // The app fetches the bytes and opens them itself. A PDF opened straight from this address is
            // downloaded instead of shown on the API's own origin.
            http.Response.Headers.ContentDisposition = "attachment";
            return Results.Bytes(memory.ToArray(), row.ContentType);
        })
        .RequireAuthorization(CollectionPolicies.View);

        // PUT: the bytes as they are stored on the device, a PDF, a WebP or a JPEG, as the body
        files.MapPut("/{fileId:guid}", async (
            Guid collectionId, Guid fileId, HttpRequest request,
            StiklingDbContext db, FileStorage storage, IOptions<PhotoOptions> options, TimeProvider clock) =>
        {
            // The record says whether the file is still wanted
            if (await FileProblemAsync(db, collectionId, fileId) is { } problem)
                return problem;

            if (await ReadAsync(request) is not { } bytes)
                return Results.Problem($"A file can be at most {FileRules.MaxFileBytes} bytes.", statusCode: StatusCodes.Status413PayloadTooLarge);
            if (FileRules.FormatOf(bytes) is not { } format)
                return Results.Problem("The file needs to be a PDF, a WebP or a JPEG.", statusCode: StatusCodes.Status415UnsupportedMediaType);

            var limit = options.Value.MaxBytesPerCollection;
            if (await UsedAfterAsync(db, collectionId, fileId, bytes.Length) > limit)
                return Full(limit);

            await storage.SaveAsync(collectionId, fileId, BinaryData.FromBytes(bytes));

            // Checked again while no other change to the collection can happen, since two devices
            // can send at once, and the file may have been deleted in the meantime
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);

            var existing = await db.StoredFiles.FindAsync(collectionId, fileId);
            var nowProblem = await FileProblemAsync(db, collectionId, fileId);
            var tooBig = await UsedAfterAsync(db, collectionId, fileId, bytes.Length) > limit;
            if (nowProblem is not null || tooBig)
            {
                // The one just saved only replaced bytes here when there was a row for them
                if (existing is null)
                    await storage.DeleteAsync(collectionId, fileId);
                return nowProblem ?? Full(limit);
            }

            if (existing is null)
            {
                db.StoredFiles.Add(new StoredFile
                {
                    CollectionId = collectionId,
                    FileId = fileId,
                    Bytes = bytes.Length,
                    ContentType = format.ContentType,
                    StoredAt = clock.GetUtcNow(),
                });
            }
            else
            {
                existing.Bytes = bytes.Length;
                existing.ContentType = format.ContentType;
                existing.StoredAt = clock.GetUtcNow();
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        return app;
    }

    /// <summary>What the collection would use with these bytes, photos and files together, counting replaced bytes only once.</summary>
    private static async Task<long> UsedAfterAsync(StiklingDbContext db, Guid collectionId, Guid fileId, long bytes)
    {
        var replaced = await db.StoredFiles
            .Where(f => f.CollectionId == collectionId && f.FileId == fileId)
            .SumAsync(f => f.Bytes);
        return await PhotoEndpoints.UsedAsync(db, collectionId) - replaced + bytes;
    }

    /// <summary>Why the file can't have bytes, or null when it can.</summary>
    private static async Task<IResult?> FileProblemAsync(StiklingDbContext db, Guid collectionId, Guid fileId)
    {
        var record = await db.Records
            .Where(r => r.CollectionId == collectionId && r.Kind == SyncKinds.Files && r.Id == fileId)
            .Select(r => new { r.DeletedAt })
            .SingleOrDefaultAsync();

        if (record is null)
            return Results.Problem("Send the file's record before its bytes.", statusCode: StatusCodes.Status404NotFound);
        if (record.DeletedAt is not null)
            return Results.Problem("The file was deleted.", statusCode: StatusCodes.Status410Gone);
        return null;
    }

    private static IResult Full(long limit) =>
        Results.Problem(
            $"The collection's photos and files have used all of their {limit / (1024 * 1024)} MB.",
            statusCode: StatusCodes.Status507InsufficientStorage);

    /// <summary>The body, or null when it's larger than a file may be.</summary>
    private static async Task<byte[]?> ReadAsync(HttpRequest request)
    {
        if (request.ContentLength > FileRules.MaxFileBytes)
            return null;

        using var bytes = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            bytes.Write(chunk, 0, read);
            if (bytes.Length > FileRules.MaxFileBytes)
                return null;
        }

        return bytes.ToArray();
    }
}
