using Strings = Stikling.Web.Resources;
using Stikling.Core.People;
using Stikling.Core.Text;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>What a collection is called on screen, the same everywhere a name is shown.</summary>
public static class CollectionText
{
    /// <summary>
    /// A name someone chose is shown as it is. One still called by the server's English default is
    /// "Your plants" or "Anna's plants", in the language of the app.
    /// </summary>
    public static string Name(RegisteredCollection collection) => Name(CollectionNames.Label(collection));

    public static string Name(CollectionLabel label) => label.Kind switch
    {
        CollectionLabelKind.Yours => Strings.People.NameYours,
        CollectionLabelKind.Owners => F(Strings.People.NameOwners, Possessive.Of(label.Text)),
        _ => label.Text,
    };

    /// <summary>The line under a collection's name: whose it is and what the person can do in it.</summary>
    public static string Hint(RegisteredCollection collection)
    {
        if (collection.Gone)
            return Strings.People.CollectionGone;
        if (collection.IsOwner)
            return Strings.People.CollectionYours;

        var verb = RoleText.Verb(collection.Role, collection.ManagesPeople);
        return string.IsNullOrWhiteSpace(collection.OwnerName)
            ? F(Strings.People.CollectionSharedNoOwner, verb)
            : F(Strings.People.CollectionShared, collection.OwnerName, verb);
    }
}
