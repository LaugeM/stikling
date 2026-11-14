using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// The rules of the Gave some away sheet, kept apart from the page so they can be tested. The page binds
/// to the properties and calls <see cref="CountForm.SaveAsync"/>.
/// </summary>
public sealed class GiveAwayForm : CountForm
{
    internal GiveAwayForm(UnitForms deps, Propagation propagation) : base(deps, propagation)
    {
    }

    public string? To { get; set; }

    /// <summary>The "They were sold" tick. The price is only used while it is on.</summary>
    public bool Sold { get; set; }

    /// <summary>What all of them were sold for, as a number. Optional even when sold.</summary>
    public decimal? Price { get; set; }

    public bool PriceIsValid => !Sold || Price is null or >= 0;

    private protected override Task RecordAsync(Propagation propagation) =>
        Deps.PropagationService.GiveAwayAsync(propagation, Count, To, Date, Sold ? Price : null);
}
