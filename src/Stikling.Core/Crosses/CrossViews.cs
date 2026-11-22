using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;
using static Stikling.Core.Text.Format;

namespace Stikling.Core.Crosses;

/// <summary>
/// One side of a cross as it reads now. The plant when it is in the app, else the name that was
/// kept beside it or typed, else a line that says it is gone.
/// </summary>
/// <param name="Plant">The plant, or null when it isn't in the app (typed, deleted or not synced yet).</param>
public sealed record ParentRef(Plant? Plant, string Name, bool Known)
{
    public static ParentRef Of(Guid? plantId, string? typed, IReadOnlyDictionary<Guid, Plant> plants)
    {
        if (plantId is { } id && plants.TryGetValue(id, out var plant))
            return new ParentRef(plant, plant.DisplayName, true);
        if (Clean(typed) is { } name)
            return new ParentRef(null, name, true);
        return new ParentRef(null, Strings.Crosses.ParentGone, false);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>A cross with both parents resolved and its title, "Pod parent × Pollen parent".</summary>
public sealed record CrossView(Cross Cross, ParentRef Pod, ParentRef Pollen)
{
    public bool IsSelf => Cross.IsSelf;

    /// <summary>"Pod × Pollen", or "Pod selfed" when both sides are the same plant.</summary>
    public string Title => IsSelf ? F(Strings.Crosses.TitleSelf, Pod.Name) : F(Strings.Crosses.TitleOf, Pod.Name, Pollen.Name);

    /// <summary>The working name, or the title when it has none.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Cross.Name) ? Title : Cross.Name.Trim();

    /// <summary>The working name with the title after it, or just the title when it has no name: "Dark Star (A × B)".</summary>
    public string FullName => HasWorkingName ? F(Strings.Crosses.NameAndTitle, DisplayName, Title) : Title;

    public bool HasWorkingName => !string.IsNullOrWhiteSpace(Cross.Name);
}

/// <summary>
/// What came of a cross, counted from the records that point back to it: seeds sown, plants made
/// and seedlings alive. Nothing is stored on the cross for this.
/// </summary>
/// <param name="Lots">The seed lots made from the cross.</param>
/// <param name="Propagations">The seed propagations sown from those lots.</param>
/// <param name="Plants">The plants potted up from those propagations.</param>
/// <param name="Sown">Seeds sown, summed over the propagations.</param>
/// <param name="CameUp">Seeds that came up, where the propagations say.</param>
/// <param name="Growing">Seedlings still in a propagation: those that came up and have not left it.</param>
/// <param name="Alive">Plants of your own that came from the cross, plus seedlings still growing.</param>
public sealed record CrossSeedlings(
    IReadOnlyList<SeedLot> Lots,
    IReadOnlyList<Propagation> Propagations,
    IReadOnlyList<Plant> Plants,
    int Sown,
    int CameUp,
    int Growing,
    int Alive)
{
    public static readonly CrossSeedlings None = new([], [], [], 0, 0, 0, 0);

    public bool Any => Propagations.Count > 0 || Lots.Count > 0;

    /// <summary>The first day anything was sown from the cross's lots. Worked out here, not stored on the cross.</summary>
    public DateOnly? FirstSown => Propagations.Count == 0 ? null : Propagations.Min(p => p.StartedOn);
}

/// <summary>A cross in the log with what came of it.</summary>
public sealed record CrossRow(CrossView View, CrossSeedlings Seedlings);

/// <summary>Every cross of one pair of parents with the totals. "Kept" is seedlings alive until keep or pass exists.</summary>
/// <param name="Rows">The crosses, newest pollination first.</param>
public sealed record PairHistory(IReadOnlyList<CrossRow> Rows, int Took, int Sown, int CameUp, int Alive)
{
    public int Count => Rows.Count;
}

/// <summary>Which crosses the log shows.</summary>
public enum CrossLogFilter
{
    All,
    Took,
    DidNotTake,
    NotKnown
}

/// <summary>A seed lot with the cross it came from, resolved.</summary>
public sealed record LotView(SeedLot Lot, CrossView? Cross, Propagations.SowingSummary Sowings)
{
    /// <summary>The lot's own name, else its cross's, else where it came from.</summary>
    public string Title
    {
        get
        {
            if (Lot.BotanicalName is { } botanical)
                return botanical;
            if (Cross is { } cross)
                return cross.DisplayName;
            return string.IsNullOrWhiteSpace(Lot.Source) ? Strings.Crosses.LotUnnamed : Lot.Source.Trim();
        }
    }
}
