using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.Photos;
using Stikling.Core.Sync;

namespace Stikling.Api.People;

public static class ExportEndpoints
{
    /// <summary>The largest request body taken, which is plenty for what the sign-in service has about someone.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <param name="Account">The person's details from the sign-in service, collected by the app.</param>
    public record ExportRequest(JsonElement? Account);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// <c>POST /me/export</c>: a ZIP of everything the server holds about the caller, so they can
    /// see it for themselves. It has their account, settings, collections, every record (deleted
    /// ones included) and the photo images. Nothing about other members of a shared collection is
    /// in it. The body is optional. It can hold the person's details from the sign-in service under
    /// <c>account</c>, which the server doesn't check or keep, only puts in the ZIP. The ZIP is
    /// written straight to the response, so the photos are never all in memory at once.
    /// </summary>
    public static IEndpointRouteBuilder MapExport(this IEndpointRouteBuilder app)
    {
        app.MapPost("/me/export", async (HttpContext http, CurrentPerson current, StiklingDbContext db, PhotoStorage photos, TimeProvider clock) =>
        {
            if (await ReadAccountAsync(http.Request) is not { } body)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["body"] = [$"Send at most {MaxBodyBytes / 1024} KB, as JSON."],
                });
            }

            if (await current.FindAsync() is not { } person)
                return Results.NotFound();

            // ZipArchive still closes its entries with a synchronous write, which the server only
            // allows when asked. It is on for this request alone.
            if (http.Features.Get<IHttpBodyControlFeature>() is { } control)
                control.AllowSynchronousIO = true;

            var now = clock.GetUtcNow();
            http.Response.ContentType = "application/zip";
            http.Response.Headers.ContentDisposition =
                new ContentDisposition { FileName = $"stikling-data-{now:yyyy-MM-dd}.zip" }.ToString();

