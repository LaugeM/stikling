using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Core.Sharing;

namespace Stikling.Api.Sharing;

/// <summary>
/// What the app calls to turn the public page of a collection on and off and to change it. A
/// collection has one page that is on at a time. The page itself is in <see cref="PublicPagePages"/>.
/// </summary>
public static class PublicPageEndpoints
{
    public static IEndpointRouteBuilder MapPublicPageSettings(this IEndpointRouteBuilder app)
    {
        var pages = app.MapGroup("/collections/{collectionId:guid}/public-page");

        pages.MapGet("", async (Guid collectionId, StiklingDbContext db, IOptions<ShareOptions> options) =>
        {
            var page = await db.PublicPages.AsNoTracking().SingleOrDefaultAsync(p => p.CollectionId == collectionId && p.TurnedOffAt == null);
            return page is null ? Results.NotFound() : Results.Ok(Info(page, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.View);

        pages.MapPost("", async (
            Guid collectionId, TurnOnPublicPageRequest? request, CurrentPerson current,
            StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            // The share domain is set up by hand after a deploy, and until then the page would lead nowhere
            if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
                return Results.Problem("Public pages aren't set up on the server yet.", statusCode: StatusCodes.Status503ServiceUnavailable);

            if (await current.FindAsync() is not { } person)
                return Results.NotFound();

            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Two devices can ask at once, so the checks and the insert take turns
            await db.LockCollectionAsync(collectionId);

            var existing = await db.PublicPages.AsNoTracking().SingleOrDefaultAsync(p => p.CollectionId == collectionId && p.TurnedOffAt == null);
            if (existing is not null)
                return Results.Ok(Info(existing, options.Value));

            var since = now.AddDays(-1);
            var recent = await db.PublicPages.CountAsync(p => p.CreatedBy == person.Id && p.CreatedAt > since);
            if (recent >= PublicPageRules.MaxTurnedOnPerPersonPerDay)
            {
                return Results.Problem(
                    $"You have turned on {PublicPageRules.MaxTurnedOnPerPersonPerDay} public pages today. Try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var page = new PublicPage
            {
                Token = NewToken(),
                CollectionId = collectionId,
                CreatedBy = person.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Language = PublicPageLanguages.Normalize(request?.Language),
                TimeZone = ShareLinkEndpoints.KnownZone(request?.TimeZone),
            };
            db.PublicPages.Add(page);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Created($"/collections/{collectionId}/public-page", Info(page, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        pages.MapPut("", async (
            Guid collectionId, UpdatePublicPageRequest request,
            StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            var title = ShareLinkRules.Clean(request.Title);
            var line = ShareLinkRules.Clean(request.Line);

            var errors = new Dictionary<string, string[]>();
            if (title is { Length: > PublicPageRules.MaxTitleLength })
                errors["title"] = [$"The title can have at most {PublicPageRules.MaxTitleLength} characters."];
            if (line is { Length: > PublicPageRules.MaxLineLength })
                errors["line"] = [$"The line can have at most {PublicPageRules.MaxLineLength} characters."];
            if (!Enum.IsDefined(request.Layout))
                errors["layout"] = ["Unknown layout."];
            if (!Enum.IsDefined(request.Order))
                errors["order"] = ["Unknown order."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var page = await db.PublicPages.SingleOrDefaultAsync(p => p.CollectionId == collectionId && p.TurnedOffAt == null);
            if (page is null)
                return Results.NotFound();

            page.Title = title;
            page.Line = line;
            page.Layout = request.Layout;
            page.Order = request.Order;
            page.ShowPropagations = request.ShowPropagations;
            page.ShowSwap = request.ShowSwap;
            page.ShowWishlist = request.ShowWishlist;
            page.ShowStories = request.ShowStories;
            page.Searchable = request.Searchable;
            // A device that doesn't say which language it uses leaves the page's as it is
            if (request.Language is not null)
                page.Language = PublicPageLanguages.Normalize(request.Language);
            page.TimeZone = ShareLinkEndpoints.KnownZone(request.TimeZone);
            page.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();

            return Results.Ok(Info(page, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        pages.MapPut("/name", async (
            Guid collectionId, SetPublicPageNameRequest request,
            StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            var name = PublicPageRules.NormalizeName(request.Name);
            if (name is not null && PublicPageRules.CheckName(name) is { } problem)
            {
                return Results.Problem(
                    "That name can't be used.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["problem"] = problem.ToString() });
            }

            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Names are claimed one collection at a time, and the page's own limit is read and written here
            await db.LockCollectionAsync(collectionId);

            var page = await db.PublicPages.SingleOrDefaultAsync(p => p.CollectionId == collectionId && p.TurnedOffAt == null);
            if (page is null)
                return Results.NotFound();

            // Giving the name up keeps its hash, so nobody can take it, and the old address answers "turned off"
            if (name is null)
            {
                if (page.Name is not null)
                {
                    page.Name = null;
                    page.UpdatedAt = now;
                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                return Results.Ok(Info(page, options.Value));
            }

            if (page.Name == name)
                return Results.Ok(Info(page, options.Value));

            var hash = PublicPageRules.NameHash(name);
            if (await db.PublicPageNames.AnyAsync(n => n.NameHash == hash))
                return Taken();

            var since = now.AddDays(-1);
            var claims = page.NameClaims.Where(c => c > since).ToList();
            if (claims.Count >= PublicPageRules.MaxNamesPerPagePerDay)
            {
                return Results.Problem(
                    $"The name of a page can be changed {PublicPageRules.MaxNamesPerPagePerDay} times a day. Try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            db.PublicPageNames.Add(new PublicPageName { NameHash = hash });
            claims.Add(now);
            page.NameClaims = claims;
            page.Name = name;
            page.UpdatedAt = now;
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Someone else claimed the same name at the same moment, in another collection
                return Taken();
            }

            return Results.Ok(Info(page, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        // Turning off keeps the row, so the address answers "turned off" from then on. The name is given up and retired.
        pages.MapDelete("", async (Guid collectionId, StiklingDbContext db, TimeProvider clock) =>
        {
            var page = await db.PublicPages.SingleOrDefaultAsync(p => p.CollectionId == collectionId && p.TurnedOffAt == null);
            if (page is not null)
            {
                var now = clock.GetUtcNow();
                page.TurnedOffAt = now;
                page.Name = null;
                page.UpdatedAt = now;
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        return app;
    }

    // The same words whether the name is in use or was given up
    private static IResult Taken() =>
        Results.Problem("That name can't be used.", statusCode: StatusCodes.Status409Conflict);

    internal static PublicPageInfo Info(PublicPage page, ShareOptions options) =>
        new(page.Id, options.UrlOf(page.Token), page.Name is null ? null : options.NamedUrlOf(page.Name), page.Name, page.Title, page.Line,
            page.Layout, page.Order, page.ShowPropagations, page.ShowSwap, page.ShowWishlist, page.ShowStories, page.Searchable,
            page.Language, page.CreatedAt);

    /// <summary>16 random bytes as base64url without padding, 22 characters that can't be guessed.</summary>
    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
}
