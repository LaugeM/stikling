namespace Stikling.Api.Data;

/// <summary>
/// The plants and everything around them that a group of people share. Each person gets their
/// own on first sign-in, and others can be added to it through <see cref="Membership"/>.
/// </summary>
public class Collection
{
    public const int MaxNameLength = 100;

    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The number given to the last change that arrived. See <see cref="SyncedRecord.Version"/>.</summary>
    public long LastVersion { get; set; }

    /// <summary>
    /// The one person who owns it. Always set in practice (the column is nullable only so the
    /// migration could add it), and always a member with the editor role.
    /// </summary>
    public Guid? OwnerPersonId { get; set; }

    public List<Membership> Members { get; set; } = [];
}
