namespace Stikling.Api.Data;

/// <summary>A person's place in a collection, and what they are allowed to do there.</summary>
public class Membership
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public MemberRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Stored as text, like the enums on the device.</summary>
public enum MemberRole
{
    /// <summary>Can only read, for example a plant sitter.</summary>
    Viewer,

    /// <summary>Can change everything.</summary>
    Editor,
}
