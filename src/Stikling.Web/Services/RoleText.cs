using Strings = Stikling.Web.Resources;
using Stikling.Core.People;

namespace Stikling.Web.Services;

/// <summary>The words for what a person can do in a collection, the same on every screen that names a role.</summary>
public static class RoleText
{
    /// <summary>"Can edit", for a list or a choice.</summary>
    public static string Label(RoleChoice choice) => choice switch
    {
        { Role: CollectionRole.Viewer } => Strings.People.RoleCanView,
        { Role: CollectionRole.Helper } => Strings.People.RoleCanLog,
        { ManagesPeople: true } => Strings.People.RoleCanEditAndManage,
        _ => Strings.People.RoleCanEdit,
    };

    public static string Label(CollectionRole role, bool managesPeople) => Label(RoleChoice.Of(role, managesPeople));

    /// <summary>"edit", for after "You can".</summary>
    public static string Verb(RoleChoice choice) => choice switch
    {
        { Role: CollectionRole.Viewer } => Strings.People.VerbView,
        { Role: CollectionRole.Helper } => Strings.People.VerbLog,
        { ManagesPeople: true } => Strings.People.VerbEditAndManage,
        _ => Strings.People.VerbEdit,
    };

    public static string Verb(CollectionRole role, bool managesPeople) => Verb(RoleChoice.Of(role, managesPeople));
}
