namespace Stikling.Api.Data;

/// <summary>
/// One device's Web Push subscription. It belongs to the person, since the daily notification goes
/// to every device they turned notifications on for. Only kept on the server, and not a synced record.
/// </summary>
public class StoredPushSubscription
{
    public const int MaxPerPerson = 10;
    public const int MaxEndpointLength = 2000;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public required string Endpoint { get; set; }

    /// <summary>SHA-256 of the endpoint as lower case hex. The endpoint is too long to have an index of its own.</summary>
    public required string EndpointHash { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSentAt { get; set; }
}