            await WriteZipAsync(http.Response.Body, person, body.Account, db, photos, now, http.RequestAborted);
            return Results.Empty;
        })
        .RequireAuthorization();

        return app;
    }

    private sealed record Body(JsonElement? Account);

    /// <summary>What the body holds, or null when it is refused for being too big or not JSON.</summary>
    private static async Task<Body?> ReadAccountAsync(HttpRequest request)
    {
        if (request.ContentLength > MaxBodyBytes)
            return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBodyBytes)
                return null;
        }

        if (buffer.Length == 0)
            return new Body(null);

        try
        {
            var parsed = JsonSerializer.Deserialize<ExportRequest>(buffer.ToArray(), Json);
            return new Body(parsed?.Account is { ValueKind: JsonValueKind.Object } account ? account : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteZipAsync(
        Stream output, Person person, JsonElement? account, StiklingDbContext db, PhotoStorage photos, DateTimeOffset now, CancellationToken cancel)
    {
        var memberships = await db.Memberships.AsNoTracking()
            .Where(m => m.PersonId == person.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.CollectionId, m.Collection.Name, CollectionCreatedAt = m.Collection.CreatedAt, m.Role, m.CreatedAt })
            .ToListAsync(cancel);
        var settings = await db.PersonSettings.AsNoTracking().Where(s => s.PersonId == person.Id).SingleOrDefaultAsync(cancel);

        await using var zip = await ZipArchive.CreateAsync(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancel);

        await WriteTextAsync(zip, "README.txt", Readme(now), cancel);

        if (account is { } details)
            await WriteJsonAsync(zip, "account.json", details, cancel);

        await WriteJsonAsync(zip, "person.json", new { person.Id, person.ClerkUserId, person.CreatedAt }, cancel);

        if (settings is not null)
            await WriteRawAsync(zip, "settings.json", settings.Data, cancel);

        await WriteJsonAsync(zip, "collections.json", memberships.Select(m => new
        {
            Id = m.CollectionId,
            m.Name,
            CreatedAt = m.CollectionCreatedAt,
            Role = m.Role.ToString(),
            JoinedAt = m.CreatedAt,
        }), cancel);

        foreach (var collectionId in memberships.Select(m => m.CollectionId))
        {
            var kinds = await db.Records.AsNoTracking()
                .Where(r => r.CollectionId == collectionId)
                .Select(r => r.Kind)
                .Distinct()
                .OrderBy(k => k)
                .ToListAsync(cancel);

            foreach (var kind in kinds)
                await WriteRecordsAsync(zip, db, collectionId, kind, cancel);

            var images = await db.PhotoImages.AsNoTracking()
                .Where(i => i.CollectionId == collectionId)
                .OrderBy(i => i.PhotoId).ThenBy(i => i.Size)
                .ToListAsync(cancel);

            foreach (var image in images)
                await WriteImageAsync(zip, photos, image, cancel);

            // Whether the plant's or propagation's page is shared, and with what settings
            var shares = await db.ShareLinks.AsNoTracking()
                .Where(l => l.CollectionId == collectionId)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync(cancel);
            if (shares.Count > 0)
                await WriteJsonAsync(zip, $"collections/{collectionId}/shareLinks.json", shares.Select(l => new
                {
                    l.Id,
                    l.Token,
                    SubjectType = l.SubjectType.ToString(),
                    l.SubjectId,
                    l.CreatedAt,
                    l.UpdatedAt,
                    l.TurnedOffAt,
                    l.ShowNotes,
                    l.Name,
                    l.Line,
                    l.LeftOutPhotoIds,
                    l.TimeZone,
                }), cancel);
        }
    }

    private static async Task WriteRecordsAsync(ZipArchive zip, StiklingDbContext db, Guid collectionId, string kind, CancellationToken cancel)
    {
        var entry = zip.CreateEntry($"collections/{collectionId}/{kind}.json");
        await using var stream = await entry.OpenAsync(cancel);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();
        var records = db.Records.AsNoTracking()
            .Where(r => r.CollectionId == collectionId && r.Kind == kind)
            .OrderBy(r => r.Version)
            .Select(r => r.Data)
            .AsAsyncEnumerable();
        await foreach (var data in records.WithCancellation(cancel))
            writer.WriteRawValue(data, skipInputValidation: true);
        writer.WriteEndArray();
        await writer.FlushAsync(cancel);
    }

    private static async Task WriteImageAsync(ZipArchive zip, PhotoStorage photos, PhotoImage image, CancellationToken cancel)
    {
        await using var blob = await photos.OpenAsync(image.CollectionId, image.PhotoId, image.Size);
        if (blob is null)
            return;

        // An image is at most PhotoRules.MaxImageBytes, so one at a time can be held in memory
        using var bytes = new MemoryStream();
        await blob.CopyToAsync(bytes, cancel);
        var content = bytes.GetBuffer().AsMemory(0, (int)bytes.Length);

        var extension = PhotoRules.FormatOf(content.Span)?.Extension ?? "bin";
        var suffix = image.Size == PhotoSize.Full ? "" : "-small";

        // Images are compressed already
        var entry = zip.CreateEntry($"collections/{image.CollectionId}/photos/{image.PhotoId}{suffix}.{extension}", CompressionLevel.NoCompression);
        await using var stream = await entry.OpenAsync(cancel);
        await stream.WriteAsync(content, cancel);
    }

    private static async Task WriteJsonAsync<T>(ZipArchive zip, string name, T value, CancellationToken cancel)
    {
        var entry = zip.CreateEntry(name);
        await using var stream = await entry.OpenAsync(cancel);
        await JsonSerializer.SerializeAsync(stream, value, Json, cancel);
    }

    private static async Task WriteRawAsync(ZipArchive zip, string name, string json, CancellationToken cancel)
    {
        var entry = zip.CreateEntry(name);
        await using var stream = await entry.OpenAsync(cancel);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteRawValue(json, skipInputValidation: true);
    }

    private static async Task WriteTextAsync(ZipArchive zip, string name, string text, CancellationToken cancel)
    {
        var entry = zip.CreateEntry(name);
        await using var stream = await entry.OpenAsync(cancel);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancel);
    }

    private static string Readme(DateTimeOffset now) => $"""
        Your data from Stikling

        This was made on {now:yyyy-MM-dd HH:mm} UTC. It is everything the Stikling server holds
        about you. Data that only lives on your devices and was never synced is not in it.

        account.json      What the sign-in service has about you, such as your name and email
                          address. The app fetched it when you asked for this download, and the
                          Stikling server doesn't keep it. Left out if it wasn't sent.
        person.json       Your account on the Stikling server.
        settings.json     Your settings, such as the theme. Left out if you have none.
        collections.json  The collections you are in, when you joined, and your role in each.
        collections/      One folder for each collection, named by its id.
          <kind>.json     The records of one kind, such as plants or timeline, as the app stores
                          them. Something you deleted is in there as only its id and dates, since
                          the rest is removed from the server when it's deleted.
          shareLinks.json The pages you shared with a link, and their settings. Left out if you
                          have none.
          photos/         Your photos. The file name is the photo's id. A file ending in -small
                          is the thumbnail.

        The .json files are plain text and open in any text editor.
        """;
}
