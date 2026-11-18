using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.People;
using static Stikling.Api.Collections.CollectionEndpoints;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Collections;

/// <summary>
/// Invites to a collection. Someone with the right to manage people makes a link with a role, and
/// whoever opens it while signed in can join. The table is only kept on the server.
/// </summary>
public static class InviteEndpoints
{
    public record CreateInviteRequest(MemberRole? Role, bool ManagesPeople);

    public record InviteResponse(
        Guid Id, string? Token, MemberRole Role, bool ManagesPeople, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string? CreatedByName);

    public record InvitePreview(
        Guid CollectionId, string CollectionName, string? InvitedByName, MemberRole Role, bool ManagesPeople, bool AlreadyMember,
        string? OwnerName = null);

    public static IEndpointRouteBuilder MapInvites(this IEndpointRouteBuilder app)
    {
        var invites = app.MapGroup("/collections/{collectionId:guid}/invites");

        invites.MapPost("", async (
            Guid collectionId, CreateInviteRequest request, HttpContext http, CurrentPerson current,
            StiklingDbContext db, TimeProvider clock) =>
        {
            if (request.Role is not { } role || !Enum.IsDefined(role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Choose a role."] });
            if (!PeopleRules.IsValidCombination(role, request.ManagesPeople))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["managesPeople"] = ["Only an editor can manage people."] });

            if (await current.FindAsync() is not { } person)
                return Results.NotFound();

            // The name is what the person sees on the invite, so it has to be set first
            if (string.IsNullOrWhiteSpace(person.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Set your name before you invite someone."] });

            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);
            var state = await CollectionState.LoadAsync(db, collectionId);

            if (state.RightsOf(person.Id) is not { } caller || !caller.Allows(CollectionRight.Manage))
                return Results.Problem("You can't do that in this collection.", statusCode: StatusCodes.Status403Forbidden);
            if (!PeopleRules.CanInvite(caller, request.ManagesPeople))
                return Results.Problem("Only the owner can let someone manage people.", statusCode: StatusCodes.Status403Forbidden);

            var open = await db.Invites.CountAsync(i =>
                i.CollectionId == collectionId && i.UsedAt == null && i.RevokedAt == null && i.ExpiresAt > now);
            if (open >= PeopleRules.MaxOpenInvites)
            {
                return Results.Problem(
                    $"This collection already has {PeopleRules.MaxOpenInvites} open invites. Withdraw one to make another.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var since = now.AddDays(-1);
            var recent = await db.Invites.CountAsync(i => i.CreatedBy == person.Id && i.CreatedAt > since);
            if (recent >= PeopleRules.MaxInvitesPerPersonPerDay)
            {
                return Results.Problem(
                    $"You have made {PeopleRules.MaxInvitesPerPersonPerDay} invites today. Try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var invite = new Invite
            {
                Token = NewToken(),
                CollectionId = collectionId,
                Role = role,
                ManagesPeople = request.ManagesPeople,
                CreatedBy = person.Id,
                CreatedAt = now,
                ExpiresAt = now + PeopleRules.InviteLifetime,
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Created($"/collections/{collectionId}/invites/{invite.Id}", Respond(invite, person.Name));
        })
        .RequireAuthorization(CollectionPolicies.Manage);

        invites.MapGet("", async (Guid collectionId, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();

            // Only the owner gets the link of an invite that lets someone manage people, since only the owner can make one
            var isOwner = http.Rights().IsOwner;
            return await db.Invites.AsNoTracking()
                .Where(i => i.CollectionId == collectionId && i.UsedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
                .OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
                .Select(i => new InviteResponse(
                    i.Id, i.ManagesPeople && !isOwner ? null : i.Token, i.Role, i.ManagesPeople, i.CreatedAt, i.ExpiresAt,
                    db.People.Where(p => p.Id == i.CreatedBy).Select(p => p.Name).FirstOrDefault()))
                .ToListAsync();
        })
        .RequireAuthorization(CollectionPolicies.Manage);

        invites.MapDelete("/{inviteId:guid}", async (Guid collectionId, Guid inviteId, StiklingDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);

            var invite = await db.Invites.SingleOrDefaultAsync(i => i.Id == inviteId && i.CollectionId == collectionId);
            if (invite is null)
                return Results.NotFound();

            // One that is used or already withdrawn stays as it is
            if (invite.UsedAt is null && invite.RevokedAt is null)
            {
                invite.RevokedAt = clock.GetUtcNow();
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Manage);

        // What the person who opened the link is told before they decide to join
        app.MapGet("/invites/{token}", async (string token, CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            var person = await current.FindOrCreateAsync();
            var invite = await db.Invites.AsNoTracking().SingleOrDefaultAsync(i => i.Token == token);
            if (invite is null)
                return Results.NotFound();
            if (!invite.IsOpen(clock.GetUtcNow()))
                return Gone();

            var collection = await db.Collections.Where(c => c.Id == invite.CollectionId)
                .Select(c => new { c.Name, OwnerName = db.People.Where(p => p.Id == c.OwnerPersonId).Select(p => p.Name).FirstOrDefault() })
                .SingleAsync();
            var invitedBy = await db.People.Where(p => p.Id == invite.CreatedBy).Select(p => p.Name).SingleOrDefaultAsync();
            var already = await db.Memberships.AnyAsync(m => m.CollectionId == invite.CollectionId && m.PersonId == person.Id);

            return Results.Ok(new InvitePreview(invite.CollectionId, collection.Name, invitedBy, invite.Role, invite.ManagesPeople, already, collection.OwnerName));
        })
        .RequireAuthorization();

        app.MapPost("/invites/{token}/accept", async (string token, CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            var person = await current.FindOrCreateAsync();
            var now = clock.GetUtcNow();

            var found = await db.Invites.AsNoTracking().Where(i => i.Token == token).Select(i => new { i.CollectionId }).SingleOrDefaultAsync();
            if (found is null)
                return Results.NotFound();

            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(found.CollectionId);

            // Read again with the collection locked, since someone else may have just used it
            var invite = await db.Invites.SingleAsync(i => i.Token == token);
            var collection = await db.Collections.SingleAsync(c => c.Id == invite.CollectionId);
            var members = await db.Memberships.Where(m => m.CollectionId == invite.CollectionId).ToListAsync();

            // Someone who is in already isn't changed, and the invite stays open for the one it was meant for
            if (members.SingleOrDefault(m => m.PersonId == person.Id) is { } existing)
                return Results.Ok(await SummaryAsync(db, collection, existing));

            if (!invite.IsOpen(now))
                return Gone();

            if (string.IsNullOrWhiteSpace(person.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Set your name before you join."] });

            if (members.Count >= PeopleRules.MaxMembers)
            {
                return Results.Problem(
                    $"This collection already has {PeopleRules.MaxMembers} people.",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["problem"] = "full" });
            }

            var membership = new Membership
            {
                CollectionId = collection.Id,
                PersonId = person.Id,
                Role = invite.Role,
                ManagesPeople = invite.ManagesPeople,
                CreatedAt = now,
            };
            db.Memberships.Add(membership);
            invite.UsedBy = person.Id;
            invite.UsedAt = now;

            if (await db.FormerMembers.FindAsync(collection.Id, person.Id) is { } former)
                db.FormerMembers.Remove(former);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.Ok(await SummaryAsync(db, collection, membership));
        })
        .RequireAuthorization();

        return app;
    }

    private static IResult Gone() =>
        Results.Problem("This invite has been used, withdrawn or has run out.", statusCode: StatusCodes.Status410Gone);

    private static async Task<CollectionSummary> SummaryAsync(StiklingDbContext db, Collection collection, Membership membership) =>
        new(collection.Id, collection.Name, membership.Role, membership.ManagesPeople, collection.OwnerPersonId == membership.PersonId,
            await db.People.Where(p => p.Id == collection.OwnerPersonId).Select(p => p.Name).FirstOrDefaultAsync());

    private static InviteResponse Respond(Invite invite, string? createdByName) =>
        new(invite.Id, invite.Token, invite.Role, invite.ManagesPeople, invite.CreatedAt, invite.ExpiresAt, createdByName);

    /// <summary>16 random bytes as base64url without padding, 22 characters that can't be guessed.</summary>
    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
}
