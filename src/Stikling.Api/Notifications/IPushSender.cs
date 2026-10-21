using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace Stikling.Api.Notifications;

public record PushTarget(string Endpoint, string P256dh, string Auth);

public enum PushResult
{
    Sent,

    /// <summary>The push service says the subscription is gone (404 or 410). It is deleted.</summary>
    Gone,

    Failed,
}

/// <summary>Sends one Web Push message. Behind an interface so the tests can use a fake.</summary>
public interface IPushSender
{
    Task<PushResult> SendAsync(PushTarget target, string payloadJson, TimeSpan ttl, CancellationToken cancellationToken);
}

/// <summary>Standard Web Push with VAPID, through Lib.Net.Http.WebPush. No paid push service is involved.</summary>
public sealed class WebPushSender(IOptions<NotificationOptions> options, ILogger<WebPushSender> logger) : IPushSender, IDisposable
{
    private readonly PushServiceClient _client = new();
    private VapidAuthentication? _vapid;

    public async Task<PushResult> SendAsync(PushTarget target, string payloadJson, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            return PushResult.Failed;

        _vapid ??= new VapidAuthentication(settings.VapidPublicKey!, settings.VapidPrivateKey!) { Subject = settings.Subject };

        var subscription = new PushSubscription { Endpoint = target.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, target.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, target.Auth);

        var message = new PushMessage(payloadJson) { TimeToLive = (int)ttl.TotalSeconds, Urgency = PushMessageUrgency.Normal };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, _vapid, cancellationToken);
            return PushResult.Sent;
        }
        catch (PushServiceClientException e) when (e.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushResult.Gone;
        }
        catch (PushServiceClientException e)
        {
            logger.LogWarning("A push service answered {StatusCode}.", (int)e.StatusCode);
            return PushResult.Failed;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException or ArgumentException)
        {
            // The service couldn't be reached, or the subscription's keys are not valid base64url
            logger.LogWarning(e, "A push message could not be sent.");
            return PushResult.Failed;
        }
    }

    public void Dispose() => _vapid?.Dispose();
}
