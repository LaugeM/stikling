using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Crosses;

/// <summary>
/// Crosses, seed lots, the seed propagations sown from them and the plants made from those, read
/// together. Everything that follows a seedling back to both of its parents goes through here, so a
/// cross whose plant was deleted or never synced still reads, and nothing is stored twice.
/// </summary>
public sealed class CrossBook
{
    private readonly IReadOnlyList<Cross> crosses;
    private readonly IReadOnlyList<SeedLot> lots;
    private readonly IReadOnlyList<Propagation> propagations;
    private readonly IReadOnlyList<Plant> plants;
    private readonly Dictionary<Guid, Plant> plantById;
    private readonly Dictionary<Guid, Cross> crossById;
    private readonly Dictionary<Guid, SeedLot> lotById;

    public CrossBook(IEnumerable<Cross> crosses, IEnumerable<SeedLot> lots, IEnumerable<Propagation> propagations, IEnumerable<Plant> plants)
    {
        this.crosses = [.. crosses.Where(c => !c.IsDeleted)];
        this.lots = [.. lots.Where(l => !l.IsDeleted)];
        this.propagations = [.. propagations.Where(p => !p.IsDeleted)];
        this.plants = [.. plants.Where(p => !p.IsDeleted)];
        plantById = this.plants.ToDictionary(p => p.Id);
        crossById = this.crosses.ToDictionary(c => c.Id);
        lotById = this.lots.ToDictionary(l => l.Id);
    }

    public IReadOnlyList<Cross> Crosses => crosses;

    public IReadOnlyList<SeedLot> Lots => lots;

    /// <summary>True when there is a cross or a seed lot, which is when the Crosses section shows.</summary>
    public bool Any => crosses.Count > 0 || lots.Count > 0;

    public CrossView ViewOf(Cross cross) => new(
        cross,
        ParentRef.Of(cross.PodParentPlantId, cross.PodParentName, plantById),
        ParentRef.Of(cross.PollenParentPlantId, cross.PollenParentName, plantById));

    public CrossView? ViewOf(Guid crossId) => crossById.TryGetValue(crossId, out var c) ? ViewOf(c) : null;

    public Cross? Find(Guid id) => crossById.GetValueOrDefault(id);

    public SeedLot? FindLot(Guid id) => lotById.GetValueOrDefault(id);

    /// <summary>Lots made from the cross, the newest first.</summary>
    public IReadOnlyList<SeedLot> LotsOf(Cross cross) =>
        [.. lots.Where(l => l.CrossId == cross.Id).OrderByDescending(l => l.CollectedOn ?? DateOnly.MinValue).ThenByDescending(l => l.CreatedAt)];

    /// <summary>The seed propagations sown from the lot, the first sown first.</summary>
    public SowingSummary SowingsOf(SeedLot lot) =>
        new([.. propagations.Where(p => p.SeedLotId == lot.Id).OrderBy(p => p.StartedOn)]);

    public LotView LotView(SeedLot lot) =>
        new(lot, lot.CrossId is { } id ? ViewOf(id) : null, SowingsOf(lot));

    /// <summary>What came of a cross, counted from what points back to it.</summary>
    public CrossSeedlings SeedlingsOf(Cross cross)
    {
        var madeLots = LotsOf(cross);
        if (madeLots.Count == 0)
            return CrossSeedlings.None;

        var lotIds = madeLots.Select(l => l.Id).ToHashSet();
        var sown = propagations.Where(p => p.SeedLotId is { } id && lotIds.Contains(id)).OrderBy(p => p.StartedOn).ToList();
        var sownIds = sown.Select(p => p.Id).ToHashSet();
        var made = plants.Where(p => p.FromPropagationId is { } id && sownIds.Contains(id))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Seedlings that came up and haven't left their propagation. A unit that failed is not taken
        // off, since the count can't say whether a seed or a seedling failed. Units split off a sowing
        // came up there: the sowing takes them off its own, and the split counts them as up.
        var growing = sown.Where(p => p.IsActive)
            .Sum(p => Math.Max(0, Math.Min(p.RemainingCount,
                (SowingSummary.IsSplitOfAnother(p, sownIds) ? p.SeedsGerminated ?? p.InitialCount : p.SeedsGerminated ?? 0)
                - p.PottedUpCount - p.GivenAwayCount - p.SplitOffCount)));

        // A split's seeds were sown and came up in the sowing it was split from, so only the sowings count
        var sowings = sown.Where(p => !SowingSummary.IsSplitOfAnother(p, sownIds)).ToList();
        return new CrossSeedlings(
            madeLots,
            sown,
            made,
            sowings.Sum(p => p.InitialCount),
            sowings.Sum(p => p.SeedsGerminated ?? 0),
            growing,
            made.Count(p => p.Status == PlantStatus.Active) + growing);
    }

    /// <summary>The cross a seed propagation was sown from, through its lot.</summary>
    public CrossView? OriginOf(Propagation propagation) =>
        propagation.SeedLotId is { } lotId && lotById.TryGetValue(lotId, out var lot) && lot.CrossId is { } crossId
            ? ViewOf(crossId)
            : null;

