using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;

namespace Stikling.Api.Notifications;

/// <summary>
/// One run of the job that sends the daily notifications. The job is started every 15 minutes, and
/// sends each person's text for the day when their time has come. The server only sends text the
/// device made, and nothing here looks at the person's records.
/// </summary>
public class NotificationSender(StiklingDbContext db, IPushSender push, TimeProvider clock, ILogger<NotificationSender> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.NotificationSchedules.AsNoTracking()
            .Where(s => s.NextSendAt <= now)
            .Select(s => s.PersonId)
            .ToListAsync(cancellationToken);

        foreach (var personId in due)
        {
            try
            {
                await SendToAsync(personId, now, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Each person is saved on their own, so the others are not sent twice. This one was claimed before it failed, so it isn't tried again
                logger.LogError(e, "Sending a person's notification failed.");
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task SendToAsync(Guid personId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var schedule = await db.NotificationSchedules.AsNoTracking().SingleOrDefaultAsync(s => s.PersonId == personId, cancellationToken);
        if (schedule?.NextSendAt is not { } due || due > now)
            return;

        NotificationRules.TryParseTimeZone(schedule.TimeZone, out var zone);
        var day = NotificationRules.LocalDay(due, zone);
        var days = NotificationRules.ReadDays(schedule.DaysJson);
        var text = days.FirstOrDefault(d => d.Day == day)?.Text;
        var previous = schedule.LastSentOn;
        var next = NotificationRules.NextSendAt(schedule.TimeZone, schedule.NotifyAt, days, day, now);

        // The day is claimed before anything is pushed, so a run that overlaps this one (a slow job
        // still going when the next starts) finds the claim taken and leaves the person alone
        var claimed = await db.NotificationSchedules
            .Where(s => s.PersonId == personId && s.NextSendAt == due)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSentOn, (DateOnly?)day).SetProperty(s => s.NextSendAt, next), cancellationToken);
        if (claimed == 0)
            return;

        // A day missed by too long, like when the job was stopped, is not sent late. A person without devices has nothing to send to
        if (string.IsNullOrWhiteSpace(text) || now - due > NotificationRules.MissedAfter)
            return;

        var payload = JsonSerializer.Serialize(new { title = "Today in Stikling", body = text, tag = "today", url = "/" });
        var subscriptions = await db.PushSubscriptions.AsNoTracking().Where(s => s.PersonId == personId).ToListAsync(cancellationToken);

        var delivered = 0;
        var failed = 0;
        foreach (var subscription in subscriptions)
        {
            var target = new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth);
            switch (await push.SendAsync(target, payload, NotificationRules.Ttl, cancellationToken))
            {
                case PushResult.Sent:
                    delivered++;
                    await db.PushSubscriptions.Where(s => s.Id == subscription.Id)
                        .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSentAt, (DateTimeOffset?)now), cancellationToken);
                    break;
                case PushResult.Gone:
                    // Deleted by id, so one that is already gone isn't an error
                    await db.PushSubscriptions.Where(s => s.Id == subscription.Id).ExecuteDeleteAsync(cancellationToken);
                    break;
                default:
                    failed++;
                    logger.LogWarning("A push message was not delivered.");
                    break;
            }
        }

        // Nothing got through and a push service failed: give the day back, so the next run tries again.
        // The 2 hour rule above bounds how long that goes on
        if (delivered == 0 && failed > 0)
        {
            await db.NotificationSchedules
                .Where(s => s.PersonId == personId && s.LastSentOn == day && s.NextSendAt == next)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSentOn, previous).SetProperty(s => s.NextSendAt, (DateTimeOffset?)due), cancellationToken);
        }
    }
}
