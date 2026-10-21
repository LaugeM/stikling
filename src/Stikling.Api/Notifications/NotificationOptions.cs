namespace Stikling.Api.Notifications;

/// <summary>
/// The VAPID keys that identify this server to the push services. Without them the notification
/// endpoints answer 503, and nothing is sent.
/// </summary>
public class NotificationOptions
{
    public const string Section = "Notifications";

    /// <summary>The public key as base64url, which the app subscribes with.</summary>
    public string? VapidPublicKey { get; set; }

    public string? VapidPrivateKey { get; set; }

    public string Subject { get; set; } = "mailto:support@stikling.app";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
}
