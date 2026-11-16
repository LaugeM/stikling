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

    /// <summary>Whether they can invite people and change the roles of viewers and helpers. Only together with <see cref="MemberRole.Editor"/>.</summary>
    public bool ManagesPeople { get; set; }
}

/// <summary>Stored as text, like the enums on the device.</summary>
public enum MemberRole
{
    /// <summary>Can only read, for example a plant sitter.</summary>
    Viewer,

    /// <summary>Can change everything.</summary>
    Editor,

    /// <summary>Can log care, add notes and photos, and undo their own entries.</summary>
    Helper,
}
