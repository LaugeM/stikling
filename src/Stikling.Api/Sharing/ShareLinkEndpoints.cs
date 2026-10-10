using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Api.Sharing;

/// <summary>
/// What the app calls to turn sharing on and off for a plant or propagation. The public pages
/// themselves are in <see cref="SharePageEndpoints"/>.
/// </summary>
public static class ShareLinkEndpoints
{
    public static IEndpointRouteBuilder MapShareLinks(this IEndpointRouteBuilder app)
    {
        var shares = app.MapGroup("/collections/{collectionId:guid}/shares");

        shares.MapGet("", async (Guid collectionId, StiklingDbContext db, IOptions<ShareOptions> options) =>
        {
            var links = await db.ShareLinks.AsNoTracking()
                .Where(l => l.CollectionId == collectionId && l.TurnedOffAt == null)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();

            return links.Select(l => Info(l, options.Value)).ToList();
        })
        .RequireAuthorization(CollectionPolicies.View);

        shares.MapPost("", async (
            Guid collectionId, CreateShareLinkRequest request, CurrentPerson current,
            StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            if (request.SubjectType is not (SubjectType.Plant or SubjectType.Propagation))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["subjectType"] = ["Only a plant or a propagation can be shared."],
                });
            }

            // The share domain is set up by hand after a deploy, and until then a link would lead nowhere
            if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
                return Results.Problem("Share links aren't set up on the server yet.", statusCode: StatusCodes.Status503ServiceUnavailable);

            if (await current.FindAsync() is not { } person)
                return Results.NotFound();

            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Two devices can ask at once, so the checks and the insert take turns
            await db.LockCollectionAsync(collectionId);

            var kind = KindOf(request.SubjectType);
            var subject = await db.Records
                .Where(r => r.CollectionId == collectionId && r.Kind == kind && r.Id == request.SubjectId)
                .Select(r => new { r.DeletedAt })
                .SingleOrDefaultAsync();
            if (subject is null || subject.DeletedAt is not null)
            {
                return Results.Problem(
                    "It isn't on the server yet. Sync and try again.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var existing = await db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(l =>
                l.CollectionId == collectionId && l.SubjectId == request.SubjectId && l.TurnedOffAt == null);
            if (existing is not null)
                return Results.Ok(Info(existing, options.Value));

            var active = await db.ShareLinks.CountAsync(l => l.CollectionId == collectionId && l.TurnedOffAt == null);
            if (active >= ShareLinkRules.MaxActivePerCollection)
            {
                return Results.Problem(
                    $"This collection already has {ShareLinkRules.MaxActivePerCollection} shared pages. Turn one off to share another.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var since = now.AddDays(-1);
            var recent = await db.ShareLinks.CountAsync(l => l.CreatedBy == person.Id && l.CreatedAt > since);
            if (recent >= ShareLinkRules.MaxCreatedPerPersonPerDay)
            {
                return Results.Problem(
                    $"You have turned on {ShareLinkRules.MaxCreatedPerPersonPerDay} shared pages today. Try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var link = new ShareLink
            {
                Token = NewToken(),
                CollectionId = collectionId,
                SubjectType = request.SubjectType,
                SubjectId = request.SubjectId,
                CreatedBy = person.Id,
                CreatedAt = now,
                UpdatedAt = now,
                TimeZone = KnownZone(request.TimeZone),
            };
            db.ShareLinks.Add(link);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Created($"/collections/{collectionId}/shares/{link.Id}", Info(link, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        shares.MapPut("/{id:guid}", async (
            Guid collectionId, Guid id, UpdateShareLinkRequest request,
            StiklingDbContext db, IOptions<ShareOptions> options, TimeProvider clock) =>
        {
            var name = ShareLinkRules.Clean(request.Name);
            var line = ShareLinkRules.Clean(request.Line);
            var leftOut = (request.LeftOutPhotoIds ?? []).Distinct().ToList();

            var errors = new Dictionary<string, string[]>();
            if (name is { Length: > ShareLinkRules.MaxNameLength })
                errors["name"] = [$"A name can have at most {ShareLinkRules.MaxNameLength} characters."];
            if (line is { Length: > ShareLinkRules.MaxLineLength })
                errors["line"] = [$"The line can have at most {ShareLinkRules.MaxLineLength} characters."];
            if (leftOut.Count > ShareLinkRules.MaxLeftOutPhotos)
                errors["leftOutPhotoIds"] = [$"At most {ShareLinkRules.MaxLeftOutPhotos} photos can be left out."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var link = await db.ShareLinks.SingleOrDefaultAsync(l => l.Id == id && l.CollectionId == collectionId && l.TurnedOffAt == null);
            if (link is null)
                return Results.NotFound();

            link.ShowNotes = request.ShowNotes;
            link.Name = name;
            link.Line = line;
            link.LeftOutPhotoIds = leftOut;
            link.TimeZone = KnownZone(request.TimeZone);
            link.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();

            return Results.Ok(Info(link, options.Value));
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        // Turning off keeps the row, so the address answers "turned off" from then on
        shares.MapDelete("/{id:guid}", async (Guid collectionId, Guid id, StiklingDbContext db, TimeProvider clock) =>
        {
            var link = await db.ShareLinks.SingleOrDefaultAsync(l => l.Id == id && l.CollectionId == collectionId);
            if (link is null)
                return Results.NotFound();

            if (link.TurnedOffAt is null)
            {
                var now = clock.GetUtcNow();
                link.TurnedOffAt = now;
                link.UpdatedAt = now;
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        return app;
    }

    /// <summary>The kind of record a plant or propagation is kept as.</summary>
    internal static string KindOf(SubjectType type) => type == SubjectType.Plant ? "plants" : "propagations";

    internal static ShareLinkInfo Info(ShareLink link, ShareOptions options) =>
        new(link.Id, link.SubjectType, link.SubjectId, options.UrlOf(link.Token), link.ShowNotes,
            link.Name, link.Line, link.LeftOutPhotoIds, link.CreatedAt);

    /// <summary>16 random bytes as base64url without padding, 22 characters that can't be guessed.</summary>
    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    /// <summary>The time zone id if this machine knows it, or null.</summary>
    internal static string? KnownZone(string? id)
    {
        var trimmed = id?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= 100 && TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _)
            ? trimmed
            : null;
    }
}
