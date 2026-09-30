namespace Stikling.Api.Data;

/// <summary>
/// A Clerk user whose account was deleted. Another device can still hold a valid session token for
/// a minute after Clerk deletes the user, and without this its next sync would create the person
/// again and upload everything on it. Clerk never gives the same id to a new user.
/// </summary>
public class DeletedAccount
{
    public required string ClerkUserId { get; set; }

    public DateTimeOffset DeletedAt { get; set; }
}
