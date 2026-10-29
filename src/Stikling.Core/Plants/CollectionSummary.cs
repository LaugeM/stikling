using System.Globalization;
using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Text;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Plants;

/// <summary>One bar on the collection page. A null name is "No genus" or "No room".</summary>
/// <param name="Id">The room's id for a room, null for a genus.</param>
public sealed record CountRow(string? Name, Guid? Id, int Count);

/// <summary>The plant that has been with the person longest, and how exactly the date is known.</summary>
public sealed record LongestHeld(Plant Plant, DateOnly Since, bool YearOnly, bool MonthOnly)
{
    /// <summary>"Since 14 March 2019, 7 years", with only the month or the year when that is all that is known.</summary>
    public string Text(DateOnly today)
    {
        var since = Since.ToString(YearOnly ? "yyyy" : MonthOnly ? "MMMM yyyy" : Strings.Plants.LongestDayFormat, TextCulture.ForDates);
        var months = (today.Year - Since.Year) * 12 + today.Month - Since.Month - (today.Day < Since.Day ? 1 : 0);
        var length = months >= 12 ? Plural.Of(months / 12, Strings.Plants.LongestYears_One, Strings.Plants.LongestYears_Other)
            : months >= 1 ? Plural.Of(months, Strings.Plants.LongestMonths_One, Strings.Plants.LongestMonths_Other)
            : Strings.Plants.LongestLessThanMonth;
        return string.Format(CultureInfo.CurrentCulture, Strings.Plants.LongestSince, since, length);
    }
}

/// <summary>What the collection is made of, for the Your collection page.</summary>
public sealed record CollectionSummary(
    int Plants,
    int Species,
    int Genera,
    LongestHeld? Longest,
    IReadOnlyList<CountRow> ByGenus,
    IReadOnlyList<CountRow> ByRoom,
    int RootingNow,
    int RootedThisYear,
    bool AnyPropagations)
{
    public static CollectionSummary From(IEnumerable<Plant> plants, IEnumerable<Propagation> propagations, Places places, DateOnly today)
    {
        var held = plants.Where(p => !p.IsDeleted && p.InCollection).ToList();
        var byName = StringComparer.CurrentCultureIgnoreCase;

        var species = held
            .Where(p => Clean(p.Genus) is not null && Clean(p.KnownSpecies) is not null)
            .Select(p => (Clean(p.Genus)!.ToUpperInvariant(), Clean(p.KnownSpecies)!.ToUpperInvariant()))
            .Distinct()
            .Count();

        var byGenus = Rows(held.GroupBy(p => Clean(p.Genus)?.ToUpperInvariant()), g => Clean(g.First().Genus), _ => null);
        var byRoom = Rows(held.GroupBy(p => places.RoomOf(p.PlaceId)?.Id), g => places.RoomOf(g.First().PlaceId)?.Name, g => places.RoomOf(g.First().PlaceId)?.Id);

        var live = propagations.Where(p => !p.IsDeleted).ToList();
        return new CollectionSummary(
            held.Sum(p => p.HowMany),
            species,
            held.Select(p => Clean(p.Genus)).Where(g => g is not null).Distinct(byName).Count(),
            LongestOf(held),
            byGenus,
            byRoom,
            live.Count(p => p.IsActive && p.Stage is PropagationStage.Started or PropagationStage.Rooting),
            live.Count(p => p.RootedOn?.Year == today.Year),
            live.Count > 0);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // Biggest first, then by name, with the plants that have none at the end
    private static IReadOnlyList<CountRow> Rows<TKey>(
        IEnumerable<IGrouping<TKey, Plant>> groups, Func<IGrouping<TKey, Plant>, string?> name, Func<IGrouping<TKey, Plant>, Guid?> id)
    {
        var rows = groups.Select(g => new CountRow(name(g), id(g), g.Sum(p => p.HowMany))).ToList();
        return
        [
            .. rows.Where(r => r.Name is not null)
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            .. rows.Where(r => r.Name is null)
        ];
    }

    private static LongestHeld? LongestOf(List<Plant> held)
    {
        if (held.Count == 0)
            return null;

        var best = held
            .Select(p => (Plant: p, Start: StartOf(p)))
            .OrderBy(s => s.Start.Date)
            .ThenBy(s => s.Plant.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .First();
        return new LongestHeld(best.Plant, best.Start.Date, best.Start.YearOnly, best.Start.MonthOnly);
    }

    // When it was got, as precisely as it is known, otherwise the day it was added to the app
    private static (DateOnly Date, bool YearOnly, bool MonthOnly) StartOf(Plant plant) =>
        plant.AcquiredOn is { } acquired
            ? (acquired.Start, acquired.Month is null, acquired.Month is not null && acquired.Day is null)
            : (DateOnly.FromDateTime(plant.CreatedAt.LocalDateTime), false, false);
}
