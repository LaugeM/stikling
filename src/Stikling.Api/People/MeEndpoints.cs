using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.Photos;

namespace Stikling.Api.People;

public static class MeEndpoints
{
    public record MeResponse(Guid PersonId, string? Name, List<CollectionSummary> Collections);

    public record CollectionSummary(Guid Id, string Name, MemberRole Role, bool ManagesPeople, bool IsOwner);

    public record NameRequest(string? Name);

    /// <summary>
    /// <c>GET /me</c>: who the caller is and which collections they are in. The app calls it right
    /// after signing in, so the first call also creates the person and their own collection.
    /// </summary>
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            var person = await current.FindOrCreateAsync();

            // Leaving or being removed gives someone in no collection a new one. Two of those at the
            // same moment, in two collections, can each see the other membership and make none, so
            // it is made here if that happened.
            if (!await db.Memberships.AnyAsync(m => m.PersonId == person.Id))
            {
                CurrentPerson.AddOwnCollection(db, person, clock.GetUtcNow());
                await db.SaveChangesAsync();
            }

            var collections = await db.Memberships
                .Where(m => m.PersonId == person.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new CollectionSummary(
                    m.CollectionId, m.Collection.Name, m.Role, m.ManagesPeople, m.Collection.OwnerPersonId == m.PersonId))
                .ToListAsync();

            return new MeResponse(person.Id, person.Name, collections);
        })
        .RequireAuthorization();

        // The name other people in a shared collection see
        app.MapPut("/me/name", async (NameRequest request, CurrentPerson current, StiklingDbContext db) =>
        {
            var name = request.Name?.Trim() ?? "";
            if (name.Length is 0 or > Person.MaxNameLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = [$"A name needs between 1 and {Person.MaxNameLength} characters."],
                });
            }

            var person = await current.FindOrCreateAsync();
            await db.People.Where(p => p.Id == person.Id).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, name));
            return Results.NoContent();
        })
        .RequireAuthorization();

        app.MapDelete("/me", async (CurrentPerson current, StiklingDbContext db, PhotoStorage photos, TimeProvider clock) =>
        {
            await DeleteAccountAsync(current, db, photos, clock);
            return Results.NoContent();
        })
        .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// <c>DELETE /me</c>: erases the caller's account from the server. Collections nobody else is
    /// in go with it, records and photos included, and the caller leaves the ones shared with
    /// others. The app deletes the Clerk user afterwards. Asking again after it's done, or after it
    /// failed halfway, finishes what is left.
    /// </summary>
    private static async Task DeleteAccountAsync(CurrentPerson current, StiklingDbContext db, PhotoStorage photos, TimeProvider clock)
    {
        // Marked first, so no device can create the person again while the rest is deleted
        var clerkUserId = current.ClerkUserId;
        if (!await db.DeletedAccounts.AnyAsync(d => d.ClerkUserId == clerkUserId))
        {
            db.DeletedAccounts.Add(new DeletedAccount { ClerkUserId = clerkUserId, DeletedAt = clock.GetUtcNow() });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Another device asked at the same moment
                db.ChangeTracker.Clear();
            }
        }

        if (await current.FindAsync() is not { } person)
            return;

        // The collections nobody else is in. Their open invites are withdrawn while the collection is
        // locked, which is also what accepting one waits for, so nobody can join one of them between
        // here and it being deleted.
        var alone = new List<Guid>();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
// In the same order for everyone, so two accounts deleted at once can't wait for each other
            var mine = await db.Memberships.Where(m => m.PersonId == person.Id).Select(m => m.CollectionId).OrderBy(id => id).ToListAsync();
            var now = clock.GetUtcNow();
            foreach (var collectionId in mine)
            {
                await db.LockCollectionAsync(collectionId);
                if (await db.Memberships.AnyAsync(m => m.CollectionId == collectionId && m.PersonId != person.Id))
                    continue;

                await db.Invites
                    .Where(i => i.CollectionId == collectionId && i.UsedAt == null && i.RevokedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAt, now));
                alone.Add(collectionId);
            }

            await transaction.CommitAsync();
        }

        // The images go before the rows, so if deleting them fails, asking again still finds the collections
        foreach (var collectionId in alone)
            await photos.DeleteCollectionAsync(collectionId);

        // Deleting a collection takes its records, photo rows and memberships with it, and deleting
        // the person takes their settings, the memberships left and their rows as a former member
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // The collections go first, since a collection points at its owner
            await db.Collections.Where(c => alone.Contains(c.Id)).ExecuteDeleteAsync();

            // One whose last other member left since the first pass has nobody to take it over
            var emptied = await HandOverOwnedCollectionsAsync(db, person.Id);
            await db.Collections.Where(c => emptied.Contains(c.Id)).ExecuteDeleteAsync();
            alone.AddRange(emptied);

            // What the person made in the collections that stay is kept, with no one named
            var staying = await db.Memberships
                .Where(m => m.PersonId == person.Id)
                .Select(m => m.CollectionId)
                .ToListAsync();
            await db.Records
                .Where(r => staying.Contains(r.CollectionId) && r.CreatedBy == person.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.CreatedBy, (Guid?)null));
            await db.Invites
                .Where(i => i.UsedBy == person.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.UsedBy, (Guid?)null));

            await db.People.Where(p => p.Id == person.Id).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }

        // An image that was on its way while the first pass ran
        foreach (var collectionId in alone)
            await photos.DeleteCollectionAsync(collectionId);
    }

    /// <summary>
    /// Gives each collection the person owns, and that has other members, to the oldest member who
    /// is an editor that manages people, else the oldest editor, else the oldest member, who then
    /// becomes an editor. Returns the ones nobody else is in. Call it inside a transaction.
    /// </summary>
    private static async Task<List<Guid>> HandOverOwnedCollectionsAsync(StiklingDbContext db, Guid personId)
    {
        var owned = await db.Collections
            .Where(c => c.OwnerPersonId == personId)
            .Select(c => c.Id)
            .OrderBy(id => id)
            .ToListAsync();
        var empty = new List<Guid>();

        foreach (var collectionId in owned)
        {
            await db.LockCollectionAsync(collectionId);
            var collection = await db.Collections.SingleAsync(c => c.Id == collectionId);
            var others = (await db.Memberships
                    .Where(m => m.CollectionId == collectionId && m.PersonId != personId)
                    .ToListAsync())
                .OrderBy(m => m.CreatedAt).ThenBy(m => m.PersonId)
                .ToList();
            if (others.Count == 0)
            {
                empty.Add(collectionId);
                continue;
            }

            var next = others.FirstOrDefault(m => m.Role == MemberRole.Editor && m.ManagesPeople)
                ?? others.FirstOrDefault(m => m.Role == MemberRole.Editor)
                ?? others[0];
            next.Role = MemberRole.Editor;
            next.ManagesPeople = true;
            collection.OwnerPersonId = next.PersonId;
            await db.SaveChangesAsync();
        }

        return empty;
    }
}
