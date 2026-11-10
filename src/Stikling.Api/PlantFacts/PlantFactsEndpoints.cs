using Stikling.Core.PlantFacts;

namespace Stikling.Api.PlantFacts;

/// <summary>
/// The short card about a species in the app. It is open to everyone, since the app asks without a
/// sign-in, and sends only the plant's name. The answer is the same for everyone, so it is cached
/// for a day.
/// </summary>
public static class PlantFactsEndpoints
{
    public static IEndpointRouteBuilder MapPlantFacts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/plant-facts", async (
            string? genus, string? species, string? language, PlantFactsService facts, HttpContext http, CancellationToken cancellationToken) =>
        {
            if (species is { Length: 0 })
                species = null;

            if (!PlantFactNames.IsValid(genus) || (species is not null && !PlantFactNames.IsValid(species)))
                return Results.Problem("The genus and species must be letters, hyphens, × or spaces, at most 60 characters each.", statusCode: StatusCodes.Status400BadRequest);

            if (language is not (null or "en" or "da"))
                return Results.Problem("The language must be en or da.", statusCode: StatusCodes.Status400BadRequest);

            var (answer, complete) = await facts.GetAsync(
                PlantFactNames.Genus(genus!),
                species is null ? null : PlantFactNames.Species(species),
                language ?? "en",
                cancellationToken);

            if (answer is null)
                return Results.Problem("Wikipedia couldn't be reached.", statusCode: StatusCodes.Status502BadGateway);

            // An answer with a gap from a failed source isn't kept by the browser either
            http.Response.Headers.CacheControl = complete ? "public, max-age=86400" : "no-store";
            return Results.Ok(answer);
        });

        return app;
    }
}
