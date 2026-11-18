namespace Stikling.Core.People;

/// <summary>How a collection's name is to be written out.</summary>
public enum CollectionLabelKind
{
    /// <summary>As the collection is called.</summary>
    Named,

    /// <summary>The person's own, still called by the name the server gave it: "Your plants".</summary>
    Yours,

    /// <summary>Somebody else's, still called by the name the server gave it: "Anna's plants".</summary>
    Owners,
}

/// <param name="Text">The collection's name for <see cref="CollectionLabelKind.Named"/>, the owner's name for <see cref="CollectionLabelKind.Owners"/>.</param>
public readonly record struct CollectionLabel(CollectionLabelKind Kind, string Text);

public static class CollectionNames
{
    /// <summary>
    /// The name the server gives a person's first collection. It is English whatever language the app is in, so
    /// while a collection still has it, the app writes its own words instead.
    /// </summary>
    public const string ServerDefault = "My plants";

    public static bool IsServerDefault(string? name) =>
        string.Equals(name?.Trim(), ServerDefault, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What to call a collection. One someone named themselves keeps its name. One still called the server's default
    /// is "Your plants" for the person's own and "{owner}'s plants" for someone else's, or its name when the
    /// owner hasn't set one, so two of them never get the same words.
    /// </summary>
    public static CollectionLabel Label(string name, bool isOwner, string? ownerName)
    {
        if (!IsServerDefault(name))
            return new CollectionLabel(CollectionLabelKind.Named, name);
        if (isOwner)
            return new CollectionLabel(CollectionLabelKind.Yours, "");
        return string.IsNullOrWhiteSpace(ownerName)
            ? new CollectionLabel(CollectionLabelKind.Named, name)
            : new CollectionLabel(CollectionLabelKind.Owners, ownerName.Trim());
    }

    public static CollectionLabel Label(RegisteredCollection collection) =>
        Label(collection.Name, collection.IsOwner, collection.OwnerName);
}
