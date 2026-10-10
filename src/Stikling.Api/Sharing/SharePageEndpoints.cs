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
            http.Response.Headers.CacheControl = "public, max-age=60";

            var link = await FindAsync(db, token);
            if (link is null)
                return Gone(StatusCodes.Status404NotFound);
            if (link.TurnedOffAt is not null)
                return Gone(StatusCodes.Status410Gone);

            var zone = link.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(link.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
            var today = DayIn(clock.GetUtcNow(), zone);
            var settings = new ShareLinkSettings(link.ShowNotes, link.Name, link.Line, link.LeftOutPhotoIds);

            var model = link.SubjectType switch
            {
                SubjectType.Plant => await PlantPageAsync(db, link, settings, zone, today),
                SubjectType.Propagation => await PropagationPageAsync(db, link, settings, zone, today),
                _ => null,
            };

            if (model is null)
                return Gone(StatusCodes.Status404NotFound);

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

            // The same rules the page follows: the subject's own photos, and not one that is left out
            if (!subject || data is null || Read<Photo>(data) is not { } photo
                || photo.IsDeleted || photo.SubjectType != link.SubjectType || photo.SubjectId != link.SubjectId
                || link.LeftOutPhotoIds.Contains(photoId))
                return Results.NotFound();

            var image = await PhotoEndpoints.ImageAsync(db, storage, link.CollectionId, photoId, which);
            if (image is not IStatusCodeHttpResult { StatusCode: StatusCodes.Status404NotFound })
                http.Response.Headers.CacheControl = "public, max-age=3600";
            return image;
        });

        return app;
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

    private static async Task<SharePageModel?> PlantPageAsync(
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

        var propagations = taken.ToDictionary(p => p.Id);
        if (from is not null)
            propagations[from.Id] = from;

        photos = await WithImagesAsync(db, link.CollectionId, photos);
        return SharePage.ForPlant(plant, parent, from, taken, photos, entries, propagations, settings, zone, today);
    }

    private static async Task<SharePageModel?> PropagationPageAsync(
        StiklingDbContext db, ShareLink link, ShareLinkSettings settings, TimeZoneInfo zone, DateOnly today)
    {
        var propagation = await OneAsync<Propagation>(db, link.CollectionId, "propagations", link.SubjectId);
        if (propagation is null)
            return null;

        var parent = propagation.ParentPlantId is { } parentId ? await OneAsync<Plant>(db, link.CollectionId, "plants", parentId) : null;
        var photos = await ByPropertyAsync<Photo>(db, link.CollectionId, SyncKinds.Photos, "subjectId", propagation.Id);
        var entries = await ByPropertyAsync<TimelineEntry>(db, link.CollectionId, "timeline", "subjectId", propagation.Id);

        photos = await WithImagesAsync(db, link.CollectionId, photos);
        return SharePage.ForPropagation(propagation, parent, photos, entries, settings, zone, today);
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
