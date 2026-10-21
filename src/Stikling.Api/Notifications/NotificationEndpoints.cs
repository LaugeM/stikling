using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stikling.Api.Data;
using Stikling.Api.People;

namespace Stikling.Api.Notifications;

/// <summary>
/// What the app calls for the daily notification: the key to subscribe with, each device's
/// subscription, and the text for the coming days. The sending itself is <see cref="NotificationSender"/>.
/// </summary>
public static class NotificationEndpoints
{
    public record SubscriptionRequest(string? Endpoint, string? P256dh, string? Auth);

    public record EndpointRequest(string? Endpoint);

    public record DaysRequest(string? TimeZone, string? NotifyAt, List<NotificationDay>? Days);

    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder app)
    {
        // The app asks before it has a sign-in to subscribe with, so this one is open
        app.MapGet("/notifications/key", (IOptions<NotificationOptions> options) =>
            options.Value.IsConfigured
                ? Results.Ok(new { publicKey = options.Value.VapidPublicKey })
                : Results.NotFound());

        var notifications = app.MapGroup("/me/notifications").RequireAuthorization();

        // Without keys nothing could be sent, so there is nothing to save either
        notifications.AddEndpointFilter(async (context, next) =>
            context.HttpContext.RequestServices.GetRequiredService<IOptions<NotificationOptions>>().Value.IsConfigured
                ? await next(context)
                : Results.Problem("Notifications aren't set up on the server yet.", statusCode: StatusCodes.Status503ServiceUnavailable));

        notifications.MapPut("/subscriptions", async (
            SubscriptionRequest request, CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            if (request.Endpoint is not { Length: > 0 } endpoint
                || endpoint.Length > StoredPushSubscription.MaxEndpointLength
                || !Uri.TryCreate(endpoint, UriKind.Absolute, out var address)
                || address.Scheme != Uri.UriSchemeHttps)
                return Problem("endpoint", "The endpoint must be an https address of at most 2000 characters.");

            if (request.P256dh is not { Length: > 0 and <= 200 } p256dh || request.Auth is not { Length: > 0 and <= 200 } auth)
                return Problem("keys", "p256dh and auth are required.");

            var person = await current.FindOrCreateAsync();

            try
            {
                await SaveSubscriptionAsync(db, person.Id, endpoint, p256dh, auth, clock.GetUtcNow());
            }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The same device asked twice at once. The other one is saved now
                db.ChangeTracker.Clear();
                await SaveSubscriptionAsync(db, person.Id, endpoint, p256dh, auth, clock.GetUtcNow());
            }

            return Results.NoContent();
        });

        notifications.MapPost("/subscriptions/remove", async (EndpointRequest request, CurrentPerson current, StiklingDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.Endpoint))
                return Problem("endpoint", "The endpoint is required.");

            var person = await current.FindOrCreateAsync();
            var hash = NotificationRules.HashEndpoint(request.Endpoint);
            await db.PushSubscriptions.Where(s => s.PersonId == person.Id && s.EndpointHash == hash).ExecuteDeleteAsync();
            return Results.NoContent();
        });

        notifications.MapPut("/days", async (DaysRequest request, CurrentPerson current, StiklingDbContext db, TimeProvider clock) =>
        {
            var person = await current.FindOrCreateAsync();

            // No time means notifications are off for this person. The row stays with nothing to send,
            // so turning them on again the same day doesn't send that day's text twice (LastSentOn is kept)
            if (request.NotifyAt is null)
            {
                await db.NotificationSchedules.Where(s => s.PersonId == person.Id).ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.NextSendAt, (DateTimeOffset?)null)
                    .SetProperty(s => s.DaysJson, "[]")
                    .SetProperty(s => s.UpdatedAt, clock.GetUtcNow()));
                return Results.NoContent();
            }

            if (!NotificationRules.TryParseTimeZone(request.TimeZone, out var zone))
                return Problem("timeZone", "The time zone isn't known.");
            if (!NotificationRules.TryParseNotifyAt(request.NotifyAt, out var notifyAt))
                return Problem("notifyAt", "The time must be written HH:mm, on a quarter hour.");

            var now = clock.GetUtcNow();
            if (NotificationRules.DaysProblem(request.Days, zone, now) is { } problem)
                return Problem("days", problem);

            try
            {
                await SaveScheduleAsync(db, person.Id, request.TimeZone!, notifyAt, request.Days!, now);
            }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.ChangeTracker.Clear();
                await SaveScheduleAsync(db, person.Id, request.TimeZone!, notifyAt, request.Days!, now);
            }

            return Results.NoContent();
        });

        notifications.MapPost("/test", async (
            EndpointRequest request, CurrentPerson current, StiklingDbContext db, IPushSender push, CancellationToken cancellation) =>
        {
            if (string.IsNullOrEmpty(request.Endpoint))
                return Problem("endpoint", "The endpoint is required.");

            var person = await current.FindOrCreateAsync();
            var hash = NotificationRules.HashEndpoint(request.Endpoint);
            if (await db.PushSubscriptions.SingleOrDefaultAsync(s => s.PersonId == person.Id && s.EndpointHash == hash) is not { } subscription)
                return Results.NotFound();

            var payload = JsonSerializer.Serialize(new
            {
                title = "Stikling",
                body = "Notifications are on. Your day's list will come at the time you picked.",
                tag = "test",
                url = "/",
            });

            switch (await push.SendAsync(new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, NotificationRules.Ttl, cancellation))
            {
                case PushResult.Sent:
                    return Results.NoContent();
                case PushResult.Gone:
                    db.PushSubscriptions.Remove(subscription);
                    await db.SaveChangesAsync(cancellation);
                    return Results.StatusCode(StatusCodes.Status410Gone);
                default:
                    return Results.Problem("The push service didn't take the message.", statusCode: StatusCodes.Status502BadGateway);
            }
        });

        return app;
    }

    private static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>Adds the subscription or updates it by its endpoint. An endpoint held by someone else moves to this person.</summary>
    private static async Task SaveSubscriptionAsync(
        StiklingDbContext db, Guid personId, string endpoint, string p256dh, string auth, DateTimeOffset now)
    {
        var hash = NotificationRules.HashEndpoint(endpoint);
        var stored = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.EndpointHash == hash);
        if (stored is null)
        {
            db.PushSubscriptions.Add(new StoredPushSubscription
            {
                PersonId = personId, Endpoint = endpoint, EndpointHash = hash, P256dh = p256dh, Auth = auth, CreatedAt = now,
            });
        }
        else
        {
            if (stored.PersonId != personId)
            {
                stored.PersonId = personId;
                stored.CreatedAt = now;
            }

            stored.P256dh = p256dh;
            stored.Auth = auth;
        }

        await db.SaveChangesAsync();

        // The oldest go when there are more than a person can have
        var surplus = await db.PushSubscriptions
            .Where(s => s.PersonId == personId && s.EndpointHash != hash)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(StoredPushSubscription.MaxPerPerson - 1)
            .ToListAsync();
        if (surplus.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(surplus);
            await db.SaveChangesAsync();
        }
    }

    private static async Task SaveScheduleAsync(
        StiklingDbContext db, Guid personId, string timeZone, TimeOnly notifyAt, List<NotificationDay> days, DateTimeOffset now)
    {
        var schedule = await db.NotificationSchedules.SingleOrDefaultAsync(s => s.PersonId == personId);
        if (schedule is null)
            db.NotificationSchedules.Add(schedule = new NotificationSchedule { PersonId = personId, TimeZone = timeZone, DaysJson = "[]" });

        schedule.TimeZone = timeZone;
        schedule.NotifyAt = notifyAt;
        schedule.DaysJson = NotificationRules.WriteDays(days);
        schedule.UpdatedAt = now;
        schedule.NextSendAt = NotificationRules.NextSendAt(timeZone, notifyAt, days, schedule.LastSentOn, now);
        await db.SaveChangesAsync();
    }
}
