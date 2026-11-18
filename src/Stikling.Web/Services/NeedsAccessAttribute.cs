namespace Stikling.Web.Services;

/// <summary>What a person has to be allowed in the open collection to use a page.</summary>
public enum AccessLevel
{
    /// <summary>Change anything: an editor, or anyone in their own collection.</summary>
    Edit,

    /// <summary>Log care, add notes and photos: a helper or an editor.</summary>
    Log,
}

/// <summary>
/// Marks a page whose purpose is changing the collection. Someone whose role doesn't allow it sees a short
/// notice in its place (<c>GatedRoute</c>), and the page itself is never drawn.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class NeedsAccessAttribute(AccessLevel level) : Attribute
{
    public AccessLevel Level { get; } = level;
}
