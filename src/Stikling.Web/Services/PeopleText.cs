using Strings = Stikling.Web.Resources;
using Stikling.Core.People;
using static Stikling.Core.Text.Format;

namespace Stikling.Web.Services;

/// <summary>The plain words for why a call about people failed, shared by the Account page and the invite page.</summary>
public static class PeopleText
{
    /// <param name="failure">Why it failed.</param>
    /// <param name="field">The field a rule was broken in, for <see cref="PeopleFailure.Invalid"/>.</param>
    /// <param name="nameMax">The most characters the name being saved can have.</param>
    public static string Problem(PeopleFailure failure, string? field = null, int nameMax = PeopleLimits.MaxPersonNameLength) => failure switch
    {
        PeopleFailure.Offline => Strings.Account.ShareUnreachable,
        PeopleFailure.SignedOut => Strings.Account.ShareSignInRanOut,
        PeopleFailure.Forbidden => Strings.People.NotAllowed,
        PeopleFailure.TooMany => Strings.People.TooManyInvites,
        PeopleFailure.Owner => Strings.People.OwnerCantLeaveProblem,
        PeopleFailure.Invalid when string.Equals(field, "name", StringComparison.OrdinalIgnoreCase) => F(Strings.People.NeedsLength, nameMax),
        _ => Strings.Account.ShareTurnedDown,
    };
}
