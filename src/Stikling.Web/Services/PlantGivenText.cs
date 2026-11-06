using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Text;
using Strings = Stikling.Web.Resources;

namespace Stikling.Web.Services;

/// <summary>What a plant has given, in words: on its page and on its printed story.</summary>
public static class PlantGivenText
{
    public static IEnumerable<string> Parts(PlantGiven given)
    {
        if (given.Taken > 0)
            yield return F(Strings.PlantDetail.GivenTaken, Taken(given), given.Rooted);
        if (given.GivenAway + given.Sold > 0)
            yield return PassedOn(given);
        if (given.FromThoseInTurn > 0)
            yield return Plural.Of(given.FromThoseInTurn, Strings.PlantDetail.GrownInTurn_One, Strings.PlantDetail.GrownInTurn_Other);
    }

    private static string Taken(PlantGiven given) => JoinAnd(given.TakenByType.Select(t => t.Key switch
    {
        PropagationType.Offset => Plural.Of(t.Value, Strings.PlantDetail.TakenPup_One, Strings.PlantDetail.TakenPup_Other),
        PropagationType.Leaf => Plural.Of(t.Value, Strings.PlantDetail.TakenLeaf_One, Strings.PlantDetail.TakenLeaf_Other),
        PropagationType.AirLayer => Plural.Of(t.Value, Strings.PlantDetail.TakenAirLayer_One, Strings.PlantDetail.TakenAirLayer_Other),
        PropagationType.Other => Plural.Of(t.Value, Strings.PlantDetail.TakenOther_One, Strings.PlantDetail.TakenOther_Other),
        PropagationType.Corm => Plural.Of(t.Value, Strings.PlantDetail.TakenCorm_One, Strings.PlantDetail.TakenCorm_Other),
        PropagationType.Seed => Plural.Of(t.Value, Strings.PlantDetail.TakenSeed_One, Strings.PlantDetail.TakenSeed_Other),
        PropagationType.Division => Plural.Of(t.Value, Strings.PlantDetail.TakenDivision_One, Strings.PlantDetail.TakenDivision_Other),
        _ => Plural.Of(t.Value, Strings.Common.Cutting_One, Strings.Common.Cutting_Other)
    }));

    private static string PassedOn(PlantGiven given) => JoinAnd(new[]
    {
        given.GivenAway > 0 ? F(Strings.PlantDetail.GivenAway, given.GivenAway) : null,
        given.Sold > 0 ? F(Strings.PlantDetail.SoldCount, given.Sold) : null
    }.OfType<string>());

    /// <summary>"a", "a and b", "a, b and c".</summary>
    private static string JoinAnd(IEnumerable<string> parts)
    {
        var list = parts.ToList();
        return list.Count <= 1 ? string.Concat(list) : F(Strings.PlantDetail.ListAnd, string.Join(", ", list.SkipLast(1)), list[^1]);
    }

    private static string F(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
