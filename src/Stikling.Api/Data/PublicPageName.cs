namespace Stikling.Api.Data;

/// <summary>
/// A name that has been claimed for a public page, kept as the SHA-256 of the name. A row is added
/// when a name is claimed and never deleted, not even when the account or the collection goes, which
/// is what keeps the name from being taken again after the person's own data is erased. Nothing
/// here points at a person or a collection.
/// </summary>
public class PublicPageName
{
    /// <summary>The hash of the name, 64 lowercase hex characters.</summary>
    public required string NameHash { get; set; }
}
