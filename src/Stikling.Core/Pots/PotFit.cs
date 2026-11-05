using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>
/// The sizes of a nursery pot, or only a width typed in a shop. Only compared with outer pots, so
/// nothing is stored.
/// </summary>
public sealed record NurserySize(decimal TopCm, decimal? BottomCm, decimal? HeightCm, decimal? RimMm)
{
    /// <summary>The sizes of a pot, or null when it has no width across the top.</summary>
    public static NurserySize? Of(Pot pot) =>
        pot.TopCm is { } top ? new NurserySize(top, pot.BottomCm, pot.HeightCm, pot.RimMm) : null;

    /// <summary>Just a width, with nothing else known.</summary>
    public static NurserySize Width(decimal topCm) => new(topCm, null, null, null);
}

public enum PotFitKind
{
    GoesIn,
    HangsOnRim,
    TooNarrow
}

/// <param name="SpareCm">How much wider the opening is than the part that has to go in. Zero for TooNarrow.</param>
/// <param name="Tight">Goes in or hangs, with less than half a centimetre to spare.</param>
/// <param name="OpeningIsOutside">The outer pot has no rim filled in, so its outside width was used.</param>
/// <param name="BottomTighter">The nursery pot's bottom is wider than the outer pot's, so it may stop short of the floor.</param>
/// <param name="TallerByCm">How much taller the nursery pot is, when both heights are known.</param>
public sealed record PotFitResult(
    PotUse Outer, PotFitKind Kind, decimal SpareCm, bool Tight, bool OpeningIsOutside, bool BottomTighter, decimal? TallerByCm);

/// <summary>Compares the sizes of a nursery pot with the outer pots. It does not say what to do with a plant.</summary>
public static class PotFits
{
    private const decimal TightBelowCm = 0.5m;

    public static PotFitResult Check(NurserySize nursery, PotUse outer) => Check(nursery, outer.Pot, outer);

    public static PotFitResult Check(NurserySize nursery, Pot outer) => Check(nursery, outer, new PotUse(outer, 0, 0));

    private static PotFitResult Check(NurserySize nursery, Pot outer, PotUse use)
    {
        var inside = outer.InsideTopCm;
        var opening = inside ?? outer.TopCm ?? 0m;
        var outside = inside is null;

        PotFitKind kind;
        decimal spare;
        if (nursery.TopCm <= opening)
        {
            kind = PotFitKind.GoesIn;
            spare = opening - nursery.TopCm;
        }
        else if (nursery.RimMm is > 0 and { } rim && nursery.TopCm - rim / 5 is var below && below <= opening)
        {
            kind = PotFitKind.HangsOnRim;
            spare = opening - below;
        }
        else
        {
            kind = PotFitKind.TooNarrow;
            spare = 0;
        }

        var fits = kind != PotFitKind.TooNarrow;
        decimal? taller = fits && nursery.HeightCm is { } h && outer.HeightCm is { } oh && h > oh ? h - oh : null;
        var bottomTighter = kind == PotFitKind.GoesIn
            && nursery.BottomCm is { } nb && outer.BottomCm is { } ob
            && nb > ob - (outer.RimMm ?? 0) / 5;

        return new PotFitResult(use, kind, spare, fits && spare < TightBelowCm, outside, bottomTighter, taller);
    }

    /// <summary>The free outer pots with a size, ordered by how well they fit.</summary>
    public static List<PotFitResult> List(NurserySize nursery, IEnumerable<PotUse> uses)
    {
        var results = uses
            .Where(u => u.Pot.Group == PotGroup.Outer && u.Free > 0 && !u.Pot.IsDeleted && u.Pot.TopCm is not null)
            .Select(u => Check(nursery, u))
            .ToList();

        return results
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.Kind == PotFitKind.TooNarrow ? 0 : r.SpareCm)
            .ThenByDescending(r => r.Kind == PotFitKind.TooNarrow ? r.Outer.Pot.TopCm : 0)
            .ToList();
    }

    /// <summary>Free outer pots that can't be checked because they have no width.</summary>
    public static int CountWithoutSize(IEnumerable<PotUse> uses) =>
        uses.Count(u => u.Pot.Group == PotGroup.Outer && u.Free > 0 && !u.Pot.IsDeleted && u.Pot.TopCm is null);
}
