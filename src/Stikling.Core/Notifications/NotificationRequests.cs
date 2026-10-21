using System.Text.Json.Serialization;

namespace Stikling.Core.Notifications;

/// <summary>A device's Web Push subscription, as the app sends it to the API and the API reads it.</summary>
public record SubscriptionRequest(string? Endpoint, string? P256dh, string? Auth);

/// <summary>Names one device's subscription by its push address.</summary>
public record EndpointRequest(string? Endpoint);

/// <summary>
/// A person's schedule: the time zone, the time of day on a quarter hour as "HH:mm" (null turns it
/// off) and the text for each day that has something to say.
/// </summary>
public record DaysRequest(string? TimeZone, string? NotifyAt, List<NotificationDay>? Days);

/// <summary>The text of the notification for one day.</summary>
public record NotificationDay(
    [property: JsonPropertyName("day")] DateOnly Day,
    [property: JsonPropertyName("text")] string Text);
