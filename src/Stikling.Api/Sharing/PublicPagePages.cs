using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Data;
using Stikling.Api.Photos;
using Stikling.Core.Compare;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;

namespace Stikling.Api.Sharing;

/// <summary>What the views of a public page need besides the page itself.</summary>
/// <param name="Language">en or da.</param>
/// <param name="Title">The title of the whole page.</param>
/// <param name="LinkPrefix">The start of the paths on this page, like /s/@fern or /@fern.</param>
/// <param name="PageUrl">The address of the page being drawn, in full.</param>
/// <param name="RootUrl">The address of the page's front, in full.</param>
/// <param name="Canonical">The address search engines should use for this page, when it has a name.</param>
/// <param name="CompareUrl">The app's page that compares this page with your own lists, with the address as it was asked for.</param>
/// <param name="SwapContact">The contact link, only when the page is open to swap requests.</param>
public sealed record PublicPageContext(
    string Language,
    string Title,
    string LinkPrefix,
    string PageUrl,
    string RootUrl,
    string? Canonical,
    bool Searchable,
    DateOnly UpdatedDay,
    PublicPageLayout Layout,
    bool StoriesOn,
    IReadOnlyList<PublicPartLink> Links,
    HashSet<Guid> Thumbnails,
    string CompareUrl = "",
    string? SwapContact = null);

