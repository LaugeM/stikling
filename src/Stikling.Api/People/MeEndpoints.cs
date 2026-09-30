using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Api.Photos;

namespace Stikling.Api.People;

public static class MeEndpoints
{
    public record MeResponse(Guid PersonId, List<CollectionSummary> Collections);

    public record CollectionSummary(Guid Id, string Name, MemberRole Role);

    /// <summary>
    /// <c>GET /me</c>: who the caller is and which collections they are in. The app calls it right
    /// after signing in, so the first call also creates the person and their own collection.
    /// </summary>
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (CurrentPerson current, StiklingDbContext db) =>
        {
            var person = await current.FindOrCreateAsync();
            var collections = await db.Memberships
                .Where(m => m.PersonId == person.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new CollectionSummary(m.CollectionId, m.Collection.Name, m.Role))
                .ToListAsync();

            return new MeResponse(person.Id, collections);
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

        var alone = await db.Memberships
            .Where(m => m.PersonId == person.Id && !m.Collection.Members.Any(other => other.PersonId != person.Id))
            .Select(m => m.CollectionId)
            .ToListAsync();

        // The images go before the rows, so if deleting them fails, asking again still finds the collections
        foreach (var collectionId in alone)
            await photos.DeleteCollectionAsync(collectionId);

        // Deleting a collection takes its records, photo rows and memberships with it, and deleting
        // the person takes their settings and the memberships left
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Collections.Where(c => alone.Contains(c.Id)).ExecuteDeleteAsync();
            await db.People.Where(p => p.Id == person.Id).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }

        // An image that was on its way while the first pass ran
        foreach (var collectionId in alone)
            await photos.DeleteCollectionAsync(collectionId);
    }
}
