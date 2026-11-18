namespace Stikling.Api.Data;

/// <summary>
/// A link that lets someone join a collection with a role. It is only kept on the server and is
/// not a synced record. It is open until it is used, withdrawn or runs out.
/// </summary>
public class Invite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 of the secret in the address, as 64 lowercase hex characters, which is how an invite is found.
    /// The token is 16 random bytes as base64url without padding.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>The token encrypted with <c>Invites:TokenKey</c> (<see cref="Collections.InviteTokens"/>), so the link can be shown again.</summary>
    public required byte[] TokenCiphertext { get; set; }

    public Guid CollectionId { get; set; }

    public MemberRole Role { get; set; }
    public bool ManagesPeople { get; set; }

    /// <summary>Who made it. Null when that person has since been deleted from a collection that lives on.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Who joined with it. No foreign key, so deleting their account only needs it set to null.</summary>
    public Guid? UsedBy { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Who the invite is for, as the person who made it wrote it. Only shown to people who manage the collection. Null when nothing was written, and on invites made before it existed.</summary>
    public string? ForName { get; set; }

    public bool IsOpen(DateTimeOffset now) => UsedAt is null && RevokedAt is null && ExpiresAt > now;
}
