using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Core.Identification;

namespace Stikling.Api.Identification;

public static class IdentificationServices
{
    /// <summary>
    /// Sets up the Pl@ntNet client. It has no logging, since the logging of an HTTP client writes the
    /// address of every request and Pl@ntNet's key is in the address.
    /// </summary>
    public static IServiceCollection AddIdentification(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdentificationOptions>(configuration.GetSection(IdentificationOptions.Section));
        services.AddHttpClient<IPlantIdentifier, PlantNetIdentifier>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers();
        return services;
    }
}

/// <summary>
/// Names the plant in a photo through Pl@ntNet, for people who are signed in. The photo is passed on and never
/// kept or logged. Each person can identify <see cref="IdentificationOptions.DailyLimitPerPerson"/> a day and
/// everyone together <see cref="IdentificationOptions.DailyLimitTotal"/>, counted in <c>IdentificationUses</c>.
/// </summary>
public static class IdentificationEndpoints
{
    public const long MaxImageBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapIdentification(this IEndpointRouteBuilder app)
    {
        app.MapPost("/identify", IdentifyAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            // Without a key there is nothing to ask
            .AddEndpointFilter(async (context, next) =>
                context.HttpContext.RequestServices.GetRequiredService<IOptions<IdentificationOptions>>().Value.IsConfigured
                    ? await next(context)
                    : Results.Problem("Identification isn't set up on the server yet.", statusCode: StatusCodes.Status503ServiceUnavailable));

        return app;
    }

    private static async Task<IResult> IdentifyAsync(
        [FromForm] IFormFile? image,
        [FromForm] string? lang,
        CurrentPerson current,
        StiklingDbContext db,
        IPlantIdentifier identifier,
        IOptions<IdentificationOptions> options,
        TimeProvider clock,
        CancellationToken cancellation)
    {
        if (image is null || image.Length == 0)
            return Results.Problem("An image is required.", statusCode: StatusCodes.Status400BadRequest);
        if (image.Length > MaxImageBytes)
            return Results.Problem("The image can be at most 10 MB.", statusCode: StatusCodes.Status400BadRequest);

        var bytes = new byte[image.Length];
        await using (var stream = image.OpenReadStream())
            await stream.ReadExactlyAsync(bytes, cancellation);

        // By what the bytes are, not by what the caller says they are
        if (ImageType(bytes) is not { } contentType)
            return Results.Problem("The image must be a JPEG or a PNG.", statusCode: StatusCodes.Status400BadRequest);

        var settings = options.Value;
        var person = await current.FindOrCreateAsync();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Nothing older than yesterday is of use, so it goes whenever someone identifies
        await db.IdentificationUses.Where(u => u.Day < today.AddDays(-1)).ExecuteDeleteAsync(cancellation);

        var mine = await db.IdentificationUses
            .Where(u => u.PersonId == person.Id && u.Day == today)
            .Select(u => u.Count)
            .SingleOrDefaultAsync(cancellation);
        if (mine >= settings.DailyLimitPerPerson)
            return TooMany("person");

        var everyone = await db.IdentificationUses.Where(u => u.Day == today).SumAsync(u => u.Count, cancellation);
        if (everyone >= settings.DailyLimitTotal)
            return TooMany("total");

        var answer = await identifier.IdentifyAsync(bytes, contentType, lang == "da" ? "da" : "en", cancellation);
        switch (answer.Status)
        {
            case PlantNetStatus.Ok:
            case PlantNetStatus.NotFound:
                await CountAsync(db, person.Id, today, cancellation);
                return Results.Ok(new IdentificationResponse(answer.Suggestions.Take(IdentificationRules.MaxSuggestions).ToList()));
            case PlantNetStatus.TooMany:
                // Pl@ntNet's own quota for the day is gone, whatever our count says
                return TooMany("total");
            default:
                return Results.Problem("Pl@ntNet couldn't identify the photo.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult TooMany(string reason) =>
        Results.Problem(
            "Today's identifications are used up.",
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> { ["reason"] = reason });

    private static async Task CountAsync(StiklingDbContext db, Guid personId, DateOnly day, CancellationToken cancellation)
    {
        if (await AddOneAsync(db, personId, day, cancellation))
            return;

        try
        {
            db.IdentificationUses.Add(new IdentificationUse { PersonId = personId, Day = day, Count = 1 });
            await db.SaveChangesAsync(cancellation);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another identification by the same person made the row first
            db.ChangeTracker.Clear();
            await AddOneAsync(db, personId, day, cancellation);
        }
    }

    private static async Task<bool> AddOneAsync(StiklingDbContext db, Guid personId, DateOnly day, CancellationToken cancellation) =>
        await db.IdentificationUses
            .Where(u => u.PersonId == personId && u.Day == day)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.Count, u => u.Count + 1), cancellation) > 0;

    /// <summary>"image/jpeg" or "image/png" by how the bytes start, otherwise null.</summary>
    internal static string? ImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        return null;
    }
}
