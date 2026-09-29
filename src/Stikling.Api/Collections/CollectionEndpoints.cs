using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;

namespace Stikling.Api.Collections;

public static class CollectionEndpoints
{
    public record CollectionResponse(Guid Id, string Name, List<MemberResponse> Members);

    public record MemberResponse(Guid PersonId, MemberRole Role);

    public record RenameRequest(string? Name);

    public static IEndpointRouteBuilder MapCollections(this IEndpointRouteBuilder app)
    {
        var collection = app.MapGroup("/collections/{collectionId:guid}");

        collection.MapGet("", async (Guid collectionId, StiklingDbContext db) =>
        {
            var found = await db.Collections
                .Where(c => c.Id == collectionId)
                .Select(c => new CollectionResponse(
                    c.Id,
                    c.Name,
                    c.Members.OrderBy(m => m.CreatedAt).Select(m => new MemberResponse(m.PersonId, m.Role)).ToList()))
                .SingleAsync();

            return found;
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

        return app;
    }
}
