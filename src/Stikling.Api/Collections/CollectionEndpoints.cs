using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.People;

namespace Stikling.Api.Collections;

public static class CollectionEndpoints
{
    public record CollectionResponse(
        Guid Id, string Name, Guid? OwnerPersonId, List<MemberResponse> Members, List<FormerMemberResponse> FormerMembers);

    public record MemberResponse(Guid PersonId, MemberRole Role, string? Name, bool ManagesPeople, bool IsOwner, DateTimeOffset JoinedAt);

    public record FormerMemberResponse(Guid PersonId, string? Name);

    public record RenameRequest(string? Name);

    public record ChangeMemberRequest(MemberRole? Role, bool ManagesPeople);

    public record HandOverRequest(Guid PersonId);

    public static IEndpointRouteBuilder MapCollections(this IEndpointRouteBuilder app)
    {
        var collection = app.MapGroup("/collections/{collectionId:guid}");

        collection.MapGet("", async (Guid collectionId, StiklingDbContext db) =>
        {
            var found = await db.Collections
                .Where(c => c.Id == collectionId)
                .Select(c => new { c.Id, c.Name, c.OwnerPersonId })
                .SingleAsync();

            var members = await db.Memberships
                .Where(m => m.CollectionId == collectionId)
                .OrderBy(m => m.CreatedAt).ThenBy(m => m.PersonId)
                .Select(m => new MemberResponse(
                    m.PersonId, m.Role, m.Person.Name, m.ManagesPeople, m.Collection.OwnerPersonId == m.PersonId, m.CreatedAt))
                .ToListAsync();

            var former = await (
                    from f in db.FormerMembers
                    join p in db.People on f.PersonId equals p.Id
                    where f.CollectionId == collectionId
                    orderby f.LeftAt, f.PersonId
                    select new FormerMemberResponse(p.Id, p.Name))
                .ToListAsync();

            return new CollectionResponse(found.Id, found.Name, found.OwnerPersonId, members, former);
        })
        .RequireAuthorization(CollectionPolicies.View);

        collection.MapPut("/name", async (Guid collectionId, RenameRequest request, StiklingDbContext db) =>
        {
            var name = request.Name?.Trim() ?? "";
            if (name.Length is 0 or > Collection.MaxNameLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = [$"A name needs between 1 and {Collection.MaxNameLength} characters."],
                });
            }

            await db.Collections
                .Where(c => c.Id == collectionId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Name, name));

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Edit);

        // Change a member's role, and whether they manage people
        collection.MapPut("/members/{personId:guid}", async (
            Guid collectionId, Guid personId, ChangeMemberRequest request, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            if (request.Role is not { } role || !Enum.IsDefined(role))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Choose a role."] });
            if (!PeopleRules.IsValidCombination(role, request.ManagesPeople))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["managesPeople"] = ["Only an editor can manage people."] });

            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);
            var state = await CollectionState.LoadAsync(db, collectionId);

            if (state.RightsOf(http.Rights().PersonId) is not { } caller || !caller.Allows(CollectionRight.Manage))
                return Forbidden();
            if (state.Find(personId) is not { } target)
                return Results.NotFound();
            if (!PeopleRules.CanChange(caller, state.RightsOf(personId)!, request.ManagesPeople))
                return Forbidden();

            target.Role = role;
            target.ManagesPeople = request.ManagesPeople;
            await db.SaveChangesAsync();

            // Someone who no longer manages people can't let anyone in with a link they made before
            if (!request.ManagesPeople)
                await WithdrawInvitesByAsync(db, collectionId, personId, clock.GetUtcNow());
            await transaction.CommitAsync();

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Manage);

        collection.MapDelete("/members/{personId:guid}", async (
            Guid collectionId, Guid personId, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);
            var state = await CollectionState.LoadAsync(db, collectionId);

            if (state.RightsOf(http.Rights().PersonId) is not { } caller || !caller.Allows(CollectionRight.Manage))
                return Forbidden();
            if (state.Find(personId) is not { } target)
                return Results.NotFound();
            if (!PeopleRules.CanRemove(caller, state.RightsOf(personId)!))
                return Forbidden();

            await RemoveMemberAsync(db, collectionId, target, clock.GetUtcNow());
            await transaction.CommitAsync();

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Manage);

        collection.MapPost("/leave", async (Guid collectionId, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);
            var state = await CollectionState.LoadAsync(db, collectionId);

            var callerId = http.Rights().PersonId;
            if (state.Find(callerId) is not { } membership)
                return Forbidden();

            // An owner hands the collection over first
            if (state.RightsOf(callerId)!.IsOwner)
            {
                return Results.Problem(
                    "The owner can't leave. Hand the collection over to someone first.",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["problem"] = "owner" });
            }

            await RemoveMemberAsync(db, collectionId, membership, clock.GetUtcNow());
            await transaction.CommitAsync();

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.View);

        collection.MapPut("/owner", async (Guid collectionId, HandOverRequest request, HttpContext http, StiklingDbContext db) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.LockCollectionAsync(collectionId);
            var state = await CollectionState.LoadAsync(db, collectionId);

            if (state.RightsOf(http.Rights().PersonId) is not { IsOwner: true } caller)
                return Forbidden();
            if (request.PersonId == caller.PersonId)
                return Results.NoContent();
            if (state.Find(request.PersonId) is not { } next)
                return Results.NotFound();

            next.Role = MemberRole.Editor;
            next.ManagesPeople = true;
            state.Collection.OwnerPersonId = next.PersonId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.Own);

        return app;
    }

    private static IResult Forbidden() =>
        Results.Problem("You can't do that in this collection.", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Takes the member out and notes that they were once here. Someone left in no collection at all
    /// gets a new one of their own, so they always have somewhere to land. Call it inside a
    /// transaction, after locking the collection.
    /// </summary>
    internal static async Task RemoveMemberAsync(StiklingDbContext db, Guid collectionId, Membership membership, DateTimeOffset now)
    {
        var personId = membership.PersonId;
        db.Memberships.Remove(membership);

        var former = await db.FormerMembers.FindAsync(collectionId, personId);
        if (former is null)
            db.FormerMembers.Add(new FormerMember { CollectionId = collectionId, PersonId = personId, LeftAt = now });
        else
            former.LeftAt = now;

        var hasOthers = await db.Memberships.AnyAsync(m => m.PersonId == personId && m.CollectionId != collectionId);
        if (!hasOthers)
            CurrentPerson.AddOwnCollection(db, await db.People.SingleAsync(p => p.Id == personId), now);

        await db.SaveChangesAsync();

        // Days uploaded for this collection are no longer theirs to show. The link stays on and fills again on their next upload
        await db.CalendarFeeds
            .Where(f => f.PersonId == personId && f.CollectionId == collectionId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(f => f.DaysJson, "[]")
                .SetProperty(f => f.CollectionId, (Guid?)null));

        await WithdrawInvitesByAsync(db, collectionId, personId, now);
    }

    /// <summary>Withdraws the open invites this person made, so a link from someone who has left or no longer manages people stops working.</summary>
    internal static Task WithdrawInvitesByAsync(StiklingDbContext db, Guid collectionId, Guid personId, DateTimeOffset now) =>
        db.Invites
            .Where(i => i.CollectionId == collectionId && i.CreatedBy == personId && i.UsedAt == null && i.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAt, now));

    /// <summary>A collection and its members as they are now, read with the collection locked.</summary>
    internal sealed class CollectionState
    {
        public required Collection Collection { get; init; }
        public required List<Membership> Members { get; init; }

        public static async Task<CollectionState> LoadAsync(StiklingDbContext db, Guid collectionId) => new()
        {
            Collection = await db.Collections.SingleAsync(c => c.Id == collectionId),
            Members = await db.Memberships.Where(m => m.CollectionId == collectionId).ToListAsync(),
        };

        public Membership? Find(Guid personId) => Members.SingleOrDefault(m => m.PersonId == personId);

        public MemberRights? RightsOf(Guid personId) => Find(personId) is { } m
            ? new MemberRights(m.PersonId, m.Role, m.ManagesPeople, Collection.OwnerPersonId == m.PersonId)
            : null;
    }
}
