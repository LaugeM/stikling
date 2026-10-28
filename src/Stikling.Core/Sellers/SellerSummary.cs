using Stikling.Core.Models;

namespace Stikling.Core.Sellers;

/// <summary>How the plants from one seller have done since.</summary>
public sealed class SellerSummary
{
    /// <summary>A pest case counts when it started this many days after the plant was got, or sooner.</summary>
    public const int PestWindowDays = 30;

    public required IReadOnlyList<Plant> Plants { get; init; }
    public required IReadOnlyList<Propagation> Propagations { get; init; }

    public int StillHere { get; init; }
    public int Died { get; init; }
    public int GivenAway { get; init; }
    public int Sold { get; init; }

    /// <summary>Pest cases that started within 30 days of getting a plant from here, and covered that plant by name.</summary>
    public int PestCases { get; init; }

    /// <summary>How many of the plants those cases covered.</summary>
    public int PlantsWithPests { get; init; }

    public int ArrivedGood { get; init; }
    public int ArrivedOk { get; init; }
    public int ArrivedPoor { get; init; }

    /// <summary>How many times each problem was ticked, for the ones ticked at least once.</summary>
    public required IReadOnlyDictionary<ArrivalProblem, int> Problems { get; init; }

    /// <summary>What was paid for the plants with a price, and how many that is.</summary>
    public decimal PaidTotal { get; init; }
    public int PaidCount { get; init; }
    public decimal? PaidAverage => PaidCount == 0 ? null : PaidTotal / PaidCount;

    public bool HasArrivals => ArrivedGood + ArrivedOk + ArrivedPoor > 0;

    public int Total => Plants.Count + Propagations.Count;

    public static SellerSummary For(
        Seller seller,
        SellerList sellers,
        IEnumerable<Plant> plants,
        IEnumerable<Propagation> propagations,
        IEnumerable<PestCase> pestCases)
    {
        var mine = plants.Where(p => !p.IsDeleted && sellers.For(p.SellerId, p.Source)?.Id == seller.Id).ToList();
        var minePropagations = propagations.Where(p => !p.IsDeleted && sellers.For(p.SellerId, p.Source)?.Id == seller.Id).ToList();

        var ratings = mine.Select(p => p.ArrivedIn).Concat(minePropagations.Select(p => p.ArrivedIn)).ToList();
        var problems = mine.SelectMany(p => p.ArrivalProblems).Concat(minePropagations.SelectMany(p => p.ArrivalProblems))
            .GroupBy(p => p)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var cases = pestCases.Where(c => !c.IsDeleted && c.Scope == PestScope.PickedPlants).ToList();
        var caught = new HashSet<Guid>();
        var withPests = new HashSet<Guid>();
        foreach (var plant in mine)
        {
            if (plant.AcquiredOn is not { } got)
                continue;
            foreach (var pestCase in cases)
            {
                var days = pestCase.StartedOn.DayNumber - got.Start.DayNumber;
                if (days >= 0 && days <= PestWindowDays && pestCase.PlantIds.Contains(plant.Id))
                {
                    caught.Add(pestCase.Id);
                    withPests.Add(plant.Id);
                }
            }
        }

        var priced = mine.Where(p => p.PricePaid is not null).ToList();

        return new SellerSummary
        {
            Plants = mine.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
            Propagations = minePropagations,
            StillHere = mine.Count(p => p.InCollection),
            Died = mine.Count(p => p.Status == PlantStatus.Died),
            GivenAway = mine.Count(p => p.Status == PlantStatus.GivenAway),
            Sold = mine.Count(p => p.Status == PlantStatus.Sold),
            PestCases = caught.Count,
            PlantsWithPests = withPests.Count,
            ArrivedGood = ratings.Count(r => r == ArrivalRating.Good),
            ArrivedOk = ratings.Count(r => r == ArrivalRating.Ok),
            ArrivedPoor = ratings.Count(r => r == ArrivalRating.Poor),
            Problems = problems,
            PaidTotal = priced.Sum(p => p.PricePaid!.Value),
            PaidCount = priced.Count
        };
    }

    /// <summary>How many plants and propagations came from each seller, by the id of the seller that stands for it now.</summary>
    public static Dictionary<Guid, int> CountsBySeller(SellerList sellers, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var found in plants.Where(p => !p.IsDeleted).Select(p => sellers.For(p.SellerId, p.Source))
                     .Concat(propagations.Where(p => !p.IsDeleted).Select(p => sellers.For(p.SellerId, p.Source))))
        {
            if (found is { } seller)
                counts[seller.Id] = counts.GetValueOrDefault(seller.Id) + 1;
        }
        return counts;
    }
}