    /// <summary>The cross a plant grew from, through the propagation it was potted up from.</summary>
    public CrossView? OriginOf(Plant plant) =>
        plant.FromPropagationId is { } id && propagations.FirstOrDefault(p => p.Id == id) is { } propagation
            ? OriginOf(propagation)
            : null;

    /// <summary>
    /// The cross's working name for a seedling: its origin has one, and the seedling has no nickname
    /// or cultivar of its own. Null otherwise.
    /// </summary>
    public string? WorkingNameFor(Propagation propagation) =>
        string.IsNullOrWhiteSpace(propagation.Nickname) && string.IsNullOrWhiteSpace(propagation.Cultivar)
            ? NameOf(OriginOf(propagation))
            : null;

    /// <inheritdoc cref="WorkingNameFor(Propagation)"/>
    public string? WorkingNameFor(Plant plant) =>
        string.IsNullOrWhiteSpace(plant.Nickname) && string.IsNullOrWhiteSpace(plant.Cultivar)
            ? NameOf(OriginOf(plant))
            : null;

    private static string? NameOf(CrossView? view) =>
        view is { HasWorkingName: true } ? view.Cross.Name!.Trim() : null;

    /// <summary>Every cross with what came of it, the newest pollination first, for the cross log.</summary>
    public IReadOnlyList<CrossRow> Log(CrossLogFilter filter = CrossLogFilter.All) =>
        [.. crosses
            .Where(c => filter switch
            {
                CrossLogFilter.Took => c.Outcome == CrossOutcome.Took,
                CrossLogFilter.DidNotTake => c.Outcome == CrossOutcome.DidNotTake,
                CrossLogFilter.NotKnown => c.Outcome is null,
                _ => true
            })
            .OrderByDescending(c => c.PollinatedOn).ThenByDescending(c => c.CreatedAt)
            .Select(c => new CrossRow(ViewOf(c), SeedlingsOf(c)))];

    /// <summary>
    /// Every cross of the same two parents as this one, in either direction, this one included, newest
    /// pollination first. Nothing is stored: it is found by comparing the parents of the crosses.
    /// </summary>
    public PairHistory PairOf(Cross cross)
    {
        var rows = crosses
            .Where(c => c.Id == cross.Id || SamePair(c, cross))
            .OrderByDescending(c => c.PollinatedOn).ThenByDescending(c => c.CreatedAt)
            .Select(c => new CrossRow(ViewOf(c), SeedlingsOf(c)))
            .ToList();
        return new PairHistory(
            rows,
            rows.Count(r => r.View.Cross.Outcome == CrossOutcome.Took),
            rows.Sum(r => r.Seedlings.Sown),
            rows.Sum(r => r.Seedlings.CameUp),
            rows.Sum(r => r.Seedlings.Alive));
    }

    private static bool SamePair(Cross a, Cross b) =>
        SameParent(a.PodParentPlantId, a.PodParentName, b.PodParentPlantId, b.PodParentName)
            && SameParent(a.PollenParentPlantId, a.PollenParentName, b.PollenParentPlantId, b.PollenParentName)
        || SameParent(a.PodParentPlantId, a.PodParentName, b.PollenParentPlantId, b.PollenParentName)
            && SameParent(a.PollenParentPlantId, a.PollenParentName, b.PodParentPlantId, b.PodParentName);

    // A plant id only matches a plant id. Without one the kept or typed name decides, and no name matches nothing.
    private static bool SameParent(Guid? idA, string? nameA, Guid? idB, string? nameB)
    {
        if (idA is not null || idB is not null)
            return idA is not null && idA == idB;
        return !string.IsNullOrWhiteSpace(nameA) && !string.IsNullOrWhiteSpace(nameB)
            && string.Equals(nameA.Trim(), nameB.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>The crosses a plant was a parent in, newest first, as pod parent or as pollen parent.</summary>
    public IReadOnlyList<CrossView> CrossesOf(Plant plant, bool asPollenParent) =>
        [.. crosses
            .Where(c => asPollenParent ? c.PollenParentPlantId == plant.Id : c.PodParentPlantId == plant.Id)
            .OrderByDescending(c => c.PollinatedOn)
            .Select(ViewOf)];

    /// <summary>
    /// The seedlings of the crosses where the plant gave the pollen: the plants and the propagations
    /// still growing. A self, where the plant is both, is left to the plant's own children.
    /// </summary>
    public (IReadOnlyList<Plant> Plants, IReadOnlyList<Propagation> Growing) PollenChildrenOf(Plant plant)
    {
        var made = new List<Plant>();
        var growing = new List<Propagation>();
        foreach (var cross in crosses.Where(c => c.PollenParentPlantId == plant.Id && !c.IsSelf))
        {
            var seedlings = SeedlingsOf(cross);
            made.AddRange(seedlings.Plants);
            growing.AddRange(seedlings.Propagations.Where(p => p.IsActive));
        }

        return (made, growing);
    }
}
