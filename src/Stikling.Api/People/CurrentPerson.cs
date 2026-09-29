using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;

namespace Stikling.Api.People;

/// <summary>
/// The person behind the current request. The Clerk id from the token is looked up once per
/// request, and from there on only our own <see cref="Person.Id"/> is used.
/// </summary>
public class CurrentPerson(IHttpContextAccessor http, StiklingDbContext db, TimeProvider clock)
{
    public const string FirstCollectionName = "My plants";

    private Person? _person;

    private string ClerkUserId =>
        http.HttpContext?.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The request has no signed-in user.");

    /// <summary>The person, or null if they have never called <c>/me</c>.</summary>
    public async Task<Person?> FindAsync()
    {
        if (_person is not null)
            return _person;

        var clerkUserId = ClerkUserId;
        return _person = await db.People.SingleOrDefaultAsync(p => p.ClerkUserId == clerkUserId);
    }

    /// <summary>
    /// The person, created on their first sign-in together with a collection of their own. Two
    /// devices signing in at the same moment can both try to create them, and the unique Clerk id
    /// makes one of them fail. That one reads what the other made.
    /// </summary>
    public async Task<Person> FindOrCreateAsync()
    {
        if (await FindAsync() is { } existing)
            return existing;

        var now = clock.GetUtcNow();
        var person = new Person { ClerkUserId = ClerkUserId, CreatedAt = now };
        var collection = new Collection { Name = FirstCollectionName, CreatedAt = now };
        db.Memberships.Add(new Membership { Person = person, Collection = collection, Role = MemberRole.Editor, CreatedAt = now });

        try
        {
            await db.SaveChangesAsync();
            return _person = person;
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            var clerkUserId = ClerkUserId;
            return _person = await db.People.SingleAsync(p => p.ClerkUserId == clerkUserId);
        }
    }
}
