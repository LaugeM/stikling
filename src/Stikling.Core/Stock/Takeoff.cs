using Stikling.Core.Models;

namespace Stikling.Core.Stock;

/// <summary>
/// One line on the check sheet: what comes off a supply because soil or feed was mixed. The amount is
/// in the supply's own unit, or null when it can't be worked out, which the sheet shows empty for the
/// person to fill in or untick.
/// </summary>
/// <param name="What">The ingredient or product the line is for, as the recipe calls it.</param>
public sealed record TakeoffLine(Guid SupplyId, string SupplyName, string What, decimal? Amount, SupplyUnit? Unit)
{
    /// <summary>True when the amount was worked out rather than left for the person.</summary>
    public bool IsWorkedOut => Amount is not null;
}

/// <summary>
/// Works out what a mixing takes off the supplies stock, from the recipe and the size. Pure, so it is
/// tested without repositories. Nothing is taken off until the person confirms the sheet.
/// </summary>
public static class Takeoff
{
    /// <summary>
    /// Litres of a loose ingredient in a supply's unit: litres and millilitres only. A supply counted in
    /// kilograms, pieces or packs can't be worked out from a volume, so it comes back null.
    /// </summary>
    public static decimal? FromLitres(decimal litres, SupplyUnit? unit) => unit switch
    {
        SupplyUnit.Litres => Round(litres),
        SupplyUnit.Millilitres => Round(litres * 1000),
        _ => null
    };

    /// <summary>
    /// A dose in a supply's unit: millilitres and litres for a dose in millilitres, grams and kilograms for
    /// a dose in grams. Drops, and a supply counted in anything else, can't be converted.
    /// </summary>
    public static decimal? FromDose(decimal amount, DoseUnit dose, SupplyUnit? unit) => (dose, unit) switch
    {
        (DoseUnit.Millilitres, SupplyUnit.Millilitres) => Round(amount),
        (DoseUnit.Millilitres, SupplyUnit.Litres) => Round(amount / 1000),
        (DoseUnit.Grams, SupplyUnit.Grams) => Round(amount),
        (DoseUnit.Grams, SupplyUnit.Kilograms) => Round(amount / 1000),
        _ => null
    };

    /// <summary>
    /// The ingredients of a mix that are linked to a supply, with what comes off each for a batch of
    /// that size. Ingredients without a link, or whose supply is gone, aren't on the sheet.
    /// </summary>
    public static IReadOnlyList<TakeoffLine> ForMixing(SoilMix mix, decimal litres, IEnumerable<Supply> supplies)
    {
        var live = LiveById(supplies);
        var lines = new List<TakeoffLine>();
        foreach (var (ingredient, share) in mix.Portions(litres))
        {
            if (ingredient.SupplyId is { } id && live.TryGetValue(id, out var supply))
                lines.Add(new TakeoffLine(supply.Id, NameOf(supply), ingredient.Name.Trim(), share is { } l ? FromLitres(l, supply.Unit) : null, supply.Unit));
        }
        return lines;
    }

    /// <summary>
    /// The products of a can of feed that have a supply (found through <see cref="Supply.ProductId"/>),
    /// with their doses times the water. A product without a supply isn't on the sheet.
    /// </summary>
    public static IReadOnlyList<TakeoffLine> ForCan(IEnumerable<ProductDose> doses, decimal litres, IEnumerable<Supply> supplies)
    {
        var byProduct = supplies
            .Where(s => !s.IsDeleted && s.ProductId is not null)
            .GroupBy(s => s.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Id).First());

        var lines = new List<TakeoffLine>();
        foreach (var dose in doses)
        {
            if (dose.ProductId is { } id && byProduct.TryGetValue(id, out var supply))
            {
                var measured = dose.For(litres) is { } amount ? FromDose(amount, dose.Unit, supply.Unit) : null;
                lines.Add(new TakeoffLine(supply.Id, NameOf(supply), dose.Name.Trim(), measured, supply.Unit));
            }
        }
        return lines;
    }

    /// <summary>
    /// The supply an ingredient most likely comes out of: the one with the same name, ignoring case, with a
    /// supply for mixing soil winning over another of the same name. Only a suggestion, set by the person.
    /// </summary>
    public static Supply? Suggest(string? ingredient, IEnumerable<Supply> supplies)
    {
        var name = ingredient?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        return supplies
            .Where(s => !s.IsDeleted && string.Equals(s.Name?.Trim(), name, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(s => s.EffectiveKind == SupplyKind.Mixing ? 0 : 1)
            .ThenBy(s => s.Id)
            .FirstOrDefault();
    }

    private static Dictionary<Guid, Supply> LiveById(IEnumerable<Supply> supplies) =>
        supplies.Where(s => !s.IsDeleted).GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

    private static string NameOf(Supply supply) => supply.Name?.Trim() ?? "";

    private static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
