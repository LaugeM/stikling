using Stikling.Core.Models;
using Stikling.Core.Plants;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Crosses;

/// <summary>A piece of pollen with where it came from resolved.</summary>
public sealed record PollenView(Pollen Pollen, ParentRef From)
{
    public string Title => From.Name;
}

/// <summary>
/// The pollen store. It shows how long ago pollen was collected and where it is kept, and nothing
/// about how long it lasts.
/// </summary>
public sealed class PollenService(IPollenRepository pollen, IPlantRepository plants)
{
    public async Task<IReadOnlyList<PollenView>> GetAllAsync()
    {
        var byId = (await plants.GetAllAsync()).ToDictionary(p => p.Id);
        return [.. (await pollen.GetAllAsync())
            .OrderBy(p => p.IsUsedUp).ThenByDescending(p => p.CollectedOn)
            .Select(p => new PollenView(p, ParentRef.Of(p.FromPlantId, p.FromName, byId)))];
    }

    public Task<Pollen?> GetAsync(Guid id) => pollen.GetAsync(id);

    public Task SaveAsync(Pollen item) => pollen.SaveAsync(item);

    public Task DeleteAsync(Guid id) => pollen.DeleteAsync(id);

    /// <summary>A new piece of pollen collected from a plant, with its names kept on the record.</summary>
    public static Pollen StartFrom(Plant plant, DateOnly today) => new()
    {
        FromPlantId = plant.Id,
        FromName = plant.DisplayName,
        Genus = plant.Genus,
        Species = plant.KnownSpecies,
        CollectedOn = today
    };

    /// <summary>"Collected 3 weeks ago". Days up to two weeks, then weeks up to two months, then months.</summary>
    public static string Age(DateOnly collected, DateOnly today)
    {
        var days = Math.Max(0, today.DayNumber - collected.DayNumber);
        if (days == 0)
            return Strings.Crosses.PollenAgeToday;
        if (days <= 14)
            return F(days == 1 ? Strings.Crosses.PollenAgeDays_One : Strings.Crosses.PollenAgeDays_Other, days);
        if (days < 60)
        {
            var weeks = days / 7;
            return F(Strings.Crosses.PollenAgeWeeks_Other, weeks);
        }

        var months = Math.Max(2, (int)Math.Round(days / 30.4));
        return months >= 24
            ? F(Strings.Crosses.PollenAgeYears_Other, months / 12)
            : F(Strings.Crosses.PollenAgeMonths_Other, months);
    }

    /// <summary>"Collected 3 weeks ago · freezer". What was recorded, with no advice.</summary>
    public static string Line(Pollen item, DateOnly today)
    {
        var age = F(Strings.Crosses.PollenCollected, Age(item.CollectedOn, today));
        return item.KeptIn is { } kept ? $"{age} · {Where(kept)}" : age;
    }

    public static string Where(PollenStorage kept) => kept switch
    {
        PollenStorage.Freezer => Strings.Crosses.KeptFreezer,
        PollenStorage.Fridge => Strings.Crosses.KeptFridge,
        _ => Strings.Crosses.KeptRoom
    };
}
