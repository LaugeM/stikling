using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Data;
using Stikling.Api.Photos;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;

namespace Stikling.Api.Sharing;

/// <summary>
/// The public pages of share links, the only part of the API anyone can reach without signing in.
/// What a page shows is decided in Core's <see cref="SharePage"/>. This file finds the records,
/// checks the link, and hands the result to a component that writes the HTML.
/// </summary>
public static class SharePageEndpoints
{
    /// <summary>Where the public pages are, before the share host's addresses are rewritten to it.</summary>
    public const string Prefix = "/s";

    /// <summary>No scripts, no frames, nothing from elsewhere but the app's font.</summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; font-src https://stikling.app; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSharePages(this IEndpointRouteBuilder app)
    {
        var pages = app.MapGroup(Prefix).AddEndpointFilter(async (context, next) =>
        {
            var headers = context.HttpContext.Response.Headers;
            headers["X-Robots-Tag"] = "noindex, nofollow";
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["Referrer-Policy"] = "no-referrer";
            return await next(context);
        });

        pages.MapGet("/{token}", async (string token, HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            http.Response.Headers.CacheControl = "no-cache";

            var looked = await LookUpAsync(db, token, clock);
            if (looked.Status is { } status)
                return Gone(status);
            var (link, zone, _, model) = looked.Page!;

            var shown = model.Items.SelectMany(i => i.Photos).Select(p => p.Id).ToHashSet();
            var smallOnes = await db.PhotoImages.AsNoTracking()
                .Where(i => i.CollectionId == link.CollectionId && i.Size == PhotoSize.Thumbnail && shown.Contains(i.PhotoId))
                .Select(i => i.PhotoId)
                .ToListAsync();

            var url = options.Value.UrlOf(token);
            var prefix = options.Value.IsShareHost(http.Request.Host.Host) ? $"/{token}" : $"{Prefix}/{token}";
            return (IResult)new RazorComponentResult<SharePageView>(new
            {
                Page = model,
                PageUrl = url,
                ImagePrefix = prefix,
                AbsoluteImagePrefix = url,
                Thumbnails = smallOnes.ToHashSet(),
                UpdatedDay = DayIn(model.Updated, zone),
                KeepUrl = $"{options.Value.AppUrl.TrimEnd('/')}/received/{token}",
            });
        });

        // A frozen copy of the page for someone who was given the plant, which they keep in their own app
        pages.MapGet("/{token}/copy", async (string token, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            http.Response.Headers.CacheControl = "no-cache";

            var looked = await LookUpAsync(db, token, clock);
            if (looked.Status is { } status)
                return Results.StatusCode(status);
            var (_, zone, subject, model) = looked.Page!;
            if (!model.CanKeep)
                return Results.NotFound();

            return Results.Ok(subject switch
            {
                Plant plant => ShareCopy.ForPlant(model, plant, zone),
                Propagation propagation => ShareCopy.ForPropagation(model, propagation, zone),
                _ => throw new InvalidOperationException("A link is to a plant or a propagation."),
            });
        });

        pages.MapGet("/{token}/photos/{photoId:guid}/{size}", async (
            string token, Guid photoId, string size, HttpContext http, StiklingDbContext db, PhotoStorage storage) =>
        {
            var link = await FindAsync(db, token);
            if (link is null)
                return Results.NotFound();
            if (link.TurnedOffAt is not null)
                return Results.StatusCode(StatusCodes.Status410Gone);
            if (PhotoEndpoints.ParseSize(size) is not { } which)
                return Results.NotFound();

            var subject = await db.Records.AsNoTracking()
                .Where(r => r.CollectionId == link.CollectionId && r.Kind == ShareLinkEndpoints.KindOf(link.SubjectType)
                    && r.Id == link.SubjectId && r.DeletedAt == null)
                .AnyAsync();
            var data = await db.Records.AsNoTracking()
                .Where(r => r.CollectionId == link.CollectionId && r.Kind == SyncKinds.Photos && r.Id == photoId && r.DeletedAt == null)
                .Select(r => r.Data)
                .SingleOrDefaultAsync();

            // The same rules the page follows: the subject's own photos, not a reference photo, and not one that is left out
            if (!subject || data is null || Read<Photo>(data) is not { } photo || photo.IsDeleted || photo.IsReference || link.LeftOutPhotoIds.Contains(photoId))
                return Results.NotFound();
            var own = photo.SubjectType == link.SubjectType && photo.SubjectId == link.SubjectId;
            if (!own && !(link.SubjectType == SubjectType.Plant && await IsRootingPhotoAsync(db, link, photo)))
                return Results.NotFound();

            var image = await PhotoEndpoints.ImageAsync(db, storage, link.CollectionId, photoId, which);
            if (image is not IStatusCodeHttpResult { StatusCode: StatusCodes.Status404NotFound })
            {
                http.Response.Headers.CacheControl = "public, max-age=300";
                // The app fetches these with CORS when someone keeps a copy, so a copy cached from the page itself mustn't be reused for it
                http.Response.Headers.Append("Vary", "Origin");
            }
            return image;
        });

        return app;
    }

