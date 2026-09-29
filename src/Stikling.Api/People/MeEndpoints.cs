using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;

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

        return app;
    }
}
