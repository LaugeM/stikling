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

    public List<Membership> Members { get; set; } = [];
}