    // A plant's page also shows the rooting it came from, so those photos of the propagation are served too
    private static async Task<bool> IsRootingPhotoAsync(StiklingDbContext db, ShareLink link, Photo photo)
    {
        if (photo.SubjectType != SubjectType.Propagation)
            return false;
        var plant = await OneAsync<Plant>(db, link.CollectionId, "plants", link.SubjectId);
        if (plant?.FromPropagationId != photo.SubjectId)
            return false;
        var from = await OneAsync<Propagation>(db, link.CollectionId, "propagations", photo.SubjectId);
        var zone = link.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(link.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
        return SharePage.RootingPhotos(plant, from, [photo], zone).Count > 0;
    }

    private sealed record Found(ShareLink Link, TimeZoneInfo Zone, Entity Subject, SharePageModel Model);

    // The link and what its page shows, or the status to answer with: 404 for an unknown token or a subject that is gone, 410 for a link turned off
    private sealed record Lookup(int? Status, Found? Page);

    private static async Task<Lookup> LookUpAsync(StiklingDbContext db, string token, TimeProvider clock)
    {
        var link = await FindAsync(db, token);
        if (link is null)
            return new Lookup(StatusCodes.Status404NotFound, null);
        if (link.TurnedOffAt is not null)
            return new Lookup(StatusCodes.Status410Gone, null);

        var zone = link.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(link.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
        var today = DayIn(clock.GetUtcNow(), zone);
        var settings = new ShareLinkSettings(link.ShowNotes, link.Name, link.Line, link.LeftOutPhotoIds);

        var page = link.SubjectType switch
        {
            SubjectType.Plant => await PlantPageAsync(db, link, settings, zone, today),
            SubjectType.Propagation => await PropagationPageAsync(db, link, settings, zone, today),
            _ => null,
        };
        return page is null
            ? new Lookup(StatusCodes.Status404NotFound, null)
            : new Lookup(null, new Found(link, zone, page.Value.Subject, page.Value.Model));
    }

    /// <summary>The small page for a link that isn't there (404) or was turned off (410).</summary>
    private static IResult Gone(int status) => new RazorComponentResult<ShareGone> { StatusCode = status };

    private static Task<ShareLink?> FindAsync(StiklingDbContext db, string token) =>
        // A token is 22 characters, so anything much longer is somebody trying things
        token.Length > 40
            ? Task.FromResult<ShareLink?>(null)
            : db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(l => l.Token == token);

    private static DateOnly DayIn(DateTimeOffset moment, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, zone).DateTime);

    private static async Task<(Entity Subject, SharePageModel Model)?> PlantPageAsync(
        StiklingDbContext db, ShareLink link, ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var plant = await OneAsync<Plant>(db, link.CollectionId, "plants", link.SubjectId);
        if (plant is null)
            return null;

        var parent = plant.ParentPlantId is { } parentId ? await OneAsync<Plant>(db, link.CollectionId, "plants", parentId) : null;
        var from = plant.FromPropagationId is { } fromId ? await OneAsync<Propagation>(db, link.CollectionId, "propagations", fromId) : null;
        var taken = await ByPropertyAsync<Propagation>(db, link.CollectionId, "propagations", "parentPlantId", plant.Id);
        var photos = await ByPropertyAsync<Photo>(db, link.CollectionId, SyncKinds.Photos, "subjectId", plant.Id);
        var entries = await ByPropertyAsync<TimelineEntry>(db, link.CollectionId, "timeline", "subjectId", plant.Id);
        var received = await ByPropertyAsync<ReceivedHistory>(db, link.CollectionId, "receivedHistories", "subjectId", plant.Id);

        var propagations = taken.ToDictionary(p => p.Id);
        if (from is not null)
            propagations[from.Id] = from;

        // The rooting it came from: the propagation's photos and entries, which ForPlant cuts off at the day it was potted up
        if (from is not null)
        {
            photos.AddRange(await ByPropertyAsync<Photo>(db, link.CollectionId, SyncKinds.Photos, "subjectId", from.Id));
            entries.AddRange(await ByPropertyAsync<TimelineEntry>(db, link.CollectionId, "timeline", "subjectId", from.Id));
            received.AddRange(await ByPropertyAsync<ReceivedHistory>(db, link.CollectionId, "receivedHistories", "subjectId", from.Id));
        }

        photos = await WithImagesAsync(db, link.CollectionId, photos);
        return (plant, SharePage.ForPlant(plant, parent, from, taken, photos, entries, received, propagations, settings, zone, today));
    }

    private static async Task<(Entity Subject, SharePageModel Model)?> PropagationPageAsync(
        StiklingDbContext db, ShareLink link, ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var propagation = await OneAsync<Propagation>(db, link.CollectionId, "propagations", link.SubjectId);
        if (propagation is null)
            return null;

        var parent = propagation.ParentPlantId is { } parentId ? await OneAsync<Plant>(db, link.CollectionId, "plants", parentId) : null;
        var photos = await ByPropertyAsync<Photo>(db, link.CollectionId, SyncKinds.Photos, "subjectId", propagation.Id);
        var entries = await ByPropertyAsync<TimelineEntry>(db, link.CollectionId, "timeline", "subjectId", propagation.Id);
        var received = await ByPropertyAsync<ReceivedHistory>(db, link.CollectionId, "receivedHistories", "subjectId", propagation.Id);

        photos = await WithImagesAsync(db, link.CollectionId, photos);
        return (propagation, SharePage.ForPropagation(propagation, parent, photos, entries, received, settings, zone, today));
    }

    // A photo's record syncs before its image does, and a page shouldn't show an image that isn't there yet
    private static async Task<List<Photo>> WithImagesAsync(StiklingDbContext db, Guid collectionId, List<Photo> photos)
    {
        var ids = photos.Select(p => p.Id).ToList();
        var there = await db.PhotoImages.AsNoTracking()
            .Where(i => i.CollectionId == collectionId && i.Size == PhotoSize.Full && ids.Contains(i.PhotoId))
            .Select(i => i.PhotoId)
            .ToListAsync();
        var set = there.ToHashSet();
        return photos.Where(p => set.Contains(p.Id)).ToList();
    }

    private static async Task<T?> OneAsync<T>(StiklingDbContext db, Guid collectionId, string kind, Guid id) where T : Entity
    {
        var data = await db.Records.AsNoTracking()
            .Where(r => r.CollectionId == collectionId && r.Kind == kind && r.Id == id && r.DeletedAt == null)
            .Select(r => r.Data)
            .SingleOrDefaultAsync();
        return data is null || Read<T>(data) is not { IsDeleted: false } record ? null : record;
    }

    /// <summary>
    /// The records of one kind whose JSON has this property set to this id, found in SQL so a page
    /// view doesn't read the whole collection. The names are camelCase, as the app stores them.
    /// </summary>
    private static async Task<List<T>> ByPropertyAsync<T>(StiklingDbContext db, Guid collectionId, string kind, string property, Guid value)
        where T : Entity
    {
        var path = $"$.{property}";
        var wanted = value.ToString();
        var rows = await db.Records
            .FromSql($"SELECT * FROM [Records] WHERE [CollectionId] = {collectionId} AND [Kind] = {kind} AND [DeletedAt] IS NULL AND JSON_VALUE([Data], {path}) = {wanted}")
            .AsNoTracking()
            .Select(r => r.Data)
            .ToListAsync();
        return rows.Select(Read<T>).OfType<T>().Where(r => !r.IsDeleted).ToList();
    }

    // A record this version can't read, like one from a newer app, is left out instead of breaking the page
    private static T? Read<T>(string data) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
