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

    private protected override Task RecordAsync(Propagation propagation) =>
        Deps.PropagationService.GiveAwayAsync(propagation, Count, To, Date);
}
