using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>The words for the choices on a cross and a seed lot.</summary>
public static class CrossLabels
{
    public static string Method(CrossMethod method) => method switch
    {
        CrossMethod.Brush => Resources.Crosses.MethodBrush,
        CrossMethod.ByHand => Resources.Crosses.MethodByHand,
        _ => Resources.Crosses.MethodOther
    };

    public static string Outcome(CrossOutcome? outcome) => outcome switch
    {
        CrossOutcome.Took => Resources.Crosses.OutcomeTook,
        CrossOutcome.DidNotTake => Resources.Crosses.OutcomeDidNotTake,
        _ => Resources.Crosses.OutcomeNotKnown
    };

    public static string Unit(SeedUnit unit) => unit == SeedUnit.Grams ? Resources.Crosses.UnitGrams : Resources.Crosses.UnitSeeds;

    public static string Filter(Core.Crosses.CrossLogFilter filter) => filter switch
    {
        Core.Crosses.CrossLogFilter.Took => Resources.Crosses.FilterTook,
        Core.Crosses.CrossLogFilter.DidNotTake => Resources.Crosses.FilterDidNotTake,
        Core.Crosses.CrossLogFilter.NotKnown => Resources.Crosses.FilterNotKnown,
        _ => Resources.Crosses.FilterAll
    };
}
