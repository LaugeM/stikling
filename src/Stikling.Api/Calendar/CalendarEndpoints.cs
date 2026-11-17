using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Auth;
using Stikling.Api.Collections;
using Stikling.Api.Data;
using Stikling.Api.People;
using Stikling.Core.Calendar;

namespace Stikling.Api.Calendar;

/// <summary>
/// The calendar link: making and turning off a person's link, the days a device uploads for it, and
/// the feed itself, which needs no sign-in because the token in the address is the secret. The
/// token is never logged, and the server never looks at a person's records.
/// </summary>
public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendar(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me/calendar").RequireAuthorization();

        me.MapGet("", async (CurrentPerson current, StiklingDbContext db) =>
        {
            var person = await current.FindOrCreateAsync();
            return Results.Ok(new CalendarStatusResponse(await db.CalendarFeeds.AnyAsync(f => f.PersonId == person.Id)));
        });

        // Turns the link on, or replaces it: the old token stops working at once and the days stay
        me.MapPost("", async (CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            var person = await current.FindOrCreateAsync();
            var token = CalendarRules.NewToken();

            try
            {
                await SaveTokenAsync(db, person.Id, token, clock.GetUtcNow());
            }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Two devices asked at once and the other one made the row. This one's token replaces it
                db.ChangeTracker.Clear();
                await SaveTokenAsync(db, person.Id, token, clock.GetUtcNow());
            }

            return Results.Ok(new CalendarLinkResponse(token));
        });

        me.MapDelete("", async (CurrentPerson current, StiklingDbContext db) =>
        {
            var person = await current.FindOrCreateAsync();
            await db.CalendarFeeds.Where(f => f.PersonId == person.Id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

        app.MapPut("/collections/{collectionId:guid}/calendar", async (
            Guid collectionId, CalendarDaysRequest request, HttpContext http, StiklingDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            if (CalendarRules.DaysProblem(request.Days, now) is { } problem)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["days"] = [problem] });

            var personId = http.Rights().PersonId;
            var changed = await db.CalendarFeeds.Where(f => f.PersonId == personId).ExecuteUpdateAsync(set => set
                .SetProperty(f => f.DaysJson, CalendarRules.WriteDays(request.Days!))
                .SetProperty(f => f.CollectionId, collectionId)
                .SetProperty(f => f.UpdatedAt, now));

            return changed == 0 ? Results.NotFound() : Results.NoContent();
        })
        .RequireAuthorization(CollectionPolicies.View);

        app.MapMethods("/calendar/{token}.ics", ["GET", "HEAD"], async (string token, StiklingDbContext db, TimeProvider clock, HttpContext http) =>
        {
            // Nothing that isn't a token we made reaches the database
            if (!CalendarRules.IsWellFormed(token))
                return Results.NotFound();

            var hash = CalendarRules.Hash(token);
            var feed = await db.CalendarFeeds.AsNoTracking().SingleOrDefaultAsync(f => f.TokenHash == hash);
            if (feed is null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(feed.TokenHash), Encoding.ASCII.GetBytes(hash)))
                return Results.NotFound();

            var from = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-CalendarRules.KeepBackDays);
            var days = CalendarRules.ReadDays(feed.DaysJson).Where(d => d.Day >= from);

            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return Results.Text(IcsWriter.Write(feed.FeedId, days, feed.UpdatedAt), "text/calendar; charset=utf-8", Encoding.UTF8);
        });

        return app;
    }

    private static async Task SaveTokenAsync(StiklingDbContext db, Guid personId, string token, DateTimeOffset now)
    {
        var hash = CalendarRules.Hash(token);
        var feed = await db.CalendarFeeds.SingleOrDefaultAsync(f => f.PersonId == personId);
        if (feed is null)
        {
            db.CalendarFeeds.Add(new Data.CalendarFeed
            {
                PersonId = personId, TokenHash = hash, FeedId = Guid.NewGuid(), DaysJson = "[]", CreatedAt = now, UpdatedAt = now,
            });
        }
        else
        {
            feed.TokenHash = hash;
            feed.FeedId = Guid.NewGuid();
        }

        await db.SaveChangesAsync();
    }
}
