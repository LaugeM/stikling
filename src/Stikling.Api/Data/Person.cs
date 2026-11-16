namespace Stikling.Api.Data;

/// <summary>
/// Someone who has signed in. Everything else points at <see cref="Id"/>, never at the Clerk
/// id, so a different sign-in service only has to fill in a different column.
/// </summary>
public class Person
{
    public const int MaxNameLength = 40;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The <c>sub</c> claim of the person's Clerk session tokens.</summary>
    public required string ClerkUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The name other people in a collection see, or null until they have chosen one.</summary>
    public string? Name { get; set; }

    public List<Membership> Memberships { get; set; } = [];
}