/// <summary>
/// The public pages of whole collections, which anyone can open. What they show is decided in Core's
/// <see cref="PublicCollectionPage"/>. This file finds the page and the records, and hands the result
/// to a component that writes the HTML. They share the address space of <see cref="SharePageEndpoints"/>:
/// the address is a token or a name with a leading @.
/// </summary>
public static class PublicPagePages
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Most urls a sitemap can hold.</summary>
    private const int MaxSitemapUrls = 50_000;

    internal static void Map(RouteGroupBuilder pages)
    {
        pages.MapGet("/robots.txt", (HttpContext http, IOptions<ShareOptions> options) =>
        {
            if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
                return Results.NotFound();
            Asked(http);
            return Results.Text($"User-agent: *\nAllow: /\nSitemap: {options.Value.BaseUrl.TrimEnd('/')}/sitemap.xml\n", "text/plain");
        });

        pages.MapGet("/sitemap.xml", async (HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
                return Results.NotFound();
            Asked(http);

            // Building it reads every listed collection, so it is kept for a while instead of built on every request
            var now = clock.GetUtcNow();
            var kept = sitemap;
            if (kept is null || now - kept.Value.At >= TimeSpan.FromMinutes(options.Value.SitemapCacheMinutes))
            {
                kept = (await SitemapAsync(db, options.Value), now);
                sitemap = kept;
            }
            return Results.Text(kept.Value.Xml, "application/xml", Encoding.UTF8);
        });

        foreach (var (segment, kind) in new[]
        {
            ("plants", PublicPartKind.Plants), ("cuttings", PublicPartKind.Cuttings),
            ("swap", PublicPartKind.Swap), ("wishlist", PublicPartKind.Wishlist),
        })
        {
            var which = kind;
            pages.MapGet($"/{{address}}/{segment}", (string address, HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
                PartAsync(http, db, options.Value, address, which, segment, clock));
        }

        // What the page shows, as a list the app can compare with its own. Open to the app's origin like the rest.
        pages.MapGet("/{address}/compare", (string address, HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options) =>
            CompareAsync(http, db, options.Value, address));

        pages.MapGet("/{address}/plants/{id:guid}", (string address, Guid id, HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
            StoryAsync(http, db, options.Value, address, id, SubjectType.Plant, clock));
        pages.MapGet("/{address}/cuttings/{id:guid}", (string address, Guid id, HttpContext http, StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
            StoryAsync(http, db, options.Value, address, id, SubjectType.Propagation, clock));
    }

    // robots.txt and the sitemap are for the search engines, so the group's noindex doesn't belong on them
    private static void Asked(HttpContext http)
    {
        http.Response.Headers.Remove("X-Robots-Tag");
        http.Response.Headers.CacheControl = "public, max-age=3600";
    }

    // The page an address stands for

    private sealed record Live(PublicPage Page, string Address, ShareOptions Options, HttpContext Http, TimeZoneInfo Zone, CultureInfo Culture)
    {
        public PublicPageSettings Settings => new(
            Page.Title, Page.Line, Page.Layout, Page.Order, Page.ShowPropagations, Page.ShowSwap, Page.ShowWishlist, Page.ShowStories);

        /// <summary>The start of the paths, with the address as it was asked for.</summary>
        public string LinkPrefix => Options.IsShareHost(Http.Request.Host.Host) ? $"/{Address}" : $"{SharePageEndpoints.Prefix}/{Address}";

        public string RootUrl => Page.Name is null ? Options.UrlOf(Page.Token) : Options.NamedUrlOf(Page.Name);

        /// <summary>The address of a path on this page, the way it was asked for, so a token visit stays on the token.</summary>
        public string Url(string path) => (Address.StartsWith('@') && Page.Name is not null ? Options.NamedUrlOf(Page.Name) : Options.UrlOf(Address)) + path;

        public string? Canonical(string path) => Page.Name is null ? null : Options.NamedUrlOf(Page.Name) + path;
    }

    // The page, or the small page that says it isn't there (404) or was turned off (410)
    private static async Task<(Live? Live, IResult? Failure)> ResolveAsync(HttpContext http, StiklingDbContext db, ShareOptions options, string address)
    {
        http.Response.Headers.CacheControl = "no-cache";

        PublicPage? page;
        if (address.StartsWith('@'))
        {
            var name = PublicPageRules.NormalizeName(address);
            if (name is null || PublicPageRules.CheckName(name) is not null)
                return (null, SharePageEndpoints.Gone(StatusCodes.Status404NotFound));

            page = await db.PublicPages.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name && p.TurnedOffAt == null);
            if (page is null)
            {
                // A name given up stays given up, so its address says "turned off" and never "not here"
                var hash = PublicPageRules.NameHash(name);
                var retired = await db.PublicPageNames.AnyAsync(n => n.NameHash == hash);
                return (null, SharePageEndpoints.Gone(retired ? StatusCodes.Status410Gone : StatusCodes.Status404NotFound));
            }
        }
        else
        {
            // A token is 22 characters, so anything much longer is somebody trying things
            page = address.Length > 40 ? null : await db.PublicPages.AsNoTracking().SingleOrDefaultAsync(p => p.Token == address);
            if (page is null)
                return (null, SharePageEndpoints.Gone(StatusCodes.Status404NotFound));
            if (page.TurnedOffAt is not null)
                return (null, SharePageEndpoints.Gone(StatusCodes.Status410Gone));
        }

        if (page.Searchable)
            http.Response.Headers.Remove("X-Robots-Tag");

        var zone = page.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(page.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
        return (new Live(page, address, options, http, zone, CultureOf(page.Language)), null);
    }

    private static CultureInfo CultureOf(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language == "da" ? "da" : "en-GB");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    // RazorComponentResult runs after the handler has returned, so the culture set while the handler ran is gone by then
    private sealed class InCulture(IResult inner, CultureInfo culture) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return inner.ExecuteAsync(httpContext);
        }
    }

    private static void Speak(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static IResult Written<T>(CultureInfo culture, object parameters) where T : Microsoft.AspNetCore.Components.IComponent =>
        new InCulture(new RazorComponentResult<T>(parameters), culture);

    // Records

    private sealed record Records(List<Plant> Plants, List<Propagation> Propagations, List<Wish> Wishes);

    private static async Task<List<T>> AllAsync<T>(StiklingDbContext db, Guid collectionId, string kind) where T : Entity
    {
        var rows = await db.Records.AsNoTracking()
            .Where(r => r.CollectionId == collectionId && r.Kind == kind && r.DeletedAt == null)
            .Select(r => r.Data)
            .ToListAsync();
        return rows.Select(SharePageEndpoints.Read<T>).OfType<T>().Where(r => !r.IsDeleted).ToList();
    }

    private static async Task<PublicSubjects> SubjectsAsync(StiklingDbContext db, Live live)
    {
        var settings = live.Settings;
        var plants = await AllAsync<Plant>(db, live.Page.CollectionId, "plants");
        var propagations = settings.ShowPropagations || settings.ShowSwap
            ? await AllAsync<Propagation>(db, live.Page.CollectionId, "propagations")
            : [];
        var wishes = settings.ShowWishlist ? await AllAsync<Wish>(db, live.Page.CollectionId, "wishes") : [];
        return PublicCollectionPage.Subjects(settings, plants, propagations, wishes);
    }

    /// <summary>
    /// The photos of these subjects, found in SQL so the page doesn't read the whole collection, and
    /// only those whose image has reached the server.
    /// </summary>
    private static async Task<List<Photo>> PhotosOfAsync(StiklingDbContext db, Guid collectionId, IReadOnlyCollection<Guid> subjectIds)
    {
        if (subjectIds.Count == 0)
            return [];

        var ids = JsonSerializer.Serialize(subjectIds.Select(i => i.ToString()));
        var rows = await db.Records
            .FromSql($"SELECT * FROM [Records] WHERE [CollectionId] = {collectionId} AND [Kind] = {SyncKinds.Photos} AND [DeletedAt] IS NULL AND JSON_VALUE([Data], '$.subjectId') IN (SELECT value FROM OPENJSON({ids}))")
            .AsNoTracking()
            .Select(r => r.Data)
            .ToListAsync();
        var photos = rows.Select(SharePageEndpoints.Read<Photo>).OfType<Photo>().Where(p => !p.IsDeleted).ToList();
        return await SharePageEndpoints.WithImagesAsync(db, collectionId, photos);
    }

    // The photos that have a small image. The others show the full one.
    private static async Task<HashSet<Guid>> ThumbnailsAsync(StiklingDbContext db, Guid collectionId, IEnumerable<Guid> photoIds)
    {
        var wanted = photoIds.Distinct().ToList();
        if (wanted.Count == 0)
            return [];
        var there = await db.PhotoImages.AsNoTracking()
            .Where(i => i.CollectionId == collectionId && i.Size == PhotoSize.Thumbnail && wanted.Contains(i.PhotoId))
            .Select(i => i.PhotoId)
            .ToListAsync();
        return there.ToHashSet();
    }

    private static PublicPageContext ContextFor(Live live, string title, string path, DateTimeOffset updated, IReadOnlyList<PublicPartLink> links, HashSet<Guid> thumbnails) =>
        new(live.Page.Language, title, live.LinkPrefix, live.Url(path), live.RootUrl, live.Canonical(path), live.Page.Searchable,
            SharePageEndpoints.DayIn(updated, live.Zone), live.Page.Layout, live.Page.ShowStories, links, thumbnails,
            $"{live.Options.AppUrl.TrimEnd('/')}/compare/{(live.Address.StartsWith('@') ? live.Address.ToLowerInvariant() : live.Address)}",
            live.Page.SwapRequests ? live.Page.SwapContact : null);

    // The front page

    internal static async Task<IResult> FrontAsync(HttpContext http, StiklingDbContext db, ShareOptions options, string address, TimeProvider clock)
    {
        var (live, failure) = await ResolveAsync(http, db, options, address);
        if (live is null)
            return failure!;
        Speak(live.Culture);

        var subjects = await SubjectsAsync(db, live);
        var photos = await PhotosOfAsync(db, live.Page.CollectionId, PublicCollectionPage.FrontIds(subjects));
        var model = PublicCollectionPage.Front(subjects, photos, live.Page.UpdatedAt);
        var covers = model.Parts.SelectMany(p => p.Tiles).Select(t => t.Cover?.Id).OfType<Guid>().ToList();
        var thumbnails = await ThumbnailsAsync(db, live.Page.CollectionId, covers);

        var context = ContextFor(live, model.Title, "", model.Updated, PublicCollectionPage.Links(subjects), thumbnails);
        return Written<PublicPageView>(live.Culture, new { Page = model, Context = context });
    }

    // A part, a numbered page at a time

    private static async Task<IResult> PartAsync(
        HttpContext http, StiklingDbContext db, ShareOptions options, string address, PublicPartKind kind, string segment, TimeProvider clock)
    {
        var (live, failure) = await ResolveAsync(http, db, options, address);
        if (live is null)
            return failure!;
        Speak(live.Culture);

        var number = 1;
        if (http.Request.Query.TryGetValue("page", out var asked) && !int.TryParse(asked.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out number))
            return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

        var subjects = await SubjectsAsync(db, live);
        var photos = await PhotosOfAsync(db, live.Page.CollectionId, PublicCollectionPage.PartIds(subjects, kind, number));
        if (PublicCollectionPage.Part(subjects, kind, number, photos) is not { } part)
            return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

        var covers = part.Tiles.Select(t => t.Cover?.Id).OfType<Guid>().ToList();
        var thumbnails = await ThumbnailsAsync(db, live.Page.CollectionId, covers);
        var title = PublicCollectionPage.TitleOf(live.Page.Title);
        var updated = new[] { live.Page.UpdatedAt }
            .Concat(subjects.SubjectsOf(kind).Select(s => s.UpdatedAt))
            .Concat(subjects.Wishlist.Select(l => l.UpdatedAt))
            .Max();

        var path = $"/{segment}" + (number > 1 ? $"?page={number}" : "");
        var context = ContextFor(live, title, path, updated, PublicCollectionPage.Links(subjects), thumbnails);
        return Written<PublicPartView>(live.Culture, new { Part = part, Context = context, Segment = segment });
    }

    // The page as a friend's list, for comparing with one's own. Only what the page itself shows.

    private static async Task<IResult> CompareAsync(HttpContext http, StiklingDbContext db, ShareOptions options, string address)
    {
        var (live, failure) = await ResolveAsync(http, db, options, address);
        if (live is null)
            return failure!;
        Speak(live.Culture);

        var subjects = await SubjectsAsync(db, live);
        var photos = await PhotosOfAsync(db, live.Page.CollectionId, PublicCollectionPage.CompareIds(subjects));
        var list = PublicCollectionPage.ForCompare(subjects, photos, live.Page.SwapRequests, live.Page.SwapContact)
            with { Title = PublicCollectionPage.TitleOf(live.Page.Title) };

        var covers = list.Plants.Select(p => p.Cover?.Id).OfType<Guid>().Distinct().ToList();
        var thumbnails = await ThumbnailsAsync(db, live.Page.CollectionId, covers);
        var urls = covers.ToDictionary(id => id, id => live.Url($"/photos/{id}/{(thumbnails.Contains(id) ? "thumb" : "full")}"));

        var asked = address.StartsWith('@') ? address.ToLowerInvariant() : address;
        return Results.Ok(new FriendPageResponse(list, asked, urls));
    }

    // A story: what a plant's or a cutting's page shows, for a collection that has stories on

    private static async Task<IResult> StoryAsync(
        HttpContext http, StiklingDbContext db, ShareOptions options, string address, Guid id, SubjectType type, TimeProvider clock)
    {
        var (live, failure) = await ResolveAsync(http, db, options, address);
        if (live is null)
            return failure!;
        Speak(live.Culture);

        var settings = live.Settings;
        if (!settings.ShowStories)
            return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

        var collectionId = live.Page.CollectionId;
        var today = SharePageEndpoints.DayIn(clock.GetUtcNow(), live.Zone);
        SharePageModel? story;

        if (type == SubjectType.Plant)
        {
            var plant = await SharePageEndpoints.OneAsync<Plant>(db, collectionId, "plants", id);
            if (plant is null)
                return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

            var parent = plant.ParentPlantId is { } parentId ? await SharePageEndpoints.OneAsync<Plant>(db, collectionId, "plants", parentId) : null;
            var from = plant.FromPropagationId is { } fromId ? await SharePageEndpoints.OneAsync<Propagation>(db, collectionId, "propagations", fromId) : null;
            var taken = await SharePageEndpoints.ByPropertyAsync<Propagation>(db, collectionId, "propagations", "parentPlantId", plant.Id);
            var (photos, entries, received) = await HistoryOfAsync(db, collectionId, plant.Id, from?.Id);

            story = PublicCollectionPage.PlantStory(settings, plant, parent, from, taken, photos, entries, received, live.Zone, today);
        }
        else
        {
            var propagation = await SharePageEndpoints.OneAsync<Propagation>(db, collectionId, "propagations", id);
            if (propagation is null)
                return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

            var parent = propagation.ParentPlantId is { } parentId ? await SharePageEndpoints.OneAsync<Plant>(db, collectionId, "plants", parentId) : null;
            var (photos, entries, received) = await HistoryOfAsync(db, collectionId, propagation.Id, null);

            story = PublicCollectionPage.PropagationStory(settings, propagation, parent, photos, entries, received, live.Zone, today);
        }

        if (story is null)
            return SharePageEndpoints.Gone(StatusCodes.Status404NotFound);

        var shown = story.Items.SelectMany(i => i.Photos).Select(p => p.Id).ToList();
        var thumbnails = await ThumbnailsAsync(db, collectionId, shown);
        var title = PublicCollectionPage.TitleOf(live.Page.Title);
        var segment = type == SubjectType.Plant ? "plants" : "cuttings";
        var context = ContextFor(live, title, $"/{segment}/{id}", story.Updated, [], thumbnails);
        return Written<PublicStoryView>(live.Culture, new { Story = story, Context = context });
    }

    // The subject's photos, entries and received histories, and the same of the propagation it came from
    private static async Task<(List<Photo> Photos, List<TimelineEntry> Entries, List<ReceivedHistory> Received)> HistoryOfAsync(
        StiklingDbContext db, Guid collectionId, Guid subjectId, Guid? fromId)
    {
        var photos = await SharePageEndpoints.ByPropertyAsync<Photo>(db, collectionId, SyncKinds.Photos, "subjectId", subjectId);
        var entries = await SharePageEndpoints.ByPropertyAsync<TimelineEntry>(db, collectionId, "timeline", "subjectId", subjectId);
        var received = await SharePageEndpoints.ByPropertyAsync<ReceivedHistory>(db, collectionId, "receivedHistories", "subjectId", subjectId);
        if (fromId is { } from)
        {
            photos.AddRange(await SharePageEndpoints.ByPropertyAsync<Photo>(db, collectionId, SyncKinds.Photos, "subjectId", from));
            entries.AddRange(await SharePageEndpoints.ByPropertyAsync<TimelineEntry>(db, collectionId, "timeline", "subjectId", from));
            received.AddRange(await SharePageEndpoints.ByPropertyAsync<ReceivedHistory>(db, collectionId, "receivedHistories", "subjectId", from));
        }
        return (await SharePageEndpoints.WithImagesAsync(db, collectionId, photos), entries, received);
    }

    // Photos

    internal static async Task<IResult> PhotoAsync(HttpContext http, StiklingDbContext db, PhotoStorage storage, string address, Guid photoId, string size)
    {
        // The options aren't needed to find the page, only to write its addresses
        var (live, failure) = await ResolveAsync(http, db, http.RequestServices.GetRequiredService<IOptions<ShareOptions>>().Value, address);
        if (live is null)
            return failure!;
        // Images are not pages, so they are cached
        http.Response.Headers.Remove("Cache-Control");

        if (PhotoEndpoints.ParseSize(size) is not { } which)
            return Results.NotFound();

        var collectionId = live.Page.CollectionId;
        var data = await db.Records.AsNoTracking()
            .Where(r => r.CollectionId == collectionId && r.Kind == SyncKinds.Photos && r.Id == photoId && r.DeletedAt == null)
            .Select(r => r.Data)
            .SingleOrDefaultAsync();
        if (data is null || SharePageEndpoints.Read<Photo>(data) is not { } photo || !PublicCollectionPage.ShowsPhoto(photo))
            return Results.NotFound();

        Plant? plant = null;
        Propagation? propagation = null;
        var pottedUp = new List<Plant>();
        if (photo.SubjectType == SubjectType.Plant)
            plant = await SharePageEndpoints.OneAsync<Plant>(db, collectionId, "plants", photo.SubjectId);
        else if (photo.SubjectType == SubjectType.Propagation)
        {
            propagation = await SharePageEndpoints.OneAsync<Propagation>(db, collectionId, "propagations", photo.SubjectId);
            if (propagation is not null)
                pottedUp = await SharePageEndpoints.ByPropertyAsync<Plant>(db, collectionId, "plants", "fromPropagationId", propagation.Id);
        }
        else
            return Results.NotFound();
        if (plant is null && propagation is null)
            return Results.NotFound();

        var own = await PhotosOfAsync(db, collectionId, [photo.SubjectId]);
        if (!PublicCollectionPage.ServesPhoto(live.Settings, photo, plant, propagation, own, pottedUp, live.Zone))
            return Results.NotFound();

        var image = await PhotoEndpoints.ImageAsync(db, storage, collectionId, photoId, which);
        if (image is not IStatusCodeHttpResult { StatusCode: StatusCodes.Status404NotFound })
        {
            http.Response.Headers.CacheControl = "public, max-age=300";
            http.Response.Headers.Append("Vary", "Origin");
        }
        return image;
    }

    // The sitemap: the pages that asked to be listed, with their parts and, with stories on, every story

    // The last sitemap built and when. Two requests at once may both build it, which is harmless
    private static (string Xml, DateTimeOffset At)? sitemap;

    private static async Task<string> SitemapAsync(StiklingDbContext db, ShareOptions options)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset");
        var count = 0;

        void Add(string url, DateTimeOffset modified)
        {
            if (count >= MaxSitemapUrls)
                return;
            count++;
            urlset.Add(new XElement(ns + "url", new XElement(ns + "loc", url), new XElement(ns + "lastmod", modified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }

        var pages = await db.PublicPages.AsNoTracking()
            .Where(p => p.TurnedOffAt == null && p.Searchable)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        foreach (var page in pages)
        {
            if (count >= MaxSitemapUrls)
                break;

            var live = new Live(page, page.Name is null ? page.Token : $"@{page.Name}", options, null!, TimeZoneInfo.Utc, CultureInfo.InvariantCulture);
            var subjects = await SubjectsAsync(db, live);
            var root = live.RootUrl;
            var modified = subjects.Plants.Concat(subjects.Cuttings).Concat(subjects.Swap).Select(s => s.UpdatedAt)
                .Concat(subjects.Wishlist.Select(l => l.UpdatedAt)).Append(page.UpdatedAt).Max();

            Add(root, modified);
            foreach (var (segment, kind) in new[]
            {
                ("plants", PublicPartKind.Plants), ("cuttings", PublicPartKind.Cuttings),
                ("swap", PublicPartKind.Swap), ("wishlist", PublicPartKind.Wishlist),
            })
            {
                if (!subjects.IsOn(kind) || (kind != PublicPartKind.Plants && subjects.Total(kind) == 0))
                    continue;
                var perPage = kind == PublicPartKind.Wishlist ? PublicPageRules.LinesPerPage : PublicPageRules.TilesPerPage;
                var pageCount = Math.Max(1, (subjects.Total(kind) + perPage - 1) / perPage);
                for (var n = 1; n <= pageCount; n++)
                    Add($"{root}/{segment}" + (n > 1 ? $"?page={n}" : ""), modified);
            }

            if (page.ShowStories)
            {
                foreach (var plant in subjects.Plants)
                    Add($"{root}/plants/{plant.Id}", plant.UpdatedAt);
                foreach (var cutting in subjects.Cuttings)
                    Add($"{root}/cuttings/{cutting.Id}", cutting.UpdatedAt);
            }
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), urlset).Declaration + "\n" + urlset;
    }
}
